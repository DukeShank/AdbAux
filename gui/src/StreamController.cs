using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AdbAux
{
    /// <summary>Colour of the status dot and tray badge.</summary>
    internal enum StatusLevel { Gray, Blue, Green, Yellow, Red }

    internal sealed class StreamStatus
    {
        public string Text;
        public StatusLevel Level;
    }

    internal sealed class NotifyEventArgs : EventArgs
    {
        public NotifyEventArgs(string title, string text, ToolTipIcon icon)
        {
            Title = title;
            Text = text;
            Icon = icon;
        }

        public string Title { get; private set; }
        public string Text { get; private set; }
        public ToolTipIcon Icon { get; private set; }
    }

    /// <summary>Names of the selections, as shown to the user.</summary>
    internal static class Names
    {
        public static string Content(ContentType content)
        {
            switch (content)
            {
                case ContentType.Video: return "Video";
                case ContentType.LowLatency: return "Low latency";
                default: return "Music";
            }
        }

        public static string Mode(CaptureMode mode)
        {
            switch (mode)
            {
                case CaptureMode.Everything: return "all sounds";
                case CaptureMode.Duplicate: return "PC and phone";
                default: return "media only";
            }
        }
    }

    /// <summary>
    /// Finds the phone and starts, stops and restarts scrcpy. Contains no controls: the window shows
    /// what GetStatus returns and reacts to the events. Runs on the window's thread; events from the
    /// scrcpy process (other threads) are passed to that thread first.
    ///
    /// While no scrcpy runs, adb is asked every 2 seconds which phone is connected. While scrcpy runs,
    /// adb is not asked: scrcpy ends by itself when the phone is unplugged, and then the checks resume.
    /// </summary>
    internal sealed class StreamController : IDisposable
    {
        private enum Phase
        {
            Idle,        // no scrcpy running
            Starting,    // scrcpy started, its helper on the phone has not reported yet
            Streaming    // the helper on the phone reported: audio is flowing
        }

        /// <summary>Why AdbAux stopped scrcpy itself (an exit without one is unexpected).</summary>
        private sealed class StopRequest
        {
            public string Reason;
            public bool Restart;
        }

        private const int PollIntervalMs = 2000;
        private const int RetrySeconds = 5;

        private readonly AppSettings settings;
        private readonly Action<string> log;
        private readonly SynchronizationContext ui;
        private readonly System.Windows.Forms.Timer pollTimer = new System.Windows.Forms.Timer();

        // The scrcpy session.
        private ScrcpySession session;
        private int sessionId;                  // ignores events from an older, already replaced session
        private Phase phase = Phase.Idle;
        private StopRequest stopRequest;
        private bool nextStartIsQuiet;          // the next start follows a restart: no "streaming" notification
        private bool sessionIsQuiet;

        // What the running session was started with (to decide whether a change needs a restart).
        private string activeSerial;
        private string activeArguments;
        private string activeAdb;
        private string activeScrcpyDir;
        private bool activeMixerIcon;

        // Wishes and errors.
        private bool wantStreaming;             // the user (or auto-start) wants audio running
        private string lastError;
        private string reportedError;           // last error shown as a notification
        private bool reportErrorAtNextCheck;    // report lastError if the phone is still connected
        private DateTime retryAt = DateTime.MinValue;

        // adb checks.
        private bool polling;
        private bool adbServerStarted;
        private string lastAdbError;
        private bool installing;
        private bool disposed;

        public StreamController(AppSettings settings, Action<string> log)
        {
            this.settings = settings;
            this.log = log;
            ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            Content = settings.DefaultContent;
            Mode = settings.DefaultMode;
            pollTimer.Interval = PollIntervalMs;
            pollTimer.Tick += (s, e) => Poll();
        }

        /// <summary>Something shown by GetStatus, CanStart or CanStop may have changed.</summary>
        public event EventHandler StateChanged;

        /// <summary>A tray notification should be shown (the settings for notifications are already checked).</summary>
        public event EventHandler<NotifyEventArgs> Notification;

        /// <summary>A phone was connected and authorised; raised before streaming starts.</summary>
        public event EventHandler<PhoneInfo> PhoneConnected;

        public string ScrcpyDir { get; private set; }
        public string AdbPath { get; private set; }
        public PhoneInfo Phone { get; private set; }
        public bool PhoneReady { get; private set; }
        public ContentType Content { get; private set; }
        public CaptureMode Mode { get; private set; }

        public bool Installing { get { return installing; } }
        public bool CanStart { get { return !wantStreaming && ScrcpyDir != null && !installing; } }
        public bool CanStop { get { return wantStreaming || session != null; } }

        /// <summary>For the status line and notifications, e.g. "SM-A366E: Music, 40 ms, media only".</summary>
        public string Describe()
        {
            string model = Phone != null ? Phone.DisplayModel : "phone";
            return model + ": " + Names.Content(Content) + ", " + settings.GetBuffer(Content) + " ms, " + Names.Mode(Mode);
        }

        // ---------------- commands ----------------

        /// <summary>Finds scrcpy and chooses the adb to use (after start-up, a changed setting or an update).</summary>
        public void Locate()
        {
            string oldDir = ScrcpyDir;
            string oldAdb = AdbPath;
            ScrcpyDir = ScrcpyLocator.Find(settings);
            string note;
            AdbPath = AdbLocator.Resolve(settings, ScrcpyDir, out note);
            adbServerStarted = false;

            if (ScrcpyDir == null)
            {
                log("scrcpy not found (looked in: " + (string.IsNullOrWhiteSpace(settings.ScrcpyFolder) ? "" : settings.ScrcpyFolder + ", ")
                    + System.IO.Path.Combine(ScrcpyLocator.AppDir, "scrcpy") + ", " + ScrcpyLocator.LocalDir + ").");
            }
            else if (ScrcpyDir != oldDir)
            {
                log("Using scrcpy in " + ScrcpyDir + " (" + ScrcpyLocator.Describe(ScrcpyDir) + ").");
            }
            if (note != null)
            {
                log(note);
            }
            if (AdbPath != null && AdbPath != oldAdb)
            {
                log("Using adb: " + AdbPath);
            }
            RaiseStateChanged();
        }

        public void BeginPolling()
        {
            pollTimer.Start();
            Poll();
        }

        public void Start()
        {
            wantStreaming = true;
            lastError = null;
            retryAt = DateTime.MinValue;
            EnsureSession();
            RaiseStateChanged();
        }

        /// <summary>
        /// Stops streaming at the user's request. With the "pause media" setting, media playing on the
        /// phone is paused first, so it does not continue on the phone's speaker.
        /// </summary>
        public void Stop()
        {
            wantStreaming = false;
            lastError = null;
            bool pause = settings.PauseMediaOnStop && session != null && phase == Phase.Streaming;
            StopSession("stopped by user", false, pause);
            RaiseStateChanged();
        }

        /// <summary>
        /// Ends everything when AdbAux closes. pauseMedia: pause the phone's media first (with the
        /// setting on), as with Stop; not used for "Restart AdbAux".
        /// </summary>
        public void Shutdown(bool pauseMedia)
        {
            if (pauseMedia && settings.PauseMediaOnStop && session != null && phase == Phase.Streaming
                && PauseIfPlaying(activeAdb, activeSerial))
            {
                log("Paused media playback on the phone.");
            }
            Dispose();
        }

        /// <summary>Stops scrcpy and starts it again (or starts it, if it is not running).</summary>
        public void RestartStream()
        {
            if (session == null)
            {
                Start();
                return;
            }
            wantStreaming = true;
            StopSession("restart requested by user", true);
            RaiseStateChanged();
        }

        /// <summary>Changes what is playing; a running stream restarts with the new buffer or mode.</summary>
        public void SetSelection(ContentType content, CaptureMode mode)
        {
            Content = content;
            Mode = mode;
            RestartIfNeeded();
        }

        /// <summary>Call after the settings were changed: finds scrcpy and adb again and restarts if needed.</summary>
        public void Reconfigure()
        {
            Locate();
            RestartIfNeeded();
        }

        /// <summary>True if applying these settings would restart the running stream.</summary>
        public bool WouldRestart(AppSettings candidate)
        {
            if (session == null)
            {
                return false;
            }
            string note;
            string dir = ScrcpyLocator.Find(candidate);
            string adb = AdbLocator.Resolve(candidate, dir, out note);
            return !string.Equals(dir, activeScrcpyDir, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(adb, activeAdb, StringComparison.OrdinalIgnoreCase)
                || candidate.ShowIconInVolumeMixer != activeMixerIcon
                || ArgumentsFor(candidate, activeSerial) != activeArguments;
        }

        /// <summary>
        /// Downloads and installs a scrcpy release into target. A running stream is stopped first (and
        /// waited for, so its files can be replaced); it starts again afterwards. Exceptions are passed on.
        /// </summary>
        public async Task InstallScrcpyAsync(ScrcpyRelease release, string target, bool stopAdbServer)
        {
            installing = true;
            RaiseStateChanged();
            try
            {
                await StopSession("updating scrcpy", true);
                await ScrcpyInstaller.InstallAsync(release, target, stopAdbServer, message => Post(() => log(message)));
            }
            finally
            {
                installing = false;
                retryAt = DateTime.MinValue;
                RaiseStateChanged();
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            pollTimer.Stop();
            pollTimer.Dispose();
            wantStreaming = false;
            if (session != null)
            {
                session.Dispose();   // stops scrcpy (and waits for it) before the program ends
                session = null;
            }
        }

        // ---------------- status ----------------

        public StreamStatus GetStatus()
        {
            if (installing)
            {
                return Status("Downloading scrcpy...", StatusLevel.Blue);
            }
            if (ScrcpyDir == null)
            {
                return Status("scrcpy not found. Use \"Check for scrcpy updates\" on the Advanced tab.", StatusLevel.Red);
            }
            if (session != null)
            {
                return phase == Phase.Streaming
                    ? Status("Streaming from " + Describe() + ".", StatusLevel.Green)
                    : Status("Starting scrcpy...", StatusLevel.Blue);
            }
            if (Phone == null)
            {
                return Status("Waiting for a phone on USB...", StatusLevel.Gray);
            }
            if (!Phone.IsReady)
            {
                return Status("Phone " + Phone.Serial + " is '" + Phone.State
                    + "'. Unlock it and tap Allow on the USB debugging prompt.", StatusLevel.Yellow);
            }
            if (wantStreaming && lastError != null)
            {
                return Status("Error: " + lastError + " Retrying...", StatusLevel.Red);
            }
            if (wantStreaming)
            {
                return Status("Starting...", StatusLevel.Blue);
            }
            return Status(Phone.DisplayModel + " connected. Press Start to stream.", StatusLevel.Blue);
        }

        private static StreamStatus Status(string text, StatusLevel level)
        {
            return new StreamStatus { Text = text, Level = level };
        }

        // ---------------- phone checks ----------------

        private async void Poll()
        {
            if (disposed || polling || installing || session != null || ScrcpyDir == null || AdbPath == null)
            {
                return;
            }

            polling = true;
            string adb = AdbPath;
            bool startServer = !adbServerStarted;
            PhoneInfo found = null;
            try
            {
                // adb runs on a background thread so the window never freezes.
                found = await Task.Run(() =>
                {
                    if (startServer)
                    {
                        AdbClient.StartServer(adb);
                    }
                    return AdbClient.FindUsbPhone(adb);
                });
                adbServerStarted = true;
                lastAdbError = null;
            }
            catch (Exception ex)
            {
                adbServerStarted = false;
                if (ex.Message != lastAdbError)
                {
                    log("adb: " + ex.Message);
                    lastAdbError = ex.Message;
                }
            }
            finally
            {
                polling = false;
            }

            if (!disposed)
            {
                OnPhoneChecked(found);
            }
        }

        private void OnPhoneChecked(PhoneInfo found)
        {
            bool ready = found != null && found.IsReady;
            bool connectedNow = ready && !PhoneReady;

            if (connectedNow)
            {
                log("Phone connected: " + found.DisplayModel + " (" + found.Serial + ").");
                RaiseNotification("Phone connected", found.DisplayModel, ToolTipIcon.Info, settings.NotifyConnection);
                lastError = null;
                reportedError = null;
                retryAt = DateTime.MinValue;
                if (settings.AutoStart)
                {
                    wantStreaming = true;
                }
            }
            else if (!ready && PhoneReady)
            {
                log("Phone disconnected or no longer authorised.");
                RaiseNotification("Phone disconnected", "Streaming resumes when the phone is connected again.",
                    ToolTipIcon.Info, settings.NotifyConnection);
            }

            if (found != null && !found.IsReady && (Phone == null || Phone.State != found.State))
            {
                log("Phone " + found.Serial + " is '" + found.State + "'.");
                if (found.State == "unauthorized")
                {
                    RaiseNotification("Phone not authorised", "Unlock the phone and tap Allow on the USB debugging prompt.",
                        ToolTipIcon.Warning, settings.NotifyConnection);
                }
            }

            Phone = found;
            PhoneReady = ready;

            if (connectedNow)
            {
                EventHandler<PhoneInfo> handler = PhoneConnected;
                if (handler != null)
                {
                    handler(this, found);   // e.g. applies the profile linked to this phone before streaming starts
                }
            }

            // An unexpected stop is reported only if the phone is still connected (unplugging is not an error).
            if (reportErrorAtNextCheck)
            {
                reportErrorAtNextCheck = false;
                if (PhoneReady && lastError != null && lastError != reportedError)
                {
                    RaiseNotification("AdbAux: streaming stopped", lastError, ToolTipIcon.Error, settings.NotifyErrors);
                    reportedError = lastError;
                }
            }

            EnsureSession();
            RaiseStateChanged();
        }

        // ---------------- scrcpy session ----------------

        private string ArgumentsFor(AppSettings s, string serial)
        {
            return ScrcpyArgs.Build(serial, s.GetBuffer(Content), s.OutputBufferMs, Mode, s.ExtraArgs);
        }

        /// <summary>Starts scrcpy when it should be running and is not.</summary>
        private void EnsureSession()
        {
            if (installing || ScrcpyDir == null || !wantStreaming || session != null || !PhoneReady || DateTime.Now < retryAt)
            {
                return;
            }

            string serial = Phone.Serial;
            string arguments = ArgumentsFor(settings, serial);
            int id = ++sessionId;
            var s = new ScrcpySession();
            s.OutputLine += (o, line) => Post(() => OnSessionLine(id, line));
            s.Exited += (o, code) => Post(() => OnSessionExited(id, code));

            phase = Phase.Starting;
            lastError = null;
            stopRequest = null;
            sessionIsQuiet = nextStartIsQuiet;
            nextStartIsQuiet = false;
            activeSerial = serial;
            activeArguments = arguments;
            activeAdb = AdbPath;
            activeScrcpyDir = ScrcpyDir;
            activeMixerIcon = settings.ShowIconInVolumeMixer;

            string exe = MixerIcon.ExeToRun(ScrcpyDir, settings.ShowIconInVolumeMixer, log);
            log("Starting: " + System.IO.Path.GetFileName(exe) + " " + arguments);
            try
            {
                s.Start(exe, arguments, AdbPath);
                session = s;
            }
            catch (Exception ex)
            {
                phase = Phase.Idle;
                lastError = "Could not start scrcpy: " + ex.Message;
                log(lastError);
                retryAt = DateTime.Now.AddSeconds(RetrySeconds);
                reportErrorAtNextCheck = true;
                s.Dispose();
            }
        }

        private void OnSessionLine(int id, string line)
        {
            if (id != sessionId)
            {
                return;
            }
            log("scrcpy: " + line);
            if (line.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                lastError = line.Trim();
            }
            if (phase == Phase.Starting && line.Contains("[server] INFO: Device:"))
            {
                phase = Phase.Streaming;   // the helper on the phone is running
                reportedError = null;
                ResumeMediaIfSet();
                if (!sessionIsQuiet)
                {
                    RaiseNotification("Streaming", Describe(), ToolTipIcon.Info, settings.NotifyStreaming);
                }
            }
            RaiseStateChanged();
        }

        private void OnSessionExited(int id, int exitCode)
        {
            if (id != sessionId || session == null)
            {
                return;
            }
            session.Dispose();
            session = null;
            phase = Phase.Idle;

            StopRequest stop = stopRequest;
            stopRequest = null;
            if (stop != null)
            {
                // Exit code 1 here only means "ended by taskkill": not an error.
                log("scrcpy stopped (" + stop.Reason + ").");
            }
            else
            {
                log("scrcpy exited unexpectedly (exit code " + exitCode + ").");
            }

            if (stop != null && stop.Restart)
            {
                nextStartIsQuiet = true;
                retryAt = DateTime.MinValue;   // start again at once with the new settings
            }
            else if (stop == null && wantStreaming && exitCode != 0)
            {
                if (lastError == null)
                {
                    lastError = "scrcpy exited with code " + exitCode + ".";
                }
                retryAt = DateTime.Now.AddSeconds(RetrySeconds);
                reportErrorAtNextCheck = true;
            }

            EnsureSession();   // a restart starts here; after an error, the phone checks retry later
            RaiseStateChanged();
        }

        private void RestartIfNeeded()
        {
            if (session != null && (ScrcpyDir != activeScrcpyDir || AdbPath != activeAdb
                || settings.ShowIconInVolumeMixer != activeMixerIcon || ArgumentsFor(settings, activeSerial) != activeArguments))
            {
                StopSession("restarting with new settings", true);
            }
            RaiseStateChanged();
        }

        /// <summary>
        /// Stops the running session on purpose; the reason is shown in the log. The stop runs on a
        /// background thread (it can take a moment); OnSessionExited cleans up afterwards.
        /// pauseMedia: first pause media playing on the phone, and remember to resume it.
        /// </summary>
        private Task StopSession(string reason, bool restart, bool pauseMedia = false)
        {
            if (session == null)
            {
                return Task.FromResult(0);
            }
            stopRequest = new StopRequest { Reason = reason, Restart = restart };
            ScrcpySession s = session;
            string adb = activeAdb;
            string serial = activeSerial;
            return Task.Run(() =>
            {
                if (pauseMedia && PauseIfPlaying(adb, serial))
                {
                    Post(() => log("Paused media playback on the phone."));
                }
                s.Stop();
            });
        }

        /// <summary>Pauses the phone's media if an app is playing. Returns true if it paused. Any thread.</summary>
        private static bool PauseIfPlaying(string adb, string serial)
        {
            try
            {
                if (adb == null || serial == null || !AdbClient.QueryMediaPlaying(adb, serial))
                {
                    return false;   // nothing playing (or no answer): nothing to pause or resume later
                }
                AdbClient.SendKey(adb, serial, AdbClient.KeyMediaPause);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Streaming has started: with the "resume" setting, presses Play on the phone if nothing is
        /// playing. Android sends it to the app that played last, as a headset's play button does.
        /// A short wait first lets scrcpy's capture begin, so playback does not start on the phone's speaker.
        /// </summary>
        private void ResumeMediaIfSet()
        {
            // Not after a restart (changed settings, Restart streaming, scrcpy update): the user did not stop,
            // so media they paused themselves in the meantime must stay paused.
            if (!settings.ResumeMediaOnStart || sessionIsQuiet)
            {
                return;
            }
            string adb = activeAdb;
            string serial = activeSerial;
            Task.Run(async () =>
            {
                await Task.Delay(500);
                try
                {
                    if (!AdbClient.QueryMediaPlaying(adb, serial))
                    {
                        AdbClient.SendKey(adb, serial, AdbClient.KeyMediaPlay);
                        Post(() => log("Resumed media playback on the phone."));
                    }
                }
                catch (Exception)
                {
                    // The phone was unplugged in the meantime; nothing to do.
                }
            });
        }

        // ---------------- helpers ----------------

        private void RaiseStateChanged()
        {
            EventHandler handler = StateChanged;
            if (handler != null && !disposed)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void RaiseNotification(string title, string text, ToolTipIcon icon, bool enabled)
        {
            EventHandler<NotifyEventArgs> handler = Notification;
            if (enabled && handler != null && !disposed)
            {
                handler(this, new NotifyEventArgs(title, text, icon));
            }
        }

        /// <summary>Runs an action on the window's thread (scrcpy's events arrive on other threads).</summary>
        private void Post(Action action)
        {
            try
            {
                ui.Post(state =>
                {
                    if (!disposed)
                    {
                        action();
                    }
                }, null);
            }
            catch (Exception)
            {
                // The window is closing.
            }
        }
    }
}
