namespace AutoDaily;

/// <summary>
/// 设置窗口 —— BetterGI 位置、一条龙配置、空闲阈值、静音开关、开机自启
/// </summary>
public partial class ConfigForm : Form
{
    private readonly AppConfig _config;

    private readonly Label _lblBetterGI, _lblConfig, _lblIdle, _lblGap;
    private readonly TextBox _txtBetterGI, _txtIdle, _txtGap;
    private readonly ComboBox _cboConfig;
    private readonly Button _btnBrowse, _btnDetect;
    private readonly CheckBox _chkMute, _chkAutoStart;
    private readonly Button _btnSave, _btnCancel;

    public ConfigForm(AppConfig config)
    {
        _config = config;

        Text = "AutoDaily 设置";
        Size = new Size(520, 360);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;

        var y = 20;

        // --- BetterGI 路径 ---
        _lblBetterGI = new Label
        {
            Text = "BetterGI 目录：", Location = new Point(20, y), AutoSize = true
        };
        _txtBetterGI = new TextBox
        {
            Text = config.BetterGIPath,
            Location = new Point(20, y + 22),
            Width = 360
        };
        _btnBrowse = new Button
        {
            Text = "📂", Location = new Point(385, y + 21), Width = 40
        };
        _btnBrowse.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "选择 BetterGI 安装目录（包含 BetterGI.exe）"
            };
            if (!string.IsNullOrEmpty(_txtBetterGI.Text) && Directory.Exists(_txtBetterGI.Text))
                dlg.InitialDirectory = _txtBetterGI.Text;
            if (dlg.ShowDialog() == DialogResult.OK)
                _txtBetterGI.Text = dlg.SelectedPath;
        };

        _btnDetect = new Button
        {
            Text = "🔍 自动检测",
            Location = new Point(430, y + 21),
            Width = 70,
            AutoSize = true
        };
        _btnDetect.Click += async (_, _) =>
        {
            _btnDetect.Enabled = false;
            _btnDetect.Text = "搜索中...";
            try
            {
                var result = await BetterGIDetector.DetectAsync();
                if (result.Found && result.Path != null)
                {
                    _txtBetterGI.Text = result.Path;

                    // 更新配置下拉
                    _cboConfig!.Items.Clear();
                    foreach (var c in result.Configs)
                        _cboConfig.Items.Add(c.Name);
                    if (result.Configs.Count > 0)
                    {
                        var idx = result.Configs.FindIndex(c => c.IsSelected);
                        _cboConfig.SelectedIndex = idx >= 0 ? idx : 0;
                    }

                    MessageBox.Show($"找到 BetterGI！\n路径：{result.Path}\n来源：{result.Source}",
                        "检测成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    var logText = string.Join("\n", result.Messages);
                    MessageBox.Show($"未自动检测到 BetterGI。\n\n{logText}",
                        "未找到", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"检测出错：{ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _btnDetect.Enabled = true;
                _btnDetect.Text = "🔍 自动检测";
            }
        };
        y += 55;

        // --- 一条龙配置 ---
        _lblConfig = new Label
        {
            Text = "一条龙配置名称：", Location = new Point(20, y), AutoSize = true
        };
        _cboConfig = new ComboBox
        {
            Text = config.OneDragonConfig,
            Location = new Point(20, y + 22),
            Width = 220
        };
        _cboConfig.Items.Add(config.OneDragonConfig);
        _cboConfig.SelectedIndex = 0;

        // 如果 BetterGI 路径有效，尝试加载配置列表
        TryLoadConfigs();

        y += 55;

        // --- 空闲阈值 ---
        _lblIdle = new Label
        {
            Text = "空闲触发阈值（分钟）：", Location = new Point(20, y), AutoSize = true
        };
        _txtIdle = new TextBox
        {
            Text = config.IdleThresholdMinutes.ToString(),
            Location = new Point(20, y + 22),
            Width = 80
        };
        y += 55;

        // --- 最小间隔 ---
        _lblGap = new Label
        {
            Text = "最小执行间隔（小时）：", Location = new Point(20, y), AutoSize = true
        };
        _txtGap = new TextBox
        {
            Text = config.MinGapHours.ToString(),
            Location = new Point(20, y + 22),
            Width = 80
        };
        y += 50;

        // --- 静音 ---
        _chkMute = new CheckBox
        {
            Checked = config.MuteSystem,
            Location = new Point(20, y),
            Text = "执行期间静音系统（停止 Windows Audio 服务，需管理员权限）",
            AutoSize = true
        };
        y += 28;

        // --- 开机自启 ---
        _chkAutoStart = new CheckBox
        {
            Checked = config.AutoStart,
            Location = new Point(20, y),
            Text = "开机自动启动 AutoDaily",
            AutoSize = true
        };
        y += 38;

        // --- 按钮 ---
        _btnSave = new Button
        {
            Text = "保存",
            Location = new Point(280, y),
            Width = 100
        };
        _btnSave.Click += OnSave;

        _btnCancel = new Button
        {
            Text = "取消",
            Location = new Point(390, y),
            Width = 100
        };
        _btnCancel.Click += (_, _) => Close();

        Controls.AddRange([
            _lblBetterGI, _txtBetterGI, _btnBrowse, _btnDetect,
            _lblConfig, _cboConfig,
            _lblIdle, _txtIdle,
            _lblGap, _txtGap,
            _chkMute, _chkAutoStart,
            _btnSave, _btnCancel
        ]);
    }

    private void TryLoadConfigs()
    {
        try
        {
            var bgiPath = _txtBetterGI.Text.Trim();
            if (string.IsNullOrEmpty(bgiPath) || !Directory.Exists(bgiPath)) return;

            var odDir = Path.Combine(bgiPath, "User", "OneDragon");
            if (!Directory.Exists(odDir)) return;

            var files = Directory.GetFiles(odDir, "*.json");
            if (files.Length == 0) return;

            _cboConfig.Items.Clear();
            foreach (var f in files)
            {
                try
                {
                    var json = File.ReadAllText(f);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var name = doc.RootElement.TryGetProperty("Name", out var el)
                        ? el.GetString() ?? Path.GetFileNameWithoutExtension(f)
                        : Path.GetFileNameWithoutExtension(f);
                    _cboConfig.Items.Add(name);
                }
                catch
                {
                    _cboConfig.Items.Add(Path.GetFileNameWithoutExtension(f));
                }
            }

            // 尝试选中当前配置
            var currentConfig = _config.OneDragonConfig;
            var idx = _cboConfig.FindStringExact(currentConfig);
            if (idx >= 0)
                _cboConfig.SelectedIndex = idx;
            else if (_cboConfig.Items.Count > 0)
                _cboConfig.SelectedIndex = 0;
        }
        catch { }
    }

    private void OnSave(object? sender, EventArgs e)
    {
        // 验证 BetterGI 路径
        var bgiPath = _txtBetterGI.Text.Trim();
        if (string.IsNullOrEmpty(bgiPath))
        {
            MessageBox.Show("BetterGI 路径不能为空。\n请使用「自动检测」或「📂 浏览」选择目录。",
                "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!File.Exists(Path.Combine(bgiPath, "BetterGI.exe")))
        {
            var answer = MessageBox.Show(
                $"在所选目录中未找到 BetterGI.exe：\n{bgiPath}\n\n确定要保存此路径吗？",
                "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes) return;
        }

        // 验证空闲阈值
        if (!int.TryParse(_txtIdle.Text, out var idle) || idle < 1)
        {
            MessageBox.Show("空闲触发阈值请输入大于 0 的整数（分钟）。",
                "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 验证最小间隔
        if (!int.TryParse(_txtGap.Text, out var gap) || gap < 1)
        {
            MessageBox.Show("最小执行间隔请输入大于 0 的整数（小时）。",
                "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 保存
        _config.BetterGIPath = bgiPath;
        _config.OneDragonConfig = _cboConfig.Text.Trim();
        _config.IdleThresholdMinutes = idle;
        _config.MinGapHours = gap;
        _config.MuteSystem = _chkMute.Checked;
        _config.AutoStart = _chkAutoStart.Checked;
        _config.Save();

        // 开机自启
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key != null)
            {
                if (_config.AutoStart)
                    key.SetValue("AutoDaily", $"\"{Application.ExecutablePath}\" --silent");
                else
                    key.DeleteValue("AutoDaily", false);
            }
        }
        catch { }

        MessageBox.Show("设置已保存。", "保存成功",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        Close();
    }
}
