namespace AutoDaily;

/// <summary>
/// 主窗口 —— 状态仪表盘 + 快捷操作
/// 关闭时最小化到托盘，不退出程序。
/// </summary>
public partial class MainForm : Form
{
    private readonly AppConfig _config;
    private MonitorService? _monitor;
    private bool _isManualTrigger;

    // 控件
    private readonly Label _lblTitle;
    private readonly Label _lblState;
    private readonly Panel _infoPanel;
    private readonly Label _lblGap, _lblIdle, _lblLastRun, _lblNextTrigger;
    private readonly Label _lblSuccessCount;
    private readonly Button _btnStartStop, _btnTrigger, _btnSettings;
    private readonly ListBox _lstLog;
    private readonly NotifyIcon? _trayIcon; // 可选引用（由 TrayApplicationContext 传入）

    public MainForm(AppConfig config, MonitorService? monitor = null, NotifyIcon? trayIcon = null)
    {
        _config = config;
        _monitor = monitor;
        _trayIcon = trayIcon;

        Text = "AutoDaily - 原神每日自动监控";
        Size = new Size(520, 520);
        MinimumSize = new Size(420, 400);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;

        // 关闭 → 隐藏到托盘
        FormClosing += (_, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
        };

        // 窗口显示/隐藏时更新托盘菜单
        VisibleChanged += (_, _) =>
        {
            if (_trayIcon?.ContextMenuStrip?.Items[0] is ToolStripMenuItem showHide)
            {
                showHide.Text = Visible ? "隐藏主窗口" : "显示主窗口";
            }
        };

        // ---- 布局 ----
        var y = 12;

        // Logo + 标题
        var logo = new PictureBox
        {
            Image = AppIcon.GetLogo(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(16, y),
            Size = new Size(48, 48)
        };

        _lblTitle = new Label
        {
            Text = "AutoDaily 监控仪表盘",
            Font = new Font("Microsoft YaHei", 13, FontStyle.Bold),
            Location = new Point(72, y + 6),
            AutoSize = true
        };
        y = 64; // logo height + margin

        // 状态标签
        _lblState = new Label
        {
            Text = "● 监控未启动",
            Font = new Font("Microsoft YaHei", 10, FontStyle.Regular),
            ForeColor = Color.Gray,
            Location = new Point(16, y),
            AutoSize = true
        };
        y += 28;

        // 信息面板
        _infoPanel = new Panel
        {
            Location = new Point(16, y),
            Size = new Size(472, 110),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.WhiteSmoke
        };

        _lblGap = new Label
        {
            Text = "距上次执行：--",
            Location = new Point(12, 12),
            AutoSize = true,
            Font = new Font("Microsoft YaHei", 9)
        };
        _lblIdle = new Label
        {
            Text = "当前空闲：--",
            Location = new Point(12, 36),
            AutoSize = true,
            Font = new Font("Microsoft YaHei", 9)
        };
        _lblLastRun = new Label
        {
            Text = "上次执行：--",
            Location = new Point(12, 60),
            AutoSize = true,
            Font = new Font("Microsoft YaHei", 9)
        };
        _lblNextTrigger = new Label
        {
            Text = "下次可触发：--",
            Location = new Point(12, 84),
            AutoSize = true,
            Font = new Font("Microsoft YaHei", 9),
            ForeColor = Color.DarkGreen
        };
        _infoPanel.Controls.AddRange([_lblGap, _lblIdle, _lblLastRun, _lblNextTrigger]);
        y += 118;

        // 成功次数
        _lblSuccessCount = new Label
        {
            Text = "✅ 总成功执行：-- 次",
            Location = new Point(16, y),
            AutoSize = true,
            Font = new Font("Microsoft YaHei", 9, FontStyle.Bold),
            ForeColor = Color.DarkGreen
        };
        y += 24;

        // 按钮行
        _btnStartStop = new Button
        {
            Text = "▶ 启动监控",
            Location = new Point(16, y),
            Width = 120,
            Height = 34
        };
        _btnStartStop.Click += OnStartStop;

        _btnTrigger = new Button
        {
            Text = "⚡ 立即触发",
            Location = new Point(144, y),
            Width = 120,
            Height = 34,
            Enabled = false
        };
        _btnTrigger.Click += OnTrigger;

        _btnSettings = new Button
        {
            Text = "⚙ 设置",
            Location = new Point(272, y),
            Width = 100,
            Height = 34
        };
        _btnSettings.Click += OnSettings;
        y += 44;

        // 日志预览
        var lblLogTitle = new Label
        {
            Text = "── 最近日志 ──",
            Location = new Point(16, y),
            AutoSize = true,
            Font = new Font("Microsoft YaHei", 9, FontStyle.Bold)
        };
        y += 22;

        _lstLog = new ListBox
        {
            Location = new Point(16, y),
            Size = new Size(472, 150),
            Font = new Font("Consolas", 8.5f),
            HorizontalScrollbar = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        y += 158;

        Controls.AddRange([
            logo,
            _lblTitle, _lblState, _infoPanel, _lblSuccessCount,
            _btnStartStop, _btnTrigger, _btnSettings,
            lblLogTitle, _lstLog
        ]);

        // 初始加载日志
        RefreshLogPreview();
        _lblSuccessCount.Text = $"✅ 总成功执行：{LogWriter.CountSuccesses()} 次";

        // 如果已有 MonitorService，绑定事件
        if (_monitor != null)
            BindMonitor(_monitor);
    }

    /// <summary>
    /// 绑定 MonitorService 事件（由 TrayApplicationContext 调用或初始化时）
    /// </summary>
    public void BindMonitor(MonitorService monitor)
    {
        // 先解绑旧的
        if (_monitor != null)
        {
            _monitor.StatusChanged -= OnMonitorStatusChanged;
            _monitor.LogWritten -= OnMonitorLog;
            _monitor.MonitoringCompleted -= OnMonitorCompleted;
        }

        _monitor = monitor;
        _monitor.StatusChanged += OnMonitorStatusChanged;
        _monitor.LogWritten += OnMonitorLog;
        _monitor.MonitoringCompleted += OnMonitorCompleted;

        // 更新按钮状态
        UpdateButtons(running: true);
    }

    private void OnMonitorStatusChanged(string gap, string idle, MonitorService.MonitorState state)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnMonitorStatusChanged(gap, idle, state));
            return;
        }

