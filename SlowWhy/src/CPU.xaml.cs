using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LibreHardwareMonitor.Hardware;

namespace SlowWhy
{
    public partial class CPU : UserControl
    {
        private PerformanceCounter _totalCpuCounter;
        private DispatcherTimer _timer;
        private Dictionary<int, TimeSpan> _prevProcessTimes = new();
        private DateTime _prevCheckTime;
        private Computer _computer;

        public CPU()
        {
            InitializeComponent();

            _totalCpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _prevCheckTime = DateTime.Now;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            _timer.Tick += OnTimerTick;
            _timer.Start();
            OnTimerTick(null, null);

            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled = true
            };
            _computer.Open();
        }

        private async void OnTimerTick(object sender, EventArgs e)
        {
            float totalUsage = _totalCpuCounter.NextValue();
            txtTotalUsage.Text = $"{totalUsage:F0}%";
            pbTotalUsage.Value = totalUsage;

            var processList = await Task.Run(() => GetTopProcesses());
            dgProcesses.ItemsSource = processList;
        }

        private List<CpuProcessModel> GetTopProcesses()
        {
            var currentProcesses = Process.GetProcesses();
            var results = new List<CpuProcessModel>();
            var now = DateTime.Now;

            double timeDiffMs = (now - _prevCheckTime).TotalMilliseconds;
            if (timeDiffMs <= 0) timeDiffMs = 1;

            int coreCount = Environment.ProcessorCount;

            foreach (var p in currentProcesses)
            {
                try
                {
                    if (p.Id == 0) continue;

                    TimeSpan currentTotal = p.TotalProcessorTime;
                    double usagePercent = 0;

                    if (_prevProcessTimes.TryGetValue(p.Id, out var prevTime))
                    {
                        double cpuUsedMs = (currentTotal - prevTime).TotalMilliseconds;
                        usagePercent = (cpuUsedMs / timeDiffMs) / coreCount * 100;
                    }

                    _prevProcessTimes[p.Id] = currentTotal;

                    if (usagePercent > 0.1 || results.Count < 20)
                    {
                        results.Add(new CpuProcessModel
                        {
                            Id = p.Id,
                            Name = p.ProcessName,
                            UsageRaw = usagePercent,
                            Usage = $"{usagePercent:F1}%"
                        });
                    }
                }
                catch { }
            }

            _prevCheckTime = now;
            return results.OrderByDescending(x => x.UsageRaw).Take(20).ToList();
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.DashboardView.Visibility = Visibility.Visible;
                mainWindow.PagesContainer.Visibility = Visibility.Collapsed;
            }
        }

        private void CpuAppClose_Click(object sender, RoutedEventArgs e)
        {
            if (dgProcesses.SelectedItem is not CpuProcessModel selected) return;

            try
            {
                var process = Process.GetProcessById(selected.Id);
                process.Kill(true);
                process.WaitForExit(1000);
                (dgProcesses.ItemsSource as IList)?.Remove(selected);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
