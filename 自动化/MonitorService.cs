using System.Diagnostics;

namespace AutoDaily;

/// <summary>
/// 后台监控服务 —— 完全移植 IdleMonitor.ps1 v3 的状态机逻辑
/// </summary>
public class MonitorService : IDisposable
{
    private readonly AppConfig _config;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private DateTime? _lastRun;
    private string _lastStatusState = ""; // 防重复写入 status.txt

    public enum MonitorState
    {
        Stopped, FirstRun, Warmup, WaitingGap, WaitingIdle, Triggered
    }

    public MonitorState State { get; private set; } = MonitorState.Stopped;
    public string GapText { get; private set; } = "";
    public string IdleText { get; private set; } = "";
    public DateTime? LastRunTime => _lastRun;

    /// <summary>状态变化事件（UI 更新）</summary>
    public event Action<string, string, MonitorState>? StatusChanged;
    /// <summary>日志事件（兼容旧代码，也可直接写 LogWriter）</summary>
    public event Action<string, string>? LogWritten;
    /// <summary>触发后监控循环结束事件（UI 需要知道监控停了）</summary>
    public event Action? MonitoringCompleted;
    /// <summary>自动触发（非手动）时点火</summary>
    public event Action? Triggered;

    public MonitorService(AppConfig config)
    {
        _config = config;
    }

    public void Start()
    {
        if (_cts != null) return;
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => RunLoop(_cts.Token));
        OnLog("启动", "AutoDaily 监控服务已启动");
    }

    public void Stop()
    {
        if (_cts == null) return;
        _cts.Cancel();
        try { _loopTask?.Wait(TimeSpan.FromSeconds(5)); } catch { }
        _cts.Dispose();
        _cts = null;
        _loopTask = null;
        State = MonitorState.Stopped;
        OnStatusChanged(0, 0, MonitorState.Stopped);
    }

    private void OnStatusChanged(int gapSec, int idleSec, MonitorState state)
    {
        State = state;
        var stateStr = state.ToString();

        var gapH = gapSec >= 99999999 ? "N/A" : $"{gapSec / 3600}";
        var gapM = gapSec >= 99999999 ? "N/A" : $"{(gapSec % 3600) / 60}";
        var idleM = $"{idleSec / 60}";

        GapText = gapSec >= 99999999 ? "N/A" : $"{gapH} h {gapM} m";
        IdleText = $"{idleM} min";

        // UI 事件
        StatusChanged?.Invoke(GapText, IdleText, state);

        // 写 status.txt（仅在状态变化时）
        if (stateStr != _lastStatusState)
        {
            _lastStatusState = stateStr;
            LogWriter.WriteStatus(gapSec, idleSec, stateStr);
        }
    }

    private void OnLog(string tag, string msg)
    {
        LogWriter.WriteLog(tag, msg);
        LogWritten?.Invoke(tag, msg);
    }

    /// <summary>
    /// 冲突检测：BetterGI 进程是否已在运行
    /// </summary>
    private static bool IsBetterGIRunning()
    {
        try
        {
            return Process.GetProcessesByName("BetterGI").Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task RunLoop(CancellationToken ct)
    {
        // 读取历史执行记录
        _lastRun = _config.ReadLastRunTime();

        var gapSec = _lastRun.HasValue
            ? (int)(DateTime.Now - _lastRun.Value).TotalSeconds
            : int.MaxValue;

        if (!_lastRun.HasValue)
        {
            OnLog("状态", "未找到历史执行记录，视为首次运行\n进入高频监控模式");
            OnStatusChanged(99999999, 0, MonitorState.FirstRun);
        }
        else
        {
            var gt = FormatGap(gapSec);
            if (gapSec < _config.MinGapSeconds)
            {
                OnLog("状态", $"距上次执行：{gt}（不足 {_config.MinGapHours} 小时）\n进入低频轮询模式");
                OnStatusChanged(gapSec, 0, MonitorState.Warmup);
            }
            else
            {
                var idle = (int)NativeMethods.GetIdleSeconds();
                OnLog("状态", $"距上次执行：{gt}（已满 {_config.MinGapHours} 小时）\n进入高频监控模式");
                OnStatusChanged(gapSec, idle, MonitorState.Warmup);
            }
        }

        while (!ct.IsCancellationRequested)
        {
            // 冲突检测：BetterGI 是否已在运行
            if (IsBetterGIRunning())
            {
                _config.WriteLastRunTime();
                OnLog("跳过", "检测到 BetterGI 已在运行\n判定为手动启动，写入时间戳，监控退出。");
                OnStatusChanged(gapSec, 0, MonitorState.Stopped);
                MonitoringCompleted?.Invoke();
                return;
            }

            // 重新计算间隔
            _lastRun = _config.ReadLastRunTime();
            gapSec = _lastRun.HasValue
                ? (int)(DateTime.Now - _lastRun.Value).TotalSeconds
                : int.MaxValue;

            if (gapSec >= _config.MinGapSeconds)
            {
                // 时间够了，检测空闲
                // 使用 long 避免 TickCount 回绕溢出
                var idleRaw = NativeMethods.GetIdleSeconds();
                var idle = idleRaw > int.MaxValue ? int.MaxValue : (int)idleRaw;

                if (idle >= _config.IdleThresholdSeconds)
                {
                    // 触发！
                    _config.WriteLastRunTime();
                    OnLog("DONE",
                        $"系统空闲 {idle / 60} 分钟，距上次执行 {FormatGap(gapSec)}\n已启动 BetterGI 一条龙。");
                    OnStatusChanged(gapSec, idle, MonitorState.Triggered);

                    // 点火
                    Triggered?.Invoke();
                    MonitoringCompleted?.Invoke();

                    // 清理自身资源
                    _cts?.Cancel();
                    _cts?.Dispose();
                    _cts = null;
                    return; // 触发后退出监控
                }

                OnStatusChanged(gapSec, idle, MonitorState.WaitingIdle);
                // 高频轮询
                try { await Task.Delay(_config.MonitorIntervalSeconds * 1000, ct); }
                catch (OperationCanceledException) { return; }
            }
            else
            {
                OnStatusChanged(gapSec, 0, MonitorState.WaitingGap);
                // 低频轮询
                var remaining = _config.MinGapSeconds - gapSec;
                var sleep = Math.Min(remaining, 3600);
                try { await Task.Delay(sleep * 1000, ct); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    private static string FormatGap(int sec)
    {
        if (sec >= 3600)
        {
            var h = sec / 3600;
            var m = (sec % 3600) / 60;
            return $"{h} h {m} m";
        }
        return $"{sec / 60} m";
    }

    public void Dispose()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }
}
