using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using System.Management;
using System.IO;

namespace SlowWhy
{
    public partial class Ram : UserControl
    {
        #region Win32 API Imports
        [DllImport("psapi.dll")]
        public static extern int EmptyWorkingSet(IntPtr hwProc);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetSystemFileCacheSize(IntPtr MinimumFileCacheSize, IntPtr MaximumFileCacheSize, int Flags);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSystemFileCacheSize(ref IntPtr lpMinimumFileCacheSize, ref IntPtr lpMaximumFileCacheSize, ref int lpFlags);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern uint NtSetSystemInformation(int SystemInformationClass, IntPtr SystemInformation, int SystemInformationLength);
        #endregion

        #region Memory List Commands (Native API)
        private enum SYSTEM_MEMORY_LIST_COMMAND
        {
            MemoryCaptureAccessedBits = 0,
            MemoryCaptureAndResetAccessedBits = 1,
            MemoryEmptyWorkingSets = 2,
            MemoryFlushModifiedList = 3,
            MemoryPurgeStandbyList = 4,
            MemoryPurgeLowPriorityStandbyList = 5,
            MemoryCommandMax = 6
        }

        private const int SystemMemoryListInformation = 80;
        private const int SystemFileCacheInformation = 21;
        private const int SystemCombinePhysicalMemoryInformation = 130;
        #endregion

        private PerformanceCounter ramCounter;
        private DispatcherTimer timer;
        private float previousRam;
        private float ramValueMb;
        private float currentRam;

        public Ram()
        {
            InitializeComponent();
            InitializeApp();
            timer.Start();
        }

        private void InitializeApp()
        {
            try
            {
                ramCounter = new PerformanceCounter("Memory", "Available MBytes");
                timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(1);
                timer.Tick += Timer_Tick;
            }
            catch { }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            ramValueMb = ramCounter.NextValue();
            txtRam.Text = $"{ramValueMb / 1024.0:F2} GB";
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

        private float ramDiffrence(float newRamValue)
        {
            previousRam = ramValueMb;
            currentRam = newRamValue;
            float delta = currentRam - previousRam;
            return delta;
        }

        private async void btnRamClear_Click(object sender, RoutedEventArgs e)
        {
            btnRamClear.IsEnabled = false;
            btnRamClear.Content = "Optimizing...";

            float ramBefore = ramCounter.NextValue();

            await Task.Run(() =>
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);

                ClearWorkingSets();

                PurgeStandbyList();

                FlushModifiedPageList();

                CombineMemoryPages();

                try
                {
                    ClearStandbyCache();
                }
                catch { }

                try
                {
                    FlushFileSystemCache();
                }
                catch { }

                System.Threading.Thread.Sleep(500);
            });

            float ramAfter = ramCounter.NextValue();
            float diffrenceMB = ramAfter - ramBefore;
            float diffrenceGB = diffrenceMB / 1024;

            btnRamClear.IsEnabled = true;
            btnRamClear.Content = "Clean RAM";

            MessageBox.Show($"{diffrenceGB:F2} GB Evacuated", "RAM Optimization Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ClearWorkingSets()
        {
            var processes = Process.GetProcesses();
            foreach (var p in processes)
            {
                try
                {
                    if (!p.HasExited && p.Handle != IntPtr.Zero)
                    {
                        EmptyWorkingSet(p.Handle);
                        SetProcessWorkingSetSize(p.Handle, new IntPtr(-1), new IntPtr(-1));
                    }
                }
                catch { }
                finally
                {
                    p.Dispose();
                }
            }
        }

        private void PurgeStandbyList()
        {
            try
            {
                int command = (int)SYSTEM_MEMORY_LIST_COMMAND.MemoryPurgeStandbyList;
                IntPtr mem = Marshal.AllocHGlobal(sizeof(int));
                try
                {
                    Marshal.WriteInt32(mem, command);
                    NtSetSystemInformation(SystemMemoryListInformation, mem, sizeof(int));
                }
                finally
                {
                    Marshal.FreeHGlobal(mem);
                }
            }
            catch { }
        }

        private void FlushModifiedPageList()
        {
            try
            {
                int command = (int)SYSTEM_MEMORY_LIST_COMMAND.MemoryFlushModifiedList;
                IntPtr mem = Marshal.AllocHGlobal(sizeof(int));
                try
                {
                    Marshal.WriteInt32(mem, command);
                    NtSetSystemInformation(SystemMemoryListInformation, mem, sizeof(int));
                }
                finally
                {
                    Marshal.FreeHGlobal(mem);
                }
            }
            catch { }
        }

        private void CombineMemoryPages()
        {
            try
            {
                IntPtr mem = Marshal.AllocHGlobal(sizeof(int));
                try
                {
                    Marshal.WriteInt32(mem, 1);
                    NtSetSystemInformation(SystemCombinePhysicalMemoryInformation, mem, sizeof(int));
                }
                finally
                {
                    Marshal.FreeHGlobal(mem);
                }
            }
            catch { }
        }

        private void ClearStandbyCache()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c echo y | PowerShell.exe -Command \"Clear-RecycleBin -Force -ErrorAction SilentlyContinue\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                using (var proc = Process.Start(psi))
                {
                    proc?.WaitForExit(3000);
                }
            }
            catch { }

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Process WHERE Name = 'RuntimeBroker.exe'"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        try
                        {
                            var pid = Convert.ToInt32(obj["ProcessId"]);
                            var proc = Process.GetProcessById(pid);
                            EmptyWorkingSet(proc.Handle);
                            SetProcessWorkingSetSize(proc.Handle, new IntPtr(-1), new IntPtr(-1));
                            proc.Dispose();
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private void FlushFileSystemCache()
        {
            IntPtr min = IntPtr.Zero;
            IntPtr max = IntPtr.Zero;
            int flags = 0;

            if (GetSystemFileCacheSize(ref min, ref max, ref flags))
            {
                SetSystemFileCacheSize(new IntPtr(-1), new IntPtr(-1), 0);
                System.Threading.Thread.Sleep(100);
                SetSystemFileCacheSize(min, max, flags);
            }
        }
    }
}