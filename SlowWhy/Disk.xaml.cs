using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SlowWhy
{

    public partial class Disk : UserControl
    {
        public Disk()
        {
            InitializeComponent();
            this.Loaded += Disk_Loaded;
        }

        private async void Disk_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadFilesAsync();
        }

        private async Task LoadFilesAsync()
        {
            try
            {
                var files = await Task.Run(() =>
                {
                    List<DiskFileModel> list = new List<DiskFileModel>();
                    DirectoryInfo di = new DirectoryInfo("C:\\");

                    try
                    {
                        foreach (var file in di.GetFiles("*.*", SearchOption.TopDirectoryOnly))
                        {
                            list.Add(new DiskFileModel
                            {
                                FileName = file.Name,
                                Extension = file.Extension,
                                FullPath = file.FullName,
                                FileSize = (file.Length / 1024.0 / 1024.0).ToString("0.00") + " MB"
                            });
                        }
                    }
                    catch (UnauthorizedAccessException) {}
                    catch (Exception) {}

                    return list;
                });
                dgFiles.ItemsSource = files;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
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
    }
}