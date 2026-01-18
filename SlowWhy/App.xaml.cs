using System.Configuration;
using System.Data;
using System.Runtime.InteropServices;
using System.Windows;

namespace SlowWhy
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);

        protected override void OnStartup(StartupEventArgs e)
        {
            SetCurrentProcessExplicitAppUserModelID("com.efe.slowwhy");
            base.OnStartup(e);
        }
    }

}
