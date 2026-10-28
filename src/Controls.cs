using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace S136Battery
{
    static class Theme
    {
        public static readonly Color Back = Color.Black;
        public static readonly Color Card = Color.FromArgb(28, 29, 32);
        public static readonly Color CardHot = Color.FromArgb(38, 39, 43);
        public static readonly Color CardOff = Color.FromArgb(52, 53, 57);
        public static readonly Color Line = Color.FromArgb(40, 40, 44);
        public static readonly Color Text = Color.FromArgb(240, 240, 244);
        public static readonly Color Dim = Color.FromArgb(140, 140, 148);
        public static readonly Color Accent = Color.FromArgb(232, 20, 24);
        public static readonly Color Track = Color.FromArgb(58, 58, 64);

        public static Font Ui(float size, FontStyle st = FontStyle.Regular) { return new Font("Segoe UI", size, st); }
        public static Font Bold(float size) { return new Font("Segoe UI Semibold", size, FontStyle.Bold); }
    }

    abstract class Painted : Control
    {
        protected bool Hot;

        protected Painted()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
        }

        protected override void OnMouseEnter(EventArgs e) { Hot = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Hot = false; Invalidate(); base.OnMouseLeave(e); }

        protected static void Prep(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        }

        protected static void Draw(Graphics g, string s, Font f, Color c, RectangleF r, StringAlignment h = StringAlignment.Near, StringAlignment v = StringAlignment.Center)
        {
            using (var sf = new StringFormat { Alignment = h, LineAlignment = v, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            using (var b = new SolidBrush(c)) g.DrawString(s, f, b, r, sf);
        }
    }

    // Tarjeta de aparato en la pantalla principal.
    class DeviceCard : Painted
    {
        public string Title;
        public Image Picture;
        int level = -2;
        readonly Font titleFont = Theme.Bold(17f), small = Theme.Ui(9.5f);

        public int Level { get { return level; } set { level = value; Invalidate(); } }

        public DeviceCard() { Cursor = Cursors.Hand; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Prep(g);
            g.Clear(Parent != null ? Parent.BackColor : Theme.Back);
            bool off = level == -1;
            var rc = new RectangleF(0, 0, Width - 1, Height - 1);
            using (var p = Art.Round(rc, 8))
            using (var b = new SolidBrush(off ? Theme.CardOff : Hot ? Theme.CardHot : Theme.Card)) g.FillPath(b, p);

            float pad = 24;
            Color fg = off ? Color.FromArgb(150, 150, 156) : Theme.Text;
            Draw(g, Title, titleFont, fg, new RectangleF(pad, 30, Width - 2 * pad, 40));
            if (level >= 0)
            {
                Art.DrawWireless(g, new RectangleF(pad, 84, 18, 13), Theme.Dim);
                Art.DrawBattery(g, new RectangleF(pad + 28, 84, 24, 12), level, Theme.Dim);
                Draw(g, level + "%", small, Theme.Dim, new RectangleF(pad + 58, 78, 80, 24));
            }
            else
            {
                Draw(g, level == -1 ? "Apagado o dormido" : "Buscando…", small, Theme.Dim, new RectangleF(pad, 78, Width - 2 * pad, 24));
            }

            if (Picture != null)
            {
                var box = new RectangleF(pad, 120, Width - 2 * pad, Height - 170);
                float s = Math.Min(box.Width / Picture.Width, box.Height / Picture.Height);
                float w = Picture.Width * s, h = Picture.Height * s;
                var dst = new RectangleF(box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h);
                if (off)
                    using (var ia = new System.Drawing.Imaging.ImageAttributes())
                    {
                        var m = new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.35f };
                        ia.SetColorMatrix(m);
                        g.DrawImage(Picture, Rectangle.Round(dst), 0, 0, Picture.Width, Picture.Height, GraphicsUnit.Pixel, ia);
                    }
                else g.DrawImage(Picture, dst);
            }
        }
    }

    // Deslizador con pasos (0..Max).
    class StepSlider : Painted
    {
        int value, max = 4;
        public event EventHandler ValueChanged;
        public int Max { get { return max; } set { max = value; Invalidate(); } }
        public int Value
        {
            get { return value; }
            set
            {
                int v = Math.Max(0, Math.Min(max, value));
                if (v == this.value) return;
                this.value = v;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        public StepSlider() { Cursor = Cursors.Hand; Height = 28; }

        float X(int v) { return 10 + (Width - 20) * v / (float)max; }

        protected override void OnMouseDown(MouseEventArgs e) { Pick(e.X); Capture = true; }
        protected override void OnMouseMove(MouseEventArgs e) { if (Capture) Pick(e.X); }
        protected override void OnMouseUp(MouseEventArgs e) { Capture = false; }
        void Pick(int x) { Value = (int)Math.Round((x - 10) * max / (float)(Width - 20)); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Prep(g);
            g.Clear(Parent != null ? Parent.BackColor : Theme.Back);
            float cy = Height / 2f;
            using (var t = new Pen(Theme.Track, 4) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(t, X(0), cy, X(max), cy);
            using (var a = new Pen(Theme.Accent, 4) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(a, X(0), cy, X(value), cy);
            using (var d = new SolidBrush(Theme.Track))
                for (int i = 0; i <= max; i++) if (i > value) g.FillEllipse(d, X(i) - 3, cy - 3, 6, 6);
            using (var k = new SolidBrush(Color.White)) g.FillEllipse(k, X(value) - 8, cy - 8, 16, 16);
        }
    }

    class Toggle : Painted
    {
        bool on;
        public string Label;
        public event EventHandler Changed;
        public bool On
        {
            get { return on; }
            set { if (on == value) return; on = value; Invalidate(); if (Changed != null) Changed(this, EventArgs.Empty); }
        }

        readonly Font font = Theme.Ui(10f);
        public Toggle() { Cursor = Cursors.Hand; Height = 32; }
        protected override void OnClick(EventArgs e) { On = !On; base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Prep(g);
            g.Clear(Parent != null ? Parent.BackColor : Theme.Back);
            var sw = new RectangleF(Width - 46, Height / 2f - 11, 44, 22);
            using (var p = Art.Round(sw, 11))
            using (var b = new SolidBrush(on ? Theme.Accent : Theme.Track)) g.FillPath(b, p);
            float kx = on ? sw.Right - 19 : sw.X + 3;
            using (var k = new SolidBrush(Color.White)) g.FillEllipse(k, kx, sw.Y + 3, 16, 16);
            Draw(g, Label, font, Theme.Text, new RectangleF(0, 0, Width - 56, Height));
        }
    }

    // Botón de texto plano o relleno.
    class FlatButton : Painted
    {
        public bool Filled, Selected;
        readonly Font font = Theme.Ui(10f);
        public FlatButton() { Cursor = Cursors.Hand; Height = 34; }

        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Prep(g);
            g.Clear(Parent != null ? Parent.BackColor : Theme.Back);
            var rc = new RectangleF(0, 0, Width - 1, Height - 1);
            Color bg = Filled ? (Hot ? Color.FromArgb(255, 50, 50) : Theme.Accent)
                     : Selected ? Color.FromArgb(70, 232, 20, 24)
                     : Hot ? Theme.CardHot : Theme.Card;
            using (var p = Art.Round(rc, 6))
            {
                using (var b = new SolidBrush(bg)) g.FillPath(b, p);
                if (Selected) using (var pen = new Pen(Theme.Accent, 1.5f)) g.DrawPath(pen, p);
            }
            Draw(g, Text, font, Enabled ? Theme.Text : Theme.Dim, rc, StringAlignment.Center);
        }
    }

    class Swatch : Painted
    {
        public Color Color;
        public bool Selected, Custom;
        public Swatch() { Cursor = Cursors.Hand; Size = new Size(28, 28); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Prep(g);
            g.Clear(Parent != null ? Parent.BackColor : Theme.Back);
            if (Selected || Hot)
                using (var pen = new Pen(Selected ? Color.White : Theme.Dim, 2)) g.DrawEllipse(pen, 1, 1, Width - 3, Height - 3);
            var r = new RectangleF(5, 5, Width - 11, Height - 11);
            if (Custom && !Selected)
            {
                // Rueda de color con "+" para elegir uno propio.
                using (var pb = new PathGradientBrush(new[] { new PointF(r.X, r.Y), new PointF(r.Right, r.Y), new PointF(r.Right, r.Bottom), new PointF(r.X, r.Bottom) })
                { CenterColor = Color.White, SurroundColors = new[] { Color.Red, Color.Yellow, Color.Lime, Color.Blue } })
                    g.FillEllipse(pb, r);
                using (var pen = new Pen(Color.Black, 2f))
                {
                    g.DrawLine(pen, Width / 2f, r.Y + 4, Width / 2f, r.Bottom - 4);
                    g.DrawLine(pen, r.X + 4, Height / 2f, r.Right - 4, Height / 2f);
                }
                return;
            }
            using (var b = new SolidBrush(Color)) g.FillEllipse(b, r);
        }
    }
}
