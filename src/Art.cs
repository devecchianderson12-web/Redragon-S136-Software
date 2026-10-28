using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace S136Battery
{
    // Dibujos vectoriales: icono de la app, batería y señal inalámbrica.
    static class Art
    {
        public static readonly Color Red = Color.FromArgb(232, 20, 24);
        static readonly Color RedGlow = Color.FromArgb(255, 70, 60);
        static readonly Color Charcoal = Color.FromArgb(52, 52, 56);
        static readonly Color Body = Color.FromArgb(24, 24, 26);
        static readonly Color Key = Color.FromArgb(40, 40, 44);
        static readonly Color Shell = Color.FromArgb(214, 214, 218);

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // Icono de la app en un cuadro de 'size' px. Bajo 32 px se simplifica.
        public static void DrawLogo(Graphics g, float x, float y, float size)
        {
            var st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TranslateTransform(x, y);
            g.ScaleTransform(size / 100f, size / 100f);
            bool small = size < 32;

            float ring = small ? 7f : 4.6f;
            using (var red = new Pen(Red, ring) { StartCap = LineCap.Flat, EndCap = LineCap.Flat })
            using (var gray = new Pen(Charcoal, ring))
            {
                var rc = new RectangleF(6, 6, 88, 88);
                g.DrawArc(red, rc, 192, 100);
                g.DrawArc(gray, rc, 300, 34);
                g.DrawArc(red, rc, 30, 92);
                g.DrawArc(gray, rc, 130, 22);
            }

            if (!small) DrawKeyboard(g);
            DrawBattery(g, small);
            if (!small) DrawMouse(g);
            g.Restore(st);
        }

        static void DrawKeyboard(Graphics g)
        {
            var st = g.Save();
            g.TranslateTransform(27, 55);
            g.RotateTransform(-14);
            var body = new RectangleF(-22, -17, 44, 34);
            using (var p = Round(body, 3.5f))
            {
                using (var b = new SolidBrush(Body)) g.FillPath(b, p);
                using (var pen = new Pen(Red, 1.6f)) g.DrawPath(pen, p);
            }
            using (var glow = new SolidBrush(Color.FromArgb(150, RedGlow)))
            using (var key = new SolidBrush(Key))
            {
                for (int row = 0; row < 5; row++)
                    for (int col = 0; col < 6; col++)
                    {
                        var k = new RectangleF(-19.5f + col * 6.6f, -14.5f + row * 6.0f, 5.4f, 4.8f);
                        g.FillRectangle(glow, k.X - 0.5f, k.Bottom - 0.3f, k.Width + 1, 0.9f);
                        using (var kp = Round(k, 1f)) g.FillPath(key, kp);
                    }
            }
            g.Restore(st);
        }

        static void DrawBattery(Graphics g, bool small)
        {
            float w = small ? 34 : 28, h = small ? 56 : 48;
            float x = 50 - w / 2, y = small ? 28 : 31;
            var cap = new RectangleF(50 - w * 0.22f, y - 6, w * 0.44f, 8);
            var outer = new RectangleF(x, y, w, h);
            using (var shell = new SolidBrush(Shell))
            {
                using (var cp = Round(cap, 2.5f)) g.FillPath(shell, cp);
                using (var op = Round(outer, 6)) g.FillPath(shell, op);
            }
            using (var edge = new Pen(Color.FromArgb(20, 20, 22), small ? 2.4f : 1.4f))
            using (var op = Round(outer, 6)) g.DrawPath(edge, op);
            var inner = RectangleF.Inflate(outer, -3.2f, -3.2f);
            using (var ip = Round(inner, 4))
            using (var b = new SolidBrush(Color.FromArgb(14, 10, 10))) g.FillPath(b, ip);
            int bars = small ? 3 : 5;
            float gap = small ? 3f : 2.2f;
            float bh = (inner.Height - 4 - gap * (bars - 1)) / bars;
            for (int i = 0; i < bars; i++)
            {
                var r = new RectangleF(inner.X + 3, inner.Y + 2 + i * (bh + gap), inner.Width - 6, bh);
                using (var rp = Round(r, 1.6f))
                {
                    if (!small)
                        using (var halo = new Pen(Color.FromArgb(90, RedGlow), 2f)) g.DrawPath(halo, rp);
                    using (var lg = new LinearGradientBrush(r, RedGlow, Red, LinearGradientMode.Vertical)) g.FillPath(lg, rp);
                }
            }
        }

        static void DrawMouse(Graphics g)
        {
            var st = g.Save();
            g.TranslateTransform(78, 56);
            g.RotateTransform(8);
            var p = new GraphicsPath();
            p.AddBezier(-3, -22, 6, -24, 13, -18, 14, -6);
            p.AddBezier(14, -6, 16, 6, 14, 18, 2, 22);
            p.AddBezier(2, 22, -8, 25, -15, 16, -15, 4);
            p.AddBezier(-15, 4, -15, -10, -12, -21, -3, -22);
            p.CloseFigure();
            using (var b = new SolidBrush(Body)) g.FillPath(b, p);
            using (var pen = new Pen(Color.FromArgb(10, 10, 12), 1.2f)) g.DrawPath(pen, p);
            using (var red = new Pen(Red, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawBezier(red, -12, -10, -14, 0, -13, 10, -8, 18);
                g.DrawBezier(red, 11, -8, 13, 2, 12, 12, 7, 18);
                g.DrawLine(red, -1.5f, -18, -1.5f, -9);
            }
            using (var seam = new Pen(Color.FromArgb(70, 70, 76), 0.9f)) g.DrawLine(seam, -1.5f, -9, -1.5f, 2);
            p.Dispose();
            g.Restore(st);
        }

        // Batería horizontal estilo bandeja. level <0 = sin dato.
        public static void DrawBattery(Graphics g, RectangleF r, int level, Color fg)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var body = new RectangleF(r.X, r.Y, r.Width - r.Height * 0.18f, r.Height);
            using (var pen = new Pen(fg, Math.Max(1f, r.Height / 9f)))
            using (var bp = Round(body, r.Height * 0.18f)) g.DrawPath(pen, bp);
            using (var b = new SolidBrush(fg))
                g.FillRectangle(b, body.Right + 0.5f, r.Y + r.Height * 0.3f, r.Height * 0.14f, r.Height * 0.4f);
            if (level < 0) return;
            float pad = Math.Max(2f, r.Height / 5f);
            var fill = RectangleF.Inflate(body, -pad, -pad);
            fill.Width = Math.Max(1f, fill.Width * level / 100f);
            using (var b = new SolidBrush(LevelColor(level, fg))) g.FillRectangle(b, fill);
        }

        public static Color LevelColor(int level, Color normal)
        {
            if (level < 0) return Color.FromArgb(120, 120, 128);
            if (level <= 15) return Color.FromArgb(240, 70, 70);
            if (level <= 30) return Color.FromArgb(240, 180, 50);
            return normal;
        }

        // Ondas de señal inalámbrica (abanico hacia arriba).
        public static void DrawWireless(Graphics g, RectangleF r, Color fg)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = r.X + r.Width / 2, by = r.Bottom;
            using (var pen = new Pen(fg, Math.Max(1.2f, r.Height / 8f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                for (int i = 1; i <= 3; i++)
                {
                    float rad = r.Height * i / 3f;
                    g.DrawArc(pen, cx - rad, by - rad, rad * 2, rad * 2, 225, 90);
                }
            using (var b = new SolidBrush(fg))
                g.FillEllipse(b, cx - r.Height / 9f, by - r.Height / 4.5f, r.Height / 4.5f, r.Height / 4.5f);
        }
    }
}
