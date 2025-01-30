using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Diagnostics;
using System.Management;   // For ManagementObjectSearcher if .NET 5+ 
using System.Runtime.InteropServices; // For DllImport
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CircleMonitorWPF
{
    public partial class MainWindow : Window
    {
        // CPU & RAM usage
        private PerformanceCounter _cpuCounter;
        private PerformanceCounter _memAvailableCounter;
        private float _totalRamMB;
        private DispatcherTimer _timer;

        // Tray icon object
        private TaskbarIcon _taskbarIcon;

        // Win32 API for monitor detection
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private const uint MONITOR_DEFAULTTONEAREST = 2;

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

        public MainWindow()
        {
            InitializeComponent();

            // Initialize PerformanceCounters
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _memAvailableCounter = new PerformanceCounter("Memory", "Available MBytes");

            // Get total RAM in MB
            try
            {
                var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    double totalBytes = Convert.ToDouble(obj["TotalPhysicalMemory"]);
                    _totalRamMB = (float)(totalBytes / 1024 / 1024);
                }
            }
            catch
            {
                _totalRamMB = 8192f; // fallback: 8GB
            }

            // Timer to update CPU/RAM every second
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdateUsage();
        }

        private void UpdateUsage()
        {
            // CPU usage
            float cpuUsage = 0f;
            try { cpuUsage = _cpuCounter.NextValue(); } catch { }

            // Memory usage
            float memAvailable = 0f;
            try { memAvailable = _memAvailableCounter.NextValue(); } catch { }

            float usedRamMB = _totalRamMB - memAvailable;
            float usedRamGB = usedRamMB / 1024f;

            float ramUsagePercent = 0f;
            if (_totalRamMB > 0)
            {
                ramUsagePercent = (usedRamMB / _totalRamMB) * 100f;
            }

            // Update text
            CpuTextBlock.Text = $"CPU: {cpuUsage:0.0}%";
            RamTextBlock.Text = $"RAM: {usedRamGB:0.0} GB";
            // Tooltip: detail in MB
            MainGrid.ToolTip = $"RAM usage: {usedRamMB:0.0} MB / {_totalRamMB:0.0} MB";

            // Update arcs
            double cpuAngle = (cpuUsage / 100.0) * 360.0;
            UpdateArcSegment(OuterArcSegment, 75, 75, 70, cpuAngle);
            OuterArcPath.Stroke = GetUsageBrush(cpuUsage);

            double ramAngle = (ramUsagePercent / 100.0) * 360.0;
            UpdateArcSegment(InnerArcSegment, 75, 75, 55, ramAngle);
            InnerArcPath.Stroke = GetUsageBrush(ramUsagePercent);
        }

        private Brush GetUsageBrush(float usagePercent)
        {
            if (usagePercent < 50)
                return Brushes.LimeGreen;
            else if (usagePercent < 80)
                return Brushes.Gold;
            else
                return Brushes.Red;
        }

        private void UpdateArcSegment(ArcSegment arc, double cx, double cy, double radius, double angle)
        {
            double radians = (Math.PI / 180.0) * angle;
            double endX = cx + radius * Math.Sin(radians);
            double endY = cy - radius * Math.Cos(radians);

            arc.Point = new Point(endX, endY);
            arc.IsLargeArc = angle > 180.0;
        }

        // ================== Window Events ================== //

        /// <summary>
        /// On loaded, place window at bottom-right of the monitor where the mouse is located.
        /// Also initialize the TaskbarIcon for the system tray.
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 1) Win32: get mouse position
            POINT mousePos;
            if (!GetCursorPos(out mousePos))
            {
                mousePos.X = 0;
                mousePos.Y = 0;
            }

            // 2) find which monitor the cursor is on
            IntPtr hMonitor = MonitorFromPoint(mousePos, MONITOR_DEFAULTTONEAREST);
            if (hMonitor != IntPtr.Zero)
            {
                // 3) get the monitor's info (working area)
                MONITORINFO mi = new MONITORINFO();
                mi.cbSize = Marshal.SizeOf(mi);
                if (GetMonitorInfo(hMonitor, ref mi))
                {
                    RECT work = mi.rcWork;
                    // In 100% DPI scenario: direct approach
                    this.Left = work.right - this.Width;
                    this.Top = work.bottom - this.Height;
                }
            }

            // Initialize tray icon
            _taskbarIcon = new TaskbarIcon();
            // You can use your own icon file, or a System.Drawing.SystemIcons
            // e.g. _taskbarIcon.Icon = new System.Drawing.Icon("MyIcon.ico");
            _taskbarIcon.Icon = System.Drawing.SystemIcons.Application;
            _taskbarIcon.ToolTipText = "CPU & RAM Monitor";
            // The context menu is defined as resource "TrayMenu" in App.xaml
            _taskbarIcon.ContextMenu = (System.Windows.Controls.ContextMenu)FindResource("TrayMenu");
            _taskbarIcon.TrayMouseDoubleClick += TaskbarIcon_TrayMouseDoubleClick;
        }

        /// <summary>
        /// When user double-clicks on tray icon => restore the window
        /// </summary>
        private void TaskbarIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            Show();
            if (this.WindowState == WindowState.Minimized)
            {
                this.WindowState = WindowState.Normal;
            }
            this.Activate();
        }

        /// <summary>
        /// If the window is minimized, hide it (so that only tray icon remains)
        /// </summary>
        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                // hide from desktop
                this.Hide();
            }
        }

        // Left-click drag
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        // Mouse wheel => adjust opacity
        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double step = (e.Delta > 0) ? 0.05 : -0.05;
            double newOpacity = this.Opacity + step;
            if (newOpacity < 0.3) newOpacity = 0.3;
            if (newOpacity > 1.0) newOpacity = 1.0;
            this.Opacity = newOpacity;
        }

        // Double-click => open Task Manager
        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try
                {
                    Process.Start("taskmgr");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to open Task Manager.\n" + ex.Message,
                                    "Error",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                }
            }
        }

        // Right-click context menu => Minimize
        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        // Right-click context menu => Exit
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Clean up tray icon when window closes
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (_taskbarIcon != null)
            {
                _taskbarIcon.Dispose();
                _taskbarIcon = null;
            }
        }
    }
}