        _lblGap.Text = $"距上次执行：{gap}（阈值 {_config.MinGapHours}h）";
        _lblIdle.Text = $"当前空闲：{idle}（阈值 {_config.IdleThresholdMinutes}min）";
        _lblLastRun.Text = $"上次执行：{_monitor?.LastRunTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "无记录"}";

        // 计算下次可触发时间
        if (_monitor?.LastRunTime is DateTime last)
        {
            var next = last.AddHours(_config.MinGapHours);
            if (next > DateTime.Now)
                _lblNextTrigger.Text = $"下次可触发：{next:yyyy-MM-dd HH:mm:ss}（还需等待）";
            else
                _lblNextTrigger.Text = "下次可触发：现在即可（已满间隔）";
        }
        else
        {
            _lblNextTrigger.Text = "下次可触发：满足空闲即可（首次运行）";
        }

        // 状态颜色
        (_lblState.Text, var color) = state switch
        {
            MonitorService.MonitorState.FirstRun => ("● 首次运行 — 等待空闲…", Color.DodgerBlue),
            MonitorService.MonitorState.Warmup => ("● 预热中 — 计算间隔…", Color.DarkOrange),
            MonitorService.MonitorState.WaitingGap => ("● 等待间隔 — 低频轮询中", Color.DarkOrange),
            MonitorService.MonitorState.WaitingIdle => ("● 等待空闲 — 高频监控中", Color.DodgerBlue),
            MonitorService.MonitorState.Triggered => ("● 已触发 — 正在执行", Color.DarkGreen),
            MonitorService.MonitorState.Stopped => ("● 监控已停止", Color.Gray),
            _ => ("● 未知状态", Color.Gray)
        };
        _lblState.ForeColor = color;

