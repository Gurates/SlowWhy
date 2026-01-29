using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MessageBox = HandyControl.Controls.MessageBox;
using System.Windows.Media.Animation;
using Microsoft.Win32;

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
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.DashboardView.Visibility = Visibility.Visible;
                mainWindow.PagesContainer.Visibility = Visibility.Collapsed;
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (dgItems.SelectedItem is not DiskItemModel selected) return;

            if (selected.Type == "File")
            {
                var result = MessageBox.Show(
                    "Are you sure you want to delete this file?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No) return;

                try
                {
                    string fullPath = Path.Combine(selected.Path, selected.Name);
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                        (dgItems.ItemsSource as System.Collections.IList)?.Remove(selected);
                        dgItems.Items.Refresh();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            if (selected.Type == "App")
            {
                try { Process.Start("appwiz.cpl"); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        private void MenuItem_OpenLocation_Click(object sender, RoutedEventArgs e)
        {
            if (dgItems.SelectedItem is not DiskItemModel selected) return;

            try
            {
                if (selected.Type == "App")
                {
                    MessageBox.Show("Application location is not available in registry.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
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

                if (Directory.Exists(selected.Path))
                    Process.Start("explorer.exe", selected.Path);
                else
                    MessageBox.Show($"Path does not exist:\n{selected.Path}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot open location:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnScan_Click(object sender, RoutedEventArgs e)
        {
            btnScan.IsEnabled = false;
            txtStatus.Text = "Scanning system files...";
            pnlLoading.Visibility = Visibility.Visible;
            dgItems.ItemsSource = null;

            var duration = new Duration(TimeSpan.FromSeconds(25));
            var animation = new DoubleAnimation(0.0, 100.0, duration) { FillBehavior = FillBehavior.HoldEnd };
            progressBar1.BeginAnimation(ProgressBar.ValueProperty, animation);

            try
            {
                var results = await Task.Run(PerformFullScan);
                dgItems.ItemsSource = results.OrderByDescending(x => x.RawSize).ToList();
                pnlLoading.Visibility = Visibility.Collapsed;
                progressBar1.BeginAnimation(ProgressBar.ValueProperty, null);
                progressBar1.Value = 100;
                MessageBox.Show($"{results.Count} large content items found.", "Scan Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                pnlLoading.Visibility = Visibility.Collapsed;
                MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnScan.IsEnabled = true;
                pnlLoading.Visibility = Visibility.Collapsed;
                progressBar1.BeginAnimation(ProgressBar.ValueProperty, null);
                progressBar1.Value = 100;
            }
        }

        private List<DiskItemModel> PerformFullScan()
        {
            var combinedList = new List<DiskItemModel>();
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
                using var key = Registry.LocalMachine.OpenSubKey(path);
                if (key == null) continue;

                foreach (var subkeyName in key.GetSubKeyNames())
                {
                    using var subkey = key.OpenSubKey(subkeyName);
                    try
                    {
                        string name = subkey?.GetValue("DisplayName") as string;
                        if (string.IsNullOrEmpty(name)) continue;

                        object sizeObj = subkey?.GetValue("EstimatedSize");
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
                var di = new DirectoryInfo(currentDir);

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
                catch (UnauthorizedAccessException) { }
                catch { }
            }
            return files;
        }
    }
}
