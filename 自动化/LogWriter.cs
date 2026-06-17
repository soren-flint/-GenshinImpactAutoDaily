namespace AutoDaily;

/// <summary>
/// 统一日志与状态文件写入。兼容 IdleMonitor.ps1 的格式。
/// 自动检测数据目录：如果在 每日驱动 树下则沿用项目根目录，否则写入 %APPDATA%\AutoDaily\。
/// </summary>
public static class LogWriter
{
    private static readonly object _lock = new();
    private static string? _dataDir;
    private const int MaxLogLines = 800;

    // 执行次数缓存
    private static int _cachedCount = -1;
    private static DateTime _cacheTime = DateTime.MinValue;

    /// <summary>
    /// 获取数据目录（config、log、marker、status 的存放位置）
    /// </summary>
    public static string GetDataDirectory()
    {
        if (_dataDir != null) return _dataDir;

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var candidate = baseDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (int i = 0; i < 3; i++)
        {
            if (File.Exists(Path.Combine(candidate, "IdleMonitor.ps1")) ||
                File.Exists(Path.Combine(candidate, ".bettergi_last_run")))
            {
                _dataDir = candidate;
                return _dataDir;
            }
            candidate = Path.GetDirectoryName(candidate);
            if (candidate == null) break;
        }

        _dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AutoDaily");
        Directory.CreateDirectory(_dataDir);
        return _dataDir;
    }

    /// <summary>
    /// 写入 Execution-Log.md（追加，与 PS 脚本格式完全一致）
    /// </summary>
    public static void WriteLog(string tag, string msg)
    {
        lock (_lock)
        {
            try
            {
                var logPath = Path.Combine(GetDataDirectory(), "Execution-Log.md");
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var divider = "==================================================";
                var entry = $"\n{divider}\n\n[{tag}]  {timestamp}\n{msg}\n";

                File.AppendAllText(logPath, entry);
                InvalidateCache();

                // 行数上限截断
                try
                {
                    var lines = File.ReadAllLines(logPath);
                    if (lines.Length > MaxLogLines)
                    {
                        var keep = lines.Skip(lines.Length - MaxLogLines / 2).ToArray();
                        File.WriteAllLines(logPath, keep);
                    }
                }
                catch { }
            }
            catch { }
        }
    }

    /// <summary>
    /// 写入 status.txt（覆盖，单行格式，与 PS 脚本一致）
    /// </summary>
    public static void WriteStatus(int gapSec, int idleSec, string state)
    {
        lock (_lock)
        {
            try
            {
                var statusPath = Path.Combine(GetDataDirectory(), "status.txt");
                string gapH, gapM;
                if (gapSec >= 99999999)
                {
                    gapH = "N/A"; gapM = "N/A";
                }
                else
                {
                    gapH = (gapSec / 3600).ToString();
                    gapM = ((gapSec % 3600) / 60).ToString();
                }
                var idleM = (idleSec / 60).ToString();
                var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                var line = $"# Monitor: ALIVE | Time: {now} | Gap: {gapH} h {gapM} m / 20h | Idle: {idleM} min / 15min | State: {state}";
                File.WriteAllText(statusPath, line);
            }
            catch { }
        }
    }

    /// <summary>
    /// 统计总共成功执行次数（[DONE] 去重 + [手动] 成功的）。
    /// 每 30 秒缓存一次，避免频繁解析日志文件。
    /// </summary>
    public static int CountSuccesses()
    {
        if (_cachedCount >= 0 && (DateTime.Now - _cacheTime).TotalSeconds < 30)
            return _cachedCount;

        try
        {
            var logPath = Path.Combine(GetDataDirectory(), "Execution-Log.md");
            if (!File.Exists(logPath))
            {
                _cachedCount = 0;
                _cacheTime = DateTime.Now;
                return 0;
            }

            var lines = File.ReadAllLines(logPath);
            var seenDates = new HashSet<string>();
            int count = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                // [DONE] 行：按日期去重（同一天多次写入只算一次）
                if (line.StartsWith("[DONE]"))
                {
                    var date = line.Length > 18 ? line.Substring(8, 10) : line;
                    if (seenDates.Add(date))
                        count++;
                }
                // [手动] 行：下一行如果有 "成功" 才算
                else if (line.StartsWith("[手动]"))
                {
                    if (i + 1 < lines.Length && lines[i + 1].Contains("成功"))
                        count++;
                }
            }

            _cachedCount = count;
            _cacheTime = DateTime.Now;
            return count;
        }
        catch
        {
            return _cachedCount >= 0 ? _cachedCount : 0;
        }
    }

    /// <summary>
    /// 清除统计缓存（新日志写入时调用）
    /// </summary>
    public static void InvalidateCache()
    {
        _cachedCount = -1;
    }
}
