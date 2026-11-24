using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AdbAux
{
    /// <summary>
    /// A short description in the secondary text colour, followed by an (i) button that opens a panel
    /// with the details. What the user needs in order to decide stays visible; the details are one click
    /// away. The panel opens on click, Enter or Space (never on hover, which does not work with touch or
    /// the keyboard) and closes with Esc or a click elsewhere. Screen readers get the details as the
    /// button's description.
    /// </summary>
    internal sealed class InfoNote : Control, IThemedControl
    {
        private const TextFormatFlags Flags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;

        private readonly string title;
        private readonly string details;
        private Theme.Palette palette = Theme.Light;
        private Rectangle iconRect;
        private bool hoverIcon;
        private InfoPopup popup;

        /// <param name="text">The short description, shown at all times.</param>
        /// <param name="title">Heading of the details panel (usually the setting's name).</param>
        /// <param name="details">The full explanation shown in the panel.</param>
        public InfoNote(string text, string title, string details)
        {
            this.title = title;
            this.details = details;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Text = text;
            AutoSize = true;
            TabStop = true;
            AccessibleRole = AccessibleRole.PushButton;
            AccessibleName = text + " More information.";
            AccessibleDescription = details;
        }

        public void SetPalette(Theme.Palette p)
        {
            palette = p;
            Invalidate();
        }

        // ---------------- size and layout ----------------

        /// <summary>Lines of text, and where the (i) goes: after the last line, or on a line of its own.</summary>
        private sealed class Arrangement
        {
            public readonly List<string> Lines = new List<string>();
            public Rectangle Icon;
            public Size Size;
        }

        public override Size MaximumSize
        {
            get { return base.MaximumSize; }
            set
            {
                base.MaximumSize = value;
                Relayout();   // the window was resized: wrap at the new width
            }
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            return Arrange().Size;
        }

        private int WrapWidth()
        {
            return MaximumSize.Width > 0 ? MaximumSize.Width : (int)(560 * Theme.Scale(this));
        }

        private int TextWidth(string s)
        {
            return s.Length == 0 ? 0 : TextRenderer.MeasureText(s, Font, Size.Empty, Flags).Width;
        }

        private Arrangement Arrange()
        {
            var a = new Arrangement();
            float scale = Theme.Scale(this);
            int maxWidth = WrapWidth();
            int lineHeight = Font.Height;
            int icon = Math.Max(12, (int)Math.Round(lineHeight * 0.8));
            int gap = (int)(6 * scale);
            int ring = (int)Math.Ceiling(3 * scale);   // room for the focus outline around the (i)

            // Word wrap, measured with the same font and flags as the drawing.
            string current = "";
            foreach (string word in (Text ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = current.Length == 0 ? word : current + " " + word;
                if (current.Length > 0 && TextWidth(candidate) > maxWidth)
                {
                    a.Lines.Add(current);
                    current = word;
                }
                else
                {
                    current = candidate;
                }
            }
            if (current.Length > 0)
            {
                a.Lines.Add(current);
            }

            int widest = 0;
            foreach (string line in a.Lines)
            {
                widest = Math.Max(widest, TextWidth(line));
            }

            // The (i) follows the last line if it fits, otherwise it starts a new line.
            int last = a.Lines.Count > 0 ? TextWidth(a.Lines[a.Lines.Count - 1]) : 0;
            int iconLine;
            int iconX;
            if (a.Lines.Count > 0 && last + gap + icon + ring <= maxWidth)
            {
                iconLine = a.Lines.Count - 1;
                iconX = last + gap;
            }
            else
            {
                iconLine = a.Lines.Count;
                iconX = ring;
            }
            int lines = Math.Max(a.Lines.Count, iconLine + 1);
            a.Icon = new Rectangle(iconX, iconLine * lineHeight + (lineHeight - icon) / 2, icon, icon);
            a.Size = new Size(Math.Max(widest, iconX + icon + ring), lines * lineHeight);
            return a;
        }

        private void Relayout()
        {
            if (Parent != null)
            {
                Parent.PerformLayout(this, "Bounds");
            }
            Invalidate();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Relayout();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            Relayout();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            iconRect = Arrange().Icon;
        }

        // ---------------- drawing ----------------

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            Arrangement a = Arrange();
            iconRect = a.Icon;
            int y = 0;
            foreach (string line in a.Lines)
            {
                TextRenderer.DrawText(g, line, Font, new Point(0, y), palette.SubtleText, Flags);
                y += Font.Height;
            }
            DrawIcon(g, a.Icon);
        }

        /// <summary>A circle with a lower-case i, in the accent colour (the colour of other clickable items).</summary>
        private void DrawIcon(Graphics g, Rectangle r)
        {
            float scale = Theme.Scale(this);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (hoverIcon)
            {
                using (var fill = new SolidBrush(palette.Hover))
                {
                    g.FillEllipse(fill, r);
                }
            }
            float stroke = Math.Max(1F, 1.2F * scale);
            using (var pen = new Pen(palette.Accent, stroke))
            {
                g.DrawEllipse(pen, r.X + stroke / 2, r.Y + stroke / 2, r.Width - stroke, r.Height - stroke);
            }
            float cx = r.X + r.Width / 2F;
            float d = r.Width * 0.15F;
            using (var brush = new SolidBrush(palette.Accent))
            {
                g.FillEllipse(brush, cx - d / 2, r.Y + r.Height * 0.2F, d, d);                          // dot
                g.FillRectangle(brush, cx - d * 0.45F, r.Y + r.Height * 0.42F, d * 0.9F, r.Height * 0.36F); // stem
            }
            if (Focused)
            {
                float ring = Math.Max(1F, 2F * scale);
                using (var focus = new Pen(palette.Accent, ring))
                {
                    g.DrawEllipse(focus, r.X - ring, r.Y - ring, r.Width + 2 * ring, r.Height + 2 * ring);
                }
            }
            g.SmoothingMode = SmoothingMode.Default;
        }

        // ---------------- mouse and keyboard ----------------

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool over = iconRect.Contains(e.Location);
            if (over != hoverIcon)
            {
                hoverIcon = over;
                Cursor = over ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (hoverIcon)
            {
                hoverIcon = false;
                Cursor = Cursors.Default;
                Invalidate();
            }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Left && iconRect.Contains(e.Location))
            {
                Focus();
                OpenDetails();
            }
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Enter || keyData == Keys.Space || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                OpenDetails();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        /// <summary>Opens the details panel under the (i), in the current theme's colours.</summary>
        private void OpenDetails()
        {
            if (popup != null)
            {
                popup.Dispose();
            }
            float scale = Theme.Scale(this);
            popup = new InfoPopup(title, details, palette, scale);
            popup.Show(this, new Point(iconRect.Left, iconRect.Bottom + (int)(4 * scale)));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && popup != null)
            {
                popup.Dispose();
                popup = null;
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// The details panel of an InfoNote. A ToolStripDropDown closes by itself on Esc or a click
    /// elsewhere, and Windows keeps it on the screen.
    /// </summary>
    internal sealed class InfoPopup : ToolStripDropDown
    {
        public InfoPopup(string title, string text, Theme.Palette p, float scale)
        {
            AutoClose = true;
            DropShadowEnabled = true;
            Padding = new Padding(1);   // room for the outline
            BackColor = p.Surface;
            Renderer = new OutlineRenderer(p);

            int width = (int)(400 * scale);
            var body = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = p.Surface,
                Margin = Padding.Empty,
                Padding = new Padding((int)(14 * scale), (int)(10 * scale), (int)(14 * scale), (int)(12 * scale))
            };
            body.Controls.Add(new Label
            {
                Text = title,
                Font = Theme.HeadingFont(),
                AutoSize = true,
                MaximumSize = new Size(width, 0),
                ForeColor = p.Text,
                BackColor = p.Surface,
                UseMnemonic = false,
                Margin = new Padding(0, 0, 0, (int)(6 * scale))
            });
            body.Controls.Add(new Label
            {
                Text = text,
                Font = Theme.UiFont(),
                AutoSize = true,
                MaximumSize = new Size(width, 0),
                ForeColor = p.Text,
                BackColor = p.Surface,
                UseMnemonic = false,
                Margin = Padding.Empty
            });
            Items.Add(new ToolStripControlHost(body)
            {
                AutoSize = true,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = p.Surface
            });
        }

        /// <summary>Plain background and a thin outline, in the palette's colours.</summary>
        private sealed class OutlineRenderer : ToolStripRenderer
        {
            private readonly Theme.Palette p;

            public OutlineRenderer(Theme.Palette p)
            {
                this.p = p;
            }

            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                using (var brush = new SolidBrush(p.Surface))
                {
                    e.Graphics.FillRectangle(brush, e.AffectedBounds);
                }
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                Rectangle r = e.ToolStrip.ClientRectangle;
                using (var pen = new Pen(p.ButtonBorder))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, r.Width - 1, r.Height - 1);
                }
            }
        }
    }
}
