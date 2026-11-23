using System;
using System.Drawing;
using System.Windows.Forms;

namespace AdbAux
{
    /// <summary>
    /// Asks for a tested profile's name, comment and whether it is selected automatically when its
    /// phone is connected. Used by "Save as new…" and "Update…" on the Advanced tab.
    /// </summary>
    internal sealed class ProfileDialog : Form
    {
        private readonly TextBox txtName = new TextBox { MaxLength = TestedProfile.MaxNameLength };
        private readonly TextBox txtComment = new TextBox { MaxLength = TestedProfile.MaxCommentLength };
        private readonly CheckBox chkAuto = new ThemedCheckBox();
        private readonly bool dark;
        private readonly Func<string, bool> nameTaken;
        private readonly ThemedField nameField;
        private readonly System.Collections.Generic.List<Label> notes = new System.Collections.Generic.List<Label>();

        /// <param name="title">Window title.</param>
        /// <param name="phoneDisplay">Model of the phone the profile is linked to, or null if none is known.</param>
        /// <param name="nameTaken">Returns true if another profile already uses the name.</param>
        public ProfileDialog(string title, string name, string comment, string phoneDisplay, bool useWhenConnected,
            bool dark, Func<string, bool> nameTaken)
        {
            this.dark = dark;
            this.nameTaken = nameTaken;

            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = Theme.UiFont();
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            var column = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(16, 12, 16, 16)
            };

            txtName.Text = name ?? "";
            txtComment.Text = comment ?? "";
            column.Controls.Add(FieldLabel("Name"));
            nameField = new ThemedField(txtName, 400) { Margin = new Padding(3, 0, 3, 10) };
            column.Controls.Add(nameField);
            column.Controls.Add(FieldLabel("Comment (optional)"));
            column.Controls.Add(new ThemedField(txtComment, 400) { Margin = new Padding(3, 0, 3, 2) });
            column.Controls.Add(Note("For example, the audio device, apps and settings you tested with."));

            chkAuto.AutoSize = true;
            chkAuto.Margin = new Padding(3, 10, 3, 0);
            if (phoneDisplay != null)
            {
                chkAuto.Text = "Select automatically when " + phoneDisplay + " is connected";
                chkAuto.Checked = useWhenConnected;
                column.Controls.Add(chkAuto);
            }
            else
            {
                chkAuto.Text = "Select automatically when this phone is connected";
                chkAuto.Enabled = false;
                column.Controls.Add(chkAuto);
                column.Controls.Add(Note("Connect the phone to link the profile to it."));
            }

            column.Controls.Add(Note(
                "The profile stores the buffers, the output buffer and the start-up selections shown on the "
                + "Advanced tab. Extra scrcpy options are not stored."));

            var save = new ThemedButton { Text = "Save", Primary = true };
            save.Click += Save_Click;
            var cancel = new ThemedButton { Text = "Cancel", DialogResult = DialogResult.Cancel };
            var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 14, 0, 0) };
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);
            column.Controls.Add(buttons);
            AcceptButton = save;
            CancelButton = cancel;

            Controls.Add(column);
            ResumeLayout(false);
            PerformLayout();
        }

        public string ProfileName { get { return TestedProfile.CleanText(txtName.Text, TestedProfile.MaxNameLength); } }
        public string Comment { get { return TestedProfile.CleanText(txtComment.Text, TestedProfile.MaxCommentLength); } }
        public bool UseWhenConnected { get { return chkAuto.Enabled && chkAuto.Checked; } }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            foreach (Label note in notes)
            {
                note.MaximumSize = new Size(nameField.Width, 0);   // notes wrap at the width of the fields
            }
            Theme.Apply(this, dark);
            txtName.Select();
            txtName.SelectAll();
        }

        private void Save_Click(object sender, EventArgs e)
        {
            if (ProfileName.Length == 0)
            {
                MessageBox.Show(this, "Enter a name for the profile.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtName.Select();
                return;
            }
            if (nameTaken(ProfileName))
            {
                MessageBox.Show(this, "A profile named \"" + ProfileName + "\" already exists. Enter another name.", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtName.Select();
                txtName.SelectAll();
                return;
            }
            DialogResult = DialogResult.OK;
        }

        private static Label FieldLabel(string text)
        {
            return new Label { Text = text, AutoSize = true, Margin = new Padding(3, 0, 3, 4) };
        }

        private Label Note(string text)
        {
            var note = new Label { Text = text, AutoSize = true, MaximumSize = new Size(400, 0), Margin = new Padding(3, 4, 3, 0), Tag = "note" };
            notes.Add(note);
            return note;
        }
    }
}
