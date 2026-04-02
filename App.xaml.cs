using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace Eurocast_Top5_Viewer
{
    public partial class App : Application
    {
        // Importation de l'API Windows pour bloquer la mise en veille
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern uint SetThreadExecutionState(uint esFlags);

        private const uint ES_CONTINUOUS = 0x80000000;
        private const uint ES_DISPLAY_REQUIRED = 0x00000002;
        private const uint ES_SYSTEM_REQUIRED = 0x00000001;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Verrouillage de l'écran : Empêche l'écran de s'éteindre et le PC de s'endormir
            SetThreadExecutionState(ES_CONTINUOUS | ES_DISPLAY_REQUIRED | ES_SYSTEM_REQUIRED);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Libération du verrouillage à la fermeture
            SetThreadExecutionState(ES_CONTINUOUS);
            base.OnExit(e);
        }
    }
}