namespace AutoDaily;

public class TrayApplicationContext : ApplicationContext
{
    private readonly AppConfig _config;
    private MonitorService? _monitor;
    private MainForm? _mainForm;
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _showHideItem;
    private readonly ToolStripMenuItem _startStopItem;
    private readonly ToolStripMenuItem _triggerItem;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private bool _isExecuting; // 防止重入

    public TrayApplicationContext(bool showMainOnStart = true)
    {
        _config = AppConfig.Load();

        // ---- 托盘图标 ----
        _trayIcon = new NotifyIcon
        {
            Icon = AppIcon.GetTrayIcon(),
            Text = "AutoDaily - 原神自动监控",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };

        // ---- 菜单项 ----
        _statusItem = new ToolStripMenuItem("监控未启动") { Enabled = false };
        _showHideItem = new ToolStripMenuItem("显示主窗口", null, (_, _) => ShowMainWindow())!;
        _startStopItem = new ToolStripMenuItem("启动监控", null, OnStartStop!)!;
        _triggerItem = new ToolStripMenuItem("⚡ 立即触发一条龙", null, OnTrigger!)!;
        var settingsItem = new ToolStripMenuItem("设置...", null, OnSettings!)!;
        var exitItem = new ToolStripMenuItem("退出", null, OnExit!)!;

        _trayIcon.ContextMenuStrip.Items.AddRange([
            _statusItem,
            new ToolStripSeparator(),
            _showHideItem,
            _startStopItem,
            _triggerItem,
            settingsItem,
            new ToolStripSeparator(),
            exitItem
        ]);

        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();

        // 状态刷新定时器
        _refreshTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _refreshTimer.Tick += (_, _) => RefreshTrayText();
        _refreshTimer.Start();

        // 首次运行 → 弹出设置向导
        if (_config.IsFirstRun)
        {
            // 延迟弹出（等 Application.Run 就绪）
            var timer = new System.Windows.Forms.Timer { Interval = 500 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                timer.Dispose();
                RunSetupWizard();
            };
            timer.Start();
        }
        else
        {
            StartMonitor();

            // 非静默启动 → 显示主窗口
            if (showMainOnStart)
            {
                var t = new System.Windows.Forms.Timer { Interval = 800 };
                t.Tick += (_, _) =>
                {
                    t.Stop();
                    t.Dispose();
                    ShowMainWindow();
                };
                t.Start();
            }
        }
    }

    private void RunSetupWizard()
    {
        using var wizard = new SetupWizard(_config);
        var result = wizard.ShowDialog();

        _config.Save();

        if (result == DialogResult.OK || result == DialogResult.Cancel)
        {
            StartMonitor();
            ShowMainWindow();
        }
    }

    // ==== 主窗口管理 ====

    private void ShowMainWindow()
    {
        if (_mainForm == null || _mainForm.IsDisposed)
        {
            _mainForm = new MainForm(_config, _monitor, _trayIcon);
            _mainForm.FormClosed += (_, _) => _mainForm = null;
        }

        if (!_mainForm.Visible)
        {
            _mainForm.Show();
            _mainForm.WindowState = FormWindowState.Normal;
            _mainForm.BringToFront();
        }
        else
        {
            _mainForm.BringToFront();
        }

        _showHideItem.Text = _mainForm.Visible ? "隐藏主窗口" : "显示主窗口";
    }

    // ==== 监控启停 ====

