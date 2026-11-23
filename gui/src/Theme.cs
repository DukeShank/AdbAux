using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AdbAux
{
    /// <summary>Appearance setting on the Advanced tab.</summary>
    public enum ThemeChoice
    {
        System,   // follow Windows' "Choose your app mode" setting
        Light,
        Dark
    }

    /// <summary>A control that draws itself with the colours of a palette (Theme.Light until the theme is applied).</summary>
    internal interface IThemedControl
    {
        void SetPalette(Theme.Palette palette);
    }

    /// <summary>
    /// Fonts, colours and the Windows 11 style controls. WinForms on .NET Framework draws classic
    /// Windows controls that cannot be recoloured, so in both light and dark mode the controls in
    /// this file draw themselves: section cards with headings, tabs, buttons, checkboxes, radio
    /// buttons, drop-down lists, text and number fields and sliders. Message boxes and file dialogs
    /// keep Windows' own style.
    ///
    /// Colours meet the WCAG 2.2 contrast minimums (checked by calculation): text 4.5:1 or more,
    /// control outlines, the ticked state and focus outlines 3:1 or more against what surrounds them.
    /// Sizes follow the Windows 11 type ramp: body 14 px, section headings 16 px Semibold.
    /// </summary>
    internal static class Theme
    {
        internal sealed class Palette
        {
            public bool IsDark;
            public Color Back;          // window background
            public Color Card;          // background of a section card
            public Color CardBorder;    // card outline (decorative)
            public Color Surface;       // buttons, drop-down lists, fields, unticked boxes
            public Color Hover;         // control under the mouse
            public Color Pressed;       // control being clicked
            public Color FieldFocused;  // text field being typed in
            public Color Text;          // primary text
            public Color SubtleText;    // notes
            public Color DisabledText;
            public Color Border;        // dividers, outlines of disabled controls (decorative)
            public Color ButtonBorder;  // outline of buttons, fields and drop-down lists
            public Color ControlStroke; // checkbox and radio outline, slider track
            public Color Accent;        // ticked box, selected radio, focus outline, primary button
            public Color AccentHover;
            public Color AccentPressed;
            public Color OnAccent;      // check mark, dot and text on the accent colour
            public Color Link;
        }

        // Contrast (calculated): text 16.8:1 on cards, notes 6.5:1, outlines of buttons and fields 3.3:1,
        // checkbox outline 4.9:1, accent 6.1:1, text on accent 6.3:1.
        public static readonly Palette Light = new Palette
        {
            IsDark = false,
            Back = Rgb(0xF3F3F3),
            Card = Rgb(0xFBFBFB),
            CardBorder = Rgb(0xE5E5E5),
            Surface = Rgb(0xFFFFFF),
            Hover = Rgb(0xF0F0F0),
            Pressed = Rgb(0xE6E6E6),
            FieldFocused = Rgb(0xFFFFFF),
            Text = Rgb(0x1A1A1A),
            SubtleText = Rgb(0x5C5C5C),
            DisabledText = Rgb(0x8F8F8F),
            Border = Rgb(0xD6D6D6),
            ButtonBorder = Rgb(0x8A8A8A),
            ControlStroke = Rgb(0x6E6E6E),
            Accent = Rgb(0x005FB8),
            AccentHover = Rgb(0x1A6EBF),
            AccentPressed = Rgb(0x0A4F94),
            OnAccent = Rgb(0xFFFFFF),
            Link = Rgb(0x005FB8)
        };

        // Contrast (calculated): text 14.2:1 on cards, notes 8.5:1, outlines of buttons and fields 3.3:1,
        // checkbox outline 5.4:1, accent 7.9:1, text on accent 11.7:1.
        public static readonly Palette Dark = new Palette
        {
            IsDark = true,
            Back = Rgb(0x202020),
            Card = Rgb(0x2B2B2B),
            CardBorder = Rgb(0x3A3A3A),
            Surface = Rgb(0x383838),
            Hover = Rgb(0x404040),
            Pressed = Rgb(0x303030),
            FieldFocused = Rgb(0x1F1F1F),
            Text = Rgb(0xFFFFFF),
            SubtleText = Rgb(0xC8C8C8),
            DisabledText = Rgb(0x8A8A8A),
            Border = Rgb(0x4A4A4A),
            ButtonBorder = Rgb(0x7A7A7A),
            ControlStroke = Rgb(0xA0A0A0),
            Accent = Rgb(0x60CDFF),
            AccentHover = Rgb(0x5AB9E6),
            AccentPressed = Rgb(0x4FA3CC),
            OnAccent = Rgb(0x000000),
            Link = Rgb(0x60CDFF)
        };

        private static Color Rgb(int rgb)
        {
            return Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }

        // ---------------- fonts ----------------

        // Each font is created once and shared by all controls (fonts use Windows resources).
        private static Font uiFont, headingFont, titleFont, monoFont;

        /// <summary>
        /// Body text: 14 px (10.5 pt at 100 % scaling), the Windows 11 body size. Segoe UI Variable Text
        /// (Windows 11) or Segoe UI (Windows 10).
        /// </summary>
        public static Font UiFont()
        {
            return uiFont ?? (uiFont = CreateFont(new[] { "Segoe UI Variable Text", "Segoe UI" }, 10.5F, FontStyle.Regular));
        }

        /// <summary>Section headings: 16 px (12 pt) Semibold, one step above the body text.</summary>
        public static Font HeadingFont()
        {
            return headingFont ?? (headingFont = SemiboldFont(new[] { "Segoe UI Variable Text Semibold", "Segoe UI Semibold" }, 12F));
        }

        /// <summary>Title on the About tab: 20 px (15 pt) Semibold, the Windows 11 subtitle size.</summary>
        public static Font TitleFont()
        {
            return titleFont ?? (titleFont = SemiboldFont(new[] { "Segoe UI Variable Display Semib", "Segoe UI Semibold" }, 15F));
        }

        /// <summary>Log text: a monospaced font so columns line up.</summary>
        public static Font MonoFont()
        {
            return monoFont ?? (monoFont = CreateFont(new[] { "Cascadia Mono", "Consolas" }, 10F, FontStyle.Regular));
        }

        private static Font SemiboldFont(string[] families, float size)
        {
            Font f = CreateFont(families, size, FontStyle.Regular);
            if (f.Name.IndexOf("Semib", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return f;
            }
            var bold = new Font(f, FontStyle.Bold);   // no Semibold font installed
            f.Dispose();
            return bold;
        }

        /// <summary>
        /// Returns the first installed font family from the list. Windows silently substitutes another
        /// font for a missing name, so the resulting font's name is compared with the request.
        /// </summary>
        private static Font CreateFont(string[] families, float size, FontStyle style)
        {
            foreach (string family in families)
            {
                var font = new Font(family, size, style);
                if (string.Equals(font.Name, family, StringComparison.OrdinalIgnoreCase))
                {
                    return font;
                }
                font.Dispose();
            }
            return new Font(FontFamily.GenericSansSerif, size, style);
        }

        // ---------------- mode ----------------

        /// <summary>
        /// Windows' app mode (Settings > Personalisation > Colours > "Choose your app mode").
        /// Registry value AppsUseLightTheme: 0 = dark, 1 or missing = light.
        /// </summary>
        public static bool SystemUsesDark()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false))
                {
                    object value = key != null ? key.GetValue("AppsUseLightTheme") : null;
                    return value is int && (int)value == 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool IsDark(ThemeChoice choice)
        {
            return choice == ThemeChoice.Dark || (choice == ThemeChoice.System && SystemUsesDark());
        }

        /// <summary>Applies the light or dark appearance to a window and everything in it.</summary>
        public static void Apply(Form form, bool dark)
        {
            Palette p = dark ? Dark : Light;
            SetDarkTitleBar(form, dark);
            form.SuspendLayout();
            ApplyTo(form, p, p.Back);
            form.ResumeLayout(true);
            form.Invalidate(true);
        }

        /// <summary>Colours for a tray (right-click) menu.</summary>
        public static void ApplyToMenu(ContextMenuStrip menu, bool dark)
        {
            if (dark)
            {
                menu.RenderMode = ToolStripRenderMode.Professional;
                menu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors());
            }
            else
            {
                menu.RenderMode = ToolStripRenderMode.System;
            }
            menu.BackColor = dark ? Dark.Surface : SystemColors.Menu;
            foreach (ToolStripItem item in menu.Items)
            {
                item.ForeColor = dark ? Dark.Text : SystemColors.MenuText;
            }
        }

        /// <summary>
        /// Colours a control and its children. back is the colour behind the control: the window
        /// background, or the card colour inside a section card.
        /// </summary>
        private static void ApplyTo(Control c, Palette p, Color back)
        {
            var card = c as CardPanel;
            if (card != null)
            {
                card.BackColor = p.Card;
                card.ForeColor = p.Text;
                card.SetPalette(p);
                foreach (Control child in c.Controls)
                {
                    ApplyTo(child, p, p.Card);
                }
                return;
            }

            var field = c as ThemedField;
            if (field != null)
            {
                field.BackColor = back;
                field.SetPalette(p);   // colours the text or number box inside it
                SetScrollBarTheme(field.Inner, p.IsDark);
                return;
            }

            var combo = c as ThemedComboBox;
            if (combo != null)
            {
                combo.BackColor = p.Surface;   // also the background of the opened list
                combo.ForeColor = p.Text;
                combo.SetPalette(p);
                SetScrollBarTheme(combo, p.IsDark);
                return;
            }

            string tag = c.Tag as string;
            var themed = c as IThemedControl;
            var link = c as LinkLabel;
            var label = c as Label;

            if (themed != null)
            {
                c.BackColor = back;
                c.ForeColor = p.Text;
                var button = c as ButtonBase;
                if (button != null)
                {
                    button.UseVisualStyleBackColor = false;
                }
                themed.SetPalette(p);
            }
            else if (link != null)
            {
                link.LinkColor = p.Link;
                link.ActiveLinkColor = p.Link;
                link.VisitedLinkColor = p.Link;
                link.BackColor = back;
            }
            else if (label != null)
            {
                label.BackColor = back;
                if (tag != "keepcolor")   // the status dot sets its own colour
                {
                    label.ForeColor = tag == "note" ? p.SubtleText : p.Text;
                }
            }
            else
            {
                // Form, TabPage, panels, PictureBox and anything else.
                c.BackColor = back;
                c.ForeColor = p.Text;
                var page = c as TabPage;
                if (page != null)
                {
                    page.UseVisualStyleBackColor = false;
                }
                var panel = c as ScrollableControl;
                if (panel != null && panel.AutoScroll)
                {
                    SetScrollBarTheme(c, p.IsDark);
                }
            }

            foreach (Control child in c.Controls)
            {
                ApplyTo(child, p, back);
            }
        }

        // ---------------- drawing helpers ----------------

        public static float Scale(Control c)
        {
            return c.DeviceDpi / 96F;
        }

        /// <summary>
        /// Height shared by buttons, fields and drop-down lists: one line of text plus 14 px
        /// (32 px at 100 % scaling with the 14 px body font), so controls in a row line up.
        /// </summary>
        public static int ControlHeight(Control c)
        {
            return TextRenderer.MeasureText("Ag", c.Font).Height + (int)Math.Round(14 * Scale(c));
        }

        public static GraphicsPath RoundedRectangle(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5F)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRounded(Graphics g, Color color, RectangleF r, float radius)
        {
            using (GraphicsPath path = RoundedRectangle(r, radius))
            using (var brush = new SolidBrush(color))
            {
                g.FillPath(brush, path);
            }
        }

        public static void DrawRounded(Graphics g, Color color, float width, RectangleF r, float radius)
        {
            using (GraphicsPath path = RoundedRectangle(r, radius))
            using (var pen = new Pen(color, width))
            {
                g.DrawPath(pen, path);
            }
        }

        /// <summary>A control's outline rectangle, inset half a pixel so a 1 px line is drawn sharp.</summary>
        public static RectangleF OutlineRect(Control c)
        {
            return new RectangleF(0.5F, 0.5F, c.Width - 1.5F, c.Height - 1.5F);
        }

        /// <summary>Keyboard focus outline drawn just inside a control (accent colour).</summary>
        public static void DrawFocusInside(Graphics g, Control c, Color color)
        {
            float s = Scale(c);
            RectangleF f = RectangleF.Inflate(OutlineRect(c), -2.5F * s, -2.5F * s);
            DrawRounded(g, color, 1.5F * s, f, 3 * s);
        }

        /// <summary>Size of the checkbox square / radio circle in pixels (20 px at 100 % scaling, as in Windows 11).</summary>
        public static int ToggleBoxSize(Control c)
        {
            return (int)Math.Round(20 * Scale(c));
        }

        public static int ToggleGap(Control c)
        {
            return (int)Math.Round(8 * Scale(c));
        }

        /// <summary>Draws a checkbox or radio button (Windows 11 look).</summary>
        public static void PaintToggle(Graphics g, ButtonBase c, Palette p, bool isChecked, bool hover, bool focusCue, bool radio)
        {
            float s = Scale(c);
            int box = ToggleBoxSize(c);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(c.BackColor);

            var r = new RectangleF(2.5F * s, (c.Height - box) / 2F, box - 1, box - 1);
            Color stroke = !c.Enabled ? p.DisabledText : (hover ? p.Text : p.ControlStroke);
            Color fillOn = !c.Enabled ? p.DisabledText : (hover ? p.AccentHover : p.Accent);

            if (radio)
            {
                if (isChecked)
                {
                    using (var fill = new SolidBrush(fillOn))
                    {
                        g.FillEllipse(fill, r);
                    }
                    float dot = box * (hover ? 0.5F : 0.42F);
                    using (var mark = new SolidBrush(p.OnAccent))
                    {
                        g.FillEllipse(mark, r.X + (r.Width - dot) / 2, r.Y + (r.Height - dot) / 2, dot, dot);
                    }
                }
                else
                {
                    using (var fill = new SolidBrush(hover ? p.Hover : p.Surface))
                    using (var pen = new Pen(stroke, 1.2F * s))
                    {
                        g.FillEllipse(fill, r);
                        g.DrawEllipse(pen, r);
                    }
                }
            }
            else
            {
                if (isChecked)
                {
                    FillRounded(g, fillOn, r, 4 * s);
                    using (var pen = new Pen(p.OnAccent, 1.6F * s) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    {
                        g.DrawLines(pen, new[]
                        {
                            new PointF(r.X + r.Width * 0.25F, r.Y + r.Height * 0.52F),
                            new PointF(r.X + r.Width * 0.43F, r.Y + r.Height * 0.70F),
                            new PointF(r.X + r.Width * 0.76F, r.Y + r.Height * 0.32F)
                        });
                    }
                }
                else
                {
                    FillRounded(g, hover ? p.Hover : p.Surface, r, 4 * s);
                    DrawRounded(g, stroke, 1.2F * s, r, 4 * s);
                }
            }

            if (focusCue)
            {
                RectangleF f = RectangleF.Inflate(r, 2.5F * s, 2.5F * s);
                DrawRounded(g, p.Accent, 1.5F * s, f, radio ? f.Width / 2 : 6 * s);
            }

            int textX = (int)(r.Right + ToggleGap(c));
            var textRect = new Rectangle(textX, 0, Math.Max(0, c.Width - textX), c.Height);
            TextRenderer.DrawText(g, c.Text, c.Font, textRect, c.Enabled ? p.Text : p.DisabledText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        /// <summary>After a palette change: lets the parent re-measure a checkbox / radio button, then redraws it.</summary>
        public static void RefreshToggle(ButtonBase c)
        {
            if (c.Parent != null)
            {
                c.Parent.PerformLayout(c, "Bounds");
            }
            c.Invalidate();
        }

        /// <summary>Preferred size of a checkbox / radio button: box, gap and text, at least 30 px high.</summary>
        public static Size ToggleSize(ButtonBase c)
        {
            float s = Scale(c);
            Size text = TextRenderer.MeasureText(c.Text, c.Font, Size.Empty, TextFormatFlags.NoPrefix);
            int box = ToggleBoxSize(c);
            int width = (int)(4 * s) + box + ToggleGap(c) + text.Width + 2;
            int height = Math.Max((int)(30 * s), Math.Max(box, text.Height) + (int)(6 * s));
            return new Size(width, height);
        }

        // ---------------- Windows functions ----------------

        // DWM (Desktop Window Manager) attribute for a dark title bar: 20 on Windows 10 20H1+ and
        // Windows 11, 19 on Windows 10 1809-1909.
        private const int DwmUseImmersiveDarkMode = 20;
        private const int DwmUseImmersiveDarkModeOld = 19;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);

        private static void SetDarkTitleBar(Form form, bool dark)
        {
            try
            {
                int value = dark ? 1 : 0;
                if (DwmSetWindowAttribute(form.Handle, DwmUseImmersiveDarkMode, ref value, sizeof(int)) != 0)
                {
                    DwmSetWindowAttribute(form.Handle, DwmUseImmersiveDarkModeOld, ref value, sizeof(int));
                }
            }
            catch (Exception)
            {
                // Older Windows: the title bar stays light.
            }
        }

        public const int WmMouseWheel = 0x020A;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Passes a mouse-wheel message on to the control's parent, so the page scrolls instead of the
        /// control changing its value. Windows sends the wheel to the control under the mouse pointer.
        /// </summary>
        public static void PassWheelToParent(Control c, ref Message m)
        {
            if (c.Parent != null)
            {
                m.Result = SendMessage(c.Parent.Handle, m.Msg, m.WParam, m.LParam);
            }
        }

        /// <summary>Light or dark scroll bars (and drop-down list) using Windows' built-in Explorer styles.</summary>
        public static void SetScrollBarTheme(Control c, bool dark)
        {
            try
            {
                string style = c is ComboBox ? (dark ? "DarkMode_CFD" : "CFD") : (dark ? "DarkMode_Explorer" : "Explorer");
                SetWindowTheme(c.Handle, style, null);
            }
            catch (Exception)
            {
                // Not available: scroll bars stay light.
            }
        }

        /// <summary>Colours of the tray menu in dark mode.</summary>
        private sealed class DarkMenuColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Dark.Surface; } }
            public override Color MenuBorder { get { return Dark.ButtonBorder; } }
            public override Color MenuItemBorder { get { return Dark.ButtonBorder; } }
            public override Color MenuItemSelected { get { return Dark.Hover; } }
            public override Color ImageMarginGradientBegin { get { return Dark.Surface; } }
            public override Color ImageMarginGradientMiddle { get { return Dark.Surface; } }
            public override Color ImageMarginGradientEnd { get { return Dark.Surface; } }
            public override Color SeparatorDark { get { return Dark.Border; } }
            public override Color SeparatorLight { get { return Dark.Border; } }
        }
    }

    // =====================================================================
    // Sections
    // =====================================================================

    /// <summary>
    /// A settings section in the Windows 11 style: a Semibold heading above a rounded card.
    /// Controls go into Body (top to bottom).
    /// </summary>
    internal sealed class SectionBox : Panel
    {
        public readonly FlowLayoutPanel Body;

        public SectionBox(string title)
        {
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Body = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill
            };
            var card = new CardPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Padding = new Padding(14, 10, 14, 12)
            };
            card.Controls.Add(Body);
            var heading = new Label
            {
                Text = title,
                AutoSize = true,
                Dock = DockStyle.Top,
                Font = Theme.HeadingFont(),
                Padding = new Padding(2, 0, 0, 6),
                MaximumSize = new Size(560, 0)   // wraps; MainForm updates the width with the window
            };
            // Docked controls are laid out from the last added to the first: the heading takes the
            // top edge and the card fills the rest.
            Controls.Add(card);
            Controls.Add(heading);
        }
    }

    /// <summary>Rounded card behind the controls of a section.</summary>
    internal sealed class CardPanel : Panel, IThemedControl
    {
        private Theme.Palette palette = Theme.Light;

        public CardPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        public void SetPalette(Theme.Palette p)
        {
            palette = p;
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            float s = Theme.Scale(this);
            g.Clear(Parent != null ? Parent.BackColor : palette.Back);   // corners outside the rounding
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF r = Theme.OutlineRect(this);
            Theme.FillRounded(g, palette.Card, r, 6 * s);
            Theme.DrawRounded(g, palette.CardBorder, 1F, r, 6 * s);
        }
    }

    // =====================================================================
    // Tabs, buttons, checkboxes, radio buttons
    // =====================================================================

    /// <summary>Tabs in the Windows 11 style: text only, an accent bar under the selected tab.</summary>
    internal sealed class ThemedTabControl : TabControl, IThemedControl
    {
        private Theme.Palette palette = Theme.Light;
        private int hotIndex = -1;

        public ThemedTabControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            float s = Theme.Scale(this);
            Padding = new Point((int)(16 * s), (int)(7 * s));   // space around each tab's text
        }

        public void SetPalette(Theme.Palette p)
        {
            palette = p;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int index = -1;
            for (int i = 0; i < TabCount; i++)
            {
                if (GetTabRect(i).Contains(e.Location))
                {
                    index = i;
                }
            }
            if (index != hotIndex)
            {
                hotIndex = index;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hotIndex = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            Invalidate();
            base.OnSelectedIndexChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Palette p = palette;
            Graphics g = e.Graphics;
            float s = Theme.Scale(this);
            g.Clear(p.Back);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int stripBottom = 0;
            for (int i = 0; i < TabCount; i++)
            {
                Rectangle r = GetTabRect(i);
                stripBottom = Math.Max(stripBottom, r.Bottom);
                bool selected = i == SelectedIndex;
                bool hot = i == hotIndex && !selected;
                var shape = RectangleF.Inflate(r, -2 * s, -2 * s);
                if (hot)
                {
                    Theme.FillRounded(g, p.IsDark ? p.Hover : p.Pressed, shape, 4 * s);
                }

                string text = TabPages[i].Text;
                TextRenderer.DrawText(g, text, Font, r, selected || hot ? p.Text : p.SubtleText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

                if (selected)
                {
                    float width = Math.Min(r.Width - 12 * s, TextRenderer.MeasureText(text, Font).Width);
                    float height = 3 * s;
                    var bar = new RectangleF(r.X + (r.Width - width) / 2F, r.Bottom - height - 2 * s, width, height);
                    Theme.FillRounded(g, p.Accent, bar, height / 2);
                    if (Focused && ShowFocusCues)
                    {
                        Theme.DrawRounded(g, p.Accent, 1.5F * s, shape, 4 * s);
                    }
                }
            }

            using (var pen = new Pen(p.Border))
            {
                g.DrawLine(pen, 0, stripBottom + 1, Width, stripBottom + 1);   // divider under the tabs
            }
        }
    }

    /// <summary>Checkbox in the Windows 11 style (outline 4.9:1 or more, accent fill when ticked, focus outline).</summary>
    internal sealed class ThemedCheckBox : CheckBox, IThemedControl
    {
        private Theme.Palette palette = Theme.Light;
        private bool hover;

        public ThemedCheckBox()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }

        public void SetPalette(Theme.Palette p) { palette = p; Theme.RefreshToggle(this); }
        public override Size GetPreferredSize(Size proposedSize) { return Theme.ToggleSize(this); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.PaintToggle(e.Graphics, this, palette, Checked, hover, Focused && ShowFocusCues, false);
        }
    }

    /// <summary>Radio button in the Windows 11 style (outline 4.9:1 or more, accent fill with a dot when selected).</summary>
    internal sealed class ThemedRadioButton : RadioButton, IThemedControl
    {
        private Theme.Palette palette = Theme.Light;
        private bool hover;

        public ThemedRadioButton()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }

        public void SetPalette(Theme.Palette p) { palette = p; Theme.RefreshToggle(this); }
        public override Size GetPreferredSize(Size proposedSize) { return Theme.ToggleSize(this); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.PaintToggle(e.Graphics, this, palette, Checked, hover, Focused && ShowFocusCues, true);
        }
    }

    /// <summary>
    /// Button in the Windows 11 style. A primary button (the main action of a page, such as Start or
    /// Save) is filled with the accent colour.
    /// </summary>
    internal sealed class ThemedButton : Button, IThemedControl
    {
        private Theme.Palette palette = Theme.Light;
        private bool hover;
        private bool pressed;

        public ThemedButton()
        {
            // Every button sizes itself the same way (see GetPreferredSize).
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }

        /// <summary>Accent-coloured button for the main action of a page.</summary>
        public bool Primary { get; set; }

        public void SetPalette(Theme.Palette p)
        {
            palette = p;
            Invalidate();
        }

        /// <summary>
        /// One size rule for all buttons: text width plus 16 px on each side, at least 96 px wide,
        /// and the shared control height (see Theme.ControlHeight), scaled for the screen DPI.
        /// </summary>
        public override Size GetPreferredSize(Size proposedSize)
        {
            float s = Theme.Scale(this);
            Size text = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPrefix);
            int width = Math.Max((int)(96 * s), text.Width + (int)(32 * s));
            return new Size(width, Theme.ControlHeight(this));
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Palette p = palette;
            Graphics g = e.Graphics;
            float s = Theme.Scale(this);
            g.Clear(BackColor);   // the colour behind the button (window or card)
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF r = Theme.OutlineRect(this);

            Color text;
            if (!Enabled)
            {
                Theme.DrawRounded(g, p.Border, 1F, r, 4 * s);
                text = p.DisabledText;
            }
            else if (Primary)
            {
                Theme.FillRounded(g, pressed ? p.AccentPressed : hover ? p.AccentHover : p.Accent, r, 4 * s);
                text = p.OnAccent;
            }
            else
            {
                Theme.FillRounded(g, pressed ? p.Pressed : hover ? p.Hover : p.Surface, r, 4 * s);
                Theme.DrawRounded(g, p.ButtonBorder, 1F, r, 4 * s);
                text = p.Text;
            }

            if (Focused && ShowFocusCues && Enabled)
            {
                Theme.DrawFocusInside(g, this, Primary ? p.OnAccent : p.Accent);
            }

            TextRenderer.DrawText(g, Text, Font, ClientRectangle, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }

    // =====================================================================
    // Fields, drop-down lists, sliders
    // =====================================================================

    /// <summary>
    /// Text or number field in the Windows 11 style: a rounded outline around a borderless TextBox or
    /// NumericUpDown, with an accent line along the bottom while it has keyboard focus. Single-line
    /// fields have the shared control height; a multi-line field (fill = true) fills its parent.
    /// </summary>
    internal sealed class ThemedField : Panel, IThemedControl
    {
        private readonly Control inner;
        private readonly bool fill;
        private Theme.Palette palette = Theme.Light;

        /// <summary>Single-line field; width in pixels at 100 % scaling.</summary>
        public ThemedField(Control inner, int width) : this(inner, false)
        {
            Width = width;
        }

        public ThemedField(Control inner, bool fill)
        {
            this.inner = inner;
            this.fill = fill;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            var text = inner as TextBoxBase;
            if (text != null)
            {
                text.BorderStyle = BorderStyle.None;
                Cursor = Cursors.IBeam;
            }
            var number = inner as UpDownBase;
            if (number != null)
            {
                number.BorderStyle = BorderStyle.None;
                foreach (Control part in number.Controls)
                {
                    if (!(part is TextBoxBase))
                    {
                        part.Paint += PaintSpinButtons;   // the up/down arrows; drawn over Windows' classic ones
                    }
                }
            }

            Height = 32;
            Controls.Add(inner);
            inner.Enter += (s, e) => UpdateColours();
            inner.Leave += (s, e) => UpdateColours();
            inner.EnabledChanged += (s, e) => UpdateColours();
        }

        public Control Inner { get { return inner; } }

        public void SetPalette(Theme.Palette p)
        {
            palette = p;
            UpdateHeight();
            UpdateColours();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            UpdateHeight();
        }

        private void UpdateHeight()
        {
            if (!fill)
            {
                int h = Theme.ControlHeight(this);
                if (Height != h)
                {
                    Height = h;
                }
            }
        }

        private bool IsActive { get { return inner.ContainsFocus; } }

        private Color FillColour
        {
            get
            {
                if (!inner.Enabled)
                {
                    return BackColor;
                }
                return IsActive ? palette.FieldFocused : palette.Surface;
            }
        }

        private void UpdateColours()
        {
            inner.BackColor = FillColour;
            inner.ForeColor = inner.Enabled ? palette.Text : palette.DisabledText;
            Invalidate(true);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            float s = Theme.Scale(this);
            int left = (int)(10 * s);
            int right = inner is UpDownBase ? (int)(4 * s) : fill ? (int)(2 * s) : (int)(10 * s);
            int width = Math.Max(0, Width - left - right);
            if (fill)
            {
                int top = (int)(6 * s);
                inner.SetBounds(left, top, width, Math.Max(0, Height - 2 * top));
            }
            else
            {
                inner.SetBounds(left, Math.Max(0, (Height - inner.Height) / 2), width, inner.Height);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (inner.Enabled)
            {
                inner.Select();   // a click on the padding around the text also selects the field
            }
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            float s = Theme.Scale(this);
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF r = Theme.OutlineRect(this);
            bool enabled = inner.Enabled;

            Theme.FillRounded(g, FillColour, r, 4 * s);
            Theme.DrawRounded(g, enabled ? palette.ButtonBorder : palette.Border, 1F, r, 4 * s);
            if (IsActive)
            {
                // Windows 11 marks the field being typed in with an accent line along the bottom.
                using (GraphicsPath path = Theme.RoundedRectangle(r, 4 * s))
                using (var accent = new SolidBrush(palette.Accent))
                {
                    g.SetClip(path);
                    g.FillRectangle(accent, 0, Height - 2.5F * s, Width, 2.5F * s);
                    g.ResetClip();
                }
            }
        }

        /// <summary>Draws the up/down buttons of a number field: two small chevrons on the field colour.</summary>
        private void PaintSpinButtons(object sender, PaintEventArgs e)
        {
            var buttons = (Control)sender;
            Graphics g = e.Graphics;
            float s = Theme.Scale(this);
            int half = buttons.Height / 2;
            g.Clear(FillColour);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Hover and pressed shading for the half under the mouse.
            Point mouse = buttons.PointToClient(Cursor.Position);
            if (inner.Enabled && buttons.ClientRectangle.Contains(mouse))
            {
                bool down = (Control.MouseButtons & MouseButtons.Left) != 0;
                var halfRect = mouse.Y < half
                    ? new RectangleF(1, 1, buttons.Width - 2, half - 1)
                    : new RectangleF(1, half, buttons.Width - 2, buttons.Height - half - 1);
                Theme.FillRounded(g, down ? palette.Pressed : palette.Hover, halfRect, 3 * s);
            }

            Color colour = inner.Enabled ? palette.SubtleText : palette.DisabledText;
            float cx = buttons.Width / 2F;
            float w = 4F * s;
            float h = 2.5F * s;
            using (var pen = new Pen(colour, 1.4F * s) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                float upY = half / 2F;
                g.DrawLines(pen, new[] { new PointF(cx - w, upY + h / 2), new PointF(cx, upY - h / 2), new PointF(cx + w, upY + h / 2) });
                float downY = half + (buttons.Height - half) / 2F;
                g.DrawLines(pen, new[] { new PointF(cx - w, downY - h / 2), new PointF(cx, downY + h / 2), new PointF(cx + w, downY - h / 2) });
            }
        }
    }

    /// <summary>
    /// Number box whose value changes with the mouse wheel only while it has keyboard focus; otherwise
    /// the wheel scrolls the page. Shown inside a ThemedField.
    /// </summary>
    internal sealed class ThemedNumberBox : NumericUpDown
    {
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (ContainsFocus)
            {
                base.OnMouseWheel(e);   // changes the value
            }
            // Not focused: left unhandled, so Windows passes the wheel on to the parent and the page scrolls.
        }
    }

    /// <summary>
    /// Drop-down list in the Windows 11 style. The closed list is painted entirely by this control
    /// (WM_PAINT); the items of the opened list are drawn in OnDrawItem. The width grows to fit the
    /// longest item.
    /// </summary>
    internal sealed class ThemedComboBox : ComboBox, IThemedControl
    {
        private const int WmPaint = 0x000F;
        private const int WmEraseBkgnd = 0x0014;

        private Theme.Palette palette = Theme.Light;
        private bool themed;   // SetPalette has run (after the window's DPI scaling)
        private bool hover;

        public ThemedComboBox()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            DrawMode = DrawMode.OwnerDrawFixed;
            // Windows sets the height of a drop-down list itself (from ItemHeight). With AutoSize, the
            // layout panels ask GetPreferredSize below for the real size instead of the size first set.
            AutoSize = true;
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            return new Size(Width, Height);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Parent != null)
            {
                Parent.PerformLayout(this, "Bounds");   // make room for the new height
            }
        }

        /// <summary>Text shown in the secondary colour while nothing is selected.</summary>
        public string PlaceholderText { get; set; }

        public void SetPalette(Theme.Palette p)
        {
            palette = p;
            themed = true;
            float s = Theme.Scale(this);
            ItemHeight = TextRenderer.MeasureText("Ag", Font).Height + (int)(8 * s);
            FitToItems();
            Invalidate();
        }

        /// <summary>Widens the list (never narrows it) so the longest item fits next to the arrow.</summary>
        public void FitToItems()
        {
            if (!themed)
            {
                return;   // before the window's DPI scaling; SetPalette calls this again afterwards
            }
            float s = Theme.Scale(this);
            int widest = 0;
            foreach (object item in Items)
            {
                widest = Math.Max(widest, TextRenderer.MeasureText(GetItemText(item), Font).Width);
            }
            int needed = widest + (int)(48 * s);
            if (needed > Width)
            {
                Width = needed;
            }
            DropDownWidth = Width;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnDropDown(EventArgs e) { Invalidate(); base.OnDropDown(e); }
        protected override void OnDropDownClosed(EventArgs e) { Invalidate(); base.OnDropDownClosed(e); }
        protected override void OnSelectedIndexChanged(EventArgs e) { Invalidate(); base.OnSelectedIndexChanged(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void WndProc(ref Message m)
        {
            // The wheel changes the selection only while the list has keyboard focus (or is open);
            // otherwise the page scrolls, so scrolling past the list does not change a setting.
            if (m.Msg == Theme.WmMouseWheel && !Focused && !DroppedDown)
            {
                Theme.PassWheelToParent(this, ref m);
                return;
            }
            if (m.Msg == WmEraseBkgnd)
            {
                m.Result = (IntPtr)1;   // everything is painted in WM_PAINT
                return;
            }
            if (m.Msg == WmPaint)
            {
                var ps = new PaintStruct();
                IntPtr hdc = BeginPaint(Handle, ref ps);
                try
                {
                    using (BufferedGraphics buffer = BufferedGraphicsManager.Current.Allocate(hdc, ClientRectangle))
                    {
                        PaintFace(buffer.Graphics);
                        buffer.Render();
                    }
                }
                finally
                {
                    EndPaint(Handle, ref ps);
                }
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        private void PaintFace(Graphics g)
        {
            Theme.Palette p = palette;
            float s = Theme.Scale(this);
            Color outside = Parent != null ? Parent.BackColor : p.Back;
            g.Clear(outside);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF r = Theme.OutlineRect(this);

            if (Enabled)
            {
                Theme.FillRounded(g, DroppedDown ? p.Pressed : hover ? p.Hover : p.Surface, r, 4 * s);
                Theme.DrawRounded(g, p.ButtonBorder, 1F, r, 4 * s);
                if (Focused && ShowFocusCues && !DroppedDown)
                {
                    Theme.DrawFocusInside(g, this, p.Accent);
                }
            }
            else
            {
                Theme.DrawRounded(g, p.Border, 1F, r, 4 * s);
            }

            bool empty = SelectedIndex < 0;
            string text = empty ? (PlaceholderText ?? "") : GetItemText(SelectedItem);
            Color textColour = !Enabled ? p.DisabledText : empty ? p.SubtleText : p.Text;
            int left = (int)(11 * s);
            int arrowArea = (int)(34 * s);
            var textRect = new Rectangle(left, 0, Math.Max(0, Width - left - arrowArea), Height);
            TextRenderer.DrawText(g, text, Font, textRect, textColour,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

            // Chevron (down arrow).
            float cx = Width - 17 * s;
            float cy = Height / 2F;
            float w = 4.5F * s;
            float h = 2.5F * s;
            using (var pen = new Pen(Enabled ? p.SubtleText : p.DisabledText, 1.4F * s) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                g.DrawLines(pen, new[] { new PointF(cx - w, cy - h / 2), new PointF(cx, cy + h / 2), new PointF(cx + w, cy - h / 2) });
            }
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0)
            {
                return;
            }
            string text = GetItemText(Items[e.Index]);
            bool selected = (e.State & DrawItemState.Selected) != 0;
            Theme.Palette p = palette;
            float s = Theme.Scale(this);
            Graphics g = e.Graphics;
            using (var back = new SolidBrush(selected ? p.Hover : p.Surface))
            {
                g.FillRectangle(back, e.Bounds);
            }
            if (selected)
            {
                // Accent marker at the left of the highlighted item, as in Windows 11 lists.
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float markerHeight = Math.Min(16 * s, e.Bounds.Height - 8 * s);
                var marker = new RectangleF(e.Bounds.X + 3 * s, e.Bounds.Y + (e.Bounds.Height - markerHeight) / 2F, 3 * s, markerHeight);
                Theme.FillRounded(g, p.Accent, marker, 1.5F * s);
            }
            var textRect = new Rectangle(e.Bounds.X + (int)(12 * s), e.Bounds.Y, Math.Max(0, e.Bounds.Width - (int)(16 * s)), e.Bounds.Height);
            TextRenderer.DrawText(g, text, Font, textRect, p.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PaintStruct
        {
            // PAINTSTRUCT: BOOL fields as int and the 32 reserved bytes as 8 ints, so no conversion is needed.
            public IntPtr Hdc;
            public int Erase;
            public int Left, Top, Right, Bottom;
            public int Restore;
            public int IncUpdate;
            public int R0, R1, R2, R3, R4, R5, R6, R7;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr BeginPaint(IntPtr hwnd, ref PaintStruct ps);

        [DllImport("user32.dll")]
        private static extern bool EndPaint(IntPtr hwnd, ref PaintStruct ps);
    }

    /// <summary>
    /// Slider in the Windows 11 style: a thin track, accent-coloured up to the thumb, and a round
    /// thumb. Mouse (click or drag), arrow keys (1 step), Page Up/Down (LargeChange), Home/End and
    /// the mouse wheel (while focused) change the value.
    /// </summary>
    internal sealed class ThemedSlider : Control, IThemedControl
    {
        private Theme.Palette palette = Theme.Light;
        private int minimum;
        private int maximum = 100;
        private int current;
        private bool hover;
        private bool dragging;

        public ThemedSlider()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            Height = 32;
            SmallChange = 1;
            LargeChange = 5;
        }

        public event EventHandler ValueChanged;

        public int SmallChange { get; set; }
        public int LargeChange { get; set; }

        public int Minimum
        {
            get { return minimum; }
            set
            {
                minimum = value;
                if (maximum < minimum)
                {
                    maximum = minimum;
                }
                Value = current;
                Invalidate();
            }
        }

        public int Maximum
        {
            get { return maximum; }
            set
            {
                maximum = value;
                if (minimum > maximum)
                {
                    minimum = maximum;
                }
                Value = current;
                Invalidate();
            }
        }

        public int Value
        {
            get { return current; }
            set
            {
                int v = Math.Max(minimum, Math.Min(maximum, value));
                if (v == current)
                {
                    return;
                }
                current = v;
                Invalidate();
                AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
                EventHandler handler = ValueChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
        }

        public void SetPalette(Theme.Palette p)
        {
            palette = p;
            Invalidate();
        }

        private float ThumbRadius { get { return 10 * Theme.Scale(this); } }
        private float TrackLeft { get { return ThumbRadius + 3 * Theme.Scale(this); } }
        private float TrackRight { get { return Width - ThumbRadius - 3 * Theme.Scale(this); } }

        private float XOf(int v)
        {
            if (maximum == minimum)
            {
                return TrackLeft;
            }
            return TrackLeft + (TrackRight - TrackLeft) * (v - minimum) / (maximum - minimum);
        }

        private int ValueAt(int x)
        {
            float span = Math.Max(1F, TrackRight - TrackLeft);
            return minimum + (int)Math.Round((x - TrackLeft) / span * (maximum - minimum));
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left:
                case Keys.Right:
                case Keys.Up:
                case Keys.Down:
                case Keys.PageUp:
                case Keys.PageDown:
                case Keys.Home:
                case Keys.End:
                    return true;
                default:
                    return base.IsInputKey(keyData);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Left:
                case Keys.Down: Value -= SmallChange; e.Handled = true; break;
                case Keys.Right:
                case Keys.Up: Value += SmallChange; e.Handled = true; break;
                case Keys.PageDown: Value -= LargeChange; e.Handled = true; break;
                case Keys.PageUp: Value += LargeChange; e.Handled = true; break;
                case Keys.Home: Value = minimum; e.Handled = true; break;
                case Keys.End: Value = maximum; e.Handled = true; break;
            }
            base.OnKeyDown(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Focus();
                dragging = true;
                Capture = true;
                Value = ValueAt(e.X);
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging)
            {
                Value = ValueAt(e.X);
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (dragging)
            {
                dragging = false;
                Capture = false;
                Invalidate();
            }
            base.OnMouseUp(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            // The wheel changes the value only while the slider has keyboard focus. Otherwise the event
            // stays unhandled, and Windows passes it on to the parent, so the page scrolls.
            if (Focused)
            {
                Value += Math.Sign(e.Delta) * SmallChange;
                var handled = e as HandledMouseEventArgs;
                if (handled != null)
                {
                    handled.Handled = true;
                }
            }
            base.OnMouseWheel(e);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Palette p = palette;
            Graphics g = e.Graphics;
            float s = Theme.Scale(this);
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float cy = Height / 2F;
            float trackHeight = 4 * s;
            float x = XOf(current);
            var track = new RectangleF(TrackLeft, cy - trackHeight / 2, Math.Max(0, TrackRight - TrackLeft), trackHeight);
            Theme.FillRounded(g, Enabled ? p.ControlStroke : p.Border, track, trackHeight / 2);
            if (x > TrackLeft)
            {
                var done = new RectangleF(TrackLeft, track.Y, x - TrackLeft, trackHeight);
                Theme.FillRounded(g, Enabled ? p.Accent : p.DisabledText, done, trackHeight / 2);
            }

            float r = ThumbRadius;
            var thumb = new RectangleF(x - r, cy - r, 2 * r, 2 * r);
            using (var fill = new SolidBrush(p.Surface))
            using (var pen = new Pen(p.ButtonBorder, 1F))
            {
                g.FillEllipse(fill, thumb);
                g.DrawEllipse(pen, thumb);
            }
            float inner = (dragging ? 5F : hover ? 7F : 6F) * s;
            using (var dot = new SolidBrush(Enabled ? (hover || dragging ? p.AccentHover : p.Accent) : p.DisabledText))
            {
                g.FillEllipse(dot, x - inner, cy - inner, 2 * inner, 2 * inner);
            }

            if (Focused && ShowFocusCues)
            {
                float f = r + 3 * s;
                using (var pen = new Pen(p.Accent, 1.5F * s))
                {
                    g.DrawEllipse(pen, x - f, cy - f, 2 * f, 2 * f);
                }
            }
        }

        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new SliderAccessibleObject(this);
        }

        /// <summary>Lets screen readers read and set the value.</summary>
        private sealed class SliderAccessibleObject : ControlAccessibleObject
        {
            private readonly ThemedSlider slider;

            public SliderAccessibleObject(ThemedSlider slider) : base(slider)
            {
                this.slider = slider;
            }

            public override AccessibleRole Role { get { return AccessibleRole.Slider; } }

            public override string Value
            {
                get { return slider.Value.ToString(CultureInfo.CurrentCulture); }
                set
                {
                    int v;
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out v))
                    {
                        slider.Value = v;
                    }
                }
            }
        }
    }
}
