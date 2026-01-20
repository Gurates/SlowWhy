using LibreHardwareMonitor.Hardware;
using System;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SlowWhy
{
    public partial class GPU : UserControl
    {
        private DispatcherTimer _timer;
        private Computer _computer;

        public GPU()
        {
            InitializeComponent();
            GetGpuStaticInfo();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _timer.Tick += Timer_Tick;
            _timer.Start();

            _computer = new Computer()
            {
                IsGpuEnabled = true,
            };
            _computer.Open();
        }

        private void GetGpuStaticInfo()
        {
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");

                foreach (ManagementObject obj in searcher.Get())
                {
                    // GPU Name
                    if (obj["Name"] != null)
                        txtGpuName.Text = obj["Name"].ToString();

                    // Driver
                    if (obj["DriverVersion"] != null)
                        txtDriver.Text = obj["DriverVersion"].ToString();

                    // Resolution
                    if (obj["CurrentHorizontalResolution"] != null && obj["CurrentVerticalResolution"] != null)
                        txtResolution.Text = $"{obj["CurrentHorizontalResolution"]} x {obj["CurrentVerticalResolution"]}";

                    // Refresh Rate
                    if (obj["CurrentRefreshRate"] != null)
                        txtRefresh.Text = obj["CurrentRefreshRate"].ToString();

                    // VRAM
                    if (obj["AdapterRAM"] != null)
                    {
                        double bytes = Convert.ToDouble(obj["AdapterRAM"]);
                        double gb = bytes / (1024 * 1024 * 1024);
                        txtVramTotal.Text = $"{gb:F1} GB";
                    }
                    break;
                }
            }
            catch (Exception ex)
            {
                txtGpuName.Text = "Error reading GPU info";
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            foreach (IHardware hardware in _computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.GpuNvidia ||
                    hardware.HardwareType == HardwareType.GpuAmd ||
                    hardware.HardwareType == HardwareType.GpuIntel)
                {
                    hardware.Update();

                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Load && sensor.Name == "GPU Core")
                        {
                            txtUsage.Text = $"{sensor.Value:F0}%";
                            pbUsage.Value = (double)sensor.Value;
                        }

                        if (sensor.SensorType == SensorType.SmallData && sensor.Name == "GPU Memory Used")
                        {
                        }
                    }
                }
            }
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.MainDashboard.Visibility = Visibility.Visible;
                mainWindow.PagesContainer.Visibility = Visibility.Collapsed;
                _computer.Close();
            }
        }
    }
}