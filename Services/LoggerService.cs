using System;
using System.IO;

namespace Eurocast_Top5_Viewer.Services
{
    public static class LoggerService
    {
        private static readonly string LogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

        public static void Log(string message)
        {
            try
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }

                // Format aaaa_mm_jj respecté strictement
                string fileName = $"log_{DateTime.Now:yyyy_MM_dd}.txt";
                string fullPath = Path.Combine(LogDirectory, fileName);

                string logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
                File.AppendAllText(fullPath, logEntry);
            }
            catch
            {
                // Silencieux pour ne pas crasher l'application en cas de droits d'accès restreints
            }
        }
    }
}