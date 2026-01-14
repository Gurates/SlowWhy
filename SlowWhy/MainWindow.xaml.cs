using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace SlowWhy
{
    public partial class MainWindow : Window
    {
        DispatcherTimer timer = new DispatcherTimer();
        PerformanceCounter cpuCounter;
        PerformanceCounter ramCounter;

        public MainWindow()
        {
            InitializeComponent();
            cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            ramCounter = new PerformanceCounter("Memory", "Available MBytes");

            // Timer settings
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
        }

        private void btnAnaliz_Click(object sender, RoutedEventArgs e)
        {
            timer.Start();
            btnAnaliz.Content = "Analiz Ediliyor...";
            btnAnaliz.IsEnabled = false;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            float cpuValue = cpuCounter.NextValue();
            float ramValue = ramCounter.NextValue();
            float ramRealValue = ramValue / 1024;

            // CPU Update
            txtCpu.Text = $"%{cpuValue:F0}";

            // RAM Update
            txtRam.Text = $"{ramRealValue} MB";

            //Turn red if the CPU usage is too high.
            if (cpuValue > 80)
            {
                txtCpu.Foreground = System.Windows.Media.Brushes.Red;
            }
            else
            {
                txtCpu.Foreground = System.Windows.Media.Brushes.LightGreen;
            }
        }
    }
}