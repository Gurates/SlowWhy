using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.Management;
using System.IO;
using LibreHardwareMonitor.Hardware;

namespace SlowWhy
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer timer;
        private PerformanceCounter cpuCounter;
        private PerformanceCounter ramCounter;
        private double freeSpaceGb;
        private float previousRam;
        private float ramValueMb;
        private float currentRam;
        private Computer _computer;
        private float rpm;
        private bool isDarkTheme = false;

        [DllImport("psapi.dll")]
        public static extern int EmptyWorkingSet(IntPtr hwProc);

        public MainWindow()
        {
            InitializeComponent();
            InitializeApp();
            GetStaticHardwareInfo();
            systemStatus();

            _computer = new Computer()
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled = true,
            };
            _computer.Open();
            this.Closed += (s, e) =>
            {
                timer.Stop();
                _computer.Close();
            };
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
            float cpuValue = cpuCounter.NextValue();
            txtCpu.Text = $"%{cpuValue:F0}";

            ramValueMb = ramCounter.NextValue();
            txtRam.Text = $"{ramValueMb / 1024.0:F2} GB";

            try
            {
                DriveInfo cDrive = new DriveInfo("C");
                if (cDrive.IsReady)
                {
                    freeSpaceGb = cDrive.TotalFreeSpace / (1024.0 * 1024.0 * 1024.0);
                    txtDisk.Text = $"{freeSpaceGb:F0} GB";
                }
            }
            catch { }

            if (cpuValue > 80) txtCpu.Foreground = Brushes.Red;
            else if (cpuValue > 50) txtCpu.Foreground = Brushes.Orange;
            else txtCpu.Foreground = Brushes.Green;

            if (freeSpaceGb < 30) txtDisk.Foreground = Brushes.Red;
            else if (freeSpaceGb < 50) txtDisk.Foreground = Brushes.Orange;
            else txtDisk.Foreground = Brushes.Green;

            if (ramValueMb < 3072) txtRam.Foreground = Brushes.Red;
            else if (ramValueMb < 4096) txtRam.Foreground = Brushes.Orange;
            else txtRam.Foreground = Brushes.Green;

            FanSpeed();
            if (rpm > 3000) pbFan.Foreground = Brushes.Red;
            else if (rpm > 2000) pbFan.Foreground = Brushes.Orange;
            else pbFan.Foreground = Brushes.Green;
        }

        private float ramDiffrence(float newRamValue)
        {
            previousRam = ramValueMb;
            currentRam = newRamValue;
            float delta = currentRam - previousRam;
            return delta;
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
                btnRamClear.Content = "Quick Clean RAM";

                float newRam = ramCounter.NextValue();
                float diffrence = ramDiffrence(newRam);
                float diffrenceGB = diffrence / 1024;
                MessageBox.Show($"{diffrenceGB:F2} GB Evacuated", "Quick Clean", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        private void DiskCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MainDashboard.Visibility = Visibility.Collapsed;
            PagesContainer.Visibility = Visibility.Visible;
            PagesContainer.Content = new Disk();
        }

        private void CPU_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MainDashboard.Visibility = Visibility.Collapsed;
            PagesContainer.Visibility = Visibility.Visible;
            PagesContainer.Content = new CPU();
        }

        private void Ram_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MainDashboard.Visibility = Visibility.Collapsed;
            PagesContainer.Visibility = Visibility.Visible;
            PagesContainer.Content = new Ram();
        }

        private void GPU_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MainDashboard.Visibility = Visibility.Collapsed;
            PagesContainer.Visibility = Visibility.Visible;
            PagesContainer.Content = new GPU();
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MenuDashboard_Click(object sender, RoutedEventArgs e)
        {
            PagesContainer.Visibility = Visibility.Collapsed;
            MainDashboard.Visibility = Visibility.Visible;
            PagesContainer.Content = null;
        }

        private void themeSwitch(object sender, RoutedEventArgs e)
        {
            isDarkTheme = !isDarkTheme;

            var themeUri = isDarkTheme
                ? new Uri("Themes/DarkTheme.xaml", UriKind.Relative)
                : new Uri("Themes/LightTheme.xaml", UriKind.Relative);

            var newTheme = new ResourceDictionary { Source = themeUri };

            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(newTheme);

            menuTheme.Header = isDarkTheme ? "Use Light Theme" : "Use Dark Theme";
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Test");
        }

        private void MenuRefresh_Click(object sender, RoutedEventArgs e)
        {
            GetStaticHardwareInfo();
            MessageBox.Show("System data refreshed!", "Refresh", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuGithub_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/Gurates/SlowWhy",
                UseShellExecute = true
            });
        }

        private void FanSpeed()
        {
            if (_computer == null) return;

            foreach (IHardware hardware in _computer.Hardware)
            {
                if (hardware == null) continue;
                hardware.Update();
                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Fan)
                    {
                        rpm = sensor.Value ?? 0;

                        if (rpm > 0)
                        {
                            txtFan.Text = $"{rpm:F0} RPM";
                            pbFan.Value = rpm;
                        }
                        return;
                    }
                }
            }
        }
    }
}