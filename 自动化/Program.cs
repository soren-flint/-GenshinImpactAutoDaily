namespace AutoDaily;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // 单实例检测
        if (!SingleInstance.TryAcquire())
        {
            MessageBox.Show("AutoDaily 已在运行中。\n\n请查看系统托盘图标。",
                "AutoDaily", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();

        var showMain = !args.Contains("--silent", StringComparer.OrdinalIgnoreCase);

        var context = new TrayApplicationContext(showMain);
        Application.Run(context);

        SingleInstance.Release();
    }
}
