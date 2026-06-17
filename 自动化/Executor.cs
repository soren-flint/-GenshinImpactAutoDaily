using System.Diagnostics;

namespace AutoDaily;

/// <summary>
/// 执行器：按配置启动 BetterGI（可选静音/恢复声音）
/// </summary>
public static class Executor
{
    /// <summary>
    /// 执行结果
    /// </summary>
    public class ExecuteResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public int? ExitCode { get; set; }
    }

    /// <summary>
    /// 执行一条龙：静音 → BetterGI → 等待完成 → 恢复声音
    /// </summary>
    public static async Task<ExecuteResult> Execute(AppConfig config, Action<string>? onLog = null)
    {
        var result = new ExecuteResult();
        var muteWasEnabled = false;

        try
        {
            // 0. 检查 BetterGI.exe 是否存在（提前失败，避免静音后才发现）
            var betterGIExe = Path.Combine(config.BetterGIPath, "BetterGI.exe");
            if (!File.Exists(betterGIExe))
            {
                result.Success = false;
                result.Message = $"未找到 BetterGI.exe：{betterGIExe}";
                onLog?.Invoke($"❌ {result.Message}");
                return result;
            }

            // 1. 静音（如果配置启用）
            if (config.MuteSystem)
            {
                onLog?.Invoke("🔇 正在静音系统...");
                muteWasEnabled = await MuteSystem(true);
                onLog?.Invoke(muteWasEnabled ? "✅ 系统已静音" : "⚠️ 静音失败，继续执行");
            }

            // 2. 启动 BetterGI
            onLog?.Invoke($"🚀 启动 BetterGI（配置：{config.OneDragonConfig}）...");
            var psi = new ProcessStartInfo
            {
                FileName = betterGIExe,
                Arguments = $"--startOneDragon \"{config.OneDragonConfig}\"",
                WorkingDirectory = config.BetterGIPath,
                UseShellExecute = true
            };

            var process = Process.Start(psi);
            if (process == null)
            {
                result.Success = false;
                result.Message = "无法启动 BetterGI（进程启动返回 null）";
                onLog?.Invoke($"❌ {result.Message}");
                return result;
            }

            onLog?.Invoke($"✅ BetterGI 已启动 (PID {process.Id})");

            // 3. 等待 BetterGI 进程退出
            onLog?.Invoke("⏳ 等待 BetterGI 完成...");
            await process.WaitForExitAsync();
            result.ExitCode = process.ExitCode;
            onLog?.Invoke($"✅ BetterGI 已退出 (exit code {process.ExitCode})");

            result.Success = true;
            result.Message = "BetterGI 一条龙执行完毕";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"执行异常：{ex.Message}";
            onLog?.Invoke($"❌ {result.Message}");
        }
        finally
        {
            // 4. 恢复声音
            if (muteWasEnabled)
            {
                onLog?.Invoke("🔊 正在恢复系统声音...");
                await MuteSystem(false);
                onLog?.Invoke("✅ 系统声音已恢复");
            }
        }

        return result;
    }

    /// <summary>
    /// 通过 Windows Audio 服务静音/恢复
    /// stop=true → 停止服务（静音）; stop=false → 启动服务
    /// </summary>
    private static async Task<bool> MuteSystem(bool stop)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "net",
                Arguments = stop ? "stop Audiosrv /y" : "start Audiosrv",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                Verb = "runas"
            };

            var p = Process.Start(psi);
            if (p == null) return false;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await p.WaitForExitAsync(cts.Token);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
