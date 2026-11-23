using System;
using System.Threading;
using System.Windows.Forms;

namespace AdbAux
{
    internal static class Program
    {
        // Named objects shared by all copies of AdbAux in this Windows session ("Local\").
        private const string MutexName = @"Local\AdbAux.SingleInstance";
        private const string ShowEventName = @"Local\AdbAux.ShowWindow";

        /// <summary>
        /// Entry point. Command-line option: --minimized  (start hidden in the notification area;
        /// used by the "Start with Windows" entry).
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            // Only one copy may run: two copies would both try to start scrcpy for the same phone.
            bool createdNew;
            using (var mutex = new Mutex(true, MutexName, out createdNew))
            {
                // Started by "Restart AdbAux": wait (up to 15 s) for the old copy to close and release the mutex.
                if (!createdNew && Array.IndexOf(args, "--restarted") >= 0)
                {
                    try
                    {
                        createdNew = mutex.WaitOne(TimeSpan.FromSeconds(15));
                    }
                    catch (AbandonedMutexException)
                    {
                        createdNew = true;   // the old copy ended without releasing it; we own it now
                    }
                }

                if (!createdNew)
                {
                    // Already running: ask that copy to show its window, then exit.
                    try
                    {
                        using (EventWaitHandle show = EventWaitHandle.OpenExisting(ShowEventName))
                        {
                            show.Set();
                        }
                    }
                    catch (Exception)
                    {
                        MessageBox.Show(
                            "AdbAux is already running. Look for its icon in the notification area (system tray).",
                            "AdbAux", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return;
                }

                // Errors nobody expected: write them to the log file and tell the user, instead of
                // showing the standard .NET crash dialog.
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) => ReportUnexpectedError(e.Exception, true);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => ReportUnexpectedError(e.ExceptionObject as Exception, false);

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                using (var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName))
                {
                    bool startHidden = Array.IndexOf(args, "--minimized") >= 0;
                    var form = new MainForm(startHidden);

                    // When a second copy signals the event, bring this window to the front.
                    RegisteredWaitHandle wait = ThreadPool.RegisterWaitForSingleObject(
                        showEvent, (state, timedOut) => form.RequestShow(), null, Timeout.Infinite, false);

                    Application.Run(form);
                    wait.Unregister(null);
                }

                // Release ownership, so a restarted copy can take over at once.
                try
                {
                    mutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                    // Not owned by this thread: nothing to release.
                }
            }
        }

        private static void ReportUnexpectedError(Exception ex, bool canCloseNormally)
        {
            AppLog.Write("UNEXPECTED ERROR: " + (ex != null ? ex.ToString() : "unknown error"));
            try
            {
                MessageBox.Show(
                    "AdbAux hit an unexpected error and will close." + Environment.NewLine + Environment.NewLine
                    + "The details were written to the log file, for your own troubleshooting:" + Environment.NewLine
                    + AppLog.FilePath,
                    "AdbAux", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception)
            {
                // No message possible (e.g. during shutdown).
            }
            if (canCloseNormally)
            {
                Application.Exit();   // closes the window normally, which also stops scrcpy
            }
        }
    }
}
