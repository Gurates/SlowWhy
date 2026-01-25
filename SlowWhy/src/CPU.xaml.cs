using LibreHardwareMonitor.Hardware;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SlowWhy
{

    public partial class CPU : UserControl
    {
        private PerformanceCounter _totalCpuCounter;
        private DispatcherTimer _timer;
        private Dictionary<int, TimeSpan> _prevProcessTimes = new Dictionary<int, TimeSpan>();
        private DateTime _prevCheckTime;
        private Computer _computer;

        public CPU()
        {
            InitializeComponent();

            _totalCpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _prevCheckTime = DateTime.Now;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            _timer.Tick += Timer_Tick;
            _timer.Start();
            Timer_Tick(null, null);

            _computer = new Computer()
            {
                IsCpuEnabled = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled = true
            };
            _computer.Open();
        }

        private async void Timer_Tick(object sender, EventArgs e)
        {
            float totalUsage = _totalCpuCounter.NextValue();
            txtTotalUsage.Text = $"{totalUsage:F0}%";
            pbTotalUsage.Value = totalUsage;

            pbTotalUsage.Foreground = totalUsage > 80
                ? System.Windows.Media.Brushes.Crimson
                : System.Windows.Media.Brushes.DodgerBlue;

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

                    TimeSpan currentTotalProcessorTime = p.TotalProcessorTime;
                    double usagePercent = 0;

                    if (_prevProcessTimes.ContainsKey(p.Id))
                    {
                        double cpuUsedMs = (currentTotalProcessorTime - _prevProcessTimes[p.Id]).TotalMilliseconds;

                        usagePercent = (cpuUsedMs / timeDiffMs) / coreCount * 100;
                    }

                    _prevProcessTimes[p.Id] = currentTotalProcessorTime;

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
                catch
                {

                }
            }

            _prevCheckTime = now;
            return results.OrderByDescending(x => x.UsageRaw).Take(20).ToList();
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.MainDashboard.Visibility = Visibility.Visible;
                mainWindow.PagesContainer.Visibility = Visibility.Collapsed;
            }
        }


        private void CpuAppClose_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgProcesses.SelectedItem as CpuProcessModel;
            if (selected == null) return;

            try
            {
                var procces = System.Diagnostics.Process.GetProcessById(selected.Id);
                procces.Kill(true);
                procces.WaitForExit(1000);
                (dgProcesses.ItemsSource as IList)?.Remove(selected);
            }
            catch (Exception ex)
            {
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

    }
}