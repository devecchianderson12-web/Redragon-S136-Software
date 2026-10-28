using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace S136Battery
{
    static class Program
    {
        public const string Version = "2.1.0";

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--snap")
            {
                // Capturas de la UI para revisar el diseño sin abrir la ventana.
                Application.EnableVisualStyles();
                using (var f = new MainForm()) f.Snap(args[1]);
                return;
            }
            bool fresh;
            using (var m = new Mutex(true, "S136Battery.single", out fresh))
            {
                if (!fresh) return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }

        public static Image Resource(string name)
        {
            using (var s = typeof(Program).Assembly.GetManifestResourceStream(name))
                return s == null ? null : new Bitmap(s);
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

    class MainForm : Form
    {
        static readonly Color[] Presets =
        {
            Color.FromArgb(255, 0, 0), Color.FromArgb(255, 110, 0), Color.FromArgb(255, 220, 0), Color.FromArgb(0, 255, 60),
            Color.FromArgb(0, 220, 255), Color.FromArgb(0, 60, 255), Color.FromArgb(170, 0, 255), Color.FromArgb(255, 255, 255),
        };

        readonly Receiver rx = new Receiver();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly TrayPopup popup = new TrayPopup();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly Panel top = new Panel(), body = new Panel();
        readonly Panel home = new Panel(), device = new Panel(), settings = new Panel();
        readonly DeviceCard cardMouse = new DeviceCard(), cardKb = new DeviceCard();
        readonly Image imgMouse = Program.Resource("mouse.png"), imgKb = Program.Resource("keyboard.png");
        readonly Font h1 = Theme.Bold(20f), h2 = Theme.Bold(11f), small = Theme.Ui(9.5f);
        int mouseLevel = -2, kbLevel = -2;
        int busy;
        bool quitting, snapping;
        UpdateToast toast;
        readonly System.Windows.Forms.Timer updTimer = new System.Windows.Forms.Timer();
        Icon trayIcon;
        readonly Label updStatus = new Label();

        public MainForm()
        {
            Text = "Redragon S136";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            Font = Theme.Ui(10f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1040, 680);
            Icon = MakeIcon(32);

            BuildTop();
            body.Dock = DockStyle.Fill;
            body.BackColor = Theme.Back;
            foreach (var p in new[] { home, device, settings }) { p.Dock = DockStyle.Fill; p.BackColor = Theme.Back; body.Controls.Add(p); }
            Controls.Add(body);
            Controls.Add(top);
            BuildHome();
            BuildSettings();
            ShowPage(home);

            popup.ItemClicked += i =>
            {
                if (i == 0) { ShowWindow(); ShowPage(home); }
                else if (i == 1) { ShowWindow(); ShowPage(settings); }
                else Quit();
            };
            tray.Text = "Redragon S136";
            tray.Icon = trayIcon = MakeIcon(SystemInformation.SmallIconSize.Width);
            tray.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) ShowWindow();
                else if (e.Button == MouseButtons.Right) { popup.ShowAt(Cursor.Position); SetForegroundWindow(popup.Handle); Poll(); }
            };
            tray.Visible = true;

            Settings.FixRunPath();
            timer.Interval = 60000;
            timer.Tick += (s, e) => Poll();
            timer.Start();

            // Buscar actualización a los 8 s y luego cada 6 h.
            updTimer.Interval = 8000;
            updTimer.Tick += (s, e) => { updTimer.Interval = 6 * 3600 * 1000; CheckUpdate(false); };
            updTimer.Start();
        }

        void CheckUpdate(bool manual)
        {
            if (snapping || (toast != null && !toast.IsDisposed)) return;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var r = Updater.Check();
                if (!IsHandleCreated) return;
                BeginInvoke(new Action(() =>
                {
                    if (r != null) ShowToast(r);
                    else if (manual) updStatus.Text = "Ya tienes la última versión.";
                }));
            });
        }

        void ShowToast(Updater.Release r)
        {
            if (toast != null && !toast.IsDisposed) return;
            toast = new UpdateToast(r);
            toast.UpdateClicked += (s, e) => ThreadPool.QueueUserWorkItem(_ =>
            {
                string err = Updater.Prepare(r, Application.ExecutablePath);
                BeginInvoke(new Action(() =>
                {
                    if (err == null) Quit();
                    else toast.Fail(err);
                }));
            });
            toast.Popup();
        }

        static Icon MakeIcon(int size)
        {
            using (var bmp = new Bitmap(size, size))
            {
                using (var g = Graphics.FromImage(bmp)) Art.DrawLogo(g, 0, 0, size);
                IntPtr h = bmp.GetHicon();
                var ic = (Icon)Icon.FromHandle(h).Clone();
                DestroyIcon(h);
                return ic;
            }
        }

        // ---------- Barra superior ----------

        void BuildTop()
        {
            top.Dock = DockStyle.Top;
            top.Height = 84;
            top.BackColor = Theme.Back;
            top.Paint += (s, e) =>
            {
                var g = e.Graphics;
                Art.DrawLogo(g, 28, 22, 40);
                using (var pen = new Pen(Theme.Line)) g.DrawLine(pen, 0, top.Height - 1, top.Width, top.Height - 1);
            };
            top.MouseDown += Drag;

            var tab = new Label { Text = "Dispositivos", ForeColor = Theme.Accent, Font = Theme.Bold(11f), AutoSize = true, Cursor = Cursors.Hand, BackColor = Theme.Back };
            tab.Location = new Point(96, 31);
            tab.Click += (s, e) => ShowPage(home);
            top.Controls.Add(tab);

            var name = new Label { Text = "Redragon S136", ForeColor = Theme.Dim, Font = Theme.Ui(10f), AutoSize = true, BackColor = Theme.Back };
            name.Location = new Point(220, 33);
            name.MouseDown += Drag;
            top.Controls.Add(name);

            var gear = new IconButton(IconButton.Kind.Gear) { Size = new Size(40, 40) };
            gear.Click += (s, e) => ShowPage(settings);
            var min = new IconButton(IconButton.Kind.Min) { Size = new Size(44, 32) };
            min.Click += (s, e) => Hide();
            var close = new IconButton(IconButton.Kind.Close) { Size = new Size(44, 32) };
            close.Click += (s, e) => Hide();
            top.Controls.AddRange(new Control[] { gear, min, close });
            top.Resize += (s, e) =>
            {
                close.Location = new Point(top.Width - 45, 1);
                min.Location = new Point(top.Width - 89, 1);
                gear.Location = new Point(top.Width - 140, 30);
            };
        }

        void Drag(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
        }

        void ShowPage(Panel p)
        {
            foreach (Control c in body.Controls) c.Visible = c == p;
        }

        // ---------- Inicio: tarjetas ----------

        void BuildHome()
        {
            cardMouse.Title = "Mouse";
            cardMouse.Picture = imgMouse;
            cardKb.Title = "Teclado";
            cardKb.Picture = imgKb;
            cardMouse.Click += (s, e) => OpenDevice(DeviceLighting.Mouse);
            cardKb.Click += (s, e) => OpenDevice(DeviceLighting.Keyboard);
            home.Controls.Add(cardMouse);
            home.Controls.Add(cardKb);
            home.Resize += (s, e) => LayoutCards();
            LayoutCards();
        }

        void LayoutCards()
        {
            int w = 300, h = 450, gap = 26;
            int x = (home.ClientSize.Width - (w * 2 + gap)) / 2;
            int y = Math.Max(30, (home.ClientSize.Height - h) / 2 - 10);
            cardMouse.SetBounds(x, y, w, h);
            cardKb.SetBounds(x + w + gap, y, w, h);
        }

        // ---------- Página de aparato ----------

        void OpenDevice(DeviceLighting dev)
        {
            device.SuspendLayout();
            foreach (Control c in device.Controls) c.Dispose();
            device.Controls.Clear();
            bool isMouse = dev == DeviceLighting.Mouse;
            string title = isMouse ? "Mouse" : "Teclado";
            Image pic = isMouse ? imgMouse : imgKb;

            var back = new FlatButton { Text = "‹  Dispositivos", Width = 150 };
            back.Location = new Point(36, 22);
            back.Click += (s, e) => ShowPage(home);
            device.Controls.Add(back);

            var left = new Panel { Location = new Point(36, 74), Size = new Size(460, 500), BackColor = Theme.Back };
            left.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                TextRenderer.DrawText(g, title, h1, new Point(0, 0), Theme.Text);
                int lv = isMouse ? mouseLevel : kbLevel;
                if (lv >= 0)
                {
                    Art.DrawWireless(g, new RectangleF(2, 52, 18, 13), Theme.Dim);
                    Art.DrawBattery(g, new RectangleF(30, 52, 26, 13), lv, Theme.Dim);
                    TextRenderer.DrawText(g, lv + "% batería", small, new Point(62, 48), Art.LevelColor(lv, Theme.Dim));
                }
                else TextRenderer.DrawText(g, "Apagado o dormido", small, new Point(0, 48), Theme.Dim);
                if (pic != null)
                {
                    float sc = Math.Min(440f / pic.Width, 380f / pic.Height);
                    float w = pic.Width * sc, h = pic.Height * sc;
                    g.DrawImage(pic, (440 - w) / 2, 90 + (380 - h) / 2, w, h);
                }
            };
            device.Controls.Add(left);

            int x = 530, y = 30, colW = 470;
            var head = new Label { Text = "ILUMINACIÓN", Font = h2, ForeColor = Theme.Dim, AutoSize = true, Location = new Point(x, y), BackColor = Theme.Back };
            device.Controls.Add(head);
            y += 34;

            var state = new LightState { Mode = dev.Modes[0].Code, Brightness = 4, Speed = 2, Color = Presets[0] };
            var modeButtons = new FlatButton[dev.Modes.Length];
            int cols = 3, bw = (colW - (cols - 1) * 8) / cols;
            for (int i = 0; i < dev.Modes.Length; i++)
            {
                var m = dev.Modes[i];
                var b = new FlatButton { Text = m.Name, Size = new Size(bw, 32) };
                b.Location = new Point(x + (i % cols) * (bw + 8), y + (i / cols) * 38);
                b.Click += (s, e) => { state.Mode = m.Code; Mark(modeButtons, dev, state.Mode); };
                modeButtons[i] = b;
                device.Controls.Add(b);
            }
            y += ((dev.Modes.Length + cols - 1) / cols) * 38 + 10;

            var bright = AddSlider("Brillo", x, ref y, colW, v => state.Brightness = v);
            var speed = AddSlider("Velocidad", x, ref y, colW, v => state.Speed = v);

            var colorRow = new System.Collections.Generic.List<Control>();
            var colorLbl = new Label { Text = "Color", ForeColor = Theme.Text, AutoSize = true, Location = new Point(x, y + 4), BackColor = Theme.Back };
            device.Controls.Add(colorLbl);
            colorRow.Add(colorLbl);
            var swatches = new Swatch[Presets.Length + 1];
            for (int i = 0; i <= Presets.Length; i++)
            {
                var sw = new Swatch { Color = i < Presets.Length ? Presets[i] : Color.Black, Location = new Point(x + 90 + i * 34, y) };
                int idx = i;
                sw.Click += (s, e) =>
                {
                    if (idx == Presets.Length)
                        using (var dlg = new ColorDialog { FullOpen = true, Color = state.Color })
                        {
                            if (dlg.ShowDialog(this) != DialogResult.OK) return;
                            swatches[idx].Color = dlg.Color;
                        }
                    state.Color = swatches[idx].Color;
                    foreach (var o in swatches) { o.Selected = o == swatches[idx]; o.Invalidate(); }
                };
                swatches[i] = sw;
                device.Controls.Add(sw);
                colorRow.Add(sw);
            }
            swatches[Presets.Length].Custom = true;
            y += 42;

            var multi = new Toggle { Label = "Multicolor (arcoíris)", Width = colW, Location = new Point(x, y) };
            multi.Changed += (s, e) => state.Multicolor = multi.On;
            device.Controls.Add(multi);
            colorRow.Add(multi);
            y += 46;

            if (!dev.SupportsColor)
            {
                foreach (var c in colorRow) c.Visible = false;
                y -= 88;
                device.Controls.Add(new Label
                {
                    Text = "Este teclado tiene luz arcoíris fija: el color no se puede elegir.",
                    ForeColor = Theme.Dim, AutoSize = true, Location = new Point(x, y + 6), BackColor = Theme.Back,
                });
                y += 40;
            }
            var status = new Label { Text = "Leyendo luces…", ForeColor = Theme.Dim, AutoSize = false, Size = new Size(colW - 170, 34), Location = new Point(x, y), TextAlign = ContentAlignment.MiddleLeft, BackColor = Theme.Back };
            device.Controls.Add(status);
            var apply = new FlatButton { Text = "Aplicar", Filled = true, Size = new Size(150, 36), Location = new Point(x + colW - 150, y), Enabled = false };
            device.Controls.Add(apply);

            Action<LightState> fill = st =>
            {
                state.Mode = st.Mode; state.Brightness = st.Brightness; state.Speed = st.Speed;
                state.Multicolor = st.Multicolor; state.Color = st.Color;
                Mark(modeButtons, dev, st.Mode);
                bright.Value = st.Brightness;
                speed.Value = st.Speed;
                multi.On = st.Multicolor;
                int hit = Array.FindIndex(Presets, c => c.ToArgb() == st.Color.ToArgb());
                if (hit < 0) { hit = Presets.Length; swatches[hit].Color = st.Color; }
                for (int i = 0; i < swatches.Length; i++) { swatches[i].Selected = i == hit; swatches[i].Invalidate(); }
            };
            fill(state);

            apply.Click += (s, e) =>
            {
                if (!apply.Enabled) return;
                apply.Enabled = false;
                status.Text = "Aplicando…";
                var snap = new LightState { Mode = state.Mode, Brightness = state.Brightness, Speed = state.Speed, Multicolor = state.Multicolor, Color = state.Color };
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    bool ok;
                    try { ok = (rx.IsOpen || rx.Open()) && dev.Write(rx, snap); } catch { ok = false; rx.Close(); }
                    BeginInvoke(new Action(() =>
                    {
                        if (apply.IsDisposed) return;
                        apply.Enabled = true;
                        status.Text = ok ? "Listo. Luces aplicadas." : "No se pudo aplicar. ¿Aparato encendido?";
                    }));
                });
            };

            ThreadPool.QueueUserWorkItem(_ =>
            {
                LightState st = null;
                try { if (rx.IsOpen || rx.Open()) st = dev.Read(rx); } catch { rx.Close(); }
                BeginInvoke(new Action(() =>
                {
                    if (apply.IsDisposed) return;
                    if (st != null) { fill(st); status.Text = "Config actual leída del aparato."; }
                    else status.Text = "No se pudo leer. Mueve el aparato para despertarlo.";
                    apply.Enabled = true;
                }));
            });

            device.ResumeLayout();
            ShowPage(device);
        }

        static void Mark(FlatButton[] buttons, DeviceLighting dev, byte code)
        {
            for (int i = 0; i < buttons.Length; i++) { buttons[i].Selected = dev.Modes[i].Code == code; buttons[i].Invalidate(); }
        }

        StepSlider AddSlider(string label, int x, ref int y, int w, Action<int> set)
        {
            var l = new Label { Text = label, ForeColor = Theme.Text, AutoSize = true, Location = new Point(x, y + 5), BackColor = Theme.Back };
            var s = new StepSlider { Location = new Point(x + 90, y), Width = w - 90 };
            s.ValueChanged += (o, e) => set(s.Value);
            device.Controls.Add(l);
            device.Controls.Add(s);
            y += 40;
            return s;
        }

        // ---------- Ajustes ----------

        void BuildSettings()
        {
            int x = 60, y = 40, w = 460;
            settings.Controls.Add(new Label { Text = "Ajustes", Font = h1, ForeColor = Theme.Text, AutoSize = true, Location = new Point(x, y), BackColor = Theme.Back });
            y += 64;
            var auto = new Toggle { Label = "Iniciar con Windows", Width = w, Location = new Point(x, y) };
            auto.On = Settings.StartWithWindows;
            auto.Changed += (s, e) => { try { Settings.StartWithWindows = auto.On; } catch { } };
            settings.Controls.Add(auto);
            y += 44;
            var min = new Toggle { Label = "Iniciar minimizado en la bandeja", Width = w, Location = new Point(x, y) };
            min.On = Settings.StartMinimized;
            min.Changed += (s, e) => { try { Settings.StartMinimized = min.On; } catch { } };
            settings.Controls.Add(min);
            y += 60;
            var check = new FlatButton { Text = "Buscar actualizaciones", Size = new Size(200, 34), Location = new Point(x, y) };
            check.Click += (s, e) => { updStatus.Text = "Buscando…"; CheckUpdate(true); };
            settings.Controls.Add(check);
            updStatus.SetBounds(x + 214, y, 300, 34);
            updStatus.ForeColor = Theme.Dim;
            updStatus.BackColor = Theme.Back;
            updStatus.TextAlign = ContentAlignment.MiddleLeft;
            settings.Controls.Add(updStatus);
            y += 60;
            settings.Controls.Add(new Label
            {
                Text = "Versión " + Program.Version + "  ·  Proyecto no oficial, sin relación con Redragon.",
                ForeColor = Theme.Dim, AutoSize = true, Location = new Point(x, y), BackColor = Theme.Back
            });
        }

        // ---------- Ventana y bandeja ----------

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

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Theme.Line)) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        void ShowWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            Poll();
        }

        void Quit()
        {
            quitting = true;
            Close();
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
            if (snapping) return;
            if (Interlocked.Exchange(ref busy, 1) == 1) return;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                int kb = -1, ms = -1;
                bool found = true;
                try
                {
                    if (!rx.IsOpen && !rx.Open()) found = false;
                    else rx.ReadBattery(out kb, out ms);
                }
                catch { rx.Close(); found = false; }
                busy = 0;
                if (IsHandleCreated) BeginInvoke(new Action(() => Render(kb, ms, found)));
            });
        }

        static string Pct(int v) { return v < 0 ? "apagado" : v + "%"; }

        void Render(int kb, int ms, bool found)
        {
            mouseLevel = ms;
            kbLevel = kb;
            cardMouse.Level = ms;
            cardKb.Level = kb;
            popup.MouseLevel = ms;
            popup.KeyboardLevel = kb;
            popup.Invalidate();
            foreach (Control c in device.Controls) c.Invalidate();
            string tip = found ? "Mouse " + Pct(ms) + " · Teclado " + Pct(kb) : "Receptor no encontrado";
            tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
        }

        public void Snap(string dir)
        {
            snapping = true;
            tray.Visible = false;
            timer.Stop();
            Render(72, 100, true);
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-4000, -4000);
            base.SetVisibleCore(true);
            popup.Location = new Point(-4000, -3000);
            popup.Show();
            Application.DoEvents();
            Action<Control, string> save = (c, file) =>
            {
                using (var bmp = new Bitmap(c.Width, c.Height))
                {
                    c.DrawToBitmap(bmp, new Rectangle(0, 0, c.Width, c.Height));
                    bmp.Save(System.IO.Path.Combine(dir, file));
                }
            };
            ShowPage(home); save(this, "home.png");
            OpenDevice(DeviceLighting.Keyboard); Application.DoEvents(); save(this, "keyboard.png");
            OpenDevice(DeviceLighting.Mouse); Application.DoEvents(); save(this, "mouse.png");
            ShowPage(settings); save(this, "settings.png");
            save(popup, "tray.png");
            var t = new UpdateToast(new Updater.Release { Tag = "v9.9.9", Version = new Version(9, 9, 9) });
            t.Location = new Point(-4000, -2000);
            t.Show();
            Application.DoEvents();
            save(t, "toast.png");
        }

        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    }

    class IconButton : Control
    {
        public enum Kind { Gear, Min, Close }
        readonly Kind kind;
        bool hot;

        public IconButton(Kind kind)
        {
            this.kind = kind;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Theme.Back;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { hot = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(hot ? (kind == Kind.Close ? Color.FromArgb(200, 30, 30) : Theme.CardHot) : Theme.Back);
            float cx = Width / 2f, cy = Height / 2f;
            using (var pen = new Pen(Theme.Text, 1.6f))
            {
                if (kind == Kind.Min) g.DrawLine(pen, cx - 6, cy, cx + 6, cy);
                else if (kind == Kind.Close) { g.DrawLine(pen, cx - 6, cy - 6, cx + 6, cy + 6); g.DrawLine(pen, cx + 6, cy - 6, cx - 6, cy + 6); }
                else
                {
                    g.DrawEllipse(pen, cx - 4, cy - 4, 8, 8);
                    for (int k = 0; k < 8; k++)
                    {
                        double a = k * Math.PI / 4;
                        g.DrawLine(pen, cx + (float)Math.Cos(a) * 7, cy + (float)Math.Sin(a) * 7, cx + (float)Math.Cos(a) * 10, cy + (float)Math.Sin(a) * 10);
                    }
                    g.DrawEllipse(pen, cx - 7, cy - 7, 14, 14);
                }
            }
        }
    }
}
