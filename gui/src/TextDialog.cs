using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace AdbAux
{
    /// <summary>
    /// Shows a document built into AdbAux (the privacy statement, the licence) in a resizable window,
    /// so it can be read without an internet connection.
    /// </summary>
    internal sealed class TextDialog : Form
    {
        private readonly bool dark;
        private readonly TextBox box = new TextBox();
        private readonly ThemedButton close = new ThemedButton { Text = "Close", Primary = true };

        public TextDialog(string title, string text, bool dark)
        {
            this.dark = dark;

            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = Theme.UiFont();
            Text = title;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            ShowIcon = false;   // like other dialogs of AdbAux (the default form icon would show otherwise)
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(640, 520);
            MinimumSize = new Size(420, 320);

            box.Multiline = true;
            box.ReadOnly = true;
            box.WordWrap = true;
            box.ScrollBars = ScrollBars.Vertical;
            box.Text = text;
            var field = new ThemedField(box, true) { Dock = DockStyle.Fill };

            close.DialogResult = DialogResult.OK;
            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 12, 0, 0)
            };
            bar.Controls.Add(close);

            var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 14, 16, 16) };
            content.Controls.Add(field);   // docked controls are laid out from the last added: the bar first
            content.Controls.Add(bar);
            Controls.Add(content);
            AcceptButton = close;
            CancelButton = close;   // Esc closes
            ResumeLayout(false);
            PerformLayout();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Theme.Apply(this, dark);
            box.Select(0, 0);
            ActiveControl = close;
        }
    }

    /// <summary>Plain text from the project's Markdown documents, for showing them inside AdbAux.</summary>
    public static class PlainText
    {
        /// <summary>
        /// Turns the simple Markdown of the project's documents into plain text for a text box: headings and
        /// emphasis marks are removed, bullets become "•", links become "text (address)", and the lines of a
        /// paragraph or bullet (wrapped in the file) are joined so the text box can wrap them at its width.
        /// </summary>
        public static string FromMarkdown(string markdown)
        {
            var output = new List<string>();
            string current = null;
            Action flush = () =>
            {
                if (current != null)
                {
                    output.Add(current);
                    current = null;
                }
            };

            foreach (string raw in (markdown ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                string line = Clean(raw.Trim());
                if (line.Length == 0)
                {
                    flush();
                    output.Add("");
                }
                else if (raw.StartsWith("#", StringComparison.Ordinal))
                {
                    flush();
                    output.Add(line.TrimStart('#').Trim());
                }
                else if (raw.StartsWith("- ", StringComparison.Ordinal))
                {
                    flush();
                    current = "• " + line.Substring(2);
                }
                else
                {
                    current = current == null ? line : current + " " + line;
                }
            }
            flush();

            // At most one empty line in a row, none at the start or end.
            var sb = new StringBuilder();
            bool lastEmpty = true;
            foreach (string line in output)
            {
                bool empty = line.Length == 0;
                if (empty && lastEmpty)
                {
                    continue;
                }
                sb.Append(line).Append("\r\n");
                lastEmpty = empty;
            }
            return sb.ToString().TrimEnd();
        }

        private static string Clean(string line)
        {
            line = line.Replace("**", "").Replace("`", "");
            return Regex.Replace(line, @"\[([^\]]+)\]\(([^)]+)\)", "$1 ($2)");
        }
    }
}
