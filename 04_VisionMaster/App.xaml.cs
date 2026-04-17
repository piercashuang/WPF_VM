using System.Windows;
using VmCamera;

namespace VisionMaster
{
    public partial class App : Application
    {
        protected override void OnExit(ExitEventArgs e)
        {
            VmCameraSdk.Shutdown();
            base.OnExit(e);
        }
    }
}
