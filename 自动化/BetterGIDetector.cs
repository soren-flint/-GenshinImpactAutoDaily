using System.Diagnostics;
using System.Text.Json;

namespace AutoDaily;

/// <summary>
/// 自动检测 BetterGI 安装位置和一条龙配置列表。
/// </summary>
public static class BetterGIDetector
{
    public class DetectionResult
    {
        /// <summary>是否成功检测到 BetterGI 路径</summary>
        public bool Found { get; set; }
        /// <summary>BetterGI 安装目录（包含 BetterGI.exe）</summary>
        public string? Path { get; set; }
        /// <summary>检测来源</summary>
        public string? Source { get; set; }
        /// <summary>所有可用的 OneDragon 配置</summary>
        public List<OneDragonConfigInfo> Configs { get; set; } = new();
        /// <summary>当前选中的配置名</summary>
        public string? SelectedConfigName { get; set; }
        /// <summary>检测过程的中间信息（用于 UI 展示）</summary>
        public List<string> Messages { get; set; } = new();
    }

    public class OneDragonConfigInfo
    {
        public string Name { get; set; } = "";
        public string FilePath { get; set; } = "";
        public bool IsSelected { get; set; }

        public override string ToString() => Name;
    }

    /// <summary>
    /// 已知的 BetterGI 常见安装路径
    /// </summary>
    private static readonly string[] CommonPaths =
    {
        @"D:\MIHOYO\GenShinTool\BGI",
        @"C:\BetterGI",
        @"C:\Program Files\BetterGI",
        @"C:\Program Files (x86)\BetterGI",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "BetterGI"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "BetterGI"),
    };

    /// <summary>
    /// 要搜索的盘符（全盘搜索时）
    /// </summary>
    private static readonly string[] SearchDrives = { "D:", "C:", "E:", "F:" };

    /// <summary>
    /// 综合检测：策略①进程 → 策略②常见路径 → 策略③快速搜索 → 策略④返回未找到
    /// 不包含用户浏览（由 UI 层处理）
    /// </summary>
    public static async Task<DetectionResult> DetectAsync(IProgress<string>? progress = null)
    {
        var result = new DetectionResult();

        // 策略①：进程检测
        progress?.Report("正在检查 BetterGI 是否正在运行...");
        result.Messages.Add("[1/3] 检查运行中的 BetterGI 进程...");

        var procPath = DetectFromProcess();
        if (procPath != null)
        {
            result.Found = true;
            result.Path = procPath;
            result.Source = "检测到 BetterGI 正在运行";
            result.Messages.Add($"  ✅ 找到！来源：正在运行的进程 → {procPath}");
            await FillConfigsAsync(result, progress);
            return result;
        }
        result.Messages.Add("  ❌ 未检测到 BetterGI 进程");

        // 策略②：常见路径
        progress?.Report("正在搜索常见安装位置...");
        result.Messages.Add("[2/3] 搜索常见安装路径...");

        var commonPath = DetectFromCommonPaths();
        if (commonPath != null)
        {
            result.Found = true;
            result.Path = commonPath;
            result.Source = "在常见路径中找到";
            result.Messages.Add($"  ✅ 找到！位置：{commonPath}");
            await FillConfigsAsync(result, progress);
            return result;
        }
        result.Messages.Add("  ❌ 常见路径中未找到");

        // 策略③：快速搜索（仅搜索盘符根目录下 2 层，限时 10 秒）
        progress?.Report("正在搜索磁盘（10秒内）...");
        result.Messages.Add("[3/3] 快速搜索磁盘中的 BetterGI.exe...");

        var searchPath = await Task.Run(() => QuickSearch());
        if (searchPath != null)
        {
            result.Found = true;
            result.Path = searchPath;
            result.Source = "磁盘搜索找到";
            result.Messages.Add($"  ✅ 找到！位置：{searchPath}");
            await FillConfigsAsync(result, progress);
            return result;
        }
        result.Messages.Add("  ❌ 自动搜索未找到 BetterGI.exe");
        result.Messages.Add("");
        result.Messages.Add("👉 请手动浏览选择 BetterGI 安装目录。");

        return result;
    }

    /// <summary>
    /// 策略①：从运行中的进程检测
    /// </summary>
    private static string? DetectFromProcess()
    {
        try
        {
            var procs = Process.GetProcessesByName("BetterGI");
            foreach (var p in procs)
            {
                try
                {
                    var mainModule = p.MainModule;
                    if (mainModule != null)
                    {
                        var dir = Path.GetDirectoryName(mainModule.FileName);
                        if (dir != null && File.Exists(Path.Combine(dir, "BetterGI.exe")))
                        {
                            return dir;
                        }
                    }
                }
                catch { /* 权限不足时 MainModule 抛异常 */ }
                finally { p.Dispose(); }
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// 策略②：遍历常见路径
    /// </summary>
    private static string? DetectFromCommonPaths()
    {
        foreach (var path in CommonPaths)
        {
            if (Directory.Exists(path) && File.Exists(Path.Combine(path, "BetterGI.exe")))
                return path;
        }
        return null;
    }

    /// <summary>
    /// 策略③：在常见盘符下快速搜索 BetterGI.exe（限深度 4 层，限时 10 秒）
    /// </summary>
    private static string? QuickSearch()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            foreach (var drive in SearchDrives)
            {
                if (cts.IsCancellationRequested) break;
                if (!Directory.Exists(drive)) continue;

                try
                {
                    var result = SearchDirectory(drive, 3, cts.Token);
                    if (result != null) return result;
                }
                catch (OperationCanceledException) { break; }
                catch { /* 权限不足跳过 */ }
            }
        }
        catch { }
        return null;
    }

    private static string? SearchDirectory(string dir, int depth, CancellationToken ct)
    {
        if (depth < 0) return null;
        ct.ThrowIfCancellationRequested();

        try
        {
            // 先检查当前目录
            var exe = Path.Combine(dir, "BetterGI.exe");
            if (File.Exists(exe)) return dir;

            // 跳过明显的系统目录
            var name = Path.GetFileName(dir);
            if (name is "Windows" or "ProgramData" or "$Recycle.Bin" or "System Volume Information")
                return null;

            // 递归子目录（跳过隐藏和系统目录）
            foreach (var sub in Directory.EnumerateDirectories(dir))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var attr = File.GetAttributes(sub);
                    if ((attr & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                        continue;
                }
                catch { continue; }

                var result = SearchDirectory(sub, depth - 1, ct);
                if (result != null) return result;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { }
        return null;
    }

    /// <summary>
    /// 读取 OneDragon 配置列表和当前选中配置
    /// </summary>
    private static async Task FillConfigsAsync(DetectionResult result, IProgress<string>? progress)
    {
        if (result.Path == null) return;

        progress?.Report("正在读取一条龙配置...");
        result.Messages.Add("");
        result.Messages.Add("📋 读取一条龙配置列表...");

        try
        {
            // 读取主配置获取当前选中
            var mainConfigPath = Path.Combine(result.Path, "User", "config.json");
            if (File.Exists(mainConfigPath))
            {
                var json = await File.ReadAllTextAsync(mainConfigPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("selectedOneDragonFlowConfigName", out var selected))
                {
                    result.SelectedConfigName = selected.GetString();
                }
            }

            // 枚举 OneDragon 目录
            var oneDragonDir = Path.Combine(result.Path, "User", "OneDragon");
            if (Directory.Exists(oneDragonDir))
            {
                foreach (var file in Directory.EnumerateFiles(oneDragonDir, "*.json"))
                {
                    try
                    {
                        var fileJson = await File.ReadAllTextAsync(file);
                        using var doc = JsonDocument.Parse(fileJson);
                        var name = doc.RootElement.TryGetProperty("Name", out var nameEl)
                            ? nameEl.GetString() ?? Path.GetFileNameWithoutExtension(file)
                            : Path.GetFileNameWithoutExtension(file);

                        result.Configs.Add(new OneDragonConfigInfo
                        {
                            Name = name,
                            FilePath = file,
                            IsSelected = name == result.SelectedConfigName
                        });
                    }
                    catch { /* 单个文件解析失败不影响其他 */ }
                }
            }

            if (result.Configs.Count > 0)
            {
                result.Messages.Add($"  ✅ 找到 {result.Configs.Count} 个配置");
                if (result.SelectedConfigName != null)
                    result.Messages.Add($"  当前选中：{result.SelectedConfigName}");
            }
            else
            {
                result.Messages.Add("  ⚠️ 未找到一条龙配置文件（将使用默认配置名）");
                result.Configs.Add(new OneDragonConfigInfo
                {
                    Name = "默认配置",
                    IsSelected = true
                });
            }
        }
        catch (Exception ex)
        {
            result.Messages.Add($"  ⚠️ 读取配置失败：{ex.Message}");
            result.Configs.Add(new OneDragonConfigInfo
            {
                Name = "默认配置",
                IsSelected = true
            });
        }
    }
}