        // 刷新执行次数
        var successCount = LogWriter.CountSuccesses();
        _lblSuccessCount.Text = $"✅ 总成功执行：{successCount} 次";
    }

    private void OnMonitorLog(string tag, string msg)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnMonitorLog(tag, msg));
            return;
        }

        var line = $"[{tag}] {msg.Split('\n')[0]}";
        _lstLog.Items.Insert(0, line);
        // 保留最近 50 条
        while (_lstLog.Items.Count > 50)
            _lstLog.Items.RemoveAt(_lstLog.Items.Count - 1);

        // 同时刷新文件日志预览
        RefreshLogPreview();
    }

    private void OnMonitorCompleted()
    {
        if (InvokeRequired)
        {
            BeginInvoke(OnMonitorCompleted);
            return;
        }
        UpdateButtons(running: false);
        _lblState.Text = "● 监控已退出（触发完成或冲突保护）";
        _lblState.ForeColor = Color.Gray;
    }

    private void UpdateButtons(bool running)
    {
        _btnStartStop.Text = running ? "⏸ 停止监控" : "▶ 启动监控";
        _btnTrigger.Enabled = running;
    }

    private void OnStartStop(object? sender, EventArgs e)
    {
        if (_monitor != null && _monitor.State != MonitorService.MonitorState.Stopped
            && _monitor.State != MonitorService.MonitorState.Triggered)
        {
            // 停止
            _monitor.Stop();
            UpdateButtons(running: false);
            _lblState.Text = "● 监控已手动停止";
            _lblState.ForeColor = Color.Gray;
            LogWriter.WriteLog("停止", "用户手动停止监控");
        }
        else
        {
            // 启动（需要重新创建 MonitorService）
            StartNewMonitor();
        }
    }

    private async void OnTrigger(object? sender, EventArgs e)
    {
        if (_isManualTrigger) return;

        var result = MessageBox.Show(
            "确定要立即触发一条龙吗？\n\n这将：\n" +
            (_config.MuteSystem ? "① 静音系统\n" : "") +
            "② 启动 BetterGI 并等待完成\n" +
            (_config.MuteSystem ? "③ 恢复声音\n" : "") +
            "\n此操作需要管理员权限（静音时）。",
            "确认触发", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);

        if (result != DialogResult.OK) return;

        _isManualTrigger = true;
        _btnTrigger.Enabled = false;
        _config.WriteLastRunTime();
        LogWriter.WriteLog("手动", "用户手动触发一条龙");
        _lblState.Text = "● 手动触发 — 正在执行...";
        _lblState.ForeColor = Color.DarkGreen;

        var execResult = await Executor.Execute(_config, log =>
        {
            LogWriter.WriteLog("EXE", log);
            if (InvokeRequired)
                BeginInvoke(() => _lstLog.Items.Insert(0, log));
            else
                _lstLog.Items.Insert(0, log);
        });

        LogWriter.WriteLog("手动", $"手动触发完毕：{(execResult.Success ? "成功" : execResult.Message)}");

        if (!execResult.Success)
        {
            MessageBox.Show($"执行失败：{execResult.Message}", "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        _lblState.Text = execResult.Success ? "● 手动触发完成" : "● 手动触发失败";
        _lblState.ForeColor = execResult.Success ? Color.DarkGreen : Color.Red;
        _isManualTrigger = false;
        _btnTrigger.Enabled = _monitor != null;
        RefreshLogPreview();
    }

    private void OnSettings(object? sender, EventArgs e)
    {
        using var form = new ConfigForm(_config);
        form.ShowDialog(this);
        // 设置变更后刷新
        RefreshLogPreview();
    }

    public void StartNewMonitor()
    {
        _monitor?.Dispose();
        var monitor = new MonitorService(_config);
        monitor.Triggered += async () =>
        {
            LogWriter.WriteLog("DONE", "触发执行 BetterGI 一条龙");
            await Executor.Execute(_config, log => LogWriter.WriteLog("EXE", log));
        };
        monitor.Start();
        BindMonitor(monitor);
    }

    private void RefreshLogPreview()
    {
        try
        {
            var logPath = Path.Combine(LogWriter.GetDataDirectory(), "Execution-Log.md");
            if (!File.Exists(logPath)) return;

            var lines = File.ReadAllLines(logPath);
            _lstLog.Items.Clear();
            // 显示最后 30 行
            foreach (var line in lines.Reverse().Take(30))
            {
                if (!string.IsNullOrWhiteSpace(line) && line != "==================================================")
                    _lstLog.Items.Add(line.Trim());
            }
        }
        catch { }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _monitor?.Dispose();
        }
        base.Dispose(disposing);
    }
}
