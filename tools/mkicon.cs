using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using S136Battery;

// Hace app.ico (PNG dentro de ICO) con el logo vectorial de Art.cs.
// mkicon <salida.ico> [preview.png]
static class MkIcon
{
    static byte[] Png(int s)
    {
        using (var bmp = new Bitmap(s, s))
        {
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                Art.DrawLogo(g, 0, 0, s);
            }
            using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); return ms.ToArray(); }
        }
    }

    static void Main(string[] a)
    {
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
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
        if (a.Length > 1)
            using (var bmp = new Bitmap(560, 300))
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                Art.DrawLogo(g, 10, 10, 280);
                int x = 300;
                foreach (int s in new[] { 16, 24, 32, 48, 64 }) { Art.DrawLogo(g, x, 40, s); x += s + 10; }
                g.FillRectangle(Brushes.Black, 300, 150, 250, 140);
                x = 310;
                foreach (int s in new[] { 16, 24, 32, 48, 64 }) { Art.DrawLogo(g, x, 170, s); x += s + 10; }
                bmp.Save(a[1], ImageFormat.Png);
            }
    }
}
