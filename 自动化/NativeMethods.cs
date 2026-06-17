using System.Runtime.InteropServices;

namespace AutoDaily;

/// <summary>
/// 通过 user32.dll GetLastInputInfo 检测用户空闲时间（秒）
/// 与 IdleMonitor.ps1 中的 C# inline 代码完全一致
/// </summary>
public static class NativeMethods
{
    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    public static uint GetIdleSeconds()
    {
        var info = new LASTINPUTINFO();
        info.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
        if (!GetLastInputInfo(ref info))
            return 0;

        // 使用 long 避免 uint 溢出（TickCount 约 24.8 天回绕）
        var idle = (long)Environment.TickCount - info.dwTime;
        if (idle < 0) idle += 0x100000000L; // TickCount 回绕修正
        return (uint)(idle / 1000);
    }

    // ---- 窗口管理（单实例唤出已存在窗口）----

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
}
