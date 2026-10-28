using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace S136Battery
{
    // Menú de clic derecho en la bandeja: aparatos con batería y acciones.
    class TrayPopup : Form
    {
        static readonly Color Bg = Color.FromArgb(32, 32, 34);
        static readonly Color Hover = Color.FromArgb(52, 52, 56);
        static readonly Color Sep = Color.FromArgb(58, 58, 62);
        const int Pad = 6, DevH = 58, ItemH = 38;

        public int MouseLevel = -2, KeyboardLevel = -2;
        public event Action<int> ItemClicked;
        readonly string[] items = { "Dispositivos", "Ajustes", "Salir" };
        readonly Font name = Theme.Ui(10.5f), meta = Theme.Ui(8f), item = Theme.Ui(10.5f);
        int hot = -1;

        public TrayPopup()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Bg;
            DoubleBuffered = true;
            Size = new Size(260, Pad * 2 + DevH * 2 + 9 + ItemH * items.Length);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x20000;  // CS_DROPSHADOW
                cp.ExStyle |= 0x80;        // WS_EX_TOOLWINDOW
                return cp;
            }
        }

        public void ShowAt(Point cursor)
        {
            var wa = Screen.FromPoint(cursor).WorkingArea;
            int x = Math.Min(Math.Max(cursor.X - Width / 2, wa.Left + 8), wa.Right - Width - 8);
            int y = cursor.Y - Height - 8;
            if (y < wa.Top) y = cursor.Y + 8;
            Location = new Point(x, Math.Max(wa.Top, Math.Min(y, wa.Bottom - Height - 8)));
            hot = -1;
            Show();
            Activate();
            Invalidate();
        }

        protected override void OnDeactivate(EventArgs e) { Hide(); base.OnDeactivate(e); }

        int ItemsTop { get { return Pad + DevH * 2 + 9; } }

        int HitItem(Point p)
        {
            int i = (p.Y - ItemsTop) / ItemH;
            return p.Y >= ItemsTop && i >= 0 && i < items.Length ? i : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = HitItem(e.Location);
            if (h != hot) { hot = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { hot = -1; Invalidate(); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int i = HitItem(e.Location);
            if (i < 0) return;
            Hide();
            if (ItemClicked != null) ItemClicked(i);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Bg);
            using (var border = new Pen(Sep)) g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);

            DrawDevice(g, Pad, "Mouse", MouseLevel);
            DrawDevice(g, Pad + DevH, "Teclado", KeyboardLevel);
            using (var s = new Pen(Sep)) g.DrawLine(s, 0, Pad + DevH * 2 + 4, Width, Pad + DevH * 2 + 4);

            for (int i = 0; i < items.Length; i++)
            {
                var r = new Rectangle(4, ItemsTop + i * ItemH, Width - 8, ItemH - 2);
                if (i == hot)
                    using (var p = Art.Round(r, 4))
                    using (var b = new SolidBrush(Hover)) g.FillPath(b, p);
                DrawIcon(g, i, new RectangleF(r.X + 16, r.Y + r.Height / 2f - 8, 16, 16));
                TextRenderer.DrawText(g, items[i], item, new Rectangle(r.X + 46, r.Y, r.Width - 50, r.Height), Theme.Text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            }
        }

        void DrawDevice(Graphics g, int y, string label, int level)
        {
            int x = 24;
            TextRenderer.DrawText(g, label, name, new Point(x, y + 8), Theme.Text);
            var c = Color.FromArgb(200, 200, 206);
            if (level >= 0)
            {
                Art.DrawWireless(g, new RectangleF(x, y + 37, 13, 9), c);
                TextRenderer.DrawText(g, "2.4G", meta, new Point(x + 17, y + 33), c);
                DrawVBattery(g, new RectangleF(x + 56, y + 35, 8, 13), level, c);
                TextRenderer.DrawText(g, level + "% batería", meta, new Point(x + 69, y + 33), Art.LevelColor(level, c));
            }
            else
                TextRenderer.DrawText(g, level == -1 ? "Apagado o dormido" : "Buscando…", meta, new Point(x, y + 33), Theme.Dim);
        }

        // Pila vertical pequeña, como la del menú de G HUB.
        static void DrawVBattery(Graphics g, RectangleF r, int level, Color c)
        {
            using (var b = new SolidBrush(c)) g.FillRectangle(b, r.X + r.Width * 0.3f, r.Y, r.Width * 0.4f, 1.6f);
            var body = new RectangleF(r.X, r.Y + 2, r.Width, r.Height - 2);
            using (var pen = new Pen(c, 1.2f)) g.DrawRectangle(pen, body.X, body.Y, body.Width, body.Height);
            float h = Math.Max(1, (body.Height - 3) * level / 100f);
            using (var f = new SolidBrush(Art.LevelColor(level, c)))
                g.FillRectangle(f, body.X + 1.6f, body.Bottom - 1.6f - h, body.Width - 3.2f, h);
        }

        static void DrawIcon(Graphics g, int i, RectangleF r)
        {
            using (var pen = new Pen(Color.FromArgb(220, 220, 226), 1.4f))
            {
                if (i == 0)
                {
                    using (var p = Art.Round(new RectangleF(r.X + 3, r.Y, r.Width - 6, r.Height), 5)) g.DrawPath(pen, p);
                    g.DrawLine(pen, r.X + r.Width / 2, r.Y + 2, r.X + r.Width / 2, r.Y + 6);
                }
                else if (i == 1)
                {
                    g.DrawEllipse(pen, r.X + 4, r.Y + 4, r.Width - 8, r.Height - 8);
                    for (int k = 0; k < 8; k++)
                    {
                        double a = k * Math.PI / 4;
                        float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
                        g.DrawLine(pen, cx + (float)Math.Cos(a) * 5.5f, cy + (float)Math.Sin(a) * 5.5f,
                                        cx + (float)Math.Cos(a) * 8f, cy + (float)Math.Sin(a) * 8f);
                    }
                }
                else
                {
                    g.DrawArc(pen, r.X + 1, r.Y + 2, r.Width - 2, r.Height - 2, -60, 300);
                    g.DrawLine(pen, r.X + r.Width / 2, r.Y, r.X + r.Width / 2, r.Y + r.Height / 2);
                }
            }
        }
    }
}
