using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using System.Management;
using System.IO;

namespace SlowWhy
{
    /// <summary>
    /// UserControl1.xaml etkileşim mantığı
    /// </summary>
    public partial class Ram : UserControl
    {

        [DllImport("psapi.dll")]
        public static extern int EmptyWorkingSet(IntPtr hwProc);

        private PerformanceCounter ramCounter;
        private DispatcherTimer timer;
        public Ram()
        {
            InitializeComponent();
            InitializeApp();
            timer.Start();
        }

        private void InitializeApp()
        {
            try
            {
                ramCounter = new PerformanceCounter("Memory", "Available MBytes");

                timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(1);
                timer.Tick += Timer_Tick;
            }
            catch { }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            float ramValueMb = ramCounter.NextValue();
            txtRam.Text = $"{ramValueMb / 1024.0:F2} GB";
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;

            if (mainWindow != null)
            {
                mainWindow.MainDashboard.Visibility = Visibility.Visible;
                mainWindow.PagesContainer.Visibility = Visibility.Collapsed;
            }
        }

        private void btnRamClear_Click(object sender, RoutedEventArgs e)
        {
            btnRamClear.Content = "Cleaning...";
            Dispatcher.InvokeAsync(() =>
            {
                Process[] processes = Process.GetProcesses();
                foreach (Process p in processes)
                {
                    try { if (!p.HasExited) EmptyWorkingSet(p.Handle); } catch { }
                }
                btnRamClear.Content = "Clean RAM";
            });
        }
    }
}
