using System;
using System.IO;
using System.Text;

namespace AdbAux
{
    /// <summary>
    /// Log file for the user's own troubleshooting: %LOCALAPPDATA%\AdbAux\logs\adbaux.log.
    /// Nothing is sent anywhere. The file is limited to about 1 MB; when it grows past that,
    /// it is renamed to adbaux.old.log (replacing the previous one) and a new file is started.
    /// </summary>
    public static class AppLog
    {
        public const string Notice =
            "This log is for your own troubleshooting. It is stored only on this PC and contains your "
            + "phone's model and serial number; remove them before sharing the log with anyone.";

        private const long MaxBytes = 1024 * 1024;
        private static readonly object Sync = new object();

        public static string Folder
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AdbAux", "logs");
            }
        }

        public static string FilePath
        {
            get { return Path.Combine(Folder, "adbaux.log"); }
        }

        /// <summary>Writes a separator and the notice at the start of each program run.</summary>
        public static void StartSession()
        {
            Write("------------------------------------------------------------");
            Write("Note: " + Notice);
        }

        public static void Write(string message)
        {
            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(Folder);
                    var info = new FileInfo(FilePath);
                    if (info.Exists && info.Length > MaxBytes)
                    {
                        string old = Path.Combine(Folder, "adbaux.old.log");
                        if (File.Exists(old))
                        {
                            File.Delete(old);
                        }
                        File.Move(FilePath, old);
                    }
                    File.AppendAllText(FilePath,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine,
                        Encoding.UTF8);
                }
            }
            catch (Exception)
            {
                // Logging must never stop the program (e.g. disk full or folder not writable).
            }
        }
    }
}
