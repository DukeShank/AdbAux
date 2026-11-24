using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AdbAux
{
    // Basic, Log and About tabs, and the layout helpers used by all tabs.
    public sealed partial class MainForm
    {
        private void BuildUi()
        {
            var tabs = new ThemedTabControl { Dock = DockStyle.Fill };
            mainTabs = tabs;
            aboutPage = BuildAboutPage();
            tabs.TabPages.Add(BuildBasicPage());
            tabs.TabPages.Add(BuildAdvancedPage());
            tabs.TabPages.Add(BuildLogPage());
            tabs.TabPages.Add(aboutPage);
            Controls.Add(tabs);
        }

        private TabPage BuildBasicPage()
        {
            var page = new TabPage("Basic");
            TableLayoutPanel column = NewColumn();
            page.Controls.Add(column);

            // Status line: coloured dot + text.
            statusDot.Text = "●";   // filled circle
            statusDot.Tag = "keepcolor";   // its colour shows the status; the theme leaves it alone
            statusDot.Font = new Font(Font.FontFamily, 18F);
            statusDot.AutoSize = true;
            statusText.AutoSize = true;
            statusText.MaximumSize = new Size(520, 0);
            statusText.Padding = new Padding(0, 9, 0, 0);
            var statusRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            statusRow.Controls.Add(statusDot);
            statusRow.Controls.Add(statusText);
            AddRow(column, statusRow);

            // Update found, or automatic update check failed (hidden otherwise; see MainForm.Updates.cs).
            lblUpdateLine.AutoSize = true;
            lblUpdateLine.MaximumSize = new Size(560, 0);
            lblUpdateLine.Visible = false;
            lblUpdateLine.LinkClicked += UpdateLine_LinkClicked;
            AddRow(column, lblUpdateLine);

            // Content type.
            SectionBox contentGroup = new SectionBox("What are you playing?");
            SetupRadio(rbMusic, ContentType.Music, contentGroup.Body, ContentRadio_CheckedChanged);
            SetupRadio(rbVideo, ContentType.Video, contentGroup.Body, ContentRadio_CheckedChanged);
            SetupRadio(rbLowLatency, ContentType.LowLatency, contentGroup.Body, ContentRadio_CheckedChanged);
            AddRow(column, contentGroup);

            // Alert handling (capture mode).
            SectionBox modeGroup = new SectionBox("Notifications, ringtones and alarms");
            rbMediaOnly.Text = "Keep them on the phone (only media goes to the PC)";
            rbEverything.Text = "Send them to the PC too (all sounds at full level; the phone is silent)";
            rbDuplicate.Text = "Play all sounds on both the PC and the phone";
            SetupRadio(rbMediaOnly, CaptureMode.MediaOnly, modeGroup.Body, ModeRadio_CheckedChanged);
            SetupRadio(rbEverything, CaptureMode.Everything, modeGroup.Body, ModeRadio_CheckedChanged);
            SetupRadio(rbDuplicate, CaptureMode.Duplicate, modeGroup.Body, ModeRadio_CheckedChanged);
            modeGroup.Body.Controls.Add(NewNote(
                "Hearing safety: with \"Send them to the PC too\", the phone's volume buttons have no effect and "
                + "audio arrives at full level. Turn the PC volume down before switching."));
            AddRow(column, modeGroup);

            var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            buttons.Controls.Add(NewButton(btnStart, "Start", (s, e) => controller.Start()));
            buttons.Controls.Add(NewButton(btnStop, "Stop", (s, e) => controller.Stop()));
            buttons.Controls.Add(NewButton(btnVolumeMixer, "Open volume mixer", BtnVolumeMixer_Click));
            AddRow(column, buttons);

            var setupLink = new LinkLabel { Text = "How to set up the phone", AutoSize = true };
            setupLink.LinkClicked += (s, e) => ShowPhoneSetupHelp();
            AddRow(column, setupLink);

            AddRow(column, NewInfoNote(
                "Changes here apply at once; a running stream restarts with a gap of about one second.",
                "Basic tab",
                "Changes here apply at once; a running stream restarts, with a gap of about one second. "
                + "In the Windows volume mixer (Open volume mixer) you can choose which output device scrcpy plays on. "
                + "Closing this window keeps AdbAux running in the notification area, unless that option is turned "
                + "off on the Advanced tab."));
            MakeTextFlow(column);
            return page;
        }

        private TabPage BuildLogPage()
        {
            var page = new TabPage("Log") { Padding = new Padding(10, 8, 10, 8) };
            txtLog.Multiline = true;
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.Font = Theme.MonoFont();
            var logField = new ThemedField(txtLog, true) { Dock = DockStyle.Fill };

            var copy = new ThemedButton { Text = "Copy log" };
            copy.Click += (s, e) =>
            {
                if (txtLog.TextLength > 0)
                {
                    Clipboard.SetText(txtLog.Text);
                }
            };
            var clear = new ThemedButton { Text = "Clear view" };
            clear.Click += (s, e) => txtLog.Clear();   // clears the view only; the log file is kept
            var openFolder = new ThemedButton { Text = "Open log folder" };
            openFolder.Click += (s, e) =>
            {
                Directory.CreateDirectory(AppLog.Folder);
                OpenInExplorer(AppLog.Folder);
            };
            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
            bar.Controls.Add(copy);
            bar.Controls.Add(clear);
            bar.Controls.Add(openFolder);

            var notice = new InfoNote(
                "For your own troubleshooting, stored only on this PC. It contains your phone's model and serial "
                + "number: remove them before sharing it.",
                "Log",
                AppLog.Notice + " Saved to: " + AppLog.FilePath + ". The file is limited to about 1 MB; the previous "
                + "file is kept as adbaux.old.log. Clear view clears only this view; the file is kept.")
            {
                Dock = DockStyle.Top,
                MaximumSize = new Size(590, 0)
            };
            var gap = new Panel { Dock = DockStyle.Top, Height = 8 };

            // Docked controls are laid out from the last added to the first: the bar takes the
            // bottom edge, the notice the top edge with a gap under it, and the log fills the rest.
            page.Controls.Add(logField);
            page.Controls.Add(gap);
            page.Controls.Add(notice);
            page.Controls.Add(bar);
            page.SizeChanged += (s, e) =>
                notice.MaximumSize = new Size(Math.Max(200, page.ClientSize.Width - page.Padding.Horizontal), 0);   // re-wrap with the window
            return page;
        }

        private TabPage BuildAboutPage()
        {
            var page = new TabPage("About");
            TableLayoutPanel column = NewColumn();
            page.Controls.Add(column);

            var header = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            var picture = new PictureBox
            {
                Image = new Icon(appIcon, 64, 64).ToBitmap(),
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(64, 64),
                Margin = new Padding(0, 0, 12, 0)
            };
            var title = new Label
            {
                Text = "AdbAux " + AppInfo.Version,
                Font = Theme.TitleFont(),
                AutoSize = true,
                Padding = new Padding(0, 16, 0, 0)
            };
            header.Controls.Add(picture);
            header.Controls.Add(title);
            AddRow(column, header);

            // Updates: manual check, and the result of the last automatic check.
            AddRow(column, NewButton(btnAdbAuxUpdate, "Check for AdbAux updates", (s, e) => CheckAdbAuxUpdates()));
            lblUpdateResult.AutoSize = true;
            lblUpdateResult.MaximumSize = new Size(560, 0);
            lblUpdateResult.Tag = "note";
            AddRow(column, lblUpdateResult);
            var releasesLink = new LinkLabel { Text = GitHub.AdbAuxReleasesUrl, AutoSize = true };
            releasesLink.LinkClicked += (s, e) => OpenUrl(GitHub.AdbAuxReleasesUrl);
            AddRow(column, releasesLink);

            AddRow(column, NewText(
                "Plays an Android phone's audio on this PC over a USB cable, uncompressed "
                + "(16-bit PCM, 48 kHz stereo), using scrcpy in audio-only mode."));
            AddRow(column, NewText("Copyright (c) 2026 DukeShank. Released under the MIT License."));

            AddRow(column, NewText(
                "Disclaimer: provided as is, without warranty; use at your own risk. USB debugging gives this PC "
                + "full control of the phone: only authorise your own PC and turn USB debugging off when not in use. "
                + "Keep the volume at a safe level. AdbAux plays audio in real time and does not record it; use it "
                + "with content you are entitled to play. AdbAux is not affiliated with Google, Genymobile or any "
                + "phone, app or audio-device maker. See README.md for details."));

            AddRow(column, NewText(
                "Uses scrcpy by Genymobile (Apache License 2.0). scrcpy is not included in AdbAux: it is "
                + "downloaded separately from its official GitHub releases. The scrcpy release contains adb and "
                + "other components under their own licences; see the files in the scrcpy folder."));
            var scrcpyLink = new LinkLabel { Text = ScrcpyProjectUrl, AutoSize = true };
            scrcpyLink.LinkClicked += (s, e) => OpenUrl(ScrcpyProjectUrl);
            AddRow(column, scrcpyLink);

            lblAboutScrcpy.AutoSize = true;
            lblAboutScrcpy.MaximumSize = new Size(560, 0);
            AddRow(column, lblAboutScrcpy);

            AddRow(column, NewText("Settings file: " + AppSettings.SettingsPath));
            var openSettings = new ThemedButton { Text = "Open settings folder" };
            openSettings.Click += (s, e) => OpenInExplorer(Path.GetDirectoryName(AppSettings.SettingsPath));
            AddRow(column, openSettings);
            MakeTextFlow(column);
            return page;
        }

        // =====================================================================
        // Layout helpers
        // =====================================================================

        /// <summary>
        /// Lets wrapping text (labels) and wrapping button rows in a tab follow the window width. They
        /// wrap at their MaximumSize width; this updates that width whenever the tab's column is resized.
        /// </summary>
        private static void MakeTextFlow(TableLayoutPanel column)
        {
            var wrapping = new List<Control>();
            CollectWrapping(column, wrapping);
            column.SizeChanged += (s, e) =>
            {
                // Space left after the column padding and a possible vertical scroll bar.
                int available = column.ClientSize.Width - column.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth;
                foreach (Control c in wrapping)
                {
                    // Inside a section, the card's padding and the margins take about 40 px.
                    int indent = c.Parent == column ? 0 : (int)(40 * Theme.Scale(c));
                    c.MaximumSize = new Size(Math.Max(200, available - indent), 0);
                }
            };
        }

        private static void CollectWrapping(Control parent, List<Control> found)
        {
            foreach (Control child in parent.Controls)
            {
                var label = child as Label;
                var flow = child as FlowLayoutPanel;
                var info = child as InfoNote;
                if ((label != null && label.AutoSize && label.MaximumSize.Width > 0)
                    || (flow != null && flow.WrapContents && flow.MaximumSize.Width > 0)
                    || (info != null && info.MaximumSize.Width > 0))
                {
                    found.Add(child);
                }
                CollectWrapping(child, found);
            }
        }

        private static TableLayoutPanel NewColumn()
        {
            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                Padding = new Padding(16, 12, 16, 12),
                AutoScroll = true
            };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            return t;
        }

        private static void AddRow(TableLayoutPanel column, Control control)
        {
            column.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            // Sections, panels and text stretch with the window; buttons keep their natural width.
            control.Anchor = control is ButtonBase
                ? AnchorStyles.Left | AnchorStyles.Top
                : AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            control.Margin = new Padding(0, 0, 0, control is SectionBox ? 20 : 10);
            column.Controls.Add(control, 0, column.RowCount);
            column.RowCount++;
        }

        /// <summary>A field with its label above it (Windows 11 layout: sentence case, no colon).</summary>
        private static Control Labeled(string label, Control field)
        {
            var box = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 0, 0, 8)
            };
            box.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 0, 3, 4) });
            if (!(field is FlowLayoutPanel))
            {
                field.Margin = new Padding(3, 0, 3, 0);
            }
            box.Controls.Add(field);
            return box;
        }

        private static Button NewButton(Button button, string text, EventHandler click)
        {
            button.Text = text;
            button.Click += click;
            return button;
        }

        private static CheckBox NewCheckBox(CheckBox box, string text)
        {
            box.Text = text;
            box.AutoSize = true;
            return box;
        }

        private static void SetupRadio(RadioButton rb, object tag, FlowLayoutPanel body, EventHandler handler)
        {
            rb.Tag = tag;
            rb.AutoSize = true;
            rb.CheckedChanged += handler;
            body.Controls.Add(rb);
        }

        private static Label NewNote(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                MaximumSize = new Size(560, 0),
                Margin = new Padding(3, 4, 3, 6),
                Tag = "note"   // shown in the secondary text colour of the current theme
            };
        }

        /// <summary>
        /// A short description with an (i) button that opens the details (see InfoNote.cs). The short text
        /// holds what is needed to decide, including any privacy or safety fact; the details hold the rest.
        /// </summary>
        private static InfoNote NewInfoNote(string text, string title, string details)
        {
            return new InfoNote(text, title, details)
            {
                MaximumSize = new Size(560, 0),
                Margin = new Padding(3, 4, 3, 6)
            };
        }

        private static Label NewText(string text)
        {
            return new Label { Text = text, AutoSize = true, MaximumSize = new Size(560, 0) };
        }

        /// <summary>A buffer row: label above; slider, number field and the unit.</summary>
        private static Control BufferRowPanel(string name, BufferRow row)
        {
            row.Track.AccessibleName = name + " buffer";
            row.Number.AccessibleName = name + " buffer in milliseconds";
            var line = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            row.Track.Margin = new Padding(0, 0, 8, 0);
            row.NumberField.Margin = new Padding(3, 0, 3, 0);
            row.Unit.Margin = new Padding(6, 0, 3, 0);
            line.Controls.Add(row.Track);
            line.Controls.Add(row.NumberField);
            line.Controls.Add(row.Unit);
            return Labeled(name, line);
        }

        /// <summary>A slider and a number field (milliseconds) with the same range, kept in step.</summary>
        private sealed class BufferRow
        {
            // Anchor Left only: centred vertically in its row.
            public readonly ThemedSlider Track = new ThemedSlider
            {
                Minimum = Limits.MinBufferMs,
                Maximum = Limits.MaxBufferMs,
                SmallChange = 1,
                LargeChange = 10,
                Width = 240,
                Anchor = AnchorStyles.Left
            };

            public readonly NumericUpDown Number = new ThemedNumberBox
            {
                Minimum = Limits.MinBufferMs,
                Maximum = Limits.MaxBufferMs
            };

            public readonly ThemedField NumberField;
            public readonly Label Unit = new Label { Text = "ms", AutoSize = true, Anchor = AnchorStyles.Left };

            public BufferRow()
            {
                NumberField = new ThemedField(Number, 96) { Anchor = AnchorStyles.Left };
                // Each control sets the other only when the value differs, so the two events do not loop.
                Track.ValueChanged += (s, e) =>
                {
                    if ((int)Number.Value != Track.Value)
                    {
                        Number.Value = Track.Value;
                    }
                };
                Number.ValueChanged += (s, e) => Track.Value = (int)Number.Value;
            }

            /// <summary>Raised when the value changes (by the user or by code).</summary>
            public event EventHandler ValueChanged
            {
                add { Number.ValueChanged += value; }
                remove { Number.ValueChanged -= value; }
            }

            public int Value
            {
                get { return (int)Number.Value; }
                set { Number.Value = Limits.Clamp(value, Limits.MinBufferMs, Limits.MaxBufferMs); }
            }
        }
    }
}
