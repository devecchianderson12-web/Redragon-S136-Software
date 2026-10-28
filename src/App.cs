using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace S136Battery
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            bool fresh;
            using (var m = new Mutex(true, "S136Battery.single", out fresh))
            {
                if (!fresh) return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }
    }

    static class Settings
    {
        const string Key = @"Software\S136Battery";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "S136Battery";

        public static bool StartMinimized
        {
            get
            {
                using (var k = Registry.CurrentUser.OpenSubKey(Key))
                    return k != null && (int)k.GetValue("StartMinimized", 0) == 1;
            }
            set
            {
                using (var k = Registry.CurrentUser.CreateSubKey(Key))
                    k.SetValue("StartMinimized", value ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        public static bool StartWithWindows
        {
            get
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue(RunName) != null;
            }
            set
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (value) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\"");
                    else k.DeleteValue(RunName, false);
                }
            }
        }

        // Si movieron el .exe, arreglar la ruta de inicio.
        public static void FixRunPath()
        {
            if (StartWithWindows) StartWithWindows = true;
        }
    }

    class BatteryBar : Control
    {
        int level = -1;
        public int Level { get { return level; } set { level = value; Invalidate(); } }

        public BatteryBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            var r = new Rectangle(0, 0, Width - 5, Height - 1);
            using (var bg = new SolidBrush(Theme.Track)) g.FillRectangle(bg, r);
            using (var tip = new SolidBrush(Theme.Track)) g.FillRectangle(tip, Width - 4, Height / 2 - 4, 4, 8);
            if (level >= 0)
            {
                int w = (int)Math.Round((r.Width - 4) * level / 100.0);
                using (var fill = new SolidBrush(Theme.ForLevel(level))) g.FillRectangle(fill, 2, 2, Math.Max(w, 2), r.Height - 3);
            }
        }
    }

    static class Theme
    {
        public static readonly Color Back = Color.FromArgb(24, 24, 27);
        public static readonly Color Text = Color.FromArgb(235, 235, 240);
        public static readonly Color Dim = Color.FromArgb(140, 140, 150);
        public static readonly Color Track = Color.FromArgb(52, 52, 58);

        public static Color ForLevel(int level)
        {
            if (level < 0) return Dim;
            if (level <= 15) return Color.FromArgb(235, 70, 70);
            if (level <= 35) return Color.FromArgb(240, 180, 50);
            return Color.FromArgb(70, 200, 110);
        }
    }

    class MainForm : Form
    {
        readonly Receiver rx = new Receiver();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly ToolStripMenuItem miMouse = new ToolStripMenuItem("Ratón: —") { Enabled = false };
        readonly ToolStripMenuItem miKb = new ToolStripMenuItem("Teclado: —") { Enabled = false };
        readonly ToolStripMenuItem miAutostart = new ToolStripMenuItem("Iniciar con Windows") { CheckOnClick = true };
        readonly ToolStripMenuItem miMin = new ToolStripMenuItem("Iniciar minimizado") { CheckOnClick = true };
        readonly CheckBox cbAutostart = new CheckBox();
        readonly CheckBox cbMin = new CheckBox();
        readonly BatteryBar barMouse = new BatteryBar(), barKb = new BatteryBar();
        readonly Label lblMouse = new Label(), lblKb = new Label(), lblStatus = new Label();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        int busy;
        bool quitting, syncing;
        Icon trayIcon;

        public MainForm()
        {
            Text = "S136 Battery";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 10f);
            ClientSize = new Size(320, 196);
            AutoScaleMode = AutoScaleMode.Dpi;

            AddRow("Ratón", barMouse, lblMouse, 20);
            AddRow("Teclado", barKb, lblKb, 60);

            lblStatus.SetBounds(20, 98, 280, 20);
            lblStatus.ForeColor = Theme.Dim;
            lblStatus.Font = new Font("Segoe UI", 8.5f);
            Controls.Add(lblStatus);

            SetupCheck(cbAutostart, "Iniciar con Windows", 128);
            SetupCheck(cbMin, "Iniciar minimizado", 156);

            Settings.FixRunPath();
            cbAutostart.Checked = miAutostart.Checked = Settings.StartWithWindows;
            cbMin.Checked = miMin.Checked = Settings.StartMinimized;
            cbAutostart.CheckedChanged += (s, e) => SetAutostart(cbAutostart.Checked);
            miAutostart.CheckedChanged += (s, e) => SetAutostart(miAutostart.Checked);
            cbMin.CheckedChanged += (s, e) => SetMin(cbMin.Checked);
            miMin.CheckedChanged += (s, e) => SetMin(miMin.Checked);

            var menu = new ContextMenuStrip();
            menu.Items.Add(miMouse);
            menu.Items.Add(miKb);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Abrir", null, (s, e) => ShowWindow());
            menu.Items.Add("Actualizar", null, (s, e) => Poll());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miAutostart);
            menu.Items.Add(miMin);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Salir", null, (s, e) => { quitting = true; Close(); });
            menu.Opening += (s, e) => Poll();
            tray.ContextMenuStrip = menu;
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowWindow(); };
            tray.Text = "S136 Battery";
            SetTrayIcon(-1, -1);
            tray.Visible = true;

            timer.Interval = 60000;
            timer.Tick += (s, e) => Poll();
            timer.Start();
            Render(-1, -1, "Buscando receptor…");
        }

        void AddRow(string name, BatteryBar bar, Label value, int y)
        {
            var l = new Label { Text = name, ForeColor = Theme.Text };
            l.SetBounds(20, y + 2, 80, 22);
            bar.SetBounds(100, y + 4, 140, 18);
            bar.BackColor = Theme.Back;
            value.SetBounds(250, y + 2, 60, 22);
            value.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(l);
            Controls.Add(bar);
            Controls.Add(value);
        }

        void SetupCheck(CheckBox cb, string text, int y)
        {
            cb.Text = text;
            cb.ForeColor = Theme.Text;
            cb.SetBounds(20, y, 280, 24);
            Controls.Add(cb);
        }

        void SetAutostart(bool on)
        {
            if (syncing) return;
            syncing = true;
            try { Settings.StartWithWindows = on; } catch { }
            cbAutostart.Checked = miAutostart.Checked = Settings.StartWithWindows;
            syncing = false;
        }

        void SetMin(bool on)
        {
            if (syncing) return;
            syncing = true;
            try { Settings.StartMinimized = on; } catch { }
            cbMin.Checked = miMin.Checked = on;
            syncing = false;
        }

        protected override void SetVisibleCore(bool value)
        {
            // Primera vez: respetar "iniciar minimizado".
            if (!IsHandleCreated && Settings.StartMinimized)
            {
                CreateHandle();
                value = false;
            }
            base.SetVisibleCore(value);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Poll();
        }

        void ShowWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            Poll();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized) Hide();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!quitting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            timer.Stop();
            tray.Visible = false;
            rx.Dispose();
            base.OnFormClosing(e);
        }

        void Poll()
        {
            if (Interlocked.Exchange(ref busy, 1) == 1) return;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                int kb = -1, ms = -1;
                string status = null;
                try
                {
                    if (!rx.IsOpen && !rx.Open()) status = "Receptor no encontrado";
                    else rx.Read(out kb, out ms);
                }
                catch
                {
                    rx.Close();
                    status = "Receptor desconectado";
                }
                busy = 0;
                if (IsHandleCreated) BeginInvoke(new Action(() => Render(kb, ms, status)));
            });
        }

        static string Pct(int v) { return v < 0 ? "Apagado" : v + "%"; }

        void Render(int kb, int ms, string status)
        {
            barMouse.Level = ms;
            barKb.Level = kb;
            lblMouse.Text = Pct(ms);
            lblKb.Text = Pct(kb);
            lblMouse.ForeColor = Theme.ForLevel(ms);
            lblKb.ForeColor = Theme.ForLevel(kb);
            lblStatus.Text = status ?? "Actualizado " + DateTime.Now.ToString("HH:mm");
            miMouse.Text = "Ratón: " + Pct(ms);
            miKb.Text = "Teclado: " + Pct(kb);
            string tip = status ?? ("Ratón " + Pct(ms) + " · Teclado " + Pct(kb));
            tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
            SetTrayIcon(ms, kb);
        }

        // Icono: dos barras verticales, izquierda ratón, derecha teclado.
        void SetTrayIcon(int ms, int kb)
        {
            int size = SystemInformation.SmallIconSize.Width;
            using (var bmp = new Bitmap(size, size))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.None;
                    g.Clear(Color.Transparent);
                    float u = size / 16f;
                    DrawCell(g, ms, 1 * u, u, 6 * u, 14 * u);
                    DrawCell(g, kb, 9 * u, u, 6 * u, 14 * u);
                }
                var h = bmp.GetHicon();
                var ic = (Icon)Icon.FromHandle(h).Clone();
                DestroyIcon(h);
                tray.Icon = ic;
                if (trayIcon != null) trayIcon.Dispose();
                trayIcon = ic;
            }
        }

        static void DrawCell(Graphics g, int level, float x, float y, float w, float h)
        {
            using (var track = new SolidBrush(Color.FromArgb(200, 70, 70, 78))) g.FillRectangle(track, x, y, w, h);
            if (level < 0) return;
            float fh = Math.Max(h * level / 100f, 1);
            using (var fill = new SolidBrush(Theme.ForLevel(level))) g.FillRectangle(fill, x, y + h - fh, w, fh);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
    }
}
