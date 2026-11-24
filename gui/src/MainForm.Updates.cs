using System;
using System.Drawing;
using System.Windows.Forms;

namespace AdbAux
{
    // Update checks: the question at first start, the automatic daily check (rules in UpdateSchedule,
    // Updates.cs), the line on the Basic tab, the result on the About tab, and the dialogs shown when a
    // newer AdbAux or scrcpy is found. Nothing is downloaded or installed without the user's click.
    public sealed partial class MainForm
    {
        private readonly Timer updateTimer = new Timer { Interval = 60 * 1000 };   // looks once a minute whether a check is due
        private readonly LinkLabel lblUpdateLine = new LinkLabel();                  // Basic tab: update found, or check failed
        private readonly Label lblUpdateResult = new Label();                        // About tab: last automatic check
        private readonly Button btnAdbAuxUpdate = new ThemedButton();
        private readonly CheckBox chkUpdates = new ThemedCheckBox();                 // Advanced tab, General
        private TabControl mainTabs;
        private TabPage aboutPage;
        private Action updateLineAction;          // what clicking the Basic tab line does
        private bool automaticCheckRunning;
        private bool firstAutomaticCheck = true;  // no automatic check has run since AdbAux started
        private bool consentAsked;
        private bool checkingAdbAux;

        /// <summary>Starts the timer for the automatic check (called once, from OnLoad).</summary>
        private void StartUpdateTimer()
        {
            updateTimer.Tick += (s, e) => RunAutomaticCheckIfDue();
            updateTimer.Start();
            ShowUpdateResult();
        }

        /// <summary>
        /// First start: offers to download scrcpy if it is missing, then asks about automatic update
        /// checks. Nothing is sent to GitHub for the automatic check before the user has answered.
        /// </summary>
        private async void AskStartQuestions()
        {
            if (controller.ScrcpyDir == null)
            {
                await CheckScrcpyUpdatesAsync(
                    "AdbAux uses scrcpy (free, open source) to capture the phone's audio, and it was not found." + Environment.NewLine + Environment.NewLine);
            }
            AskUpdateConsent();
        }

