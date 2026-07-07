using System.Threading;
using System.Windows;

namespace CircleMonitorWPF
{
    public partial class App : Application
    {
        private const string SingleInstanceMutexName = @"Local\CircleMonitorWPF.SingleInstance";

        private Mutex _singleInstanceMutex;
        private bool _ownsSingleInstanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            bool createdNew;
            _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out createdNew);
            _ownsSingleInstanceMutex = createdNew;

            if (!createdNew)
            {
                Shutdown();
                return;
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_singleInstanceMutex != null)
            {
                if (_ownsSingleInstanceMutex)
                {
                    _singleInstanceMutex.ReleaseMutex();
                }

                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }

            base.OnExit(e);
        }

        private void ShowWindow_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = Current.MainWindow as MainWindow;
            if (mainWindow == null)
            {
                return;
            }

            mainWindow.Show();
            if (mainWindow.WindowState == WindowState.Minimized)
            {
                mainWindow.WindowState = WindowState.Normal;
            }
            mainWindow.Activate();
        }

        private void HideWindow_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.Hide();
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.Close();
            }
        }
    }
}
