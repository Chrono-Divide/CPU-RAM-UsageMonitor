using System.Windows;

namespace CircleMonitorWPF
{
    public partial class App : Application
    {
        private void ShowWindow_Click(object sender, RoutedEventArgs e)
        {
            if (Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.Show();
                if (mainWindow.WindowState == WindowState.Minimized)
                {
                    mainWindow.WindowState = WindowState.Normal;
                }
                mainWindow.Activate();
            }
        }

        private void HideWindow_Click(object sender, RoutedEventArgs e)
        {
            if (Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.Hide();
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            if (Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.Close();
            }
        }
    }
}
