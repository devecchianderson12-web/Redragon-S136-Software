using System;
using System.Drawing;

namespace S136Battery
{
    class LightMode
    {
        public readonly byte Code;
        public readonly string Name;
        public LightMode(byte code, string name) { Code = code; Name = name; }
        public override string ToString() { return Name; }
    }

    class LightState
    {
        public byte Mode;
        public int Brightness; // 0..4
        public int Speed;      // 0..4
        public bool Multicolor;
        public Color Color;
    }

    // Bloque de perfil en la config del aparato (sacado de S136.exe).
    // Byte 0 del bloque 0 = perfil activo. Bloque del perfil N en N*BlockSize.
    class DeviceLighting
    {
        public readonly byte Target;
        public readonly LightMode[] Modes;
        readonly int blockSize, blockLen;
        readonly int colorfulAt;
        readonly bool mouseColorOrder;

        DeviceLighting(byte target, int blockSize, int blockLen, int colorfulAt, bool mouseColorOrder, LightMode[] modes)
        {
            Target = target; this.blockSize = blockSize; this.blockLen = blockLen; this.colorfulAt = colorfulAt;
            this.mouseColorOrder = mouseColorOrder; Modes = modes;
        }

        public static readonly DeviceLighting Keyboard = new DeviceLighting(Receiver.Keyboard, 0x64, 0x63, 5, false, new[]
        {
            new LightMode(6, "Fijo"), new LightMode(5, "Respiración"), new LightMode(4, "Espectro"),
            new LightMode(1, "Ondulado"), new LightMode(13, "Ola"), new LightMode(2, "Nube"),
            new LightMode(3, "Serpentina"), new LightMode(7, "Reacción"), new LightMode(8, "Ondas"),
            new LightMode(9, "Travesía"), new LightMode(10, "Estrellas"), new LightMode(11, "Flores"),
            new LightMode(12, "Rodar"), new LightMode(14, "Caricatura"), new LightMode(15, "Lluvia"),
            new LightMode(16, "Escaneo"), new LightMode(17, "Cascada"), new LightMode(18, "Velocidad"),
        });

        public static readonly DeviceLighting Mouse = new DeviceLighting(Receiver.Mouse, 0x80, 0x4E, 5, true, new[]
        {
            new LightMode(3, "Fijo"), new LightMode(2, "Respiración"), new LightMode(1, "Espectro"),
            new LightMode(0, "Ondulado"), new LightMode(4, "Pulso"), new LightMode(5, "Reacción"),
            new LightMode(6, "Apagado"),
        });

        int Base(Receiver rx)
        {
            var p = rx.ReadConfig(Target, 0, 1);
            if (p == null) return -1;
            return (p[0] < 8 ? p[0] : 0) * blockSize;
        }

        public LightState Read(Receiver rx)
        {
            int b = Base(rx);
            if (b < 0) return null;
            var d = rx.ReadConfig(Target, b, 9);
            if (d == null) return null;
            var s = new LightState();
            s.Mode = d[1];
            s.Brightness = Math.Min((int)d[2], 4);
            s.Speed = 4 - Math.Min((int)d[3], 4);
            s.Multicolor = d[colorfulAt] != 0;
            s.Color = mouseColorOrder ? Color.FromArgb(d[6], d[8], d[7]) : Color.FromArgb(d[6], d[7], d[8]);
            return s;
        }

        // El aparato solo acepta el bloque entero desde su inicio, como hace S136.exe.
        public bool Write(Receiver rx, LightState s)
        {
            int b = Base(rx);
            if (b < 0) return false;
            var d = rx.ReadConfig(Target, b, blockLen);
            if (d == null) return false;
            d[1] = s.Mode;
            d[2] = (byte)Math.Max(0, Math.Min(4, s.Brightness));
            d[3] = (byte)(4 - Math.Max(0, Math.Min(4, s.Speed)));
            d[colorfulAt] = (byte)(s.Multicolor ? 1 : 0);
            d[6] = s.Color.R;
            d[7] = mouseColorOrder ? s.Color.B : s.Color.G;
            d[8] = mouseColorOrder ? s.Color.G : s.Color.B;
            return rx.WriteConfig(Target, b, d);
        }
    }
}
