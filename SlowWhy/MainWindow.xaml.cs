using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.Management;
using System.IO;

namespace SlowWhy
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer timer;
        private PerformanceCounter cpuCounter;
        private PerformanceCounter ramCounter;

        [DllImport("psapi.dll")]
        public static extern int EmptyWorkingSet(IntPtr hwProc);

        public MainWindow()
        {
            InitializeComponent();
            InitializeApp();
            GetStaticHardwareInfo();
            systemStatus();
        }

        private void InitializeApp()
        {
            try
            {
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                ramCounter = new PerformanceCounter("Memory", "Available MBytes");

                timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(1);
                timer.Tick += Timer_Tick;
            }
            catch { }
        }

        // GPU Name
        private void GetStaticHardwareInfo()
        {
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
                foreach (ManagementObject mo in searcher.Get())
                {
                    txtGpu.Text = mo["Name"].ToString();
                    break;
                }
            }
            catch
            {
                txtGpu.Text = "Not Detected";
            }
        }

        private void systemStatus()
        {
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // CPU
            float cpuValue = cpuCounter.NextValue();
            txtCpu.Text = $"%{cpuValue:F0}";

            //RAM
            float ramValueMb = ramCounter.NextValue();
            txtRam.Text = $"{ramValueMb / 1024.0:F2} GB";

            //DISK (C)
            try
            {
                DriveInfo cDrive = new DriveInfo("C");
                if (cDrive.IsReady)
                {
                    double freeSpaceGb = cDrive.TotalFreeSpace / (1024.0 * 1024.0 * 1024.0);
                    txtDisk.Text = $"{freeSpaceGb:F0} GB";
                }
            }
            catch { }

            // CPU Color
            if (cpuValue > 80) txtCpu.Foreground = Brushes.Red;
            else if (cpuValue > 50) txtCpu.Foreground = Brushes.Orange;
            else txtCpu.Foreground = Brushes.LightGreen;
        }

        private void DiskCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MessageBox.Show("Details");
        }

        private void CPU_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MessageBox.Show("Cpu Informations");
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