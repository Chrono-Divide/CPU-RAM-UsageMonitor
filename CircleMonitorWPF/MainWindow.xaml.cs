using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace CircleMonitorWPF
{
    public partial class MainWindow : Window
    {
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const double MinimumBackgroundOpacity = 0.3;
        private const double MaximumBackgroundOpacity = 1.0;
        private const double ArcFullCircleAngle = 359.99;

        private DispatcherTimer _timer;
        private TaskbarIcon _taskbarIcon;
        private bool _isInitialPlacementDone;
        private bool _hasPreviousCpuTimes;
        private ulong _previousIdleTime;
        private ulong _previousKernelTime;
        private ulong _previousUserTime;

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        private struct MemorySnapshot
        {
            public float UsedRamMB;
            public float TotalRamMB;
            public float UsedRamGB;
            public float UsagePercent;
        }

        public MainWindow()
        {
            InitializeComponent();

            _timer = new DispatcherTimer(DispatcherPriority.Background);
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;

            IsVisibleChanged += Window_IsVisibleChanged;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!IsVisible)
            {
                StopMonitoring();
                return;
            }

            UpdateUsage();
        }

        private void StartMonitoring()
        {
            if (_timer == null || _timer.IsEnabled)
            {
                return;
            }

            _hasPreviousCpuTimes = false;
            UpdateUsage();
            _timer.Start();
        }

        private void StopMonitoring()
        {
            if (_timer != null && _timer.IsEnabled)
            {
                _timer.Stop();
            }

            _hasPreviousCpuTimes = false;
        }

        private void UpdateUsage()
        {
            float cpuUsage;
            if (TryGetCpuUsage(out cpuUsage))
            {
                CpuTextBlock.Text = string.Format("CPU: {0:0.0}%", cpuUsage);
                UpdateArcSegment(OuterArcSegment, 75, 75, 70, GetUsageAngle(cpuUsage));
                OuterArcPath.Stroke = GetUsageBrush(cpuUsage);
            }
            else
            {
                CpuTextBlock.Text = "CPU: N/A";
                UpdateArcSegment(OuterArcSegment, 75, 75, 70, 0);
                OuterArcPath.Stroke = Brushes.Gray;
            }

            MemorySnapshot memory;
            if (TryGetMemorySnapshot(out memory))
            {
                RamTextBlock.Text = string.Format("RAM: {0:0.0} GB", memory.UsedRamGB);
                MainGrid.ToolTip = string.Format(
                    "RAM usage: {0:0.0} MB / {1:0.0} MB",
                    memory.UsedRamMB,
                    memory.TotalRamMB);

                UpdateArcSegment(InnerArcSegment, 75, 75, 55, GetUsageAngle(memory.UsagePercent));
                InnerArcPath.Stroke = GetUsageBrush(memory.UsagePercent);
            }
            else
            {
                RamTextBlock.Text = "RAM: N/A";
                MainGrid.ToolTip = "RAM usage unavailable";
                UpdateArcSegment(InnerArcSegment, 75, 75, 55, 0);
                InnerArcPath.Stroke = Brushes.Gray;
            }
        }

        private bool TryGetCpuUsage(out float cpuUsage)
        {
            cpuUsage = 0f;

            FILETIME idleTime;
            FILETIME kernelTime;
            FILETIME userTime;
            if (!GetSystemTimes(out idleTime, out kernelTime, out userTime))
            {
                return false;
            }

            ulong idle = ToUInt64(idleTime);
            ulong kernel = ToUInt64(kernelTime);
            ulong user = ToUInt64(userTime);

            if (!_hasPreviousCpuTimes)
            {
                SaveCpuTimes(idle, kernel, user);
                _hasPreviousCpuTimes = true;
                return true;
            }

            ulong idleDelta = GetDelta(idle, _previousIdleTime);
            ulong kernelDelta = GetDelta(kernel, _previousKernelTime);
            ulong userDelta = GetDelta(user, _previousUserTime);
            ulong totalDelta = kernelDelta + userDelta;

            SaveCpuTimes(idle, kernel, user);

            if (totalDelta == 0)
            {
                return true;
            }

            double usage = (1.0 - ((double)idleDelta / totalDelta)) * 100.0;
            cpuUsage = ClampUsage(usage);
            return true;
        }

        private bool TryGetMemorySnapshot(out MemorySnapshot snapshot)
        {
            snapshot = new MemorySnapshot();

            MEMORYSTATUSEX status = new MEMORYSTATUSEX();
            status.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));

            if (!GlobalMemoryStatusEx(ref status) || status.ullTotalPhys == 0)
            {
                return false;
            }

            double totalMB = status.ullTotalPhys / 1024.0 / 1024.0;
            double availableMB = status.ullAvailPhys / 1024.0 / 1024.0;
            double usedMB = Math.Max(0.0, totalMB - availableMB);

            snapshot.TotalRamMB = (float)totalMB;
            snapshot.UsedRamMB = (float)usedMB;
            snapshot.UsedRamGB = (float)(usedMB / 1024.0);
            snapshot.UsagePercent = ClampUsage((usedMB / totalMB) * 100.0);
            return true;
        }

        private void SaveCpuTimes(ulong idle, ulong kernel, ulong user)
        {
            _previousIdleTime = idle;
            _previousKernelTime = kernel;
            _previousUserTime = user;
        }

        private static ulong ToUInt64(FILETIME time)
        {
            return ((ulong)time.dwHighDateTime << 32) | time.dwLowDateTime;
        }

        private static ulong GetDelta(ulong current, ulong previous)
        {
            return current >= previous ? current - previous : 0;
        }

        private static float ClampUsage(double usagePercent)
        {
            if (double.IsNaN(usagePercent) || double.IsInfinity(usagePercent))
            {
                return 0f;
            }

            if (usagePercent < 0.0)
            {
                return 0f;
            }

            if (usagePercent > 100.0)
            {
                return 100f;
            }

            return (float)usagePercent;
        }

        private static double GetUsageAngle(float usagePercent)
        {
            double clamped = ClampUsage(usagePercent);
            return clamped >= 100.0 ? ArcFullCircleAngle : (clamped / 100.0) * 360.0;
        }

        private Brush GetUsageBrush(float usagePercent)
        {
            float clamped = ClampUsage(usagePercent);

            if (clamped < 50)
            {
                return Brushes.LimeGreen;
            }

            if (clamped < 80)
            {
                return Brushes.Gold;
            }

            return Brushes.Red;
        }

        private void UpdateArcSegment(ArcSegment arc, double cx, double cy, double radius, double angle)
        {
            double safeAngle = Math.Max(0.0, Math.Min(ArcFullCircleAngle, angle));
            double radians = (Math.PI / 180.0) * safeAngle;
            double endX = cx + radius * Math.Sin(radians);
            double endY = cy - radius * Math.Cos(radians);

            arc.Point = new Point(endX, endY);
            arc.IsLargeArc = safeAngle > 180.0;
        }

        private void PlaceWindowNearCursorMonitor()
        {
            POINT mousePos;
            if (!GetCursorPos(out mousePos))
            {
                mousePos.X = 0;
                mousePos.Y = 0;
            }

            IntPtr hMonitor = MonitorFromPoint(mousePos, MONITOR_DEFAULTTONEAREST);
            if (hMonitor == IntPtr.Zero)
            {
                return;
            }

            MONITORINFO mi = new MONITORINFO();
            mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            if (!GetMonitorInfo(hMonitor, ref mi))
            {
                return;
            }

            Point bottomRight = new Point(mi.rcWork.right, mi.rcWork.bottom);
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source != null && source.CompositionTarget != null)
            {
                bottomRight = source.CompositionTarget.TransformFromDevice.Transform(bottomRight);
            }

            double width = ActualWidth > 0 ? ActualWidth : Width;
            double height = ActualHeight > 0 ? ActualHeight : Height;
            Left = bottomRight.X - width;
            Top = bottomRight.Y - height;
        }

        private void EnsureTrayIcon()
        {
            if (_taskbarIcon != null)
            {
                return;
            }

            _taskbarIcon = new TaskbarIcon();
            _taskbarIcon.Icon = System.Drawing.SystemIcons.Application;
            _taskbarIcon.ToolTipText = "CPU & RAM Monitor";
            _taskbarIcon.ContextMenu = (System.Windows.Controls.ContextMenu)FindResource("TrayMenu");
            _taskbarIcon.TrayMouseDoubleClick += TaskbarIcon_TrayMouseDoubleClick;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isInitialPlacementDone)
            {
                PlaceWindowNearCursorMonitor();
                _isInitialPlacementDone = true;
            }

            EnsureTrayIcon();
            StartMonitoring();
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                StartMonitoring();
            }
            else
            {
                StopMonitoring();
            }
        }

        private void TaskbarIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            Show();
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            Activate();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double step = e.Delta > 0 ? 0.05 : -0.05;
            double newOpacity = Math.Max(
                MinimumBackgroundOpacity,
                Math.Min(MaximumBackgroundOpacity, BackgroundCircle.Opacity + step));

            if (Math.Abs(BackgroundCircle.Opacity - newOpacity) > 0.001)
            {
                BackgroundCircle.Opacity = newOpacity;
            }

            e.Handled = true;
        }

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            try
            {
                Process.Start("taskmgr");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to open Task Manager.\n" + ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            StopMonitoring();
            IsVisibleChanged -= Window_IsVisibleChanged;

            if (_timer != null)
            {
                _timer.Tick -= Timer_Tick;
                _timer = null;
            }

            if (_taskbarIcon != null)
            {
                _taskbarIcon.TrayMouseDoubleClick -= TaskbarIcon_TrayMouseDoubleClick;
                _taskbarIcon.Dispose();
                _taskbarIcon = null;
            }

            base.OnClosed(e);
        }
    }
}
