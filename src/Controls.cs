using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace FFmpegForge
{
    public static class Theme
    {
        // FFmpeg Forge 3.0 — "Graphite Aurora": near-black page, charcoal panels, cyan → indigo actions (flat, compact)
        public static readonly Color Bg = Color.FromArgb(14, 15, 18);            // window / page background
        public static readonly Color Panel = Color.FromArgb(30, 31, 36);         // cards, header, sidebar
        public static readonly Color Card = Color.FromArgb(21, 22, 26);          // inputs / wells inside cards
        public static readonly Color CardHover = Color.FromArgb(40, 42, 48);
        public static readonly Color Border = Color.FromArgb(52, 54, 61);
        public static readonly Color Text = Color.FromArgb(241, 244, 248);
        public static readonly Color Muted = Color.FromArgb(158, 164, 175);
        public static readonly Color Cyan = Color.FromArgb(34, 211, 238);
        public static readonly Color CyanDeep = Color.FromArgb(6, 182, 212);
        public static readonly Color Indigo = Color.FromArgb(99, 102, 241);
        public static readonly Color AccentMuted = Color.FromArgb(18, 42, 52);    // tint behind active pills / toggled buttons
        public static readonly Color Gold = Cyan;                                 // section icons, highlights (was gold in 2.x)
        public static readonly Color GoldDeep = CyanDeep;
        public static readonly Color Blue = Indigo;
        public static readonly Color BlueDeep = Color.FromArgb(79, 70, 229);
        public static readonly Color Accent1 = Color.FromArgb(251, 191, 36);      // warnings, "not installed" hints (amber)
        public static readonly Color Accent2 = Indigo;                            // actions
        public static readonly Color Success = Color.FromArgb(52, 211, 153);
        public static readonly Color Error = Color.FromArgb(248, 96, 112);
        public static readonly Color Selection = Color.FromArgb(24, 48, 60);
        public static readonly Color Side = Color.FromArgb(11, 12, 14);           // header / bottom bar background

        public static float Scale = 1f;
        public static int S(int px) { return (int)Math.Round(px * Scale); }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = rad * 2;
            if (d > r.Height) d = r.Height;
            if (d > r.Width) d = r.Width;
            if (d < 1) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        static RectangleF Safe(RectangleF r) { if (r.Width < 1) r.Width = 1; if (r.Height < 1) r.Height = 1; return r; }

        /// Primary actions: cyan → indigo.
        public static LinearGradientBrush AccentBrush(RectangleF r)
        {
            return new LinearGradientBrush(Safe(r), CyanDeep, Indigo, LinearGradientMode.Horizontal);
        }

        /// Highlights: gold.
        public static LinearGradientBrush GoldBrush(RectangleF r)
        {
            return new LinearGradientBrush(Safe(r), Color.FromArgb(20, 184, 166), CyanDeep, LinearGradientMode.ForwardDiagonal);
        }

        /// Progress: blue to gold.
        public static LinearGradientBrush ProgressBrush(RectangleF r)
        {
            var b = new LinearGradientBrush(Safe(r), CyanDeep, Indigo, LinearGradientMode.Horizontal);
            var cb = new ColorBlend(3);
            cb.Colors = new[] { CyanDeep, Indigo, Color.FromArgb(168, 85, 247) };
            cb.Positions = new[] { 0f, 0.55f, 1f };
            b.InterpolationColors = cb;
            return b;
        }
    }

    /// Rounded card (settings section): rounded corners by region, gold title drawn by the section's label.
    public class CardTable : TableLayoutPanel
    {
        public CardTable()
        {
            DoubleBuffered = true;
            BackColor = Theme.Panel;
        }
        public bool Border = true;
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Width > 2 && Height > 2)
                using (var p = Theme.Round(new RectangleF(0, 0, Width, Height), Theme.S(8))) Region = new Region(p);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!Border || Width < 4 || Height < 4) return;
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Theme.S(8)))
            using (var pen = new Pen(Theme.Border)) g.DrawPath(pen, p);
        }
    }

    /// Rounded flat button with a gradient "Primary" variant and a "Toggled" state.
    public class FlatBtn : Control
    {
        bool hover, down;
        public bool Primary;
        public bool Gold;
        public bool Glass;           // translucent button over artwork (header)
        public bool Glow;            // CONVERT: blue → gold gradient with a light rim
        public string Icon;          // vector icon drawn before the text (Icons.Draw name)
        public Color? IconColor;
        bool toggled;
        public bool Toggled { get { return toggled; } set { toggled = value; Invalidate(); } }

        public FlatBtn(string text)
        {
            Text = text;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI Semibold", 9.5f);
            ForeColor = Theme.Text;
            BackColor = Color.Transparent;
            Height = Theme.S(34);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Color fc;
            using (var path = Theme.Round(r, Theme.S(6)))
            {
                if (Glow && Enabled)
                {
                    using (var b = new LinearGradientBrush(r, Theme.CyanDeep, Theme.Indigo, LinearGradientMode.Horizontal))
                    {
                        var cb = new ColorBlend(3);
                        cb.Colors = new[] { Theme.CyanDeep, Color.FromArgb(59, 130, 246), Theme.Indigo };
                        cb.Positions = new[] { 0f, 0.5f, 1f };
                        b.InterpolationColors = cb;
                        g.FillPath(b, path);
                    }
                    if (hover) using (var w = new SolidBrush(Color.FromArgb(down ? 36 : 22, 255, 255, 255))) g.FillPath(w, path);
                    using (var pen = new Pen(Color.FromArgb(90, 255, 255, 255), 1f)) g.DrawPath(pen, path);
                    fc = Color.White;
                }
                else if (Glass && Enabled)
                {
                    using (var b = new SolidBrush(hover ? Theme.CardHover : Theme.Card)) g.FillPath(b, path);
                    using (var pen = new Pen(hover ? Color.FromArgb(160, Theme.Cyan) : Theme.Border, 1f)) g.DrawPath(pen, path);
                    fc = ForeColor;
                }
                else if (!Enabled)
                {
                    using (var b = new SolidBrush(Color.FromArgb(26, 27, 31))) g.FillPath(b, path);
                    fc = Color.FromArgb(92, 96, 106);
                }
                else if (Gold)
                {
                    using (var b = Theme.GoldBrush(r)) g.FillPath(b, path);
                    if (hover) using (var w = new SolidBrush(Color.FromArgb(down ? 40 : 28, 255, 255, 255))) g.FillPath(w, path);
                    fc = Color.White;
                }
                else if (Primary)
                {
                    using (var b = Theme.AccentBrush(r)) g.FillPath(b, path);
                    // soft top light
                    if (hover) using (var w = new SolidBrush(Color.FromArgb(down ? 36 : 22, 255, 255, 255))) g.FillPath(w, path);
                    fc = Color.White;
                }
                else if (toggled)
                {
                    using (var b = new SolidBrush(Theme.AccentMuted)) g.FillPath(b, path);
                    using (var pen = new Pen(Theme.Cyan, Theme.S(1) * 1.2f)) g.DrawPath(pen, path);
                    fc = Theme.Text;
                }
                else
                {
                    using (var b = new SolidBrush(down ? Theme.Border : hover ? Theme.CardHover : Theme.Card)) g.FillPath(b, path);
                    using (var pen = new Pen(hover ? Color.FromArgb(78, 81, 90) : Theme.Border)) g.DrawPath(pen, path);
                    fc = ForeColor;
                }
            }
            if (string.IsNullOrEmpty(Icon))
            {
                TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height), fc,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                return;
            }
            float isz = Math.Min(Height * 0.5f, Theme.S(20));
            if (Font.Size > 10.5f) isz = Math.Min(Height * 0.48f, Theme.S(24));
            int gap = Theme.S(10);
            var ts = string.IsNullOrEmpty(Text) ? Size.Empty : TextRenderer.MeasureText(g, Text, Font, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            float total = isz + (ts.Width > 0 ? gap + ts.Width : 0);
            float x0 = Math.Max(Theme.S(10), (Width - total) / 2f);
            Color ic = !Enabled ? fc : (IconColor ?? fc);
            Icons.Draw(g, Icon, new RectangleF(x0, (Height - isz) / 2f, isz, isz), ic);
            if (ts.Width > 0)
                TextRenderer.DrawText(g, Text, Font, new Rectangle((int)(x0 + isz + gap), 0, (int)Math.Max(10, Width - (x0 + isz + gap) - Theme.S(6)), Height), fc,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }
    }

    /// Gradient progress bar (blue to gold).
    public class GradientBar : Control
    {
        double value;
        public double Value { get { return value; } set { this.value = Math.Max(0, Math.Min(1, value)); Invalidate(); } }
        public string Caption = "";

        public GradientBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = Theme.S(8);
            BackColor = Theme.Side;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            var r = new RectangleF(0, 0, Width - 1, Height - 1);
            using (var p = Theme.Round(r, Height / 2f))
            using (var b = new SolidBrush(Theme.Card)) g.FillPath(b, p);
            if (value > 0)
            {
                var f = new RectangleF(0, 0, Math.Max(Height, (float)((Width - 1) * value)), Height - 1);
                using (var p = Theme.Round(f, Height / 2f))
                using (var b = Theme.ProgressBrush(new RectangleF(0, 0, Width, Height))) g.FillPath(b, p);
            }
        }
    }

    /// FFmpeg Forge logo: gold rounded tile with a navy lightning bolt.
    public static class Header
    {
        public static void DrawLogo(Graphics g, RectangleF lr)
        {
            // 3.0 logo: graphite tile, cyan → indigo ring and a white "forge spark" bolt
            using (var p = Theme.Round(lr, lr.Width * 0.24f))
            {
                using (var b = new LinearGradientBrush(lr, Theme.CyanDeep, Theme.Indigo, 45f)) g.FillPath(b, p);
                var inner = RectangleF.Inflate(lr, -lr.Width * 0.07f, -lr.Width * 0.07f);
                using (var ip = Theme.Round(inner, inner.Width * 0.2f))
                using (var ib = new LinearGradientBrush(inner, Color.FromArgb(26, 28, 34), Color.FromArgb(14, 15, 19), 90f)) g.FillPath(ib, ip);
            }
            float u = lr.Width / 10f;
            var bolt = new PointF[] {
                new PointF(lr.X + 5.9f*u, lr.Y + 1.9f*u),
                new PointF(lr.X + 2.8f*u, lr.Y + 5.6f*u),
                new PointF(lr.X + 4.9f*u, lr.Y + 5.6f*u),
                new PointF(lr.X + 4.0f*u, lr.Y + 8.3f*u),
                new PointF(lr.X + 7.3f*u, lr.Y + 4.3f*u),
                new PointF(lr.X + 5.2f*u, lr.Y + 4.3f*u),
            };
            using (var b = new LinearGradientBrush(lr, Color.FromArgb(165, 243, 252), Color.FromArgb(199, 210, 254), 60f)) g.FillPolygon(b, bolt);
        }
    }

    /// 3.0 application header: flat graphite bar, logo + wordmark + version pill, the conversion WORKFLOW
    /// (stage chips lit when a stage is on) and the FFmpeg status pill (the "FFmpeg location…" button is added by the main window).
    public class TopHeader : Control
    {
        public string Right1 = "";
        public Color Right1Color = Theme.Muted;
        public int FilterCount, FormatCount;
        public string Version = "3.0.1";
        public readonly FlatBtn StatusPill;
        /// Workflow stages shown in the header (name, icon, on).
        public string[] Stages = { "Decode", "Denoise", "Deblur", "Upscale", "HDR", "Encode" };
        public string[] StageIcons = { "film", "denoise", "frame", "upscale", "sparkle", "output" };
        public bool[] StageOn = { true, false, false, false, false, true };
        public int PillLeft = int.MaxValue;

        public TopHeader()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = Theme.S(78);
            BackColor = Theme.Side;
            StatusPill = new FlatBtn("Detecting FFmpeg…") { Glass = true, Icon = "search", Height = Theme.S(38), Font = new Font("Segoe UI", 9f), Cursor = Cursors.Default };
            StatusPill.IconColor = Theme.Cyan;
            Controls.Add(StatusPill);
        }

        public Action Relayout;
        string lastR1; int lastC;
        protected override void OnInvalidated(InvalidateEventArgs e)
        {
            if (Right1 != lastR1 || Right1Color.ToArgb() != lastC) { lastR1 = Right1; lastC = Right1Color.ToArgb(); if (Relayout != null) Relayout(); }
            base.OnInvalidated(e);
        }

        public void SetStages(bool[] on)
        {
            bool diff = on.Length != StageOn.Length;
            for (int i = 0; !diff && i < on.Length; i++) diff = on[i] != StageOn[i];
            if (!diff) return;
            StageOn = on; Invalidate();
        }

        /// Places the status pill to the left of x (the FFmpeg location button).
        public void PlacePill(int rightEdge)
        {
            string t = Right1 ?? "";
            t = t.TrimStart('●', '▲', ' ');
            if (t.Length == 0) t = "Detecting FFmpeg…";
            bool ok = Right1Color.ToArgb() == Theme.Success.ToArgb(), bad = Right1Color.ToArgb() == Theme.Error.ToArgb();
            StatusPill.Text = t;
            StatusPill.Icon = ok ? "check" : bad ? "warn" : "search";
            StatusPill.IconColor = ok ? Theme.Success : bad ? Theme.Error : Theme.Cyan;
            StatusPill.ForeColor = ok ? Color.FromArgb(200, 240, 222) : bad ? Color.FromArgb(255, 190, 196) : Theme.Text;
            int w = TextRenderer.MeasureText(t, StatusPill.Font).Width + Theme.S(64);
            StatusPill.Width = Math.Min(Theme.S(340), Math.Max(Theme.S(160), w));
            StatusPill.Location = new Point(rightEdge - StatusPill.Width - Theme.S(10), (Height - StatusPill.Height) / 2);
            PillLeft = StatusPill.Left;
            StatusPill.Invalidate();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Theme.Side);
            // faint aurora glow in the top-left corner
            using (var glow = new GraphicsPath())
            {
                glow.AddEllipse(-Theme.S(160), -Theme.S(220), Theme.S(620), Theme.S(380));
                using (var pb = new PathGradientBrush(glow) { CenterColor = Color.FromArgb(34, Theme.CyanDeep), SurroundColors = new[] { Color.FromArgb(0, Theme.CyanDeep) } })
                    g.FillPath(pb, glow);
            }
            int ls = Theme.S(44);
            var lr = new RectangleF(Theme.S(22), (Height - ls) / 2f, ls, ls);
            Header.DrawLogo(g, lr);
            float x = lr.Right + Theme.S(14), right;
            using (var f1 = new Font("Segoe UI Semibold", 16f))
            using (var f2 = new Font("Segoe UI", 8.75f))
            using (var f3 = new Font("Segoe UI Semibold", 7.5f))
            using (var fs = new Font("Segoe UI Semibold", 8f))
            {
                string a = "FFmpeg Forge";
                var sa = TextRenderer.MeasureText(g, a, f1, Size.Empty, TextFormatFlags.NoPadding);
                int ty = (int)(Height / 2f - sa.Height + Theme.S(3));
                TextRenderer.DrawText(g, a, f1, new Point((int)x, ty), Theme.Text, TextFormatFlags.NoPadding);
                // version pill
                string v = Version;
                var vs = TextRenderer.MeasureText(v, f3);
                var vr = new RectangleF(x + sa.Width + Theme.S(10), ty + (sa.Height - Theme.S(20)) / 2f + Theme.S(1), vs.Width + Theme.S(10), Theme.S(20));
                using (var p = Theme.Round(vr, Theme.S(5)))
                {
                    using (var bb = new SolidBrush(Theme.AccentMuted)) g.FillPath(bb, p);
                    using (var pen = new Pen(Color.FromArgb(150, Theme.Cyan))) g.DrawPath(pen, p);
                }
                TextRenderer.DrawText(g, v, f3, Rectangle.Round(vr), Theme.Cyan, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                string sub = "Convert anything into everything  ·  " + (FormatCount > 0 ? FormatCount : Formats.All.Count) + " formats" + (FilterCount > 0 ? "  ·  " + FilterCount + " filters" : "");
                TextRenderer.DrawText(g, sub, f2, new Point((int)x + Theme.S(1), ty + sa.Height + Theme.S(3)), Theme.Muted, TextFormatFlags.NoPadding);
                right = Math.Max(vr.Right, x + TextRenderer.MeasureText(sub, f2).Width) + Theme.S(22);

                // workflow chips
                float limit = Math.Min(PillLeft, Width) - Theme.S(14);
                int ch = Theme.S(30), isz = Theme.S(13), padx = Theme.S(8), arrow = Theme.S(12);
                var ws = new float[Stages.Length]; float total = 0;
                for (int i = 0; i < Stages.Length; i++) { ws[i] = TextRenderer.MeasureText(Stages[i], fs, Size.Empty, TextFormatFlags.NoPadding).Width + isz + padx * 2 + Theme.S(6); total += ws[i] + (i > 0 ? arrow : 0); }
                bool compact = right + total > limit;
                if (compact) { total = 0; for (int i = 0; i < Stages.Length; i++) { ws[i] = ch; total += ch + (i > 0 ? Theme.S(8) : 0); } }
                if (right + total <= limit)
                {
                    float cx = right + Math.Max(0, (limit - right - total) / 2f), cy = (Height - ch) / 2f;
                    TextRenderer.DrawText(g, "WORKFLOW", f3, new Point((int)cx, (int)cy - Theme.S(14)), Color.FromArgb(110, 116, 128), TextFormatFlags.NoPadding);
                    for (int i = 0; i < Stages.Length; i++)
                    {
                        if (i > 0)
                        {
                            float ax0 = cx + Theme.S(compact ? 1 : 4), ax1 = cx + (compact ? Theme.S(7) : arrow - Theme.S(4)), ay = cy + ch / 2f;
                            using (var pen = new Pen(StageOn[i] && StageOn[i - 1] ? Color.FromArgb(160, Theme.Cyan) : Color.FromArgb(70, 74, 82), Theme.S(1) * 1.4f)) g.DrawLine(pen, ax0, ay, ax1, ay);
                            cx += compact ? Theme.S(8) : arrow;
                        }
                        var r = new RectangleF(cx, cy, ws[i], ch);
                        bool on = StageOn[i];
                        using (var p = Theme.Round(r, Theme.S(6)))
                        {
                            if (on) using (var b = new LinearGradientBrush(r, Color.FromArgb(40, Theme.CyanDeep), Color.FromArgb(40, Theme.Indigo), LinearGradientMode.Horizontal)) g.FillPath(b, p);
                            else using (var b = new SolidBrush(Theme.Card)) g.FillPath(b, p);
                            using (var pen = new Pen(on ? Color.FromArgb(180, Theme.Cyan) : Theme.Border)) g.DrawPath(pen, p);
                        }
                        Color ic = on ? Theme.Cyan : Color.FromArgb(100, 106, 118);
                        if (compact) Icons.Draw(g, StageIcons[i], new RectangleF(r.X + (ch - isz) / 2f, cy + (ch - isz) / 2f, isz, isz), ic);
                        else
                        {
                            Icons.Draw(g, StageIcons[i], new RectangleF(r.X + padx, cy + (ch - isz) / 2f, isz, isz), ic);
                            TextRenderer.DrawText(g, Stages[i], fs, new Rectangle((int)(r.X + padx + isz + Theme.S(6)), (int)cy, (int)(r.Width - padx - isz - Theme.S(6)), ch),
                                on ? Theme.Text : Color.FromArgb(120, 126, 138), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                        }
                        cx += ws[i];
                    }
                }
            }
            using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }
    }

    /// Left navigation sidebar: brand, pages with icons, FFmpeg status.
    public class SideBar : Control
    {
        public class Item { public string Text, Icon; public int Badge; public bool Dim; }
        public readonly System.Collections.Generic.List<Item> Items = new System.Collections.Generic.List<Item>();
        int selected, hoverIdx = -1;
        public int Selected { get { return selected; } set { selected = value; Invalidate(); if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty); } }
        public event EventHandler SelectedChanged;
        public string Right1 = "";
        public Color Right1Color = Theme.Muted;
        public int FilterCount, FormatCount;
        public string Version = "3.0.1";

        public SideBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Width = Theme.S(236);
            BackColor = Theme.Side;
        }

        int Top0 { get { return Theme.S(118); } }
        int ItemH { get { return Theme.S(46); } }
        Rectangle ItemRect(int i) { return new Rectangle(Theme.S(14), Top0 + i * (ItemH + Theme.S(4)), Width - Theme.S(28), ItemH); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = -1;
            for (int i = 0; i < Items.Count; i++) if (ItemRect(i).Contains(e.Location)) h = i;
            if (h != hoverIdx) { hoverIdx = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e) { hoverIdx = -1; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            for (int i = 0; i < Items.Count; i++) if (ItemRect(i).Contains(e.Location)) { Selected = i; break; }
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            using (var bg = new LinearGradientBrush(ClientRectangle, Color.FromArgb(14, 17, 25), Theme.Side, LinearGradientMode.Vertical)) g.FillRectangle(bg, ClientRectangle);
            // blue glow behind the brand
            using (var halo = new GraphicsPath())
            {
                halo.AddEllipse(-Theme.S(110), -Theme.S(120), Theme.S(420), Theme.S(260));
                using (var pb = new PathGradientBrush(halo) { CenterColor = Color.FromArgb(46, Theme.Blue), SurroundColors = new[] { Color.FromArgb(0, Theme.Blue) } })
                    g.FillPath(pb, halo);
            }
            using (var pen = new Pen(Color.FromArgb(30, 36, 48))) g.DrawLine(pen, Width - 1, 0, Width - 1, Height);

            // brand
            int ls = Theme.S(42);
            var lr = new RectangleF(Theme.S(22), Theme.S(26), ls, ls);
            Header.DrawLogo(g, lr);
            using (var f1 = new Font("Segoe UI", 13.5f, FontStyle.Bold))
            using (var f2 = new Font("Segoe UI Semibold", 7.5f))
            {
                float x = lr.Right + Theme.S(12), y = lr.Y - Theme.S(2);
                var sa = g.MeasureString("FFmpeg", f1, PointF.Empty, StringFormat.GenericTypographic);
                using (var w = new SolidBrush(Color.White)) g.DrawString("FFmpeg", f1, w, x, y, StringFormat.GenericTypographic);
                var fr = new RectangleF(x + sa.Width + Theme.S(4), y, Theme.S(80), sa.Height);
                using (var gb = Theme.GoldBrush(fr)) g.DrawString("FORGE", f1, gb, fr.X, y, StringFormat.GenericTypographic);
                // version chip
                string v = "VERSION " + Version;
                var vs = g.MeasureString(v, f2);
                var vr = new RectangleF(x, y + sa.Height + Theme.S(5), vs.Width + Theme.S(8), Theme.S(17));
                using (var p = Theme.Round(vr, vr.Height / 2))
                {
                    using (var b = new SolidBrush(Color.FromArgb(40, Theme.Gold))) g.FillPath(b, p);
                    using (var pen = new Pen(Color.FromArgb(120, Theme.Gold))) g.DrawPath(pen, p);
                }
                TextRenderer.DrawText(g, v, f2, Rectangle.Round(vr), Theme.Gold, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            using (var m = new Font("Segoe UI Semibold", 7.5f))
                TextRenderer.DrawText(g, "WORKSPACE", m, new Point(Theme.S(26), Top0 - Theme.S(24)), Color.FromArgb(92, 102, 124));

            // items
            using (var f = new Font("Segoe UI Semibold", 10f))
            using (var fb = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                for (int i = 0; i < Items.Count; i++)
                {
                    var it = Items[i];
                    var r = ItemRect(i);
                    bool sel = i == selected, hov = i == hoverIdx;
                    if (sel || hov)
                        using (var p = Theme.Round(r, Theme.S(10)))
                        {
                            if (sel)
                                using (var b = new LinearGradientBrush(r, Color.FromArgb(34, 52, 92), Color.FromArgb(20, 28, 44), LinearGradientMode.Horizontal)) g.FillPath(b, p);
                            else using (var b = new SolidBrush(Color.FromArgb(20, 25, 35))) g.FillPath(b, p);
                        }
                    if (sel)
                        using (var p = Theme.Round(new RectangleF(r.X, r.Y + Theme.S(10), Theme.S(4), r.Height - Theme.S(20)), Theme.S(2)))
                        using (var gb = Theme.GoldBrush(r)) g.FillPath(gb, p);
                    Color ic = sel ? Theme.Gold : (it.Dim ? Color.FromArgb(70, 78, 96) : Color.FromArgb(150, 160, 182));
                    var irc = new RectangleF(r.X + Theme.S(16), r.Y + (r.Height - Theme.S(20)) / 2f, Theme.S(20), Theme.S(20));
                    Icons.Draw(g, it.Icon, irc, ic);
                    Color tc = sel ? Color.White : (it.Dim ? Color.FromArgb(80, 88, 106) : Color.FromArgb(196, 204, 220));
                    TextRenderer.DrawText(g, it.Text, f, new Rectangle(r.X + Theme.S(48), r.Y, r.Width - Theme.S(48), r.Height), tc, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                    if (it.Badge > 0)
                    {
                        string bt = it.Badge > 999 ? "999+" : it.Badge.ToString();
                        var bs = TextRenderer.MeasureText(bt, fb);
                        var br = new RectangleF(r.Right - Theme.S(14) - Math.Max(Theme.S(22), bs.Width + Theme.S(4)), r.Y + (r.Height - Theme.S(20)) / 2f, Math.Max(Theme.S(22), bs.Width + Theme.S(4)), Theme.S(20));
                        using (var p = Theme.Round(br, br.Height / 2)) using (var b = Theme.AccentBrush(br)) g.FillPath(b, p);
                        TextRenderer.DrawText(g, bt, fb, Rectangle.Round(br), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    }
                }

            // status card at the bottom
            var sr = new RectangleF(Theme.S(14), Height - Theme.S(150), Width - Theme.S(28), Theme.S(94));
            using (var p = Theme.Round(sr, Theme.S(12)))
            {
                using (var b = new SolidBrush(Color.FromArgb(18, 22, 31))) g.FillPath(b, p);
                using (var pen = new Pen(Color.FromArgb(34, 40, 54))) g.DrawPath(pen, p);
            }
            using (var f = new Font("Segoe UI Semibold", 7.5f))
                TextRenderer.DrawText(g, "ENGINE", f, new Point((int)sr.X + Theme.S(14), (int)sr.Y + Theme.S(10)), Color.FromArgb(92, 102, 124));
            using (var f = new Font("Segoe UI", 8.5f))
            {
                string st = string.IsNullOrEmpty(Right1) ? "FFmpeg…" : Right1;
                TextRenderer.DrawText(g, st, f, new Rectangle((int)sr.X + Theme.S(12), (int)sr.Y + Theme.S(26), (int)sr.Width - Theme.S(20), Theme.S(20)), Right1Color, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                string cnt = (FormatCount > 0 ? FormatCount : Formats.All.Count) + " formats" + (FilterCount > 0 ? "  ·  " + FilterCount + " filters" : "");
                TextRenderer.DrawText(g, cnt, f, new Point((int)sr.X + Theme.S(14), (int)sr.Y + Theme.S(46)), Theme.Muted);
            }
        }
    }

    /// Vector icons (20 × 20 grid), drawn so they stay sharp at any DPI.
    public static class Icons
    {
        public static void Draw(Graphics g, string name, RectangleF r, Color c)
        {
            if (string.IsNullOrEmpty(name)) return;
            var oldSm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            float u = r.Width / 20f;
            Func<float, float, PointF> P = (x, y) => new PointF(r.X + x * u, r.Y + y * u);
            Func<float, float, float, float, RectangleF> R = (x, y, w, h) => new RectangleF(r.X + x * u, r.Y + y * u, w * u, h * u);
            using (var pen = new Pen(c, 1.7f * u) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var thin = new Pen(c, 1.2f * u) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var br = new SolidBrush(c))
            {
                switch (name)
                {
                    case "file":
                        g.DrawLines(pen, new[] { P(12, 2), P(5, 2), P(5, 18), P(16, 18), P(16, 6), P(12, 2), P(12, 6), P(16, 6) });
                        break;
                    case "files":
                        using (var p = Theme.Round(R(4, 2, 11, 15), 2 * u)) g.DrawPath(pen, p);
                        g.DrawLine(pen, P(7, 7), P(12, 7)); g.DrawLine(pen, P(7, 10.5f), P(12, 10.5f)); g.DrawLine(pen, P(7, 14), P(10, 14));
                        g.DrawLine(pen, P(17, 5), P(17, 18)); g.DrawLine(pen, P(17, 18), P(8, 18));
                        break;
                    case "plus":
                        g.DrawLine(pen, P(10, 3.5f), P(10, 16.5f)); g.DrawLine(pen, P(3.5f, 10), P(16.5f, 10));
                        break;
                    case "trash":
                        g.DrawLine(pen, P(3, 5), P(17, 5)); g.DrawLines(pen, new[] { P(8, 5), P(8, 2.8f), P(12, 2.8f), P(12, 5) });
                        g.DrawLines(pen, new[] { P(4.8f, 5), P(5.8f, 17.5f), P(14.2f, 17.5f), P(15.2f, 5) });
                        g.DrawLine(thin, P(8.3f, 8.5f), P(8.6f, 14.5f)); g.DrawLine(thin, P(11.7f, 8.5f), P(11.4f, 14.5f));
                        break;
                    case "broom":
                        g.DrawLine(pen, P(16.5f, 2.5f), P(10.5f, 9.5f));
                        g.FillPolygon(br, new[] { P(9, 8), P(12.5f, 11), P(9.5f, 17.5f), P(2.5f, 13.5f), P(4, 11.5f) });
                        using (var bb = new Pen(Color.FromArgb(160, 0, 0, 0), 0.9f * u)) { g.DrawLine(bb, P(5, 14.6f), P(8, 11)); g.DrawLine(bb, P(7.3f, 15.8f), P(10, 12)); }
                        break;
                    case "terminal":
                        using (var p = Theme.Round(R(2, 3.5f, 16, 13), 2.2f * u)) g.DrawPath(pen, p);
                        g.DrawLines(pen, new[] { P(5.5f, 8), P(8.3f, 10.3f), P(5.5f, 12.6f) }); g.DrawLine(pen, P(10, 13), P(14, 13));
                        break;
                    case "funnel":
                        g.DrawPolygon(pen, new[] { P(2.5f, 3.5f), P(17.5f, 3.5f), P(11.5f, 10.5f), P(11.5f, 16.5f), P(8.5f, 15), P(8.5f, 10.5f) });
                        break;
                    case "disc":
                        g.DrawEllipse(pen, R(2, 2, 16, 16));
                        g.DrawEllipse(thin, R(7.6f, 7.6f, 4.8f, 4.8f));
                        g.DrawArc(thin, R(4.6f, 4.6f, 10.8f, 10.8f), 200, 70);
                        break;
                    case "folder":
                        g.DrawLines(pen, new[] { P(2, 6), P(2, 16.5f), P(18, 16.5f), P(18, 7), P(10, 7), P(8, 4.5f), P(2, 4.5f), P(2, 6) });
                        break;
                    case "x":
                        g.DrawLine(pen, P(4.5f, 4.5f), P(15.5f, 15.5f)); g.DrawLine(pen, P(15.5f, 4.5f), P(4.5f, 15.5f));
                        break;
                    case "refresh":
                        g.DrawArc(pen, R(3, 3, 14, 14), 200, 230); g.DrawArc(pen, R(3, 3, 14, 14), 20, 120);
                        g.FillPolygon(br, new[] { P(15.6f, 1.6f), P(18.4f, 7.8f), P(12.3f, 6.9f) });
                        g.FillPolygon(br, new[] { P(4.4f, 18.4f), P(1.6f, 12.2f), P(7.7f, 13.1f) });
                        break;
                    case "search":
                        g.DrawEllipse(pen, R(3, 3, 10.5f, 10.5f)); g.DrawLine(pen, P(11.8f, 11.8f), P(17, 17));
                        break;
                    case "check":
                        g.FillEllipse(br, R(1, 1, 18, 18));
                        using (var w = new Pen(Color.FromArgb(14, 20, 30), 2.1f * u) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                            g.DrawLines(w, new[] { P(5.8f, 10.3f), P(8.8f, 13.2f), P(14.4f, 7.2f) });
                        break;
                    case "info":
                        g.FillEllipse(br, R(1, 1, 18, 18));
                        using (var w = new SolidBrush(Color.FromArgb(14, 20, 30))) { g.FillEllipse(w, R(8.7f, 4.6f, 2.6f, 2.6f)); g.FillRectangle(w, R(8.8f, 8.6f, 2.4f, 7)); }
                        break;
                    case "error":
                        g.FillEllipse(br, R(1, 1, 18, 18));
                        using (var w = new Pen(Color.FromArgb(14, 20, 30), 2.1f * u) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        { g.DrawLine(w, P(6.8f, 6.8f), P(13.2f, 13.2f)); g.DrawLine(w, P(13.2f, 6.8f), P(6.8f, 13.2f)); }
                        break;
                    case "warn":
                        using (var p = new GraphicsPath()) { p.AddPolygon(new[] { P(10, 1.8f), P(19, 17.8f), P(1, 17.8f) }); g.FillPath(br, p); }
                        using (var w = new SolidBrush(Color.FromArgb(14, 20, 30))) { g.FillRectangle(w, R(8.9f, 6.8f, 2.2f, 6)); g.FillEllipse(w, R(8.8f, 14.1f, 2.4f, 2.4f)); }
                        break;
                    case "film":
                        using (var p = Theme.Round(R(2, 2.5f, 16, 15), 2 * u)) g.DrawPath(pen, p);
                        g.DrawLine(thin, P(5.5f, 2.5f), P(5.5f, 17.5f)); g.DrawLine(thin, P(14.5f, 2.5f), P(14.5f, 17.5f)); g.DrawLine(thin, P(5.5f, 10), P(14.5f, 10));
                        foreach (var y in new[] { 5.3f, 8.5f, 11.7f, 14.9f }) { g.FillRectangle(br, R(2.9f, y - 0.6f, 1.6f, 1.2f)); g.FillRectangle(br, R(15.5f, y - 0.6f, 1.6f, 1.2f)); }
                        break;
                    case "video":
                        using (var p = Theme.Round(R(2, 4, 16, 12), 2.5f * u)) g.DrawPath(pen, p);
                        g.FillPolygon(br, new[] { P(8.3f, 7.2f), P(13.3f, 10), P(8.3f, 12.8f) });
                        break;
                    case "note":
                        g.DrawLine(pen, P(7.5f, 15), P(7.5f, 3.5f)); g.DrawLine(pen, P(7.5f, 3.5f), P(16.5f, 2.5f)); g.DrawLine(pen, P(16.5f, 2.5f), P(16.5f, 13.5f));
                        g.FillEllipse(br, R(2.8f, 12.6f, 5.4f, 4.6f)); g.FillEllipse(br, R(11.8f, 11.2f, 5.4f, 4.6f));
                        break;
                    case "image":
                        using (var p = Theme.Round(R(2, 3, 16, 14), 2 * u)) g.DrawPath(pen, p);
                        g.FillEllipse(br, R(5, 5.8f, 3.2f, 3.2f));
                        g.DrawLines(thin, new[] { P(3, 15.5f), P(8.5f, 10.5f), P(11.5f, 13), P(14, 10.8f), P(17.2f, 14) });
                        break;
                    case "stack":
                        foreach (var y in new[] { 4f, 9f, 14f })
                        {
                            var e = R(3, y - 2.6f, 14, 5.2f);
                            if (y < 5) g.FillEllipse(br, e); else g.DrawArc(pen, e, 0, 180);
                        }
                        g.DrawLine(pen, P(3, 4), P(3, 14)); g.DrawLine(pen, P(17, 4), P(17, 14));
                        break;
                    case "frame":
                        using (var p = Theme.Round(R(2.5f, 2.5f, 15, 15), 2.2f * u)) g.DrawPath(pen, p);
                        g.DrawLines(thin, new[] { P(6.5f, 9.5f), P(6.5f, 6.5f), P(9.5f, 6.5f) });
                        g.DrawLines(thin, new[] { P(13.5f, 10.5f), P(13.5f, 13.5f), P(10.5f, 13.5f) });
                        g.DrawLine(thin, P(7, 13), P(13, 7));
                        break;
                    case "upscale":
                        using (var p = Theme.Round(R(2.5f, 2.5f, 15, 15), 2.2f * u)) g.DrawPath(thin, p);
                        g.DrawLine(pen, P(6.5f, 13.5f), P(13.5f, 6.5f)); g.DrawLines(pen, new[] { P(9, 6.5f), P(13.5f, 6.5f), P(13.5f, 11) });
                        break;
                    case "ai":
                    case "sparkle":
                        {
                            Func<float, float, float, PointF[]> star = (cx, cy, sz) => new[] {
                                P(cx, cy - sz), P(cx + sz * 0.26f, cy - sz * 0.26f), P(cx + sz, cy), P(cx + sz * 0.26f, cy + sz * 0.26f),
                                P(cx, cy + sz), P(cx - sz * 0.26f, cy + sz * 0.26f), P(cx - sz, cy), P(cx - sz * 0.26f, cy - sz * 0.26f) };
                            g.FillPolygon(br, star(8.5f, 11, 7.2f));
                            g.FillPolygon(br, star(16, 4.5f, 3.2f));
                            g.FillPolygon(br, star(16.2f, 15.8f, 2));
                        }
                        break;
                    case "denoise":
                        {
                            Func<float, float, float, PointF[]> star = (cx, cy, sz) => new[] {
                                P(cx, cy - sz), P(cx + sz * 0.26f, cy - sz * 0.26f), P(cx + sz, cy), P(cx + sz * 0.26f, cy + sz * 0.26f),
                                P(cx, cy + sz), P(cx - sz * 0.26f, cy + sz * 0.26f), P(cx - sz, cy), P(cx - sz * 0.26f, cy - sz * 0.26f) };
                            g.FillPolygon(br, star(9, 10.5f, 6.5f));
                            foreach (var d in new[] { new[] { 16f, 4f }, new[] { 17f, 9f }, new[] { 15.5f, 15f }, new[] { 3f, 4f } }) g.FillEllipse(br, R(d[0] - 0.9f, d[1] - 0.9f, 1.8f, 1.8f));
                        }
                        break;
                    case "cube":
                        g.DrawPolygon(pen, new[] { P(10, 2), P(17.5f, 6), P(17.5f, 14), P(10, 18), P(2.5f, 14), P(2.5f, 6) });
                        g.DrawLine(pen, P(2.5f, 6), P(10, 10)); g.DrawLine(pen, P(17.5f, 6), P(10, 10)); g.DrawLine(pen, P(10, 10), P(10, 18));
                        break;
                    case "format":
                        g.DrawPolygon(pen, new[] { P(10, 2), P(18, 6), P(18, 14), P(10, 18), P(2, 14), P(2, 6) });
                        g.DrawLine(pen, P(2, 6), P(10, 10)); g.DrawLine(pen, P(18, 6), P(10, 10)); g.DrawLine(pen, P(10, 10), P(10, 18));
                        break;
                    case "speaker":
                        g.FillPolygon(br, new[] { P(2.5f, 7.5f), P(6, 7.5f), P(10.5f, 3.5f), P(10.5f, 16.5f), P(6, 12.5f), P(2.5f, 12.5f) });
                        g.DrawArc(pen, R(9.5f, 6.5f, 5, 7), -55, 110); g.DrawArc(pen, R(8.5f, 3.5f, 9.5f, 13), -55, 110);
                        break;
                    case "audio":
                    case "wave":
                        foreach (var x in new[] { new[] { 2f, 8f, 12f }, new[] { 5.5f, 5f, 15f }, new[] { 9f, 2.5f, 17.5f }, new[] { 12.5f, 6f, 14f }, new[] { 16f, 8.5f, 11.5f } })
                            g.DrawLine(pen, P(x[0] + 1, x[1]), P(x[0] + 1, x[2]));
                        break;
                    case "sliders":
                    case "filters":
                        g.DrawLine(pen, P(3, 5), P(17, 5)); g.DrawLine(pen, P(3, 10), P(17, 10)); g.DrawLine(pen, P(3, 15), P(17, 15));
                        foreach (var k in new[] { new[] { 7f, 5f }, new[] { 13f, 10f }, new[] { 9f, 15f } })
                        { var cr = R(k[0] - 2, k[1] - 2, 4, 4); using (var bb = new SolidBrush(Theme.Panel)) g.FillEllipse(bb, cr); g.DrawEllipse(pen, cr); }
                        break;
                    case "scissors":
                        g.DrawEllipse(pen, R(2.5f, 12, 5, 5)); g.DrawEllipse(pen, R(12.5f, 12, 5, 5));
                        g.DrawLine(pen, P(6.6f, 12.6f), P(15, 2.5f)); g.DrawLine(pen, P(13.4f, 12.6f), P(5, 2.5f));
                        break;
                    case "chip":
                        using (var p = Theme.Round(R(4.5f, 4.5f, 11, 11), 1.6f * u)) g.DrawPath(pen, p);
                        g.FillRectangle(br, R(8, 8, 4, 4));
                        foreach (var k in new[] { 7.5f, 10f, 12.5f }) { g.DrawLine(thin, P(k, 1.8f), P(k, 4.5f)); g.DrawLine(thin, P(k, 15.5f), P(k, 18.2f)); g.DrawLine(thin, P(1.8f, k), P(4.5f, k)); g.DrawLine(thin, P(15.5f, k), P(18.2f, k)); }
                        break;
                    case "clock":
                        g.DrawEllipse(pen, R(2.5f, 2.5f, 15, 15)); g.DrawLines(pen, new[] { P(10, 5.8f), P(10, 10), P(13, 12) });
                        break;
                    case "output":
                        g.DrawLines(pen, new[] { P(2, 6), P(2, 16.5f), P(18, 16.5f), P(18, 7), P(10, 7), P(8, 4.5f), P(2, 4.5f), P(2, 6) });
                        g.DrawLine(pen, P(10, 9.5f), P(10, 14)); g.DrawLine(pen, P(7.8f, 11.8f), P(10, 14)); g.DrawLine(pen, P(12.2f, 11.8f), P(10, 14));
                        break;
                    case "code":
                        g.DrawLines(pen, new[] { P(6.5f, 5), P(2, 10), P(6.5f, 15) });
                        g.DrawLines(pen, new[] { P(13.5f, 5), P(18, 10), P(13.5f, 15) });
                        g.DrawLine(pen, P(11.5f, 3.5f), P(8.5f, 16.5f));
                        break;
                    case "log":
                        g.DrawPolygon(pen, new[] { P(10, 2), P(18, 10), P(10, 18), P(2, 10) });
                        g.DrawLine(thin, P(7, 9), P(13, 9)); g.DrawLine(thin, P(7, 12), P(11, 12));
                        break;
                    case "erase":
                        g.DrawPolygon(pen, new[] { P(8, 16), P(2.8f, 10.8f), P(10.8f, 2.8f), P(17.2f, 9.2f), P(10.4f, 16) });
                        g.DrawLine(thin, P(6, 7.6f), P(12.4f, 14)); g.DrawLine(pen, P(10.4f, 16), P(17.5f, 16));
                        break;
                    case "dot":
                        g.FillEllipse(br, R(5, 5, 10, 10));
                        break;
                    default:
                        g.DrawEllipse(pen, R(4, 4, 12, 12));
                        break;
                }
            }
            g.SmoothingMode = oldSm;
        }
    }

    /// Big page title shown above each page.
    public class PageHeader : Control
    {
        public string Title = "", Sub = "", Chip = "";
        public PageHeader()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = Theme.S(84);
            BackColor = Theme.Bg;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(BackColor);
            using (var halo = new GraphicsPath())
            {
                halo.AddEllipse(Width - Theme.S(420), -Theme.S(200), Theme.S(620), Theme.S(330));
                using (var pb = new PathGradientBrush(halo) { CenterColor = Color.FromArgb(22, Theme.Gold), SurroundColors = new[] { Color.FromArgb(0, Theme.Gold) } })
                    g.FillPath(pb, halo);
            }
            int x = Theme.S(28);
            using (var f1 = new Font("Segoe UI Semibold", 19f))
            using (var f2 = new Font("Segoe UI", 9.5f))
            {
                TextRenderer.DrawText(g, Title, f1, new Point(x - Theme.S(4), Theme.S(14)), Color.White, TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(g, Sub, f2, new Point(x, Theme.S(52)), Theme.Muted, TextFormatFlags.NoPrefix);
            }
            if (!string.IsNullOrEmpty(Chip))
                using (var f = new Font("Segoe UI Semibold", 9f))
                {
                    var cs = TextRenderer.MeasureText(Chip, f, Size.Empty, TextFormatFlags.NoPrefix);
                    var cr = new RectangleF(Width - cs.Width - Theme.S(56), Theme.S(26), cs.Width + Theme.S(28), Theme.S(30));
                    using (var p = Theme.Round(cr, cr.Height / 2))
                    {
                        using (var b = new SolidBrush(Color.FromArgb(26, 31, 42))) g.FillPath(b, p);
                        using (var pen = new Pen(Color.FromArgb(140, Theme.Gold))) g.DrawPath(pen, p);
                    }
                    using (var d = new SolidBrush(Theme.Gold)) g.FillEllipse(d, cr.X + Theme.S(12), cr.Y + cr.Height / 2 - Theme.S(3), Theme.S(6), Theme.S(6));
                    TextRenderer.DrawText(g, Chip, f, new Rectangle((int)cr.X + Theme.S(22), (int)cr.Y, (int)cr.Width - Theme.S(26), (int)cr.Height), Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
                }
            using (var pen = new Pen(Color.FromArgb(28, 33, 44))) g.DrawLine(pen, x, Height - 1, Width - Theme.S(28), Height - 1);
        }
    }

    /// Dark, double-buffered ListView.
    public class DarkList : ListView
    {
        public DarkList()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            DoubleBuffered = true;
            OwnerDraw = true;
            View = View.Details;
            FullRowSelect = true;
            BorderStyle = BorderStyle.None;
            BackColor = Theme.Panel;
            ForeColor = Theme.Text;
            HeaderStyle = ColumnHeaderStyle.Nonclickable;
            SmallImageList = new ImageList { ImageSize = new Size(1, Theme.S(30)) };   // taller rows (2.1.6 design)
        }
    }

    /// Drag & drop zone shown when the list is empty (2.1.6: film artwork, big logo, coloured feature chips).
    public class DropHint : Control
    {
        public bool Highlight;
        public DropHint()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Panel;
            Cursor = Cursors.Hand;
        }

        static readonly string[] Chips = { "Video", "Audio", "Image", "AI Denoise", "Deblur", "AI Upscale", "Dolby Atmos", "DSD" };
        static readonly string[] ChipIcons = { "film", "note", "image", "denoise", "frame", "upscale", "speaker", "wave" };

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(BackColor);
            // soft cyan / indigo light in the middle of the drop zone
            using (var glow = new GraphicsPath())
            {
                glow.AddEllipse(Width * 0.1f, Height * 0.05f, Width * 0.8f, Height * 0.9f);
                using (var pb = new PathGradientBrush(glow) { CenterColor = Color.FromArgb(Highlight ? 46 : 22, Theme.Indigo), SurroundColors = new[] { Color.FromArgb(0, Theme.Indigo) } })
                    g.FillPath(pb, glow);
            }
            var r = new RectangleF(Theme.S(14), Theme.S(14), Width - Theme.S(28), Height - Theme.S(28));
            using (var p = Theme.Round(r, Theme.S(8)))
            using (var pen = new Pen(Highlight ? Theme.Cyan : Color.FromArgb(72, 76, 86), Theme.S(1) * 1.3f) { DashStyle = DashStyle.Dash })
                g.DrawPath(pen, p);

            float cy = Height / 2f;
            int ls = Theme.S(Math.Min(76, Math.Max(52, Height / 6)));
            float block = ls + Theme.S(150);
            float top = Math.Max(Theme.S(24), cy - block / 2f);
            var lr = new RectangleF((Width - ls) / 2f, top, ls, ls);
            Header.DrawLogo(g, lr);
            using (var f1 = new Font("Segoe UI Semibold", 18f))
            using (var f2 = new Font("Segoe UI", 10f))
            using (var f3 = new Font("Segoe UI Semibold", 8.5f))
            {
                string a = "Drop your media here";
                var sa = TextRenderer.MeasureText(g, a, f1);
                float ty = lr.Bottom + Theme.S(18);
                TextRenderer.DrawText(g, a, f1, new Rectangle(0, (int)ty, Width, sa.Height), Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(g, "or click to browse  ·  videos, music, pictures, whole folders", f2, new Rectangle(0, (int)(ty + sa.Height + Theme.S(6)), Width, Theme.S(24)),
                    Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);

                // capability chips (flat, outlined)
                int ch = Theme.S(30), isz = Theme.S(14), pad = Theme.S(11), gap = Theme.S(8);
                var ws = new float[Chips.Length]; float total = 0;
                for (int i = 0; i < Chips.Length; i++) { ws[i] = TextRenderer.MeasureText(Chips[i], f3).Width + isz + pad * 2 + Theme.S(2); total += ws[i] + gap; }
                total -= gap;
                float scale = total > Width - Theme.S(60) ? (Width - Theme.S(60)) / total : 1f;
                float x = (Width - total * scale) / 2f, y = ty + sa.Height + Theme.S(46);
                for (int i = 0; i < Chips.Length; i++)
                {
                    var cr = new RectangleF(x, y, ws[i] * scale, ch);
                    using (var p = Theme.Round(cr, Theme.S(6)))
                    {
                        using (var b2 = new SolidBrush(Theme.Card)) g.FillPath(b2, p);
                        using (var pen = new Pen(Theme.Border)) g.DrawPath(pen, p);
                    }
                    float ix = cr.X + pad * scale;
                    Icons.Draw(g, ChipIcons[i], new RectangleF(ix, cr.Y + (ch - isz) / 2f, isz, isz), Theme.Cyan);
                    TextRenderer.DrawText(g, Chips[i], f3, new Rectangle((int)(ix + isz + Theme.S(6)), (int)cr.Y, (int)(cr.Right - ix - isz - Theme.S(6)), ch), Color.FromArgb(214, 219, 228),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                    x += (ws[i] + gap) * scale;
                }
            }
        }
    }
}