    private void StartMonitor()
    {
        if (_monitor != null) return;

        _monitor = new MonitorService(_config);

        _monitor.StatusChanged += (gap, idle, state) =>
        {
            _statusItem.Text = $"{state} | Gap: {gap} | Idle: {idle}";
        };

        _monitor.MonitoringCompleted += () =>
        {
            // 监控循环结束（触发后 / 冲突保护后）
            _startStopItem.Text = "启动监控";
            _triggerItem.Enabled = false;
            _statusItem.Text = "监控已退出（可手动重启）";
            _trayIcon.Text = "AutoDaily - 已退出";
        };

        _monitor.Triggered += async () =>
        {
            _trayIcon.ShowBalloonTip(3000, "AutoDaily",
                "条件满足，正在启动 BetterGI 一条龙...", ToolTipIcon.Info);

            try
            {
                _isExecuting = true;
                var result = await Executor.Execute(_config, log =>
                {
                    LogWriter.WriteLog("EXE", log);
                });

                if (!result.Success)
                {
                    _trayIcon.ShowBalloonTip(5000, "AutoDaily",
                        $"执行失败：{result.Message}", ToolTipIcon.Error);
                }
            }
            catch (Exception ex)
            {
                LogWriter.WriteLog("ERR", $"自动执行异常：{ex.Message}");
                _trayIcon.ShowBalloonTip(5000, "AutoDaily",
                    $"执行异常：{ex.Message}", ToolTipIcon.Error);
            }
            finally
            {
                _isExecuting = false;
            }
        };

        _monitor.Start();
        _startStopItem.Text = "停止监控";
        _triggerItem.Enabled = true;
    }

    private void StopMonitor()
    {
        _monitor?.Stop();
        _monitor?.Dispose();
        _monitor = null;
        _startStopItem.Text = "启动监控";
        _triggerItem.Enabled = false;
        _statusItem.Text = "监控已停止";
        _trayIcon.Text = "AutoDaily - 已停止";
    }

    private void OnStartStop(object sender, EventArgs e)
    {
        if (_monitor != null)
            StopMonitor();
        else
            StartMonitor();
    }

    // ==== 手动触发 ====

    private async void OnTrigger(object sender, EventArgs e)
    {
        if (_isExecuting) return;

        var result = MessageBox.Show(
            "确定要立即触发一条龙吗？\n\n这将：\n" +
            (_config.MuteSystem ? "① 静音系统\n" : "") +
            "② 启动 BetterGI 并等待完成\n" +
            (_config.MuteSystem ? "③ 恢复声音\n" : "") +
            "\n此操作需要管理员权限（静音时）。",
            "确认触发", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);

        if (result != DialogResult.OK) return;

        _isExecuting = true;
        _triggerItem.Enabled = false;
        _config.WriteLastRunTime();
        LogWriter.WriteLog("手动", "用户通过托盘菜单手动触发一条龙");

        try
        {
            var execResult = await Executor.Execute(_config, log =>
            {
                LogWriter.WriteLog("EXE", log);
            });

            LogWriter.WriteLog("手动",
                $"手动触发完毕：{(execResult.Success ? "成功" : execResult.Message)}");

            if (!execResult.Success)
            {
                MessageBox.Show($"执行失败：{execResult.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            LogWriter.WriteLog("ERR", $"手动执行异常：{ex.Message}");
            MessageBox.Show($"执行异常：{ex.Message}", "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isExecuting = false;
            _triggerItem.Enabled = _monitor != null;
        }
    }

    // ==== 设置 ====

    private void OnSettings(object sender, EventArgs e)
    {
        using var form = new ConfigForm(_config);
        form.ShowDialog();
        _config.Save();

        // 如果监控正在运行，提示重启生效
        if (_monitor != null)
        {
            var answer = MessageBox.Show(
                "设置已保存。部分更改需要重启监控才能生效。\n\n是否现在重启监控？",
                "设置已保存", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (answer == DialogResult.Yes)
            {
                StopMonitor();
                StartMonitor();
            }
        }
    }

    // ==== 退出 ====

    private void OnExit(object sender, EventArgs e)
    {
        StopMonitor();
        _refreshTimer.Stop();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _mainForm?.Close();
        _mainForm?.Dispose();
        SingleInstance.Release();
        Application.Exit();
    }

    private void RefreshTrayText()
    {
        if (_monitor != null)
            _trayIcon.Text = $"AutoDaily - {_monitor.State}";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _monitor?.Dispose();
            _refreshTimer?.Dispose();
            _trayIcon?.Dispose();
        }
        base.Dispose(disposing);
    }
}
