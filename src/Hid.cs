using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace S136Battery
{
    // Receptor 2.4G Evision (VID 320F). Protocolo sacado del software S136:
    // paquete 64 bytes, [0]=04, [1..2]=suma bytes 3..63, [3]=cmd, [4]=largo,
    // [5..6]=offset, [7]=estado, [8..]=datos, [32]=destino (1 teclado, 2 ratón).
    // Con destino en [32] solo caben 24 bytes de datos por paquete.
    class Receiver : IDisposable
    {
        const ushort Vid = 0x320F;
        const ushort VendorPage = 0xFF1C;
        const byte CmdBegin = 0x01;
        const byte CmdEnd = 0x02;
        const byte CmdRead = 0x05;
        const byte CmdWrite = 0x06;
        const byte CmdBattery = 0x1A;
        const byte CmdLink = 0xAA;
        const int Chunk = 0x18;
        public const byte Keyboard = 1;
        public const byte Mouse = 2;

        SafeFileHandle wr, rd;
        readonly BlockingCollection<byte[]> inbox = new BlockingCollection<byte[]>();
        readonly object gate = new object();

        public bool IsOpen { get { return wr != null && !wr.IsInvalid && !wr.IsClosed; } }

        public bool Open()
        {
            lock (gate)
            {
                Close();
                string path = Find();
                if (path == null) return false;
                wr = Native.CreateFile(path, 0xC0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                rd = Native.CreateFile(path, 0xC0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                if (wr.IsInvalid || rd.IsInvalid) { Close(); return false; }
                var h = rd;
                new Thread(() => ReadLoop(h)) { IsBackground = true }.Start();
                return true;
            }
        }

        void ReadLoop(SafeFileHandle h)
        {
            while (true)
            {
                var b = new byte[64];
                int n;
                if (!Native.ReadFile(h, b, 64, out n, IntPtr.Zero) || n == 0) break;
                inbox.Add(b);
            }
        }

        public void Close()
        {
            lock (gate)
            {
                if (rd != null) { Native.CancelIoEx(rd, IntPtr.Zero); rd.Dispose(); rd = null; }
                if (wr != null) { wr.Dispose(); wr = null; }
                byte[] x;
                while (inbox.TryTake(out x)) { }
            }
        }

        public void Dispose() { Close(); }

        // Manda un paquete y espera la respuesta del mismo comando y destino.
        byte[] Send(byte cmd, byte target, int len, int offset, byte[] data)
        {
            if (!IsOpen) throw new System.IO.IOException("closed");
            byte[] x;
            while (inbox.TryTake(out x)) { }
            var p = new byte[64];
            p[0] = 4; p[3] = cmd; p[4] = (byte)len;
            p[5] = (byte)offset; p[6] = (byte)(offset >> 8);
            if (data != null) Array.Copy(data, 0, p, 8, data.Length);
            int sum = 0;
            for (int i = 3; i < 64; i++) sum += p[i];
            p[1] = (byte)sum; p[2] = (byte)(sum >> 8);
            p[32] = target;
            int n;
            if (!Native.WriteFile(wr, p, 64, out n, IntPtr.Zero)) throw new System.IO.IOException("write");
            var until = DateTime.UtcNow.AddMilliseconds(2500);
            while (true)
            {
                int left = (int)(until - DateTime.UtcNow).TotalMilliseconds;
                if (left <= 0 || !inbox.TryTake(out x, left)) return null;
                if (x[0] != 4 || x[32] != target) continue;
                if (x[3] == cmd) return x[7] == 0xFF || x[7] == 0xFE ? null : x;
                if (x[3] == 0xFF) return null;
            }
        }

        // Batería en %, o -1 si apagado / dormido.
        public void ReadBattery(out int keyboard, out int mouse)
        {
            lock (gate)
            {
                keyboard = mouse = -1;
                var link = Send(CmdLink, 0, 0, 0, null);
                bool kbOn = link == null || link[8] == 0xFF;
                bool msOn = link == null || link[9] == 0xFF;
                if (kbOn) keyboard = Level(Send(CmdBattery, Keyboard, 6, 0, null));
                if (msOn) mouse = Level(Send(CmdBattery, Mouse, 6, 0, null));
            }
        }

        static int Level(byte[] r)
        {
            if (r == null || r[8] > 100) return -1;
            return r[8];
        }

        public byte[] ReadConfig(byte target, int offset, int len)
        {
            lock (gate)
            {
                var res = new byte[len];
                for (int done = 0; done < len; done += Chunk)
                {
                    int n = Math.Min(Chunk, len - done);
                    var r = Send(CmdRead, target, n, offset + done, null);
                    if (r == null) return null;
                    Array.Copy(r, 8, res, done, n);
                }
                return res;
            }
        }

        public bool WriteConfig(byte target, int offset, byte[] data)
        {
            lock (gate)
            {
                if (Send(CmdBegin, target, 0, 0, null) == null) return false;
                bool ok = true;
                for (int done = 0; done < data.Length && ok; done += Chunk)
                {
                    int n = Math.Min(Chunk, data.Length - done);
                    var part = new byte[n];
                    Array.Copy(data, done, part, 0, n);
                    ok = Send(CmdWrite, target, n, offset + done, part) != null;
                }
                Send(CmdEnd, target, 0, 0, null);
                return ok;
            }
        }

        static string Find()
        {
            foreach (var path in Native.HidPaths())
            {
                if (path.IndexOf("vid_" + Vid.ToString("x4"), StringComparison.OrdinalIgnoreCase) < 0) continue;
                using (var h = Native.CreateFile(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))
                {
                    if (h.IsInvalid) continue;
                    IntPtr pp;
                    if (!Native.HidD_GetPreparsedData(h, out pp)) continue;
                    Native.HIDP_CAPS c;
                    Native.HidP_GetCaps(pp, out c);
                    Native.HidD_FreePreparsedData(pp);
                    if (c.UsagePage == VendorPage && c.OutputReportByteLength == 64) return path;
                }
            }
            return null;
        }
    }

    static class Native
    {
        [DllImport("hid.dll")] public static extern void HidD_GetHidGuid(out Guid g);
        [DllImport("hid.dll")] public static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr p);
        [DllImport("hid.dll")] public static extern bool HidD_FreePreparsedData(IntPtr p);
        [DllImport("hid.dll")] public static extern int HidP_GetCaps(IntPtr p, out HIDP_CAPS c);
        [DllImport("setupapi.dll")] static extern IntPtr SetupDiGetClassDevs(ref Guid g, IntPtr e, IntPtr w, int f);
        [DllImport("setupapi.dll")] static extern bool SetupDiEnumDeviceInterfaces(IntPtr s, IntPtr d, ref Guid g, int i, ref DID did);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr s, ref DID did, IntPtr buf, int size, out int req, IntPtr d);
        [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr s);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern SafeFileHandle CreateFile(string n, uint a, uint s, IntPtr sa, uint c, uint f, IntPtr t);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool ReadFile(SafeFileHandle h, byte[] b, int len, out int read, IntPtr o);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool WriteFile(SafeFileHandle h, byte[] b, int len, out int written, IntPtr o);
        [DllImport("kernel32.dll")] public static extern bool CancelIoEx(SafeFileHandle h, IntPtr o);

        [StructLayout(LayoutKind.Sequential)] struct DID { public int cb; public Guid g; public int f; public IntPtr r; }

        [StructLayout(LayoutKind.Sequential)]
        public struct HIDP_CAPS
        {
            public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices,
                NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices,
                NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
        }

        public static List<string> HidPaths()
        {
            var res = new List<string>();
            Guid g;
            HidD_GetHidGuid(out g);
            IntPtr set = SetupDiGetClassDevs(ref g, IntPtr.Zero, IntPtr.Zero, 0x12);
            for (int i = 0; ; i++)
            {
                var did = new DID();
                did.cb = Marshal.SizeOf(did);
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref g, i, ref did)) break;
                int req;
                SetupDiGetDeviceInterfaceDetail(set, ref did, IntPtr.Zero, 0, out req, IntPtr.Zero);
                IntPtr buf = Marshal.AllocHGlobal(req);
                Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6);
                if (SetupDiGetDeviceInterfaceDetail(set, ref did, buf, req, out req, IntPtr.Zero))
                    res.Add(Marshal.PtrToStringUni(buf + 4));
                Marshal.FreeHGlobal(buf);
            }
            SetupDiDestroyDeviceInfoList(set);
            return res;
        }
    }
}
