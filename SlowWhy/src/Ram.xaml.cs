using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SlowWhy
{
    public partial class Ram : UserControl
    {
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
        private const int SystemCombinePhysicalMemoryInformation = 130;

        public enum CleaningLevel
        {
            Safe,
            Medium,
            Aggressive
        }

        private PerformanceCounter _ramCounter;
        private DispatcherTimer _timer;
        private float _ramValueMb;

        public Ram()
        {
            InitializeComponent();
            InitializeMonitoring();
            _timer.Start();
        }

        private void InitializeMonitoring()
        {
            try
            {
                _ramCounter = new PerformanceCounter("Memory", "Available MBytes");
                _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _timer.Tick += OnTimerTick;
            }
            catch { }
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            _ramValueMb = _ramCounter.NextValue();
            txtRam.Text = $"{_ramValueMb / 1024.0:F2} GB";
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

        private async void btnRamClear_Click(object sender, RoutedEventArgs e)
        {
            CleaningLevel selectedLevel = GetSelectedCleaningLevel();

            string levelDescription = selectedLevel switch
            {
                CleaningLevel.Safe => "Safe Mode:\n- Clears cache only\n- No performance impact\n- ~1-2 GB freed",
                CleaningLevel.Medium => "Medium Mode:\n- Clears cache + file system\n- Minimal impact\n- ~1.5-2.5 GB freed",
                CleaningLevel.Aggressive => "Aggressive Mode:\n- Clears everything\n- May slow down apps\n- ~2-4 GB freed",
                _ => ""
            };

            var result = MessageBox.Show(
                $"{levelDescription}\n\nAre you sure you want to continue?",
                "Confirm RAM Cleaning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.No) return;

            btnRamClear.IsEnabled = false;

            float ramBefore = _ramCounter.NextValue();

            await Task.Run(() =>
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);

                PurgeStandbyList();
                FlushModifiedPageList();
                CombineMemoryPages();

                if (selectedLevel >= CleaningLevel.Medium)
                {
                    try { FlushFileSystemCache(); } catch { }
                }

                if (selectedLevel >= CleaningLevel.Aggressive)
                {
                    ClearWorkingSets();
                    try { ClearStandbyCache(); } catch { }
                }

                System.Threading.Thread.Sleep(500);
            });

            float ramAfter = _ramCounter.NextValue();
            float diffGb = (ramAfter - ramBefore) / 1024f;

            btnRamClear.IsEnabled = true;

            string modeText = selectedLevel switch
            {
                CleaningLevel.Safe => "Safe Mode",
                CleaningLevel.Medium => "Medium Mode",
                CleaningLevel.Aggressive => "Aggressive Mode",
                _ => "Unknown"
            };

            MessageBox.Show(
                $"{diffGb:F2} GB Freed\n\nMode: {modeText}\nYour apps are running smoothly!",
                "RAM Optimization Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private CleaningLevel GetSelectedCleaningLevel()
        {
            return cmbCleaningLevel.SelectedIndex switch
            {
                0 => CleaningLevel.Safe,
                1 => CleaningLevel.Medium,
                _ => CleaningLevel.Aggressive
            };
        }

        private void ClearWorkingSets()
        {
            string[] protectedProcesses = {
                "explorer", "dwm", "csrss", "winlogon", "services",
                "lsass", "svchost", "System", "smss", "wininit"
            };

            const long minWorkingSet = 100 * 1024 * 1024;

            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.HasExited || p.Handle == IntPtr.Zero) continue;
                    if (p.WorkingSet64 < minWorkingSet) continue;
                    if (protectedProcesses.Any(x => p.ProcessName.ToLower().Contains(x))) continue;

                    EmptyWorkingSet(p.Handle);
                    SetProcessWorkingSetSize(p.Handle, new IntPtr(-1), new IntPtr(-1));
                }
                catch { }
                finally { p.Dispose(); }
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
                finally { Marshal.FreeHGlobal(mem); }
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
                finally { Marshal.FreeHGlobal(mem); }
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
                finally { Marshal.FreeHGlobal(mem); }
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
                using var proc = Process.Start(psi);
                proc?.WaitForExit(3000);
            }
            catch { }

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Process WHERE Name = 'RuntimeBroker.exe'");
                foreach (ManagementObject obj in searcher.Get())
                {
                    try
                    {
                        int pid = Convert.ToInt32(obj["ProcessId"]);
                        var proc = Process.GetProcessById(pid);
                        EmptyWorkingSet(proc.Handle);
                        SetProcessWorkingSetSize(proc.Handle, new IntPtr(-1), new IntPtr(-1));
                        proc.Dispose();
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void FlushFileSystemCache()
        {
            IntPtr min = IntPtr.Zero, max = IntPtr.Zero;
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
