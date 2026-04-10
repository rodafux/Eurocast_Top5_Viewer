using System;
using System.Windows;

namespace Eurocast_Top5_Viewer
{
    public partial class App : Application
    {
        // Objet natif Windows Runtime pour empêcher la mise en veille de l'écran de manière sécurisée (sans kernel32.dll)
        private Windows.System.Display.DisplayRequest? _displayRequest;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // Activation du maintien de l'écran allumé de manière 100% gérée (Standard .NET / API Windows)
                _displayRequest = new Windows.System.Display.DisplayRequest();
                _displayRequest.RequestActive();
            }
            catch (Exception)
            {
                // Silencieux : si l'API n'est pas disponible pour une raison système, l'application ne crashe pas.
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                // Libération propre de la demande pour autoriser à nouveau la mise en veille à la fermeture du dashboard
                if (_displayRequest != null)
                {
                    _displayRequest.RequestRelease();
                    _displayRequest = null;
                }
            }
            catch (Exception)
            {
                // Ignoré
            }

            base.OnExit(e);
        }
    }
}