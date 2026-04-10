using System;
using System.Windows;

namespace Eurocast_Top5_Viewer
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // Mode de fonctionnement strict .NET natif sans appels non gérés
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
        }
    }
}