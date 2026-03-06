using LibreHardwareMonitor.Hardware;
using System;
using System.Diagnostics;
using System.Management;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace SlowWhy
{
    public partial class DesktopWidget : Window
    {
        private PerformanceCounter _cpuCounter;
        private PerformanceCounter _ramCounter;
        private Computer _computer;
        private double _totalRamGb;
        private DispatcherTimer _timer;

        public DesktopWidget()
        {
            InitializeComponent();

            var area = SystemParameters.WorkArea;
            Left = area.Right - Width - 20;
            Top = area.Bottom - 160 - 20;

            InitHardware();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            _timer.Tick += (_, _) => Refresh();
            _timer.Start();

            MouseLeftButtonDown += (_, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed) DragMove();
            };

            Closed += (_, _) =>
            {
                _timer.Stop();
                _computer?.Close();
                _cpuCounter?.Dispose();
                _ramCounter?.Dispose();
            };
        }

        private void InitHardware()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _ramCounter = new PerformanceCounter("Memory", "Available MBytes");

                using var s = new ManagementObjectSearcher(
                    "SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
                foreach (ManagementObject o in s.Get())
                    _totalRamGb = Convert.ToDouble(o["TotalVisibleMemorySize"]) / (1024.0 * 1024.0);

                _computer = new Computer { IsGpuEnabled = true };
                _computer.Open();
            }
            catch { }
        }

        private void Refresh()
        {
            try
            {
                // CPU
                float cpu = _cpuCounter?.NextValue() ?? 0f;
                pbCpu.Value = cpu;
                lblCpu.Text = $"{cpu:F0}%";

                // RAM
                float freeMb = _ramCounter?.NextValue() ?? 0f;
                double usedPct = _totalRamGb > 0
                    ? (1.0 - freeMb / 1024.0 / _totalRamGb) * 100.0 : 0;
                pbRam.Value = Math.Clamp(usedPct, 0, 100);
                lblRam.Text = $"{usedPct:F0}%";

                // GPU
                if (_computer == null) return;
                foreach (var hw in _computer.Hardware)
                {
                    if (hw.HardwareType is HardwareType.GpuNvidia
                        or HardwareType.GpuAmd or HardwareType.GpuIntel)
                    {
                        hw.Update();
                        foreach (var sensor in hw.Sensors)
                            if (sensor.SensorType == SensorType.Load
                                && sensor.Name == "GPU Core")
                            {
                                float gpu = sensor.Value ?? 0f;
                                pbGpu.Value = gpu;
                                lblGpu.Text = $"{gpu:F0}%";
                            }
                    }
                }
            }
            catch { }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}