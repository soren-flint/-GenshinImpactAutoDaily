using System.Reflection;

namespace AutoDaily;

/// <summary>
/// 统一管理嵌入的图标资源。
/// 托盘图标从 EXE 的 ApplicationIcon 提取，主窗口 Logo 从嵌入的 PNG 资源加载。
/// </summary>
public static class AppIcon
{
    private static Icon? _trayIcon;
    private static Image? _logo;
    private static readonly object _lock = new();

    /// <summary>
    /// 获取托盘图标（从 EXE 提取，自带多尺寸）
    /// </summary>
    public static Icon GetTrayIcon()
    {
        if (_trayIcon != null) return _trayIcon;
        lock (_lock)
        {
            if (_trayIcon != null) return _trayIcon;
            try
            {
                _trayIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
                    ?? SystemIcons.Application;
            }
            catch
            {
                _trayIcon = SystemIcons.Application;
            }
            return _trayIcon;
        }
    }

    /// <summary>
    /// 获取 Logo 图像（从嵌入的 app.png 加载，256x256）
    /// </summary>
    public static Image GetLogo()
    {
        if (_logo != null) return _logo;
        lock (_lock)
        {
            if (_logo != null) return _logo;
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using var stream = asm.GetManifestResourceStream("AutoDaily.app.png");
                if (stream != null)
                {
                    _logo = Image.FromStream(stream);
                    return _logo;
                }
            }
            catch { }
            // 回退到默认
            _logo = SystemIcons.Application.ToBitmap();
            return _logo;
        }
    }
}
