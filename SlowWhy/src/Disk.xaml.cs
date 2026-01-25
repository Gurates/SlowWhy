using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace SlowWhy
{
    public partial class Disk : UserControl
    {
        public Disk()
        {
            InitializeComponent();
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

        private void MenuItem_OpenLocation_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgItems.SelectedItem as DiskItemModel;
            if (selected == null) return;

            try
            {
                string pathToOpen = selected.Path;
                if (selected.Type == "App")
                {
                    MessageBox.Show("Application location is not available in registry.", "Info");
                    return;
                }

                if (selected.Type == "File")
                {
                    string fullPath = Path.Combine(selected.Path, selected.Name);
                    if (File.Exists(fullPath))
                    {
                        Process.Start("explorer.exe", $"/select,\"{fullPath}\"");
                        return;
                    }
                }

                if (Directory.Exists(pathToOpen))
                {
                    Process.Start("explorer.exe", pathToOpen);
                }
                else
                {
                    MessageBox.Show($"Path does not exist:\n{pathToOpen}", "Error");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot open location:\n{ex.Message}", "Error");
            }
        }

        private async void btnScan_Click(object sender, RoutedEventArgs e)
        {
            btnScan.IsEnabled = false;
            btnScan.Content = "Scanning...";
            txtStatus.Text = "Scanning system files...";
            pnlLoading.Visibility = Visibility.Visible;
            dgItems.ItemsSource = null;

            // progressbar
            Duration duration = new Duration(TimeSpan.FromSeconds(25));
            DoubleAnimation doubleAnimation = new DoubleAnimation(0.0, 100.0, duration);
            doubleAnimation.FillBehavior = FillBehavior.HoldEnd;
            progressBar1.BeginAnimation(ProgressBar.ValueProperty, doubleAnimation);

            try
            {
                var results = await Task.Run(() => PerformFullScan());

                dgItems.ItemsSource = results.OrderByDescending(x => x.RawSize).ToList();

                pnlLoading.Visibility = Visibility.Collapsed;
                progressBar1.BeginAnimation(ProgressBar.ValueProperty, null);
                progressBar1.Value = 100;
                MessageBox.Show($"{results.Count} Number of large pieces of content found.");
            }
            catch (Exception ex)
            {
                pnlLoading.Visibility = Visibility.Collapsed;
                MessageBox.Show("Error: " + ex.Message);
            }
            finally
            {
                btnScan.IsEnabled = true;
                btnScan.Content = "Scan";
                pnlLoading.Visibility = Visibility.Collapsed;
                progressBar1.BeginAnimation(ProgressBar.ValueProperty, null);
                progressBar1.Value = 100;
            }
        }

        private List<DiskItemModel> PerformFullScan()
        {
            List<DiskItemModel> combinedList = new List<DiskItemModel>();

            combinedList.AddRange(GetInstalledApps());

            combinedList.AddRange(GetLargeFiles("C:\\"));

            return combinedList;
        }

        private List<DiskItemModel> GetInstalledApps()
        {
            var apps = new List<DiskItemModel>();
            string[] registryPaths = {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var path in registryPaths)
            {
                using (var key = Registry.LocalMachine.OpenSubKey(path))
                {
                    if (key == null) continue;

                    foreach (var subkeyName in key.GetSubKeyNames())
                    {
                        using (var subkey = key.OpenSubKey(subkeyName))
                        {
                            try
                            {
                                string name = subkey.GetValue("DisplayName") as string;
                                if (string.IsNullOrEmpty(name)) continue;

                                object sizeObj = subkey.GetValue("EstimatedSize");
                                if (sizeObj != null)
                                {
                                    long sizeInKb = Convert.ToInt64(sizeObj);
                                    if (sizeInKb > 0)
                                    {
                                        apps.Add(new DiskItemModel
                                        {
                                            Name = name,
                                            Type = "App",
                                            RawSize = sizeInKb * 1024,
                                            DisplaySize = (sizeInKb / 1024.0 / 1024.0).ToString("0.00") + " GB",
                                            Path = "Installed"
                                        });
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            return apps;
        }
        private List<DiskItemModel> GetLargeFiles(string rootPath)
        {
            var files = new List<DiskItemModel>();
            var stack = new Stack<string>();
            stack.Push(rootPath);

            while (stack.Count > 0)
            {
                string currentDir = stack.Pop();
                DirectoryInfo di = new DirectoryInfo(currentDir);

                try
                {
                    foreach (var file in di.GetFiles())
                    {
                        if (file.Length > 100 * 1024 * 1024)
                        {
                            files.Add(new DiskItemModel
                            {
                                Name = file.Name,
                                Type = "File",
                                RawSize = file.Length,
                                DisplaySize = (file.Length / 1024.0 / 1024.0 / 1024.0).ToString("0.00") + " GB",
                                Path = file.DirectoryName
                            });
                        }
                    }

                    foreach (var dir in di.GetDirectories())
                    {
                        string dirName = dir.Name.ToLower();
                        if (!dirName.Contains("windows") &&
                            !dirName.Contains("program files") &&
                            !dirName.Contains("$recycle.bin"))
                        {
                            stack.Push(dir.FullName);
                        }
                    }
                }
                catch (UnauthorizedAccessException) {}
                catch { }
            }
            return files;
        }
    }
}