using System;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LibreHardwareMonitor.Hardware;

namespace SlowWhy
{
    public partial class GPU : UserControl
    {
        private DispatcherTimer _timer;
        private Computer _computer;

        public GPU()
        {
            InitializeComponent();
            LoadStaticInfo();

            _computer = new Computer { IsGpuEnabled = true };
            _computer.Open();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _timer.Tick += OnTimerTick;
            _timer.Start();
        }

        private void LoadStaticInfo()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
                foreach (ManagementObject obj in searcher.Get())
                {
                    txtGpuName.Text = obj["Name"]?.ToString() ?? "Unknown";
                    txtDriver.Text = obj["DriverVersion"]?.ToString() ?? "--";

                    if (obj["CurrentHorizontalResolution"] != null && obj["CurrentVerticalResolution"] != null)
                        txtResolution.Text = $"{obj["CurrentHorizontalResolution"]} x {obj["CurrentVerticalResolution"]}";

                    if (obj["CurrentRefreshRate"] != null)
                        txtRefresh.Text = obj["CurrentRefreshRate"].ToString();

                    if (obj["AdapterRAM"] != null)
                    {
                        double gb = Convert.ToDouble(obj["AdapterRAM"]) / (1024 * 1024 * 1024);
                        txtVramTotal.Text = $"{gb:F1} GB";
                    }
                    break;
                }
            }
            catch
            {
                txtGpuName.Text = "Error reading GPU info";
            }
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            foreach (IHardware hardware in _computer.Hardware)
            {
                if (hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
                {
                    hardware.Update();
                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Load && sensor.Name == "GPU Core")
                        {
                            txtUsage.Text = $"{sensor.Value:F0}%";
                            pbUsage.Value = (double)sensor.Value;
                        }
                    }
                }
            }
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            _computer.Close();
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.DashboardView.Visibility = Visibility.Visible;
                mainWindow.PagesContainer.Visibility = Visibility.Collapsed;
            }
        }
    }
}
