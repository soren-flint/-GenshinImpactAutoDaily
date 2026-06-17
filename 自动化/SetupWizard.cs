namespace AutoDaily;

/// <summary>
/// 首次运行设置向导 —— 5 步引导用户完成配置
/// </summary>
public partial class SetupWizard : Form
{
    private readonly AppConfig _config;
    private BetterGIDetector.DetectionResult? _detectionResult;
    private int _step;

    private bool _step5Configured; // 防止重复配置 Step 5 按钮

    // 控件
    private readonly Label _lblStepTitle, _lblStepDesc;
    private readonly Panel _contentPanel;
    private readonly Button _btnNext, _btnPrev, _btnCancel;
    private readonly ProgressBar _stepBar;

    // Step 2 控件（检测结果）
    private TextBox? _txtDetectLog;
    private Button? _btnBrowse;
    private Label? _lblDetectPath;

    // Step 3 控件（配置选择）
    private ComboBox? _cboConfig;

    // Step 4 控件（参数设置）
    private NumericUpDown? _numIdle, _numGap;
    private CheckBox? _chkMute, _chkAutoStart;

    public SetupWizard(AppConfig config)
    {
        _config = config;

        Text = "AutoDaily 首次设置向导";
        Size = new Size(580, 460);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        // 步骤标题
        _lblStepTitle = new Label
        {
            Text = "欢迎使用 AutoDaily",
            Font = new Font("Microsoft YaHei", 12, FontStyle.Bold),
            Location = new Point(20, 16),
            AutoSize = true
        };

        _lblStepDesc = new Label
        {
            Text = "此向导将帮你完成首次配置。",
            Font = new Font("Microsoft YaHei", 9),
            Location = new Point(20, 44),
            AutoSize = true,
            ForeColor = Color.DimGray
        };

        // 步骤进度条
        _stepBar = new ProgressBar
        {
            Location = new Point(20, 72),
            Width = 524,
            Height = 8,
            Maximum = 5,
            Value = 1
        };

        // 内容面板
        _contentPanel = new Panel
        {
            Location = new Point(20, 92),
            Size = new Size(524, 250),
            BorderStyle = BorderStyle.FixedSingle
        };

        // 按钮
        _btnPrev = new Button
        {
            Text = "← 上一步",
            Location = new Point(240, 364),
            Width = 100,
            Enabled = false
        };
        _btnPrev.Click += (_, _) => Navigate(-1);

        _btnNext = new Button
        {
            Text = "下一步 →",
            Location = new Point(348, 364),
            Width = 100
        };
        _btnNext.Click += OnNextClick;

        _btnCancel = new Button
        {
            Text = "跳过向导",
            Location = new Point(456, 364),
            Width = 88,
            ForeColor = Color.Gray
        };
        _btnCancel.Click += (_, _) =>
        {
            _config.IsFirstRun = false;
            _config.Save();
            DialogResult = DialogResult.Cancel;
            Close();
        };

        Controls.AddRange([
            _lblStepTitle, _lblStepDesc, _stepBar,
            _contentPanel, _btnPrev, _btnNext, _btnCancel
        ]);

        Load += (_, _) => ShowStep(1);
    }

    private void OnNextClick(object? sender, EventArgs e)
    {
        if (_step == 5)
        {
            // Step 5：完成
            _config.Save();
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            Navigate(1);
        }
    }

