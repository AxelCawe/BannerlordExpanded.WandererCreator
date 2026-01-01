using System;
using System.IO;

namespace BannerlordExpanded.WandererCreator
{
    public static class FileLogger
    {
        /// <summary>
        /// Set to true to enable debug logging. Should be false for production/release.
        /// </summary>
        public static bool IsEnabled = true; // TEMP: enabled for debugging

        private static string LogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mount and Blade II Bannerlord", "WandererCreator_Debug.log");

        public static void Log(string message)
        {
            if (!IsEnabled) return;

            try
            {
                File.AppendAllText(LogPath, $"{DateTime.Now}: {message}\n");
            }
            catch { }
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(LogPath))
                    File.Delete(LogPath);
            }
            catch { }
        }
    }
}
