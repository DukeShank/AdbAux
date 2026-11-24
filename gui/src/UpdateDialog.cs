using System;
using System.Drawing;
using System.Windows.Forms;

namespace AdbAux
{
    /// <summary>The user's choice in the update dialog.</summary>
    internal enum UpdateDialogChoice
    {
        Later,      // also when the dialog is closed with Esc or the window's close button
        Proceed,    // Open release page (AdbAux) or Install (scrcpy)
        Skip        // Skip this version
    }

    /// <summary>
    /// Shown when a newer AdbAux or scrcpy was found: a message and three choices, for example
    /// "Open release page / Later / Skip this version". Nothing happens without a click.
    /// </summary>
    internal sealed class UpdateDialog : Form
    {
        private readonly bool dark;
        private readonly Label message;

        public UpdateDialog(string title, string text, string proceedText, bool dark)
        {
            this.dark = dark;

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
                Padding = new Padding(16, 14, 16, 16)
            };

            message = new Label { Text = text, AutoSize = true, MaximumSize = new Size(460, 0), Margin = new Padding(3, 0, 3, 0) };
            column.Controls.Add(message);

            var proceed = new ThemedButton { Text = proceedText, Primary = true, AutoSize = true };
            proceed.Click += (s, e) => Choose(UpdateDialogChoice.Proceed);
            var later = new ThemedButton { Text = "Later", AutoSize = true };
            later.Click += (s, e) => Choose(UpdateDialogChoice.Later);
            var skip = new ThemedButton { Text = "Skip this version", AutoSize = true };
            skip.Click += (s, e) => Choose(UpdateDialogChoice.Skip);

            var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 16, 0, 0) };
            buttons.Controls.Add(proceed);
            buttons.Controls.Add(later);
            buttons.Controls.Add(skip);
            column.Controls.Add(buttons);
            AcceptButton = proceed;
            CancelButton = later;   // Esc means Later

            Controls.Add(column);
            ResumeLayout(false);
            PerformLayout();
        }

        /// <summary>The button the user clicked. Later if the dialog was closed another way.</summary>
        public UpdateDialogChoice Choice { get; private set; }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Theme.Apply(this, dark);
        }

        private void Choose(UpdateDialogChoice choice)
        {
            Choice = choice;
            DialogResult = DialogResult.OK;
        }
    }
}
