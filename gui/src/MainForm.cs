using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AdbAux
{
    /// <summary>
    /// Main window. Tabs: Basic (what is playing, alert handling, start/stop), Advanced (settings,
    /// applied with Apply), Log, About. The phone checks and scrcpy itself are handled by
    /// StreamController; this class shows its state and passes the user's choices to it.
    ///
    /// The class is split over several files: MainForm.cs (window, tray, status), MainForm.Pages.cs
    /// (Basic, Log and About tabs, layout helpers), MainForm.Advanced.cs (Advanced tab and applying
    /// settings), MainForm.Profiles.cs (tested profiles).
    /// </summary>
    public sealed partial class MainForm : Form
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "AdbAux";
        private const string ScrcpyProjectUrl = "https://github.com/Genymobile/scrcpy";

        // ---------------- state ----------------
        private readonly AppSettings settings;
        private readonly StreamController controller;
        private readonly bool startHidden;
        private bool exiting;
        private bool restarting;                  // closing for "Restart AdbAux": the phone's media is not paused
        private bool trayHintShown;
        private bool updatingUi;                  // true while code (not the user) changes controls

        // ---------------- controls: Basic ----------------
        private readonly Label statusDot = new Label();
        private readonly Label statusText = new Label();
        private readonly RadioButton rbMusic = new ThemedRadioButton();
        private readonly RadioButton rbVideo = new ThemedRadioButton();
        private readonly RadioButton rbLowLatency = new ThemedRadioButton();
        private readonly RadioButton rbMediaOnly = new ThemedRadioButton();
        private readonly RadioButton rbEverything = new ThemedRadioButton();
        private readonly RadioButton rbDuplicate = new ThemedRadioButton();
        private readonly ThemedButton btnStart = new ThemedButton { Primary = true };
        private readonly Button btnStop = new ThemedButton();
        private readonly Button btnVolumeMixer = new ThemedButton();

        // ---------------- controls: Log and About ----------------
        private readonly TextBox txtLog = new TextBox();
        private readonly Label lblAboutScrcpy = new Label();

        // ---------------- tray ----------------
        private readonly Dictionary<StatusLevel, Icon> trayIcons = new Dictionary<StatusLevel, Icon>();
        private readonly Icon appIcon;
        private readonly ContextMenuStrip trayMenu = new ContextMenuStrip();
        private NotifyIcon tray;

        public MainForm(bool startHidden)
        {
            this.startHidden = startHidden;
            settings = AppSettings.Load();
            controller = new StreamController(settings, Log);
            controller.StateChanged += (s, e) => UpdateStatus();
            controller.Notification += (s, e) => Notify(e.Title, e.Text, e.Icon);
            controller.PhoneConnected += (s, found) => ApplyLinkedProfile(found);

            // Layout is designed at 96 DPI and scaled automatically on high-DPI screens.
            // SuspendLayout/ResumeLayout (as in Visual Studio's designer code) is when that scaling is applied.
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = Theme.UiFont();
            Text = "AdbAux";
            // Resizable with a maximise button: this is what lets Windows 10/11 snap the window
            // (drag to a screen edge, Win+arrow keys, Windows 11 snap layouts).
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(680, 760);

            appIcon = LoadAppIcon();
            Icon = appIcon;
            foreach (StatusLevel level in Enum.GetValues(typeof(StatusLevel)))
            {
                trayIcons[level] = MakeTrayIcon(appIcon, ToColor(level));
            }

            RefreshStartEntry();
            BuildUi();
            BuildTray();
            LoadSettingsIntoControls();
            UpdateContentLabels();
            SelectCurrentRadios();
            ResumeLayout(false);
            PerformLayout();

            // Smallest useful size (designed at 96 DPI, scaled for the screen's DPI).
            float scale = CurrentAutoScaleDimensions.Width / 96F;
            MinimumSize = new Size((int)(600 * scale), (int)(480 * scale));

            // First start on a small screen: keep the default size within the screen's working area.
            Rectangle work = Screen.FromPoint(Cursor.Position).WorkingArea;
            Size = new Size(Math.Min(Width, work.Width), Math.Min(Height, work.Height));
            RestoreWindowBounds();

            if (startHidden)
            {
                WindowState = FormWindowState.Minimized;
                ShowInTaskbar = false;
            }

            // "Use system setting" theme: react when the user changes Windows' app mode.
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        // =====================================================================
        // Window lifetime
        // =====================================================================

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplyTheme();   // before the window is first shown, so there is no light flash in dark mode
            AppLog.StartSession();
            Log("AdbAux " + AppInfo.Version + " started.");
            Log("Settings file: " + AppSettings.SettingsPath);
            Log("Log file: " + AppLog.FilePath);
            LocateScrcpy();
            controller.BeginPolling();

            // First run on a new PC: offer to download scrcpy straight away.
            if (controller.ScrcpyDir == null && !startHidden)
            {
                BeginInvoke(new Action(() => CheckScrcpyUpdates(
                    "AdbAux uses scrcpy (free, open source) to capture the phone's audio, and it was not found." + Environment.NewLine + Environment.NewLine)));
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (startHidden)
            {
                Hide();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Closing the window only hides it, unless the user chose Exit or Windows is shutting down.
            SaveWindowBounds();
            if (!exiting && settings.MinimizeToTray && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                if (!trayHintShown)
                {
                    Notify("AdbAux", "Still running in the notification area. Right-click the icon and choose Exit to quit.", ToolTipIcon.Info);
                    trayHintShown = true;
                }
                return;
            }

            base.OnFormClosing(e);
            controller.Shutdown(!restarting);   // stops scrcpy (and pauses the phone's media, if set)
            tray.Visible = false;
            tray.Dispose();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // SystemEvents is static: without this, it would keep a reference to the closed window.
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            base.OnFormClosed(e);
        }

        /// <summary>Called from another thread when a second copy of AdbAux was started.</summary>
        public void RequestShow()
        {
            PostToUi(ShowFromTray);
        }

        private void ShowFromTray()
        {
            Show();
            ShowInTaskbar = true;
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void ExitApplication()
        {
            exiting = true;
            Close();
        }

        /// <summary>
        /// Tray menu "Restart AdbAux": starts a new copy with --restarted (it waits until this copy has
        /// closed, so the one-copy-only check does not stop it), then closes this copy normally.
        /// </summary>
        private void RestartApplication()
        {
            try
            {
                string args = "--restarted" + (Visible ? "" : " --minimized");
                Process.Start(new ProcessStartInfo(Application.ExecutablePath, args) { UseShellExecute = false });
                Log("Restarting AdbAux.");
                restarting = true;
                ExitApplication();
            }
            catch (Exception ex)
            {
                Log("Could not restart AdbAux: " + ex.Message);
            }
        }

        /// <summary>
        /// Puts the window where it was last time. A saved position is used only if it is still
        /// visible on a connected monitor (for example, not on a monitor that has been unplugged).
        /// </summary>
        private void RestoreWindowBounds()
        {
            if (settings.WindowWidth <= 0 || settings.WindowHeight <= 0)
            {
                return;   // first start: centred, default size
            }
            var saved = new Rectangle(settings.WindowLeft, settings.WindowTop,
                Math.Max(settings.WindowWidth, MinimumSize.Width), Math.Max(settings.WindowHeight, MinimumSize.Height));
            foreach (Screen screen in Screen.AllScreens)
            {
                Rectangle visible = Rectangle.Intersect(screen.WorkingArea, saved);
                if (visible.Width >= 100 && visible.Height >= 50)   // enough of the window to grab
                {
                    StartPosition = FormStartPosition.Manual;
                    Bounds = saved;
                    if (settings.WindowMaximized && !startHidden)
                    {
                        WindowState = FormWindowState.Maximized;
                    }
                    return;
                }
            }
        }

        /// <summary>Remembers the window's normal (not maximised) size and position, and whether it was maximised.</summary>
        private void SaveWindowBounds()
        {
            // Minimised (including "start hidden"): keep what was saved before.
            if (!IsHandleCreated || WindowState == FormWindowState.Minimized)
            {
                return;
            }
            // RestoreBounds is the normal size and position while the window is maximised.
            Rectangle b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            if (b.Width <= 0 || b.Height <= 0 || b.Left <= -30000 || b.Top <= -30000)
            {
                return;   // Windows' placeholder position for minimised windows
            }
            settings.WindowLeft = b.Left;
            settings.WindowTop = b.Top;
            settings.WindowWidth = b.Width;
            settings.WindowHeight = b.Height;
            settings.WindowMaximized = WindowState == FormWindowState.Maximized;
            try
            {
                settings.Save();
            }
            catch (Exception)
            {
                // Not critical: the window just opens at the default place next time.
            }
        }

        // =====================================================================
        // Theme
        // =====================================================================

        /// <summary>Applies the light or dark appearance from the settings.</summary>
        private void ApplyTheme()
        {
            bool dark = Theme.IsDark(settings.Theme);
            Theme.Apply(this, dark);
            Theme.ApplyToMenu(trayMenu, dark);
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            // Windows reports a change of app mode in the "General" category.
            if (e.Category == UserPreferenceCategory.General && settings.Theme == ThemeChoice.System)
            {
                PostToUi(ApplyTheme);
            }
        }

        // =====================================================================
        // Status, tray and notifications
        // =====================================================================

        private void UpdateStatus()
        {
            StreamStatus status = controller.GetStatus();
            statusText.Text = status.Text;
            statusDot.ForeColor = ToColor(status.Level);
            btnStart.Enabled = controller.CanStart;
            btnStop.Enabled = controller.CanStop;

            if (tray != null)
            {
                tray.Icon = trayIcons[status.Level];
                string tip = "AdbAux: " + status.Text;
                tray.Text = tip.Length > 63 ? tip.Substring(0, 60) + "..." : tip;   // Windows limit: 63 characters
            }
            UpdatePendingState();   // whether Apply would restart the stream depends on the stream state
        }

        /// <summary>Shows a tray notification (Windows 10/11 show it as a pop-up and in the notification centre).</summary>
        private void Notify(string title, string text, ToolTipIcon icon)
        {
            if (tray == null || string.IsNullOrEmpty(text))
            {
                return;
            }
            if (text.Length > 250)
            {
                text = text.Substring(0, 247) + "...";   // Windows limit: 255 characters
            }
            tray.ShowBalloonTip(5000, title, text, icon);
        }

        private void BuildTray()
        {
            tray = new NotifyIcon { Icon = trayIcons[StatusLevel.Gray], Text = "AdbAux", Visible = true };
            trayMenu.Items.Add("Open AdbAux", null, (s, e) => ShowFromTray());
            trayMenu.Items.Add("Start", null, (s, e) => controller.Start());
            trayMenu.Items.Add("Stop", null, (s, e) => controller.Stop());
            trayMenu.Items.Add("Restart streaming", null, (s, e) => controller.RestartStream());
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Restart AdbAux", null, (s, e) => RestartApplication());
            trayMenu.Items.Add("Exit", null, (s, e) => ExitApplication());
            tray.ContextMenuStrip = trayMenu;
            tray.DoubleClick += (s, e) => ShowFromTray();
            tray.BalloonTipClicked += (s, e) => ShowFromTray();
        }

        private static Color ToColor(StatusLevel level)
        {
            switch (level)
            {
                case StatusLevel.Green: return Color.FromArgb(16, 160, 60);
                case StatusLevel.Blue: return Color.FromArgb(0, 120, 215);
                case StatusLevel.Yellow: return Color.FromArgb(230, 170, 0);
                case StatusLevel.Red: return Color.FromArgb(210, 40, 40);
                default: return Color.FromArgb(128, 128, 128);
            }
        }

        /// <summary>Loads the icon embedded in the exe (all sizes), or the Windows default icon.</summary>
        private static Icon LoadAppIcon()
        {
            using (Stream stream = typeof(MainForm).Assembly.GetManifestResourceStream("AdbAux.app.ico"))
            {
                return stream != null ? new Icon(stream) : (Icon)SystemIcons.Application.Clone();
            }
        }

        /// <summary>The app icon with a coloured status dot in the bottom-right corner (tray).</summary>
        private static Icon MakeTrayIcon(Icon baseIcon, Color color)
        {
            using (var bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                using (var sized = new Icon(baseIcon, 32, 32))
                using (var ring = new SolidBrush(Color.White))
                using (var fill = new SolidBrush(color))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.Clear(Color.Transparent);
                    g.DrawIcon(sized, new Rectangle(0, 0, 32, 32));
                    g.FillEllipse(ring, 15, 15, 17, 17);
                    g.FillEllipse(fill, 17, 17, 13, 13);
                }
                // Full type name: inside a Form, "Icon" alone also means the form's Icon property.
                return System.Drawing.Icon.FromHandle(bmp.GetHicon());
            }
        }

        /// <summary>Adds a line to the Log tab and to the log file (for the user's own troubleshooting).</summary>
        private void Log(string message)
        {
            AppLog.Write(message);
            if (txtLog.TextLength > 200000)
            {
                txtLog.Text = txtLog.Text.Substring(txtLog.TextLength - 100000);   // keep the log small
            }
            txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + message + Environment.NewLine);
        }

        /// <summary>Runs an action on the window's thread (for calls from other threads).</summary>
        private void PostToUi(Action action)
        {
            try
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    BeginInvoke(action);
                }
            }
            catch (InvalidOperationException)
            {
                // The window is closing.
            }
        }

        // =====================================================================
        // Basic tab: selections
        // =====================================================================

        private void UpdateContentLabels()
        {
            rbMusic.Text = "Music (" + settings.MusicBufferMs + " ms): music apps such as Amazon Music";
            rbVideo.Text = "Video (" + settings.VideoBufferMs + " ms): YouTube, Twitch; lowest lip-sync delay";
            rbLowLatency.Text = "Low latency (" + settings.LowLatencyBufferMs + " ms): may click now and then";
        }

        private void SelectCurrentRadios()
        {
            updatingUi = true;
            rbMusic.Checked = controller.Content == ContentType.Music;
            rbVideo.Checked = controller.Content == ContentType.Video;
            rbLowLatency.Checked = controller.Content == ContentType.LowLatency;
            rbMediaOnly.Checked = controller.Mode == CaptureMode.MediaOnly;
            rbEverything.Checked = controller.Mode == CaptureMode.Everything;
            rbDuplicate.Checked = controller.Mode == CaptureMode.Duplicate;
            updatingUi = false;
        }

        private void ContentRadio_CheckedChanged(object sender, EventArgs e)
        {
            var rb = (RadioButton)sender;
            if (updatingUi || !rb.Checked)
            {
                return;   // ignore code changes, and the "unchecked" event of the previous choice
            }
            controller.SetSelection((ContentType)rb.Tag, controller.Mode);
        }

        private void ModeRadio_CheckedChanged(object sender, EventArgs e)
        {
            var rb = (RadioButton)sender;
            if (updatingUi || !rb.Checked)
            {
                return;
            }
            var mode = (CaptureMode)rb.Tag;

            // Hearing safety: the first time "all sounds" is chosen, explain the full-level audio and
            // let the user cancel. The confirmation is remembered in the settings.
            if (mode != CaptureMode.MediaOnly && !settings.LoudModeWarningShown)
            {
                DialogResult answer = MessageBox.Show(this,
                    "In this mode the phone's volume buttons have no effect: audio arrives on the PC at full level, "
                    + "and notifications and ringtones play on the PC as well." + Environment.NewLine + Environment.NewLine
                    + "Turn the PC volume down before continuing, especially with headphones." + Environment.NewLine + Environment.NewLine
                    + "Continue?",
                    "Volume warning", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                if (answer != DialogResult.OK)
                {
                    SelectCurrentRadios();   // go back to the previous choice
                    return;
                }
                settings.LoudModeWarningShown = true;
                SaveSettingsOrWarn();
            }

            controller.SetSelection(controller.Content, mode);
        }

        private void BtnVolumeMixer_Click(object sender, EventArgs e)
        {
            try
            {
                // Windows per-app sound settings, where scrcpy's output device can be chosen.
                Process.Start(new ProcessStartInfo("ms-settings:apps-volume") { UseShellExecute = true });
            }
            catch (Exception)
            {
                Process.Start("control.exe", "mmsys.cpl");   // fallback: classic Sound dialog
            }
        }

        private void ShowPhoneSetupHelp()
        {
            MessageBox.Show(this,
                "One-time setup on the phone (Android 11 or newer; menu names vary by brand):" + Environment.NewLine + Environment.NewLine
                + "1. Settings > About phone > Software information: tap Build number 7 times. This unlocks Developer options." + Environment.NewLine
                + "2. Settings > Developer options: turn on USB debugging." + Environment.NewLine
                + "3. Samsung only: Settings > Security and privacy > Auto Blocker: turn it off while connected (it blocks USB commands)." + Environment.NewLine
                + "4. Connect the USB cable, unlock the phone, tick \"Always allow from this computer\" and tap Allow." + Environment.NewLine + Environment.NewLine
                + "An authorised PC can control the phone over USB, so only allow your own PC.",
                "How to set up the phone", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // =====================================================================
        // Small helpers
        // =====================================================================

        private void SaveSettingsOrWarn()
        {
            try
            {
                settings.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not save the settings: " + ex.Message, "AdbAux",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Keeps the "Start with Windows" entry valid if AdbAux.exe has been moved since it was made.</summary>
        private static void RefreshStartEntry()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    object current = key != null ? key.GetValue(RunValueName) : null;
                    string expected = StartEntryCommand();
                    if (current != null && !string.Equals(current.ToString(), expected, StringComparison.OrdinalIgnoreCase))
                    {
                        key.SetValue(RunValueName, expected);
                    }
                }
            }
            catch (Exception)
            {
                // Not critical: the user can set the option again on the Advanced tab.
            }
        }

        private static string StartEntryCommand()
        {
            return "\"" + Application.ExecutablePath + "\" --minimized";
        }

        private static bool IsStartWithWindows()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
            {
                return key != null && key.GetValue(RunValueName) != null;
            }
        }

        /// <summary>Adds or removes the per-user "Run at sign-in" registry entry (no admin rights needed).</summary>
        private static void SetStartWithWindows(bool enable)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                if (enable)
                {
                    key.SetValue(RunValueName, StartEntryCommand());
                }
                else
                {
                    key.DeleteValue(RunValueName, false);
                }
            }
        }

        private static void OpenInExplorer(string dir)
        {
            try
            {
                Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch (Exception)
            {
                // Explorer could not be started; nothing else to do.
            }
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });   // default browser
            }
            catch (Exception)
            {
                // No browser registered.
            }
        }
    }
}