        /// <summary>Asks once whether AdbAux may check for updates automatically. The answer is saved.</summary>
        private void AskUpdateConsent()
        {
            if (consentAsked || settings.CheckForUpdates != UpdateCheckChoice.NotAsked)
            {
                return;
            }
            consentAsked = true;
            DialogResult answer = MessageBox.Show(this,
                "Should AdbAux check for updates automatically?" + Environment.NewLine + Environment.NewLine
                + "Once a day, AdbAux would ask GitHub (api.github.com) for the latest published versions of AdbAux and "
                + "scrcpy. GitHub receives this PC's IP address and the program name and version (AdbAux/" + AppInfo.Version
                + "); nothing else is sent. If a newer version exists, AdbAux shows a notification. Nothing is downloaded "
                + "or installed without your confirmation." + Environment.NewLine + Environment.NewLine
                + "You can change this later on the Advanced tab, under General. The privacy statement is under "
                + "About > Privacy.",
                "Check for updates", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            bool yes = answer == DialogResult.Yes;
            settings.CheckForUpdates = yes ? UpdateCheckChoice.Yes : UpdateCheckChoice.No;
            if (yes)
            {
                settings.Updates.Enable(DateTime.UtcNow);
            }
            SaveSettingsOrWarn();
            Log("Automatic update check " + (yes ? "turned on" : "turned off") + " (answer to the question at start).");

            updatingUi = true;
            chkUpdates.Checked = yes;
            updatingUi = false;
            UpdatePendingState();
            ShowUpdateResult();
        }

        // =====================================================================
        // Automatic check
        // =====================================================================

        /// <summary>
        /// Runs the automatic check if it is turned on, due, and Windows reports an internet connection
        /// (so a check right after Windows starts does not fail only because the network is not ready).
        /// </summary>
        private async void RunAutomaticCheckIfDue()
        {
            if (automaticCheckRunning
                || settings.CheckForUpdates != UpdateCheckChoice.Yes
                || !UpdateSchedule.IsDue(settings.Updates, DateTime.UtcNow, firstAutomaticCheck)
                || !NetworkStatus.HasInternet())
            {
                return;
            }
            automaticCheckRunning = true;
            firstAutomaticCheck = false;
            try
            {
                AppRelease app;
                ScrcpyRelease scrcpy;
                try
                {
                    app = await GitHub.GetLatestAdbAuxAsync();
                    scrcpy = await ScrcpyInstaller.GetLatestAsync();
                }
                catch (Exception ex)
                {
                    var failure = ex as UpdateCheckException;
                    UpdateFailure kind = failure != null ? failure.Kind : UpdateFailure.UnexpectedReply;
                    DateTime retryAfter = failure != null ? failure.RetryAfterUtc : DateTime.MinValue;
                    UpdateNotice notice = UpdateSchedule.RecordFailure(settings.Updates, DateTime.UtcNow, kind, retryAfter);
                    SaveSettingsQuietly();
                    Log("Automatic update check failed: " + ex.Message);
                    if (notice.Line != null)
                    {
                        ShowUpdateLine(notice.Line, ShowAboutTab);
                    }
                    if (notice.ShowNotification)
                    {
                        Notify("AdbAux update check", notice.Line, ToolTipIcon.Warning, ShowAboutTab);
                    }
                    return;
                }

                UpdateSchedule.RecordSuccess(settings.Updates, DateTime.UtcNow);
                SaveSettingsQuietly();
                Version installedScrcpy = await InstalledScrcpyVersionAsync();
                Log("Automatic update check: latest AdbAux " + (app != null ? app.Tag : "(none published)")
                    + ", latest scrcpy " + scrcpy.Tag + ".");
                HideUpdateLine();
                ShowFoundUpdates(app, scrcpy, installedScrcpy);
            }
            finally
            {
                automaticCheckRunning = false;
                ShowUpdateResult();
            }
        }

        /// <summary>Shows the line and a notification for newer versions that were not skipped.</summary>
        private void ShowFoundUpdates(AppRelease app, ScrcpyRelease scrcpy, Version installedScrcpy)
        {
            Version current = ScrcpyInstaller.ParseVersion(AppInfo.Version);
            bool appNewer = app != null && app.Version != null && current != null && app.Version > current
                && !string.Equals(app.Tag, settings.Updates.SkippedAdbAuxVersion, StringComparison.OrdinalIgnoreCase);
            bool scrcpyNewer = scrcpy != null && scrcpy.Version != null && installedScrcpy != null && scrcpy.Version > installedScrcpy
                && !string.Equals(scrcpy.Tag, settings.Updates.SkippedScrcpyVersion, StringComparison.OrdinalIgnoreCase);
            if (!appNewer && !scrcpyNewer)
            {
                return;
            }

            string text;
            if (appNewer && scrcpyNewer)
            {
                text = "Updates available: AdbAux " + VersionText(app.Version, app.Tag) + " and scrcpy "
                    + VersionText(scrcpy.Version, scrcpy.Tag) + ".";
            }
            else if (appNewer)
            {
                text = "AdbAux " + VersionText(app.Version, app.Tag) + " is available.";
            }
            else
            {
                text = "scrcpy " + VersionText(scrcpy.Version, scrcpy.Tag) + " is available.";
            }
            Log(text);
            AppRelease offerApp = appNewer ? app : null;
            ScrcpyRelease offerScrcpy = scrcpyNewer ? scrcpy : null;
            Action offer = () => OfferUpdates(offerApp, offerScrcpy);
            ShowUpdateLine(text + " Click to see the choices.", offer);
            Notify("AdbAux", text + " Click to see the choices.", ToolTipIcon.Info, offer);
        }

        private async void OfferUpdates(AppRelease app, ScrcpyRelease scrcpy)
        {
            HideUpdateLine();   // "Later" means: remind again at the next daily check
            if (app != null)
            {
                OfferAdbAuxUpdate(app);
            }
            if (scrcpy != null)
            {
                await OfferScrcpyUpdateAsync(scrcpy);
            }
        }

        // =====================================================================
        // Offering a newer version
        // =====================================================================

        /// <summary>AdbAux never replaces its own exe: the user downloads the new one from the release page.</summary>
        private void OfferAdbAuxUpdate(AppRelease app)
        {
            string text = "AdbAux " + VersionText(app.Version, app.Tag) + " is available. This copy is version "
                + AppInfo.Version + "." + Environment.NewLine + Environment.NewLine
                + "The release page lists the changes, the AdbAux.exe download and its SHA-256 checksum. AdbAux does "
                + "not replace itself: exit AdbAux, then put the new AdbAux.exe in place of this one:"
                + Environment.NewLine + Application.ExecutablePath;
            UpdateDialogChoice choice = ShowUpdateDialog("AdbAux update", text, "Open release page");
            if (choice == UpdateDialogChoice.Proceed)
            {
                OpenUrl(app.PageUrl);
                Log("Opened the release page of AdbAux " + app.Tag + ".");
            }
            else if (choice == UpdateDialogChoice.Skip)
            {
                settings.Updates.SkippedAdbAuxVersion = app.Tag;
                SaveSettingsQuietly();
                Log("AdbAux " + app.Tag + " skipped; the next newer release is reported again.");
            }
        }

        /// <summary>Offers a newer scrcpy found by the automatic check: Install / Later / Skip this version.</summary>
        private async System.Threading.Tasks.Task OfferScrcpyUpdateAsync(ScrcpyRelease latest)
        {
            if (checkingUpdates || controller.Installing)
            {
                return;
            }
            checkingUpdates = true;
            btnScrcpyUpdate.Enabled = false;
            try
            {
                // scrcpy may have been updated since the check (for example with Check for scrcpy updates).
                Version installed = await InstalledScrcpyVersionAsync();
                if (installed != null && latest.Version != null && installed >= latest.Version)
                {
                    MessageBox.Show(this, "scrcpy is already up to date (version " + installed + ").", "scrcpy update",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                ScrcpyInstallPlan plan = PlanScrcpyInstall();
                string text = "scrcpy " + VersionText(latest.Version, latest.Tag) + " is available (installed: "
                    + (installed != null ? installed.ToString() : "unknown version") + ")." + Environment.NewLine + Environment.NewLine
                    + "Install downloads it (about 12 MB) into:" + InstallDetails(plan);
                UpdateDialogChoice choice = ShowUpdateDialog("scrcpy update", text, "Install");
                if (choice == UpdateDialogChoice.Proceed)
                {
                    await InstallScrcpyReleaseAsync(latest, plan);
                }
                else if (choice == UpdateDialogChoice.Skip)
                {
                    settings.Updates.SkippedScrcpyVersion = latest.Tag;
                    SaveSettingsQuietly();
                    Log("scrcpy " + latest.Tag + " skipped; the next newer release is reported again.");
                }
            }
            finally
            {
                checkingUpdates = false;
                btnScrcpyUpdate.Enabled = true;
            }
        }

        private UpdateDialogChoice ShowUpdateDialog(string title, string text, string proceedText)
        {
            using (var dialog = new UpdateDialog(title, text, proceedText, Theme.IsDark(settings.Theme)))
            {
                dialog.ShowDialog(this);
                return dialog.Choice;
            }
        }

        // =====================================================================
        // Manual check (About tab). Not part of the automatic schedule.
        // =====================================================================

        private async void CheckAdbAuxUpdates()
        {
            if (checkingAdbAux)
            {
                return;
            }
            checkingAdbAux = true;
            btnAdbAuxUpdate.Enabled = false;
            try
            {
                Log("Checking the latest AdbAux release on GitHub...");
                AppRelease app;
                try
                {
                    app = await GitHub.GetLatestAdbAuxAsync();
                }
                catch (Exception ex)
                {
                    Log("AdbAux update check failed: " + ex.Message);
                    MessageBox.Show(this, "Could not check for AdbAux updates: " + ex.Message, "Check for AdbAux updates",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                Version current = ScrcpyInstaller.ParseVersion(AppInfo.Version);
                if (app == null)
                {
                    Log("No published AdbAux release found.");
                    MessageBox.Show(this, "No published AdbAux release was found on GitHub:" + Environment.NewLine
                        + GitHub.AdbAuxReleasesUrl, "Check for AdbAux updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else if (app.Version != null && current != null && app.Version > current)
                {
                    Log("Latest AdbAux release: " + app.Tag + "; this copy: " + AppInfo.Version + ".");
                    OfferAdbAuxUpdate(app);   // shown even if this version was skipped before
                }
                else
                {
                    Log("Latest AdbAux release: " + app.Tag + "; this copy: " + AppInfo.Version + " (up to date).");
                    MessageBox.Show(this, "AdbAux is up to date: this copy is version " + AppInfo.Version + ", and "
                        + app.Tag + " is the latest release.", "Check for AdbAux updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            finally
            {
                checkingAdbAux = false;
                btnAdbAuxUpdate.Enabled = true;
            }
        }

        // =====================================================================
        // Display helpers
        // =====================================================================

        private void ShowUpdateLine(string text, Action onClick)
        {
            lblUpdateLine.Text = text;
            updateLineAction = onClick;
            lblUpdateLine.Visible = true;
        }

        private void HideUpdateLine()
        {
            lblUpdateLine.Visible = false;
            updateLineAction = null;
        }

        private void UpdateLine_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Action action = updateLineAction;
            if (action != null)
            {
                action();
            }
        }

        private void ShowAboutTab()
        {
            if (mainTabs != null && aboutPage != null)
            {
                mainTabs.SelectedTab = aboutPage;
            }
        }

        private void ShowUpdateResult()
        {
            lblUpdateResult.Text = UpdateSchedule.Describe(settings.Updates, settings.CheckForUpdates);
        }

        private void SaveSettingsQuietly()
        {
            try
            {
                settings.Save();
            }
            catch (Exception ex)
            {
                Log("Could not save the settings: " + ex.Message);
            }
        }

        private static string VersionText(Version version, string tag)
        {
            return version != null ? version.ToString() : tag;
        }
    }
}
