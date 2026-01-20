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
using System.Runtime.InteropServices;
using System.Windows.Threading;
using System.Management;
using System.IO;

namespace SlowWhy
{
    public partial class Ram : UserControl
    {
        [DllImport("psapi.dll")]
        public static extern int EmptyWorkingSet(IntPtr hwProc);

        private PerformanceCounter ramCounter;
        private DispatcherTimer timer;

        private float previousRam;
        private float ramValueMb;
        private float currentRam;

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
            ramValueMb = ramCounter.NextValue();
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

        private float ramDiffrence(float newRamValue)
        {
            previousRam = ramValueMb;
            currentRam = newRamValue;
            float delta = currentRam - previousRam;
            return delta;
        }

        private async void btnRamClear_Click(object sender, RoutedEventArgs e)
        {
            btnRamClear.IsEnabled = false;
            btnRamClear.Content = "Optimizing...";

            await Task.Run(() =>
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();

                var processes = Process.GetProcesses();
                foreach (var p in processes)
                {
                    try
                    {
                        if (!p.HasExited)
                        {
                            EmptyWorkingSet(p.Handle);
                        }
                    }
                    catch
                    {
                    }
                }
            });

            btnRamClear.IsEnabled = true;
            btnRamClear.Content = "Clean RAM";
            float newRam = ramCounter.NextValue();
            float diffrence = ramDiffrence(newRam);
            float diffrenceGB = diffrence / 1024;
            MessageBox.Show($"{diffrenceGB:F2}GB Evacuated");
        }
    }
}