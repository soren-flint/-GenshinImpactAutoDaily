using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoDaily;

public class AppConfig
{
    // ---- 持久化路径（统一使用数据目录，不再相对 EXE 位置）----

    private static string ConfigPath => Path.Combine(LogWriter.GetDataDirectory(), "config.json");

    [JsonIgnore]
    public string MarkerFile => Path.Combine(LogWriter.GetDataDirectory(), ".bettergi_last_run");

    // ---- 配置字段 ----

    public string BetterGIPath { get; set; } = "";
    public string OneDragonConfig { get; set; } = "默认配置";
    public int IdleThresholdMinutes { get; set; } = 15;
    public int MinGapHours { get; set; } = 20;
    public bool MuteSystem { get; set; } = true;
    public bool AutoStart { get; set; } = false;
    public int MonitorIntervalSeconds { get; set; } = 60;
    public bool IsFirstRun { get; set; } = true; // 是否首次运行（决定是否弹出设置向导）

    // ---- 计算属性 ----

    [JsonIgnore]
    public int IdleThresholdSeconds => IdleThresholdMinutes * 60;

    [JsonIgnore]
    public int MinGapSeconds => MinGapHours * 3600;

    // ---- 加载 / 保存 ----

    /// <summary>
    /// 加载配置。优先级：数据目录 > EXE 同目录（迁移旧配置）
    /// </summary>
    public static AppConfig Load()
    {
        // 1. 尝试数据目录
        if (File.Exists(ConfigPath))
        {
            try
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                if (cfg != null)
                {
                    cfg.Sanitize();
                    return cfg;
                }
            }
            catch { /* 解析失败 → 继续尝试旧位置 */ }
        }

        // 2. 尝试 EXE 同目录（从旧版本迁移）
        var legacyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        if (File.Exists(legacyPath))
        {
            try
            {
                var json = File.ReadAllText(legacyPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                if (cfg != null)
                {
                    cfg.IsFirstRun = false; // 有旧配置说明不是首次运行
                    cfg.Sanitize();
                    cfg.Save(); // 迁移到数据目录
                    return cfg;
                }
            }
            catch { }
        }

        // 3. 全新默认
        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(ConfigPath, json);
        }
        catch { /* 保存失败不崩溃 */ }
    }

    /// <summary>
    /// 确保配置值合法
    /// </summary>
    private void Sanitize()
    {
        if (IdleThresholdMinutes < 1) IdleThresholdMinutes = 15;
        if (MinGapHours < 1) MinGapHours = 20;
        if (MonitorIntervalSeconds < 10) MonitorIntervalSeconds = 60;
        if (OneDragonConfig == "默认配置" && BetterGIPath == @"D:\MIHOYO\GenShinTool\BGI")
        {
            // 旧默认值不变（用户未配置过）
        }
    }

    // ---- 标记文件读写（兼容 PS 脚本）----

    /// <summary>
    /// 读取上次执行时间
    /// </summary>
    public DateTime? ReadLastRunTime()
    {
        try
        {
            var resolved = Path.GetFullPath(MarkerFile);
            if (File.Exists(resolved))
            {
                var raw = File.ReadAllText(resolved).Trim();
                if (DateTime.TryParse(raw, out var dt))
                    return dt;
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// 写入当前时间为上次执行时间
    /// </summary>
    public void WriteLastRunTime()
    {
        try
        {
            var resolved = Path.GetFullPath(MarkerFile);
            var dir = Path.GetDirectoryName(resolved);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(resolved,
                DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"));
        }
        catch { }
    }
}
