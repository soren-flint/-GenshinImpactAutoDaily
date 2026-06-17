using System.Diagnostics;

namespace AutoDaily;

/// <summary>
/// Mutex 单实例保护。防止用户同时运行多个 AutoDaily.exe。
/// </summary>
public static class SingleInstance
{
    private const string MutexName = "Local\\AutoDaily_DualGame_Monitor";
    private static Mutex? _mutex;

    /// <summary>
    /// 尝试获取单实例锁。
    /// </summary>
    /// <returns>true = 首个实例，可继续运行；false = 已有实例在运行</returns>
    public static bool TryAcquire()
    {
        _mutex = new Mutex(true, MutexName, out bool createdNew);

        if (createdNew)
        {
            return true;
        }

        // 已有实例：尝试唤出它的主窗口
        BringExistingToFront();
        return false;
    }

    /// <summary>
    /// 查找已有 AutoDaily 进程并尝试将其主窗口带到前台
    /// </summary>
    private static void BringExistingToFront()
    {
        try
        {
            var current = Process.GetCurrentProcess();
            var others = Process.GetProcessesByName(current.ProcessName)
                .Where(p => p.Id != current.Id);

            foreach (var p in others)
            {
                if (p.MainWindowHandle != IntPtr.Zero)
                {
                    NativeMethods.ShowWindow(p.MainWindowHandle, 9); // SW_RESTORE
                    NativeMethods.SetForegroundWindow(p.MainWindowHandle);
                    break;
                }
            }
        }
        catch { /* 唤出失败静默 */ }
    }

    /// <summary>
    /// 释放 Mutex（应用退出时调用）
    /// </summary>
    public static void Release()
    {
        _mutex?.Close();
        _mutex = null;
    }
}
