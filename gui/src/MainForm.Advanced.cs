using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AdbAux
{
    // Advanced tab. Every setting on it takes effect only when the user clicks Apply; Discard changes
    // goes back to the values in use. The exceptions are actions with their own confirmation:
    // managing tested profiles, restoring the profiles of a backup, and installing scrcpy.
    public sealed partial class MainForm
    {
        private readonly BufferRow rowMusic = new BufferRow();
        private readonly BufferRow rowVideo = new BufferRow();
        private readonly BufferRow rowLowLatency = new BufferRow();
        private readonly ComboBox cbDefaultContent = new ThemedComboBox();
        private readonly ComboBox cbDefaultMode = new ThemedComboBox();
        private readonly NumericUpDown numOutputBuffer = new ThemedNumberBox();
        private readonly TextBox txtExtra = new TextBox();
        private readonly CheckBox chkNotifyConnection = new ThemedCheckBox();
        private readonly CheckBox chkNotifyStreaming = new ThemedCheckBox();
        private readonly CheckBox chkNotifyErrors = new ThemedCheckBox();
        private readonly ComboBox cbAdb = new ThemedComboBox();
        private readonly TextBox txtAdbPath = new TextBox();
        private readonly Button btnBrowseAdb = new ThemedButton();
        private readonly Label lblAdb = new Label();
        private readonly CheckBox chkAutoStart = new ThemedCheckBox();
        private readonly CheckBox chkPauseMedia = new ThemedCheckBox();
        private readonly CheckBox chkResumeMedia = new ThemedCheckBox();
        private readonly CheckBox chkTray = new ThemedCheckBox();
        private readonly CheckBox chkStartWithWindows = new ThemedCheckBox();
        private readonly CheckBox chkMixerIcon = new ThemedCheckBox();
        private readonly ComboBox cbTheme = new ThemedComboBox();
        private readonly Label lblScrcpy = new Label();
        private readonly Label lblScrcpyPending = new Label();
        private readonly Button btnScrcpyUpdate = new ThemedButton();
        private readonly Button btnBackup = new ThemedButton();
        private readonly Button btnRestore = new ThemedButton();
        private readonly Button btnChooseFolder = new ThemedButton();
        private readonly Button btnAutoFolder = new ThemedButton();
        private readonly Button btnOpenFolder = new ThemedButton();
        private readonly ThemedButton btnApply = new ThemedButton { Primary = true };
        private readonly Button btnDiscard = new ThemedButton();
        private readonly Button btnDefaults = new ThemedButton();
        private readonly Label lblPending = new Label();

        private string pendingScrcpyFolder;       // chosen with Choose folder… / Use AdbAux folder, not applied yet (null = no change)
        private bool startWithWindowsApplied;     // the registry entry as last read or written
        private bool checkingUpdates;             // a scrcpy update check or install is running

        private TabPage BuildAdvancedPage()
        {
            var page = new TabPage("Advanced");
            TableLayoutPanel column = NewColumn();
            page.Controls.Add(column);

            // Apply bar at the top, so it is visible without scrolling.
            var applyRow = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(560, 0) };
            applyRow.Controls.Add(NewButton(btnApply, "Apply", BtnApply_Click));
            applyRow.Controls.Add(NewButton(btnDiscard, "Discard changes", BtnDiscard_Click));
            applyRow.Controls.Add(NewButton(btnDefaults, "Reset to defaults", BtnDefaults_Click));
            AddRow(column, applyRow);
            lblPending.AutoSize = true;
            lblPending.MaximumSize = new Size(560, 0);
            lblPending.Tag = "note";
            AddRow(column, lblPending);

            // Buffer per content type (these are the values the Basic page uses).
            SectionBox bufferGroup = new SectionBox("Buffer for each content type");
            bufferGroup.Body.Controls.Add(BufferRowPanel("Music", rowMusic));
            bufferGroup.Body.Controls.Add(BufferRowPanel("Video", rowVideo));
            bufferGroup.Body.Controls.Add(BufferRowPanel("Low latency", rowLowLatency));
            bufferGroup.Body.Controls.Add(NewNote(
                "The Basic tab uses these buffers, in milliseconds (" + Limits.MinBufferMs + " to " + Limits.MaxBufferMs
                + "). A smaller buffer gives less delay but a higher risk of clicks. Tested: Amazon Music HD plays "
                + "without clicks from 40 ms (36 ms with all sounds), YouTube and Twitch from 20 ms; clicks start at about 15 ms."));
            AddRow(column, bufferGroup);

            // What the Basic page selects at start-up.
            SectionBox defaultsGroup = new SectionBox("When AdbAux starts");
            cbDefaultContent.Width = 260;
            cbDefaultContent.Items.AddRange(new object[] { "Music", "Video", "Low latency" });
            cbDefaultMode.Width = 260;
            cbDefaultMode.Items.AddRange(new object[]
            {
                "Media only (alerts stay on the phone)",
                "All sounds to the PC",
                "All sounds on the PC and the phone"
            });
            defaultsGroup.Body.Controls.Add(Labeled("Content type", cbDefaultContent));
            defaultsGroup.Body.Controls.Add(Labeled("Notifications, ringtones and alarms", cbDefaultMode));
            AddRow(column, defaultsGroup);

            AddRow(column, BuildProfileSection());

            // Extra scrcpy options.
            SectionBox optionsGroup = new SectionBox("scrcpy options");
            numOutputBuffer.Minimum = Limits.MinOutputBufferMs;
            numOutputBuffer.Maximum = Limits.MaxOutputBufferMs;
            optionsGroup.Body.Controls.Add(Labeled("Output buffer (ms)", new ThemedField(numOutputBuffer, 96)));
            optionsGroup.Body.Controls.Add(Labeled("Extra options", new ThemedField(txtExtra, 360)));
            optionsGroup.Body.Controls.Add(NewNote(
                "Output buffer: the scrcpy default is 10 ms, and scrcpy advises changing it only with good reason. "
                + "Extra options are added to the scrcpy command, for example audio-output-buffer=5 (the leading -- "
                + "is optional). Put a value with spaces in double quotes. Profiles do not store extra options."));
            AddRow(column, optionsGroup);

            // Notifications.
            SectionBox notifyGroup = new SectionBox("Notifications");
            notifyGroup.Body.Controls.Add(NewCheckBox(chkNotifyConnection, "Phone connected, disconnected or not authorised"));
            notifyGroup.Body.Controls.Add(NewCheckBox(chkNotifyStreaming, "Streaming started"));
            notifyGroup.Body.Controls.Add(NewCheckBox(chkNotifyErrors, "Streaming stopped unexpectedly (phone still connected)"));
            AddRow(column, notifyGroup);

            // Which adb to use.
            SectionBox adbGroup = new SectionBox("adb (Android Debug Bridge)");
            cbAdb.Width = 380;
            cbAdb.Items.AddRange(new object[]
            {
                "The adb included with scrcpy (default)",
                "Android SDK platform-tools (used by Android Studio)",
                "A specific adb.exe"
            });
            cbAdb.SelectedIndexChanged += (s, e) =>
            {
                bool custom = cbAdb.SelectedIndex == (int)AdbChoice.Custom;
                txtAdbPath.Enabled = custom;
                btnBrowseAdb.Enabled = custom;
            };
            var adbPathRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            adbPathRow.Controls.Add(new ThemedField(txtAdbPath, 380));
            adbPathRow.Controls.Add(NewButton(btnBrowseAdb, "Browse…", BtnBrowseAdb_Click));
            lblAdb.AutoSize = true;
            lblAdb.MaximumSize = new Size(540, 0);
            adbGroup.Body.Controls.Add(Labeled("adb to use", cbAdb));
            adbGroup.Body.Controls.Add(Labeled("Location of adb.exe", adbPathRow));
            adbGroup.Body.Controls.Add(lblAdb);
            adbGroup.Body.Controls.Add(NewNote(
                "If you also use Android Studio or another adb tool, choose the same adb here. Two different adb "
                + "versions keep restarting each other's background server, which interrupts both tools."));
            AddRow(column, adbGroup);

            // Program behaviour.
            SectionBox programGroup = new SectionBox("General");
            programGroup.Body.Controls.Add(NewCheckBox(chkAutoStart, "Start streaming automatically when the phone is connected"));
            programGroup.Body.Controls.Add(NewCheckBox(chkPauseMedia, "Pause the phone's media when I stop streaming"));
            programGroup.Body.Controls.Add(NewCheckBox(chkResumeMedia, "Resume the phone's media when streaming starts"));
            programGroup.Body.Controls.Add(NewNote(
                "Both use the phone's media keys over USB. Pausing applies to Stop and Exit in AdbAux (not to unplugging "
                + "the cable or Restart AdbAux), and only when an app is playing. Resuming applies when streaming starts "
                + "with Start or automatically when the phone is connected, not when a change of settings restarts it; "
                + "if nothing is playing, it presses Play, which resumes the app that played last, as a headset's play "
                + "button does."));
            programGroup.Body.Controls.Add(NewCheckBox(chkTray, "Keep AdbAux running in the notification area when the window is closed"));
            programGroup.Body.Controls.Add(NewCheckBox(chkStartWithWindows, "Start with Windows, hidden in the notification area"));
            programGroup.Body.Controls.Add(NewCheckBox(chkMixerIcon, "Show the AdbAux icon in the Windows volume mixer"));
            programGroup.Body.Controls.Add(NewNote(
                "The volume mixer shows the icon of the program playing the audio, scrcpy. With this option, "
                + "AdbAux runs a copy of scrcpy.exe with the AdbAux icon (" + MixerIcon.CopyName + " in the scrcpy "
                + "folder; scrcpy.exe is not changed). Not available for a scrcpy folder you chose yourself."));
            cbTheme.Width = 200;
            cbTheme.Items.AddRange(new object[] { "Use system setting", "Light", "Dark" });   // order = ThemeChoice
            Control themeField = Labeled("App theme", cbTheme);
            themeField.Margin = new Padding(0, 6, 0, 2);
            programGroup.Body.Controls.Add(themeField);
            AddRow(column, programGroup);

            // Back up and restore.
            SectionBox backupGroup = new SectionBox("Back up and restore");
            var backupButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(560, 0) };
            backupButtons.Controls.Add(NewButton(btnBackup, "Back up settings…", BtnBackup_Click));
            backupButtons.Controls.Add(NewButton(btnRestore, "Restore settings…", BtnRestore_Click));
            backupGroup.Body.Controls.Add(backupButtons);
            backupGroup.Body.Controls.Add(NewNote(
                "Back up settings… saves the settings in use and all tested profiles to one file, for a new PC or "
                + "before experimenting (changes not applied yet are not included). Restore settings… fills in this "
                + "tab from a backup, for you to check and apply, and adds its profiles at once, replacing profiles "
                + "with the same name. The window position and Start with Windows are not restored. To share values "
                + "with others, export a profile instead."));
            AddRow(column, backupGroup);

            // scrcpy location and update.
            SectionBox scrcpyGroup = new SectionBox("scrcpy");
            lblScrcpy.AutoSize = true;
            lblScrcpy.MaximumSize = new Size(540, 0);
            lblScrcpyPending.AutoSize = true;
            lblScrcpyPending.MaximumSize = new Size(540, 0);
            lblScrcpyPending.Tag = "note";
            var scrcpyButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(560, 0) };
            scrcpyButtons.Controls.Add(NewButton(btnScrcpyUpdate, "Check for scrcpy updates", (s, e) => CheckScrcpyUpdates("")));
            scrcpyButtons.Controls.Add(NewButton(btnChooseFolder, "Choose folder…", BtnChooseFolder_Click));
            scrcpyButtons.Controls.Add(NewButton(btnAutoFolder, "Use AdbAux folder", (s, e) => SetPendingScrcpyFolder("")));
            scrcpyButtons.Controls.Add(NewButton(btnOpenFolder, "Open folder", (s, e) => OpenInExplorer(controller.ScrcpyDir ?? ScrcpyLocator.AppDir)));
            scrcpyGroup.Body.Controls.Add(lblScrcpy);
            scrcpyGroup.Body.Controls.Add(lblScrcpyPending);
            scrcpyGroup.Body.Controls.Add(scrcpyButtons);
            scrcpyGroup.Body.Controls.Add(NewNote(
                "AdbAux installs scrcpy in a scrcpy folder next to AdbAux.exe, or in your user profile "
                + "(%LOCALAPPDATA%\\AdbAux\\scrcpy) when that folder cannot be written to, for example under Program Files. "
                + "Check for scrcpy updates compares the installed version with the latest official release and asks "
                + "before installing; adb is included in scrcpy and updated with it. A different folder is used after Apply."));
            AddRow(column, scrcpyGroup);

            TrackChanges(page);
            MakeTextFlow(column);
            return page;
        }

        // =====================================================================
        // Settings <-> controls
        // =====================================================================

        /// <summary>Shows the settings in use on the Advanced tab (discarding changes not applied).</summary>
        private void LoadSettingsIntoControls()
        {
            startWithWindowsApplied = IsStartWithWindows();
            FillControlsFrom(settings);
        }

        /// <summary>
        /// Shows the given settings on the Advanced tab: the settings in use, or settings from a backup
        /// (which then wait for Apply). Start with Windows always shows the registry entry in use.
        /// </summary>
        private void FillControlsFrom(AppSettings s)
        {
            updatingUi = true;
            FillValueControls(s.MusicBufferMs, s.VideoBufferMs, s.LowLatencyBufferMs,
                s.OutputBufferMs, s.DefaultContent, s.DefaultMode);
            txtExtra.Text = s.ExtraArgs ?? "";
            chkNotifyConnection.Checked = s.NotifyConnection;
            chkNotifyStreaming.Checked = s.NotifyStreaming;
            chkNotifyErrors.Checked = s.NotifyErrors;
            cbAdb.SelectedIndex = (int)s.Adb;
            txtAdbPath.Text = s.AdbCustomPath ?? "";
            chkAutoStart.Checked = s.AutoStart;
            chkPauseMedia.Checked = s.PauseMediaOnStop;
            chkResumeMedia.Checked = s.ResumeMediaOnStart;
            chkTray.Checked = s.MinimizeToTray;
            chkStartWithWindows.Checked = startWithWindowsApplied;
            chkMixerIcon.Checked = s.ShowIconInVolumeMixer;
            cbTheme.SelectedIndex = (int)s.Theme;
            pendingScrcpyFolder = string.Equals(s.ScrcpyFolder ?? "", settings.ScrcpyFolder ?? "", StringComparison.OrdinalIgnoreCase)
                ? null : (s.ScrcpyFolder ?? "");
            updatingUi = false;
            ShowScrcpyPending();
            ShowProfilesKeepingSelection();
            UpdatePendingState();
        }

        private void FillValueControls(int music, int video, int lowLatency, int output, ContentType content, CaptureMode mode)
        {
            rowMusic.Value = music;
            rowVideo.Value = video;
            rowLowLatency.Value = lowLatency;
            numOutputBuffer.Value = Limits.Clamp(output, Limits.MinOutputBufferMs, Limits.MaxOutputBufferMs);
            cbDefaultContent.SelectedIndex = (int)content;
            cbDefaultMode.SelectedIndex = (int)mode;
        }

        /// <summary>The settings as they would be after Apply.</summary>
        private AppSettings SettingsFromControls()
        {
            AppSettings c = settings.Clone();
            c.MusicBufferMs = rowMusic.Value;
            c.VideoBufferMs = rowVideo.Value;
            c.LowLatencyBufferMs = rowLowLatency.Value;
            c.DefaultContent = (ContentType)Math.Max(0, cbDefaultContent.SelectedIndex);
            c.DefaultMode = (CaptureMode)Math.Max(0, cbDefaultMode.SelectedIndex);
            c.OutputBufferMs = (int)numOutputBuffer.Value;
            c.ExtraArgs = txtExtra.Text.Trim();
            c.NotifyConnection = chkNotifyConnection.Checked;
            c.NotifyStreaming = chkNotifyStreaming.Checked;
            c.NotifyErrors = chkNotifyErrors.Checked;
            c.Adb = (AdbChoice)Math.Max(0, cbAdb.SelectedIndex);
            c.AdbCustomPath = txtAdbPath.Text.Trim();
            c.AutoStart = chkAutoStart.Checked;
            c.PauseMediaOnStop = chkPauseMedia.Checked;
            c.ResumeMediaOnStart = chkResumeMedia.Checked;
            c.MinimizeToTray = chkTray.Checked;
            c.ShowIconInVolumeMixer = chkMixerIcon.Checked;
            c.Theme = (ThemeChoice)Math.Max(0, cbTheme.SelectedIndex);
            if (pendingScrcpyFolder != null)
            {
                c.ScrcpyFolder = pendingScrcpyFolder;
            }
            return c;
        }

        private bool HasPendingChanges()
        {
            return !SettingsFromControls().SameAdvancedSettings(settings) || chkStartWithWindows.Checked != startWithWindowsApplied;
        }

        /// <summary>Enables Apply / Discard changes and says whether applying restarts the stream.</summary>
        private void UpdatePendingState()
        {
            if (updatingUi)
            {
                return;
            }
            bool pending = HasPendingChanges();
            btnApply.Enabled = pending;
            btnDiscard.Enabled = pending;
            if (!pending)
            {
                lblPending.Text = "Changes on this tab take effect when you click Apply.";
            }
            else if (controller.WouldRestart(SettingsFromControls()))
            {
                lblPending.Text = "Changes not applied yet. Applying restarts the stream, with a gap of about one second.";
            }
            else
            {
                lblPending.Text = "Changes not applied yet.";
            }
        }

        /// <summary>Calls UpdatePendingState whenever the user changes a setting on the tab.</summary>
        private void TrackChanges(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                if (c == cbProfile)
                {
                    continue;   // choosing a profile is not a setting; the values it fills in are
                }
                var check = c as CheckBox;
                var combo = c as ComboBox;
                var number = c as NumericUpDown;
                var text = c as TextBox;
                if (check != null)
                {
                    check.CheckedChanged += (s, e) => UpdatePendingState();
                }
                else if (combo != null)
                {
                    combo.SelectedIndexChanged += (s, e) => UpdatePendingState();
                }
                else if (number != null)
                {
                    number.ValueChanged += (s, e) => UpdatePendingState();
                }
                else if (text != null)
                {
                    text.TextChanged += (s, e) => UpdatePendingState();
                }
                TrackChanges(c);
            }
        }

        // =====================================================================
        // Apply, discard, reset
        // =====================================================================

        private void BtnApply_Click(object sender, EventArgs e)
        {
            AppSettings applied = SettingsFromControls();
            bool themeChanged = applied.Theme != settings.Theme;
            bool folderChanged = pendingScrcpyFolder != null;

            settings.CopyAdvancedSettings(applied);
            pendingScrcpyFolder = null;
            SaveSettingsOrWarn();
            if (chkStartWithWindows.Checked != startWithWindowsApplied)
            {
                try
                {
                    SetStartWithWindows(chkStartWithWindows.Checked);
                    startWithWindowsApplied = chkStartWithWindows.Checked;
                }
                catch (Exception ex)
                {
                    Log("Could not change the Start with Windows entry: " + ex.Message);
                }
            }
            Log("Settings applied.");

            UpdateContentLabels();
            if (themeChanged)
            {
                ApplyTheme();
            }
            controller.Reconfigure();   // finds scrcpy and adb again, and restarts the stream if needed
            if (folderChanged)
            {
                ShowScrcpyInfo();
            }
            ShowAdbInfo();
            ShowScrcpyPending();
            ShowProfilesKeepingSelection();
            UpdatePendingState();
        }

        private void BtnDiscard_Click(object sender, EventArgs e)
        {
            LoadSettingsIntoControls();
            Log("Changes on the Advanced tab discarded.");
        }

        private void BtnDefaults_Click(object sender, EventArgs e)
        {
            // Fills in the AdbAux defaults for the audio values; Apply uses them.
            FillValueControls(AppSettings.DefaultMusicBufferMs, AppSettings.DefaultVideoBufferMs,
                AppSettings.DefaultLowLatencyBufferMs, AppSettings.DefaultOutputBufferMs,
                ContentType.Music, CaptureMode.MediaOnly);
            txtExtra.Text = "";
        }

        // =====================================================================
        // adb and scrcpy
        // =====================================================================

        /// <summary>Finds scrcpy and adb, and shows them.</summary>
        private void LocateScrcpy()
        {
            controller.Locate();
            ShowScrcpyInfo();
            ShowAdbInfo();
        }

        private void ShowAdbInfo()
        {
            lblAdb.Text = controller.AdbPath != null ? "In use: " + controller.AdbPath : "In use: none (scrcpy not found)";
        }

        /// <summary>Shows the scrcpy folder and version in use (the version is read in the background).</summary>
        private async void ShowScrcpyInfo()
        {
            string dir = controller.ScrcpyDir;
            if (dir == null)
            {
                lblScrcpy.Text = string.IsNullOrWhiteSpace(settings.ScrcpyFolder)
                    ? "scrcpy not found. Click \"Check for scrcpy updates\" to install it."
                    : "scrcpy not found in the folder you chose: " + settings.ScrcpyFolder;
                lblAboutScrcpy.Text = "scrcpy: not found";
                return;
            }

            // Third line: what the folder is, so a folder in the user profile is not mistaken for a found copy.
            string source = ScrcpyLocator.IsManagedFolder(dir)
                ? "Folder managed by AdbAux. Check for scrcpy updates installs here."
                : "Folder you chose. AdbAux does not change it.";
            lblScrcpy.Text = dir + Environment.NewLine + source;
            lblAboutScrcpy.Text = "scrcpy: " + dir;
            try
            {
                string version = await Task.Run(() => ScrcpyLocator.GetVersion(dir));
                if (version != null && dir == controller.ScrcpyDir)
                {
                    // "scrcpy 5.0.1" instead of the full first line, which also contains the project's web address.
                    Version number = ScrcpyInstaller.ParseVersion(version);
                    string shown = number != null ? "scrcpy " + number : version;
                    lblScrcpy.Text = shown + Environment.NewLine + dir + Environment.NewLine + source;
                    lblAboutScrcpy.Text = shown + Environment.NewLine + dir;
                    Log(version);
                }
            }
            catch (Exception ex)
            {
                Log("Could not read the scrcpy version: " + ex.Message);
            }
        }

        private void SetPendingScrcpyFolder(string dir)
        {
            pendingScrcpyFolder = string.Equals(dir, settings.ScrcpyFolder ?? "", StringComparison.OrdinalIgnoreCase) ? null : dir;
            ShowScrcpyPending();
            UpdatePendingState();
        }

        private void ShowScrcpyPending()
        {
            lblScrcpyPending.Visible = pendingScrcpyFolder != null;
            lblScrcpyPending.Text = pendingScrcpyFolder == null ? ""
                : "After Apply: " + (pendingScrcpyFolder.Length == 0 ? "folder managed by AdbAux" : pendingScrcpyFolder);
        }

        /// <summary>
        /// Compares the installed scrcpy with the latest official release, and installs the release
        /// after the user confirms. intro is shown at the start of the question (used at first start).
        /// </summary>
        private async void CheckScrcpyUpdates(string intro)
        {
            if (checkingUpdates || controller.Installing)
            {
                return;
            }
            checkingUpdates = true;
            btnScrcpyUpdate.Enabled = false;
            try
            {
                Log("Checking the latest scrcpy release on GitHub...");
                ScrcpyRelease latest;
                try
                {
                    latest = await ScrcpyInstaller.GetLatestAsync();
                }
                catch (Exception ex)
                {
                    Log("Update check failed: " + ex.Message);
                    MessageBox.Show(this, "Could not check for scrcpy updates: " + ex.Message, "Check for scrcpy updates",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string dir = controller.ScrcpyDir;
                Version installed = null;
                if (dir != null)
                {
                    installed = ScrcpyInstaller.ParseVersion(await Task.Run(() => ScrcpyLocator.GetVersion(dir)));
                }
                string latestText = latest.Version != null ? latest.Version.ToString() : latest.Tag;
                Log("Latest scrcpy release: " + latest.Tag + "; installed: "
                    + (dir == null ? "none" : installed != null ? installed.ToString() : "unknown version") + ".");

                // Downloads only ever go into a folder AdbAux manages (next to AdbAux.exe, or
                // %LOCALAPPDATA%\AdbAux\scrcpy), because the target folder is replaced completely.
                // A folder the user chose with "Choose folder…" is never changed.
                string target = ScrcpyLocator.ManagedInstallTarget(dir);
                bool usingChosenFolder = !string.IsNullOrWhiteSpace(settings.ScrcpyFolder);
                string adb = controller.AdbPath;
                bool stopAdbServer = adb != null
                    && string.Equals(Path.GetDirectoryName(Path.GetFullPath(adb)), target, StringComparison.OrdinalIgnoreCase);

                bool upToDate = installed != null && latest.Version != null && installed >= latest.Version;
                string question;
                if (dir == null)
                {
                    question = intro + "Download scrcpy " + latestText + ", the latest official release (about 12 MB), into:";
                }
                else if (upToDate)
                {
                    question = "scrcpy is up to date: version " + installed + " is installed, and " + latestText
                        + " is the latest release." + Environment.NewLine + Environment.NewLine
                        + "Download and install it again anyway, for example to repair a damaged copy? It goes into:";
                }
                else
                {
                    question = "scrcpy " + latestText + " is available (installed: "
                        + (installed != null ? installed.ToString() : "unknown version") + ")." + Environment.NewLine + Environment.NewLine
                        + "Download and install it (about 12 MB) into:";
                }
                question += Environment.NewLine + target + Environment.NewLine + Environment.NewLine
                    + "The download is checked against the developer's SHA-256 list. adb is included in scrcpy and "
                    + "is updated with it. A running stream stops and starts again afterwards.";
                if (stopAdbServer)
                {
                    question += " The adb server restarts, which also disconnects other adb devices (for example a TV over Wi-Fi).";
                }
                if (usingChosenFolder)
                {
                    question += Environment.NewLine + Environment.NewLine
                        + "Your chosen scrcpy folder is not changed:" + Environment.NewLine + settings.ScrcpyFolder;
                }

                DialogResult answer = MessageBox.Show(this, question, "Check for scrcpy updates",
                    upToDate ? MessageBoxButtons.YesNo : MessageBoxButtons.OKCancel, MessageBoxIcon.Question,
                    upToDate ? MessageBoxDefaultButton.Button2 : MessageBoxDefaultButton.Button1);
                if (answer != DialogResult.OK && answer != DialogResult.Yes)
                {
                    return;
                }

                bool installed2 = false;
                try
                {
                    await controller.InstallScrcpyAsync(latest, target, stopAdbServer);
                    installed2 = true;
                }
                catch (Exception ex)
                {
                    Log("Download failed: " + ex.Message);
                    MessageBox.Show(this, ex.Message, "Download failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                if (installed2 && usingChosenFolder)
                {
                    DialogResult switchAnswer = MessageBox.Show(this,
                        "scrcpy was downloaded to:" + Environment.NewLine + target + Environment.NewLine + Environment.NewLine
                        + "AdbAux is set to use your chosen folder instead:" + Environment.NewLine + settings.ScrcpyFolder
                        + Environment.NewLine + Environment.NewLine + "Use the downloaded copy from now on?",
                        "Check for scrcpy updates", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (switchAnswer == DialogResult.Yes)
                    {
                        settings.ScrcpyFolder = "";
                        SaveSettingsOrWarn();
                    }
                }

                LocateScrcpy();
                ShowScrcpyPending();
                UpdatePendingState();
            }
            finally
            {
                checkingUpdates = false;
                btnScrcpyUpdate.Enabled = true;
            }
        }

        // =====================================================================
        // Back up and restore
        // =====================================================================

        private void BtnBackup_Click(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "Back up settings";
                dialog.Filter = "AdbAux settings backup (*.xml)|*.xml";
                dialog.FileName = "AdbAux settings " + DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + ".xml";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                try
                {
                    SettingsBackup.Write(settings, dialog.FileName, AppInfo.Version);
                    Log("Settings and " + CountOf(settings.Profiles.Count, "tested profile") + " backed up to " + dialog.FileName + ".");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Could not back up the settings: " + ex.Message, "Back up settings",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>"1 tested profile", "2 tested profiles".</summary>
        private static string CountOf(int count, string singular)
        {
            return count + " " + singular + (count == 1 ? "" : "s");
        }

        private void BtnRestore_Click(object sender, EventArgs e)
        {
            string path;
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Restore settings";
                dialog.Filter = "AdbAux settings backup (*.xml)|*.xml|All files (*.*)|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                path = dialog.FileName;
            }

            AppSettings restored;
            try
            {
                restored = SettingsBackup.Read(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not read the backup: " + ex.Message, "Restore settings",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Paths that do not exist on this PC keep their current value.
            var notRestored = new List<string>();
            if (!string.IsNullOrWhiteSpace(restored.ScrcpyFolder) && !Directory.Exists(restored.ScrcpyFolder))
            {
                notRestored.Add("scrcpy folder: " + restored.ScrcpyFolder);
                restored.ScrcpyFolder = settings.ScrcpyFolder;
            }
            if (restored.Adb == AdbChoice.Custom && !File.Exists(restored.AdbCustomPath ?? ""))
            {
                notRestored.Add("adb.exe: " + restored.AdbCustomPath);
                restored.Adb = settings.Adb;
                restored.AdbCustomPath = settings.AdbCustomPath;
            }

            int replacing = 0;
            foreach (TestedProfile p in restored.Profiles)
            {
                if (settings.FindProfile(p.Name) != null)
                {
                    replacing++;
                }
            }
            string extra = (restored.ExtraArgs ?? "").Trim();
            bool newExtraOptions = extra.Length > 0 && extra != (settings.ExtraArgs ?? "").Trim();

            string nl = Environment.NewLine;
            string message = "Restore settings from:" + nl + path + nl + nl
                + "The Advanced tab is filled in with the backup's settings. They take effect when you click Apply.";
            if (restored.Profiles.Count > 0)
            {
                message += nl + nl + CountOf(restored.Profiles.Count, "tested profile") + (restored.Profiles.Count == 1 ? " is" : " are") + " added now"
                    + (replacing > 0 ? "; " + replacing + " of them replace profiles with the same name." : ".");
            }
            if (notRestored.Count > 0)
            {
                message += nl + nl + "Not found on this PC, so not restored:" + nl + string.Join(nl, notRestored);
            }
            if (newExtraOptions)
            {
                message += nl + nl + "The backup contains extra scrcpy options, which are passed to scrcpy unchanged:" + nl
                    + extra + nl + "Continue only if you made this backup yourself or trust where it came from.";
            }
            message += nl + nl + "Continue?";

            DialogResult answer = MessageBox.Show(this, message, "Restore settings", MessageBoxButtons.YesNo,
                newExtraOptions ? MessageBoxIcon.Warning : MessageBoxIcon.Question,
                newExtraOptions ? MessageBoxDefaultButton.Button2 : MessageBoxDefaultButton.Button1);
            if (answer != DialogResult.Yes)
            {
                return;
            }

            int profileCount = restored.Profiles.Count;
            if (profileCount > 0)
            {
                SettingsBackup.MergeProfiles(settings.Profiles, restored.Profiles);
                SaveSettingsOrWarn();
            }
            FillControlsFrom(restored);
            Log("Settings restored from " + path + " and filled in (click Apply to use them); "
                + CountOf(profileCount, "tested profile") + " added or replaced.");
        }

        private void BtnBrowseAdb_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Choose adb.exe";
                dialog.Filter = "adb.exe|adb.exe|Programs (*.exe)|*.exe";
                string sdkAdb = AdbLocator.FindAndroidSdkAdb();
                if (sdkAdb != null)
                {
                    dialog.InitialDirectory = Path.GetDirectoryName(sdkAdb);
                }
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    txtAdbPath.Text = dialog.FileName;   // used after Apply
                }
            }
        }

        private void BtnChooseFolder_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose the folder that contains scrcpy.exe and adb.exe";
                dialog.ShowNewFolderButton = false;
                if (controller.ScrcpyDir != null)
                {
                    dialog.SelectedPath = controller.ScrcpyDir;
                }
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                string dir = dialog.SelectedPath;
                if (!File.Exists(Path.Combine(dir, "scrcpy.exe")) || !File.Exists(Path.Combine(dir, "adb.exe")))
                {
                    MessageBox.Show(this, "That folder does not contain both scrcpy.exe and adb.exe.", "AdbAux",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                SetPendingScrcpyFolder(dir);
            }
        }
    }
}
