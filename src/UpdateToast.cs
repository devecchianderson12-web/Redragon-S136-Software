using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace S136Battery
{
    static class Rounded
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);

        // Esquinas redondeadas: nativas en Windows 11, recorte en versiones viejas.
        public static void Apply(Form f, int radius)
        {
            int pref = 2; // DWMWCP_ROUND
            bool native = Environment.OSVersion.Version.Build >= 22000 &&
                          DwmSetWindowAttribute(f.Handle, 33, ref pref, sizeof(int)) == 0;
            if (native) return;
            using (var p = Art.Round(new RectangleF(0, 0, f.Width, f.Height), radius))
                f.Region = new Region(p);
        }
    }

    // Aviso chico abajo a la derecha. No roba el foco.
    class UpdateToast : Form
    {
        static readonly Color Bg = Color.FromArgb(30, 30, 33);
        readonly FlatButton update = new FlatButton { Text = "Actualizar", Filled = true };
        readonly FlatButton later = new FlatButton { Text = "Más tarde" };
        readonly Font title = Theme.Bold(10.5f), body = Theme.Ui(9.5f);
        string message;
        readonly Timer fade = new Timer { Interval = 15 };

        public event EventHandler UpdateClicked;

        public UpdateToast(Updater.Release r)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Bg;
            DoubleBuffered = true;
            Size = new Size(360, 132);
            message = "La versión " + r.Tag + " está lista para instalar.";

            later.BackColor = Bg;
            update.BackColor = Bg;
            later.SetBounds(Width - 236, Height - 50, 104, 34);
            update.SetBounds(Width - 124, Height - 50, 108, 34);
            later.Click += (s, e) => Close();
            update.Click += (s, e) =>
            {
                update.Enabled = later.Enabled = false;
                update.Text = "Descargando…";
                if (UpdateClicked != null) UpdateClicked(this, EventArgs.Empty);
            };
            Controls.Add(later);
            Controls.Add(update);

            fade.Tick += (s, e) =>
            {
                Opacity = Math.Min(1, Opacity + 0.08);
                if (Opacity >= 1) fade.Stop();
            };
        }

        public void Fail(string error)
        {
            message = error;
            update.Text = "Reintentar";
            update.Enabled = later.Enabled = true;
            Invalidate();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x08000000; // WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE
                cp.ClassStyle |= 0x20000;        // CS_DROPSHADOW
                return cp;
            }
        }

        public void Popup()
        {
            var wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - 16, wa.Bottom - Height - 16);
            Opacity = 0;
            Show();
            Rounded.Apply(this, 12);
            fade.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Bg);
            using (var p = Art.Round(new RectangleF(0.5f, 0.5f, Width - 2, Height - 2), 12))
            using (var pen = new Pen(Color.FromArgb(62, 62, 68))) g.DrawPath(pen, p);
            Art.DrawLogo(g, 16, 16, 40);
            TextRenderer.DrawText(g, "Actualización disponible", title, new Point(68, 16), Theme.Text);
            TextRenderer.DrawText(g, message, body, new Rectangle(68, 40, Width - 84, 36), Theme.Dim,
                TextFormatFlags.WordBreak | TextFormatFlags.Left);
        }
    }
}
