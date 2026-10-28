using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// Hace app.ico (PNG dentro de ICO) con el mismo dibujo del icono de bandeja.
static class MkIcon
{
    static byte[] Png(int s)
    {
        using (var bmp = new Bitmap(s, s))
        {
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                float u = s / 16f;
                using (var bg = new SolidBrush(Color.FromArgb(24, 24, 27)))
                using (var path = Round(0, 0, s, s, 3 * u)) g.FillPath(bg, path);
                using (var track = new SolidBrush(Color.FromArgb(70, 70, 78)))
                {
                    g.FillRectangle(track, 3 * u, 3 * u, 4 * u, 10 * u);
                    g.FillRectangle(track, 9 * u, 3 * u, 4 * u, 10 * u);
                }
                using (var green = new SolidBrush(Color.FromArgb(70, 200, 110))) g.FillRectangle(green, 3 * u, 6 * u, 4 * u, 7 * u);
                using (var amber = new SolidBrush(Color.FromArgb(240, 180, 50))) g.FillRectangle(amber, 9 * u, 9 * u, 4 * u, 4 * u);
            }
            using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); return ms.ToArray(); }
        }
    }

    static GraphicsPath Round(float x, float y, float w, float h, float r)
    {
        var p = new GraphicsPath();
        p.AddArc(x, y, 2 * r, 2 * r, 180, 90);
        p.AddArc(x + w - 2 * r - 1, y, 2 * r, 2 * r, 270, 90);
        p.AddArc(x + w - 2 * r - 1, y + h - 2 * r - 1, 2 * r, 2 * r, 0, 90);
        p.AddArc(x, y + h - 2 * r - 1, 2 * r, 2 * r, 90, 90);
        p.CloseFigure();
        return p;
    }

    static void Main(string[] a)
    {
        int[] sizes = { 16, 24, 32, 48, 256 };
        var imgs = new byte[sizes.Length][];
        for (int i = 0; i < sizes.Length; i++) imgs[i] = Png(sizes[i]);
        using (var w = new BinaryWriter(File.Create(a[0])))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
            int off = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)0); w.Write((byte)0); w.Write((short)1); w.Write((short)32);
                w.Write(imgs[i].Length); w.Write(off);
                off += imgs[i].Length;
            }
            foreach (var b in imgs) w.Write(b);
        }
    }
}
