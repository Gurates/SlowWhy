using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using HandyControl.Themes;
using LibreHardwareMonitor.Hardware;

namespace SlowWhy
{
    public partial class MainWindow : HandyControl.Controls.Window
    {
        private DispatcherTimer _timer;
        private PerformanceCounter _cpuCounter;
        private PerformanceCounter _ramCounter;
        private Computer _computer;
        private double _freeSpaceGb;
        private float _ramValueMb;

        [DllImport("psapi.dll")]
        public static extern int EmptyWorkingSet(IntPtr hwProc);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public MainWindow()
        {
            InitializeComponent();
            InitializeMonitoring();
            LoadStaticInfo();
            StartMonitoring();

            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled = true,
            };
            _computer.Open();

            Loaded += (_, _) => SetDarkMode(ThemeManager.Current.ApplicationTheme == ApplicationTheme.Dark);

            Closed += (_, _) =>
            {
                _timer.Stop();
                _computer.Close();
            };
        }

        private void InitializeMonitoring()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _ramCounter = new PerformanceCounter("Memory", "Available MBytes");

                _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _timer.Tick += OnTimerTick;
            }
            catch { }
        }

        private void LoadStaticInfo()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                foreach (ManagementObject mo in searcher.Get())
                {
                    txtGpu.Text = mo["Name"]?.ToString() ?? "Unknown";
                    break;
                }
            }
            catch
            {
                txtGpu.Text = "Not Detected";
            }
        }

        private void StartMonitoring()
        {
            _timer.Start();
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            float cpuValue = _cpuCounter.NextValue();
            txtCpu.Text = $"{cpuValue:F0}%";

            _ramValueMb = _ramCounter.NextValue();
            txtRam.Text = $"{_ramValueMb / 1024.0:F2} GB";

            try
            {
                var cDrive = new DriveInfo("C");
                if (cDrive.IsReady)
                {
                    _freeSpaceGb = cDrive.TotalFreeSpace / (1024.0 * 1024.0 * 1024.0);
                    txtDisk.Text = $"{_freeSpaceGb:F0} GB";
                }
            }
            catch { }

            txtCpu.Foreground = cpuValue > 80 ? Brushes.Red : cpuValue > 50 ? Brushes.Orange : Brushes.Green;
            txtDisk.Foreground = _freeSpaceGb < 30 ? Brushes.Red : _freeSpaceGb < 50 ? Brushes.Orange : Brushes.Green;
            txtRam.Foreground = _ramValueMb < 3072 ? Brushes.Red : _ramValueMb < 4096 ? Brushes.Orange : Brushes.Green;
        }

        private void btnRamClear_Click(object sender, RoutedEventArgs e)
        {
            float ramBefore = _ramValueMb;

            Dispatcher.InvokeAsync(() =>
            {
                foreach (var p in Process.GetProcesses())
                {
                    try { if (!p.HasExited) EmptyWorkingSet(p.Handle); } catch { }
                }

                float ramAfter = _ramCounter.NextValue();
                float diffGb = (ramAfter - ramBefore) / 1024f;
                MessageBox.Show($"{diffGb:F2} GB Evacuated", "Quick Clean", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        private void CPU_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => NavigateTo(new CPU());
        private void Ram_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => NavigateTo(new Ram());
        private void GPU_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => NavigateTo(new GPU());
        private void DiskCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => NavigateTo(new Disk());

        private void NavigateTo(System.Windows.Controls.UserControl page)
        {
            DashboardView.Visibility = Visibility.Collapsed;
            PagesContainer.Visibility = Visibility.Visible;
            PagesContainer.Content = page;
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private void MenuDashboard_Click(object sender, RoutedEventArgs e)
        {
            PagesContainer.Visibility = Visibility.Collapsed;
            DashboardView.Visibility = Visibility.Visible;
            PagesContainer.Content = null;
        }

        private void themeSwitch(object sender, RoutedEventArgs e)
        {
            var tm = ThemeManager.Current;
            if (tm.ApplicationTheme == ApplicationTheme.Dark)
            {
                tm.ApplicationTheme = ApplicationTheme.Light;
                menuTheme.Header = "Use Dark Theme";
                SetDarkMode(false);
            }
            else
            {
                tm.ApplicationTheme = ApplicationTheme.Dark;
                menuTheme.Header = "Use Light Theme";
                SetDarkMode(true);
            }
        }

        private void SetDarkMode(bool isDark)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                int value = isDark ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
            }
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("SlowWhy - System Monitor & Optimizer", "About", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadStaticInfo();
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
    }
}
