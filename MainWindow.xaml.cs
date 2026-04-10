using System.Windows;
using System.Windows.Input;

namespace Eurocast_Top5_Viewer
{
    /// <summary>
    /// Logique d'interaction pour MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F11)
            {
                ToggleFullScreen();
            }
        }

        private void ToggleFullScreen()
        {
            var vm = DataContext as ViewModels.MainViewModel;

            // Si on est en mode Plein Écran (Style = None), on repasse en mode Fenêtré
            if (WindowStyle == WindowStyle.None)
            {
                WindowStyle = WindowStyle.SingleBorderWindow;
                WindowState = WindowState.Normal;
                Topmost = false;
                if (vm != null) vm.IsWindowed = true;
            }
            // Sinon, on passe en mode Plein Écran (Kiosk Production)
            else
            {
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
                Topmost = true;
                if (vm != null) vm.IsWindowed = false;
            }
        }
    }
}