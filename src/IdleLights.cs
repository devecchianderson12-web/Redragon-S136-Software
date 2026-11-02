using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace S136Battery
{
    // Apaga las luces de cada aparato tras X minutos sin usarlo y las restaura al volver a usarlo.
    // Solo cuenta la actividad del receptor S136, no la de otros teclados o ratones.
    class IdleLights
    {
        class Slot
        {
            public DeviceLighting Dev;
            public long Last = Environment.TickCount;
            public bool Off;
            public int Busy;
        }

        const int WM_INPUT = 0xFF;
        readonly Receiver rx;
        readonly Slot kb = new Slot { Dev = DeviceLighting.Keyboard };
        readonly Slot ms = new Slot { Dev = DeviceLighting.Mouse };
        readonly Dictionary<IntPtr, bool> ours = new Dictionary<IntPtr, bool>();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 5000 };

        public IdleLights(Receiver rx)
        {
            this.rx = rx;
            kb.Off = Settings.HasSavedLight(kb.Dev.Target);
            ms.Off = Settings.HasSavedLight(ms.Dev.Target);
            timer.Tick += (s, e) => { Check(kb); Check(ms); };
            timer.Start();
        }

        public void Listen(IntPtr hwnd)
        {
            var devs = new[]
            {
                new RAWINPUTDEVICE { Page = 1, Usage = 2, Flags = 0x100, Target = hwnd },
                new RAWINPUTDEVICE { Page = 1, Usage = 6, Flags = 0x100, Target = hwnd },
            };
            RegisterRawInputDevices(devs, devs.Length, Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
        }

        public void Message(ref Message m)
        {
            if (m.Msg != WM_INPUT) return;
            RAWINPUTHEADER h;
            int size = Marshal.SizeOf(typeof(RAWINPUTHEADER));
            if (GetRawInputData(m.LParam, 0x10000005, out h, ref size, size) < 0 || !IsOurs(h.Device)) return;
            var slot = h.Type == 1 ? kb : ms;
            slot.Last = Environment.TickCount;
            if (slot.Off) Check(slot);
        }

        // El usuario aplicó luces a mano: esas mandan y no hay nada que restaurar.
        public void Reset(DeviceLighting dev)
        {
            var slot = dev == kb.Dev ? kb : ms;
            slot.Off = false;
            slot.Last = Environment.TickCount;
            Settings.ClearSavedLight(dev.Target);
        }

        bool IsOurs(IntPtr dev)
        {
            bool yes;
            if (ours.TryGetValue(dev, out yes)) return yes;
            int len = 0;
            GetRawInputDeviceInfo(dev, 0x20000007, null, ref len);
            var sb = new StringBuilder(len + 1);
            GetRawInputDeviceInfo(dev, 0x20000007, sb, ref len);
            yes = sb.ToString().IndexOf("vid_320f", StringComparison.OrdinalIgnoreCase) >= 0;
            ours[dev] = yes;
            return yes;
        }

        void Check(Slot s)
        {
            int minutes = Settings.IdleMinutes;
            long idle = (uint)(Environment.TickCount - (int)s.Last);
            bool restore = s.Off && (minutes == 0 || idle < 10000);
            bool turnOff = !s.Off && minutes > 0 && idle >= minutes * 60000L;
            if ((!restore && !turnOff) || Interlocked.Exchange(ref s.Busy, 1) == 1) return;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    if (rx.IsOpen || rx.Open())
                    {
                        if (restore)
                        {
                            int mode, bright;
                            if (Settings.GetSavedLight(s.Dev.Target, out mode, out bright) && s.Dev.Restore(rx, (byte)mode, bright))
                                Settings.ClearSavedLight(s.Dev.Target);
                            if (!Settings.HasSavedLight(s.Dev.Target)) s.Off = false;
                        }
                        else
                        {
                            byte mode; int bright;
                            if (s.Dev.TurnOff(rx, out mode, out bright))
                            {
                                Settings.SaveLight(s.Dev.Target, mode, bright);
                                s.Off = true;
                            }
                            else s.Last = Environment.TickCount; // dormido: reintenta en otro ciclo
                        }
                    }
                }
                catch { rx.Close(); }
                s.Busy = 0;
            });
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RAWINPUTDEVICE { public ushort Page, Usage; public uint Flags; public IntPtr Target; }

        [StructLayout(LayoutKind.Sequential)]
        struct RAWINPUTHEADER { public uint Type, Size; public IntPtr Device, WParam; }

        [DllImport("user32.dll")] static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] d, int n, int size);
        [DllImport("user32.dll")] static extern int GetRawInputData(IntPtr h, uint cmd, out RAWINPUTHEADER data, ref int size, int headerSize);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetRawInputDeviceInfo(IntPtr h, uint cmd, StringBuilder data, ref int size);
    }
}