    private void Navigate(int delta)
    {
        var newStep = _step + delta;
        if (newStep < 1 || newStep > 5) return;

        // Step 2 → 3：用户在 Step 2 可能手动浏览了路径
        if (_step == 2 && newStep == 3 && _detectionResult == null)
        {
            MessageBox.Show("请先完成 BetterGI 检测或手动浏览选择路径。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ShowStep(newStep);
    }

    private void ShowStep(int step)
    {
        _step = step;
        _stepBar.Value = step;
        _btnPrev.Enabled = step > 1;

        _contentPanel.Controls.Clear();

        switch (step)
        {
            case 1: BuildStep1(); break;
            case 2: BuildStep2(); break;
            case 3: BuildStep3(); break;
            case 4: BuildStep4(); break;
            case 5: BuildStep5(); break;
        }
    }

    // ==== Step 1: 欢迎 ====
    private void BuildStep1()
    {
        _lblStepTitle.Text = "① 欢迎";
        _lblStepDesc.Text = "AutoDaily 帮你自动完成原神每日任务。";
        _btnNext.Text = "下一步 →";

        var logo = new PictureBox
        {
            Image = AppIcon.GetLogo(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(180, 8),
            Size = new Size(96, 96)
        };

        var lbl = new Label
        {
            Text = "\n\n\n\n\n欢迎使用 AutoDaily！\n\n" +
                   "本程序会在电脑空闲时，自动帮你运行 BetterGI 一条龙任务。\n\n" +
                   "接下来的几个步骤将帮你：\n" +
                   "  • 找到 BetterGI 安装位置\n" +
                   "  • 选择一条龙配置\n" +
                   "  • 设定触发条件和参数\n\n" +
                   "准备好了吗？点击「下一步」开始。",
            Location = new Point(16, 12),
            AutoSize = true,
            Font = new Font("Microsoft YaHei", 9.5f),
            MaximumSize = new Size(490, 0)
        };
        _contentPanel.Controls.Add(logo);
        _contentPanel.Controls.Add(lbl);
    }

    // ==== Step 2: 检测 BetterGI ====
    private void BuildStep2()
    {
        _lblStepTitle.Text = "② 检测 BetterGI 安装位置";
        _lblStepDesc.Text = "正在自动搜索你的 BetterGI...";
        _btnNext.Text = "下一步 →";

        _txtDetectLog = new TextBox
        {
            Location = new Point(16, 12),
            Size = new Size(492, 150),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 8.5f),
            BackColor = Color.WhiteSmoke
        };

        _lblDetectPath = new Label
        {
            Text = "",
            Location = new Point(16, 172),
            AutoSize = true,
            Font = new Font("Microsoft YaHei", 9, FontStyle.Bold),
            ForeColor = Color.DarkGreen
        };

        _btnBrowse = new Button
        {
            Text = "📂 手动浏览...",
            Location = new Point(16, 200),
            Width = 140
        };
        _btnBrowse.Click += OnBrowse;

        _contentPanel.Controls.AddRange([_txtDetectLog, _lblDetectPath, _btnBrowse]);

        // 自动开始检测
        RunDetection();
    }

    private async void RunDetection()
    {
        if (_txtDetectLog == null) return;

        var progress = new Progress<string>(msg =>
        {
            if (_txtDetectLog != null)
                _txtDetectLog.AppendText(msg + "\r\n");
        });

        _detectionResult = await BetterGIDetector.DetectAsync(progress);

        if (_detectionResult.Found && _detectionResult.Path != null)
        {
            _config.BetterGIPath = _detectionResult.Path;
            if (_lblDetectPath != null)
                _lblDetectPath.Text = $"✅ 找到 BetterGI：{_detectionResult.Path}";

            // 预填配置
            if (_detectionResult.SelectedConfigName != null)
                _config.OneDragonConfig = _detectionResult.SelectedConfigName;

            _btnNext!.Text = "下一步 →";
        }
        else
        {
            if (_lblDetectPath != null)
            {
                _lblDetectPath.Text = "❌ 未自动检测到 BetterGI，请手动浏览";
                _lblDetectPath.ForeColor = Color.Red;
            }
        }
    }

    private void OnBrowse(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "选择 BetterGI 安装目录（包含 BetterGI.exe 的文件夹）",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        };

        if (dlg.ShowDialog() == DialogResult.OK)
        {
            var path = dlg.SelectedPath;
            if (File.Exists(Path.Combine(path, "BetterGI.exe")))
            {
                _config.BetterGIPath = path;
                _detectionResult = new BetterGIDetector.DetectionResult
                {
                    Found = true,
                    Path = path,
                    Source = "用户手动选择"
                };
                if (_lblDetectPath != null)
                {
                    _lblDetectPath.Text = $"✅ 已选择：{path}";
                    _lblDetectPath.ForeColor = Color.DarkGreen;
                }

                // 后台读取配置，完成后回到 UI 线程更新
                Task.Run(async () =>
                {
                    var configs = await LoadConfigsForPath(path);
                    BeginInvoke(() =>
                    {
                        if (_detectionResult != null)
                        {
                            _detectionResult.Configs = configs;
                            _detectionResult.SelectedConfigName = _config.OneDragonConfig;
                        }
                    });
                });
            }
            else
            {
                MessageBox.Show("所选目录中未找到 BetterGI.exe，请确认。", "未找到",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    /// <summary>
    /// 后台读取 BetterGI 的 OneDragon 配置列表（纯数据，不修改 UI 状态）
    /// </summary>
    private static async Task<List<BetterGIDetector.OneDragonConfigInfo>> LoadConfigsForPath(string path)
    {
        var configs = new List<BetterGIDetector.OneDragonConfigInfo>();
        try
        {
            var mainCfg = Path.Combine(path, "User", "config.json");
            string? selectedName = null;
            if (File.Exists(mainCfg))
            {
                var json = await File.ReadAllTextAsync(mainCfg);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("selectedOneDragonFlowConfigName", out var sel))
                    selectedName = sel.GetString();
            }

            var odDir = Path.Combine(path, "User", "OneDragon");
            if (Directory.Exists(odDir))
            {
                foreach (var f in Directory.EnumerateFiles(odDir, "*.json"))
                {
                    try
                    {
                        var fj = await File.ReadAllTextAsync(f);
                        using var doc = System.Text.Json.JsonDocument.Parse(fj);
                        var name = doc.RootElement.TryGetProperty("Name", out var n)
                            ? n.GetString() ?? Path.GetFileNameWithoutExtension(f)
                            : Path.GetFileNameWithoutExtension(f);
                        configs.Add(new BetterGIDetector.OneDragonConfigInfo
                        {
                            Name = name,
                            FilePath = f,
                            IsSelected = name == selectedName
                        });
                    }
                    catch { }
                }
            }
        }
        catch { }
        return configs;
    }

    // ==== Step 3: 选择配置 ====
    private void BuildStep3()
    {
        _lblStepTitle.Text = "③ 选择一条龙配置";
        _lblStepDesc.Text = "选择 BetterGI 中要使用的一条龙任务配置。";
        _btnNext.Text = "下一步 →";

        var lbl = new Label
        {
            Text = "一条龙配置名称：",
            Location = new Point(16, 20),
            AutoSize = true,
            Font = new Font("Microsoft YaHei", 9.5f)
        };

        _cboConfig = new ComboBox
        {
            Location = new Point(16, 48),
            Width = 300,
            Font = new Font("Microsoft YaHei", 9.5f),
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        // 填充配置列表
        if (_detectionResult?.Configs.Count > 0)
        {
            foreach (var cfg in _detectionResult.Configs)
                _cboConfig.Items.Add(cfg.Name);

            var selectedIdx = _detectionResult.Configs.FindIndex(c => c.IsSelected);
            _cboConfig.SelectedIndex = selectedIdx >= 0 ? selectedIdx : 0;
        }
        else
        {
            _cboConfig.Items.Add("默认配置");
            _cboConfig.SelectedIndex = 0;
        }

        _cboConfig.SelectedIndexChanged += (_, _) =>
        {
            if (_cboConfig.SelectedItem != null)
                _config.OneDragonConfig = _cboConfig.SelectedItem.ToString()!;
        };

        var lblHint = new Label
        {
            Text = "💡 这是 BetterGI 的「一条龙」任务配置名称。\n" +
                   "如果你不清楚，保持「默认配置」即可。",
            Location = new Point(16, 84),
            AutoSize = true,
            Font = new Font("Microsoft YaHei", 8.5f),
            ForeColor = Color.DimGray,
            MaximumSize = new Size(490, 0)
        };

        _contentPanel.Controls.AddRange([lbl, _cboConfig, lblHint]);
    }

    // ==== Step 4: 参数设置 ====
    private void BuildStep4()
    {
        _lblStepTitle.Text = "④ 设置触发参数";
        _lblStepDesc.Text = "调整触发条件和执行选项。";
        _btnNext.Text = "完成 →";

        var y = 16;

        // 空闲阈值
        var lblIdle = new Label
        {
            Text = "空闲触发阈值（分钟）：",
            Location = new Point(16, y),
            AutoSize = true,
            Font = new Font("Microsoft YaHei", 9.5f)
        };
        y += 24;
        _numIdle = new NumericUpDown
        {
            Location = new Point(16, y),
            Width = 80,
            Minimum = 1,
            Maximum = 120,
            Value = _config.IdleThresholdMinutes
        };
        y += 36;

        // 最小间隔
        var lblGap = new Label
        {
            Text = "最小执行间隔（小时）：",
            Location = new Point(16, y),
            AutoSize = true,
            Font = new Font("Microsoft YaHei", 9.5f)
        };
        y += 24;
        _numGap = new NumericUpDown
        {
            Location = new Point(16, y),
            Width = 80,
            Minimum = 1,
            Maximum = 48,
            Value = _config.MinGapHours
        };
        y += 40;

        // 静音
        _chkMute = new CheckBox
        {
            Text = "执行期间静音系统（需要管理员权限）",
            Location = new Point(16, y),
            AutoSize = true,
            Font = new Font("Microsoft YaHei", 9.5f),
            Checked = _config.MuteSystem
        };
        y += 28;

        // 开机自启
        _chkAutoStart = new CheckBox
        {
            Text = "开机自动启动 AutoDaily",
            Location = new Point(16, y),
            AutoSize = true,
            Font = new Font("Microsoft YaHei", 9.5f),
            Checked = _config.AutoStart
        };

        _contentPanel.Controls.AddRange([
            lblIdle, _numIdle, lblGap, _numGap, _chkMute, _chkAutoStart
        ]);
    }

    // ==== Step 5: 完成 ====
    private void BuildStep5()
    {
        _lblStepTitle.Text = "⑤ 完成！";
        _lblStepDesc.Text = "一切就绪。点击「完成」开始使用。";
        _btnNext.Text = "✅ 完成";

        // 只在首次进入 Step 5 时保存配置和注册自启（防止反复前进后退重复注册）
        if (!_step5Configured)
        {
            _step5Configured = true;

            // 保存配置
            _config.IdleThresholdMinutes = (int)(_numIdle?.Value ?? 15);
            _config.MinGapHours = (int)(_numGap?.Value ?? 20);
            _config.MuteSystem = _chkMute?.Checked ?? true;
            _config.AutoStart = _chkAutoStart?.Checked ?? false;
            _config.IsFirstRun = false;

            // 处理开机自启
            if (_config.AutoStart)
                RegisterAutoStart();
            else
                UnregisterAutoStart();
        }

        var lbl = new Label
        {
            Text = "🎉 设置完成！\n\n" +
                   $"BetterGI 路径：{_config.BetterGIPath}\n" +
                   $"一条龙配置：{_config.OneDragonConfig}\n" +
                   $"空闲阈值：{_config.IdleThresholdMinutes} 分钟\n" +
                   $"最小间隔：{_config.MinGapHours} 小时\n" +
                   $"执行静音：{(_config.MuteSystem ? "是" : "否")}\n" +
                   $"开机自启：{(_config.AutoStart ? "是" : "否")}\n\n" +
                   "关闭此窗口后，监控将在后台自动运行。\n" +
                   "你可以在系统托盘中找到 AutoDaily 图标。",
            Location = new Point(16, 12),
            AutoSize = true,
            Font = new Font("Microsoft YaHei", 9.5f),
            MaximumSize = new Size(490, 0)
        };

        _contentPanel.Controls.Add(lbl);
    }

    // ==== 开机自启注册 ====

    private static void RegisterAutoStart()
    {
        try
        {
            var startupPath = Environment.GetFolderPath(
                Environment.SpecialFolder.Startup);
            var shortcutPath = Path.Combine(startupPath, "AutoDaily.lnk");

            // 简单方案：写注册表 Run 键
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key != null)
            {
                var exePath = Application.ExecutablePath;
                key.SetValue("AutoDaily", $"\"{exePath}\" --silent");
            }

            // 同时创建 Startup 快捷方式作为兜底
            if (!File.Exists(shortcutPath))
            {
                // 使用 COM 创建快捷方式太复杂，注册表方案已经足够可靠
            }
        }
        catch { /* 权限不足静默失败 */ }
    }

    private static void UnregisterAutoStart()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true);
            key?.DeleteValue("AutoDaily", false);
        }
        catch { }
    }
}
