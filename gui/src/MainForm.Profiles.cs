using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace AdbAux
{
    // Tested profiles on the Advanced tab. Choosing a profile fills in its values (applied with Apply).
    // Save as new…, Update…, Delete, Import… and Export… change the stored profiles at once; each
    // asks first (a dialog or a confirmation).
    public sealed partial class MainForm
    {
        private readonly ThemedComboBox cbProfile = new ThemedComboBox();
        private readonly Label lblProfile = new Label();
        private readonly Button btnProfileNew = new ThemedButton();
        private readonly Button btnProfileUpdate = new ThemedButton();
        private readonly Button btnProfileDelete = new ThemedButton();
        private readonly Button btnProfileImport = new ThemedButton();
        private readonly Button btnProfileExport = new ThemedButton();

        private SectionBox BuildProfileSection()
        {
            var group = new SectionBox("Tested profiles");
            group.Body.Controls.Add(NewNote(
                "Keep the values that work for a phone and audio device combination, and switch between them. "
                + "Choosing a profile fills in its values on this tab; click Apply to use them."));
            cbProfile.Width = 360;
            cbProfile.SelectedIndexChanged += CbProfile_SelectedIndexChanged;
            group.Body.Controls.Add(Labeled("Profile", cbProfile));
            lblProfile.AutoSize = true;
            lblProfile.MaximumSize = new Size(540, 0);
            lblProfile.Margin = new Padding(3, 0, 3, 8);
            group.Body.Controls.Add(lblProfile);
            var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(560, 0) };
            buttons.Controls.Add(NewButton(btnProfileNew, "Save as new…", BtnProfileNew_Click));
            buttons.Controls.Add(NewButton(btnProfileUpdate, "Update…", BtnProfileUpdate_Click));
            buttons.Controls.Add(NewButton(btnProfileDelete, "Delete", BtnProfileDelete_Click));
            buttons.Controls.Add(NewButton(btnProfileImport, "Import…", BtnProfileImport_Click));
            buttons.Controls.Add(NewButton(btnProfileExport, "Export…", BtnProfileExport_Click));
            group.Body.Controls.Add(buttons);
            return group;
        }

        /// <summary>The profile chosen in the list, or null. The list has the same order as settings.Profiles.</summary>
        private TestedProfile SelectedProfile
        {
            get
            {
                int i = cbProfile.SelectedIndex;
                return i >= 0 && i < settings.Profiles.Count ? settings.Profiles[i] : null;
            }
        }

        /// <summary>Fills the profile list and chooses the named profile (null = none).</summary>
        private void ShowProfiles(string selectName)
        {
            FillProfileList(selectName != null ? settings.Profiles.FindIndex(p => p.Name == selectName) : -1);
        }

        /// <summary>
        /// Fills the profile list, keeping the chosen profile if the settings in use still match it;
        /// otherwise chooses the first profile that matches them, or none.
        /// </summary>
        private void ShowProfilesKeepingSelection()
        {
            TestedProfile chosen = SelectedProfile;
            int index = chosen != null && chosen.MatchesSettings(settings)
                ? settings.Profiles.IndexOf(chosen)
                : settings.Profiles.FindIndex(p => p.MatchesSettings(settings));
            FillProfileList(index);
        }

        private void FillProfileList(int selectedIndex)
        {
            bool wasUpdating = updatingUi;
            updatingUi = true;
            cbProfile.Items.Clear();
            foreach (TestedProfile p in settings.Profiles)
            {
                cbProfile.Items.Add(p.Name);
            }
            cbProfile.SelectedIndex = selectedIndex;
            cbProfile.PlaceholderText = settings.Profiles.Count == 0 ? "No profiles saved yet" : "Choose a profile";
            cbProfile.Enabled = settings.Profiles.Count > 0;
            cbProfile.FitToItems();
            updatingUi = wasUpdating;
            UpdateProfileDetails();
        }

        /// <summary>Shows the chosen profile's values under the list and enables the matching buttons.</summary>
        private void UpdateProfileDetails()
        {
            TestedProfile p = SelectedProfile;
            btnProfileUpdate.Enabled = p != null;
            btnProfileDelete.Enabled = p != null;
            btnProfileExport.Enabled = p != null;
            if (p == null)
            {
                lblProfile.Text = settings.Profiles.Count == 0
                    ? "None yet. After testing values on your phone and PC, click Save as new… to keep them."
                    : "The values in use do not match a saved profile.";
                return;
            }

            var lines = new List<string>
            {
                "Buffers: music " + p.MusicBufferMs + " ms, video " + p.VideoBufferMs + " ms, low latency "
                    + p.LowLatencyBufferMs + " ms; output buffer " + p.OutputBufferMs + " ms.",
                "When AdbAux starts: " + Names.Content(p.DefaultContent) + ", " + Names.Mode(p.DefaultMode) + "."
            };
            if (p.PhoneModel.Length > 0)
            {
                lines.Add("Tested on " + p.PhoneDisplay
                    + (p.UseWhenPhoneConnected ? "; selected automatically when it is connected." : "."));
            }
            if (p.Comment.Length > 0)
            {
                lines.Add("Comment: " + p.Comment);
            }
            if (p.Saved.Length > 0)
            {
                lines.Add("Saved on " + p.Saved + ".");
            }
            lblProfile.Text = string.Join(Environment.NewLine, lines);
        }

        private void CbProfile_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (updatingUi)
            {
                return;
            }
            UpdateProfileDetails();
            TestedProfile p = SelectedProfile;
            if (p != null)
            {
                FillFromProfile(p);   // a change to apply, like any other change on the tab
            }
        }

        private void FillFromProfile(TestedProfile p)
        {
            FillValueControls(p.MusicBufferMs, p.VideoBufferMs, p.LowLatencyBufferMs, p.OutputBufferMs,
                p.DefaultContent, p.DefaultMode);
        }

        /// <summary>A new profile holding the values currently shown on the Advanced tab.</summary>
        private TestedProfile ProfileFromControls()
        {
            AppSettings shown = SettingsFromControls();
            return new TestedProfile
            {
                MusicBufferMs = shown.MusicBufferMs,
                VideoBufferMs = shown.VideoBufferMs,
                LowLatencyBufferMs = shown.LowLatencyBufferMs,
                OutputBufferMs = shown.OutputBufferMs,
                DefaultContent = shown.DefaultContent,
                DefaultMode = shown.DefaultMode,
                Saved = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            };
        }

        /// <summary>The model of the connected phone, or null.</summary>
        private string ConnectedPhoneModel
        {
            get
            {
                PhoneInfo phone = controller.Phone;
                return controller.PhoneReady && phone != null && !string.IsNullOrEmpty(phone.Model) ? phone.Model : null;
            }
        }

        private void BtnProfileNew_Click(object sender, EventArgs e)
        {
            string model = ConnectedPhoneModel;
            string display = model != null ? model.Replace('_', '-') : null;
            using (var dialog = new ProfileDialog("Save as new profile", display ?? "", "", display, false,
                Theme.IsDark(settings.Theme), name => settings.FindProfile(name) != null))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                TestedProfile p = ProfileFromControls();
                p.Name = dialog.ProfileName;
                p.Comment = dialog.Comment;
                p.PhoneModel = model ?? "";
                p.UseWhenPhoneConnected = dialog.UseWhenConnected;
                p.Sanitize();
                settings.Profiles.Add(p);
                UnlinkOtherProfiles(p);
                SaveSettingsOrWarn();
                ShowProfiles(p.Name);
                Log("Profile \"" + p.Name + "\" saved.");
            }
        }

        private void BtnProfileUpdate_Click(object sender, EventArgs e)
        {
            TestedProfile old = SelectedProfile;
            if (old == null)
            {
                return;
            }
            // Linked to the phone connected now; if none is connected, the phone recorded before.
            string model = ConnectedPhoneModel ?? (old.PhoneModel.Length > 0 ? old.PhoneModel : null);
            string display = model != null ? model.Replace('_', '-') : null;
            using (var dialog = new ProfileDialog("Update profile", old.Name, old.Comment, display, old.UseWhenPhoneConnected,
                Theme.IsDark(settings.Theme), name =>
                {
                    TestedProfile other = settings.FindProfile(name);
                    return other != null && other != old;
                }))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                TestedProfile p = ProfileFromControls();
                p.Name = dialog.ProfileName;
                p.Comment = dialog.Comment;
                p.PhoneModel = model ?? "";
                p.UseWhenPhoneConnected = dialog.UseWhenConnected;
                p.Sanitize();
                settings.Profiles[settings.Profiles.IndexOf(old)] = p;
                UnlinkOtherProfiles(p);
                SaveSettingsOrWarn();
                ShowProfiles(p.Name);
                Log("Profile \"" + p.Name + "\" updated with the values shown on the Advanced tab.");
            }
        }

        private void BtnProfileDelete_Click(object sender, EventArgs e)
        {
            TestedProfile p = SelectedProfile;
            if (p == null)
            {
                return;
            }
            DialogResult answer = MessageBox.Show(this,
                "Delete the profile \"" + p.Name + "\"?" + Environment.NewLine + Environment.NewLine
                + "The values in use do not change. A deleted profile cannot be restored, unless you exported it.",
                "Delete profile", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes)
            {
                return;
            }
            settings.Profiles.Remove(p);
            SaveSettingsOrWarn();
            ShowProfiles(null);
            Log("Profile \"" + p.Name + "\" deleted.");
        }

        private void BtnProfileExport_Click(object sender, EventArgs e)
        {
            TestedProfile p = SelectedProfile;
            if (p == null)
            {
                return;
            }
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "Export profile";
                dialog.Filter = "AdbAux profile (*.xml)|*.xml";
                dialog.FileName = SafeFileName("AdbAux profile - " + p.Name) + ".xml";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                try
                {
                    ProfileFiles.Export(p, dialog.FileName, AppInfo.Version);
                    Log("Profile \"" + p.Name + "\" exported to " + dialog.FileName + ".");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Could not export the profile: " + ex.Message, "Export profile",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnProfileImport_Click(object sender, EventArgs e)
        {
            string path;
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Import profile";
                dialog.Filter = "AdbAux profile (*.xml)|*.xml|All files (*.*)|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                path = dialog.FileName;
            }

            TestedProfile p;
            try
            {
                p = ProfileFiles.Import(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not import the profile: " + ex.Message, "Import profile",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            p.Name = UniqueProfileName(p.Name);
            settings.Profiles.Add(p);
            SaveSettingsOrWarn();
            ShowProfiles(p.Name);
            FillFromProfile(p);
            Log("Profile \"" + p.Name + "\" imported from " + path + " and filled in.");
            MessageBox.Show(this,
                "Imported the profile \"" + p.Name + "\". Its values are filled in on the Advanced tab; click Apply to use them.",
                "Import profile", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Called when a phone is connected: applies the profile linked to its model, if there is one and
        /// its values are not already in use. Only the values change; the current Basic selections stay,
        /// so a profile can never switch to the full-volume "all sounds" mode without the user.
        /// </summary>
        private void ApplyLinkedProfile(PhoneInfo found)
        {
            TestedProfile p = settings.Profiles.Find(x => x.UseWhenPhoneConnected
                && string.Equals(x.PhoneModel, found.Model, StringComparison.OrdinalIgnoreCase));
            if (p == null)
            {
                return;
            }
            if (!p.MatchesSettings(settings))
            {
                bool hadChanges = HasPendingChanges();
                p.ApplyTo(settings);
                SaveSettingsOrWarn();
                LoadSettingsIntoControls();
                UpdateContentLabels();
                Log("Profile \"" + p.Name + "\" applied for " + found.DisplayModel + ".");
                if (hadChanges)
                {
                    Log("Changes on the Advanced tab that were not applied were discarded.");
                }
            }
            ShowProfiles(p.Name);
        }

        /// <summary>Only one profile per phone model is selected automatically: the given one.</summary>
        private void UnlinkOtherProfiles(TestedProfile linked)
        {
            if (!linked.UseWhenPhoneConnected)
            {
                return;
            }
            foreach (TestedProfile other in settings.Profiles)
            {
                if (other != linked && other.UseWhenPhoneConnected
                    && string.Equals(other.PhoneModel, linked.PhoneModel, StringComparison.OrdinalIgnoreCase))
                {
                    other.UseWhenPhoneConnected = false;
                    Log("Profile \"" + other.Name + "\" is no longer selected automatically; \"" + linked.Name
                        + "\" is now used for " + linked.PhoneDisplay + ".");
                }
            }
        }

        private string UniqueProfileName(string name)
        {
            if (settings.FindProfile(name) == null)
            {
                return name;
            }
            string stem = TestedProfile.CleanText(name, TestedProfile.MaxNameLength - 5);
            for (int n = 2; ; n++)
            {
                string candidate = stem + " (" + n + ")";
                if (settings.FindProfile(candidate) == null)
                {
                    return candidate;
                }
            }
        }

        private static string SafeFileName(string name)
        {
            foreach (char bad in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(bad, '_');
            }
            return name;
        }
    }
}
