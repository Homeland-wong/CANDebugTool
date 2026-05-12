using System.Windows;

namespace CANDebugTool
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 确保退出时关闭所有设备
            Services.CanDeviceService.Instance?.Dispose();
            base.OnExit(e);
        }
    }
}
