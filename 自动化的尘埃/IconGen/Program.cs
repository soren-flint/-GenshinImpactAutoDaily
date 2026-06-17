using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace IconGen;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: IconGen <srcImg> <outIco> <outPng>");
            return;
        }
        var srcPath = args[0];
        var icoPath = args[1];
        var pngPath = args[2];

        if (!File.Exists(srcPath))
        {
            Console.WriteLine("Source not found: " + srcPath);
            return;
        }

        using var src = Image.FromFile(srcPath);
        Console.WriteLine($"Source: {src.Width}x{src.Height}");

        // 中心裁剪为正方形
        var side = Math.Min(src.Width, src.Height);
        var x0 = (src.Width - side) / 2;
        var y0 = (src.Height - side) / 2;

        using var square = new Bitmap(side, side);
        using (var g = Graphics.FromImage(square))
        {
            g.DrawImage(src, new Rectangle(0, 0, side, side),
                new Rectangle(x0, y0, side, side), GraphicsUnit.Pixel);
        }

        // 生成 256 PNG（UI 用）
        using var png256 = new Bitmap(256, 256);
        using (var g = Graphics.FromImage(png256))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(square, 0, 0, 256, 256);
        }
        png256.Save(pngPath, ImageFormat.Png);
        Console.WriteLine("PNG: " + pngPath);

        // 生成多尺寸 ICO（PNG-in-ICO）
        var sizes = new[] { 256, 128, 64, 48, 32, 16 };
        var pngBytesList = new List<byte[]>();
        foreach (var s in sizes)
        {
            using var bmp = new Bitmap(s, s);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.DrawImage(square, 0, 0, s, s);
            }
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            pngBytesList.Add(ms.ToArray());
        }

        using var fs = File.Create(icoPath);
        using var bw = new BinaryWriter(fs);
        var count = sizes.Length;

        // ICONDIR
        bw.Write((ushort)0);
        bw.Write((ushort)1);
        bw.Write((ushort)count);

        // ICONDIRENTRY
        var dirSize = 6 + 16 * count;
        var offset = dirSize;
        for (var i = 0; i < count; i++)
        {
            var s = sizes[i];
            var bytes = pngBytesList[i];
            var dimByte = (byte)(s == 256 ? 0 : s);
            bw.Write(dimByte);
            bw.Write(dimByte);
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write((ushort)1);
            bw.Write((ushort)32);
            bw.Write((uint)bytes.Length);
            bw.Write((uint)offset);
            offset += bytes.Length;
        }
        foreach (var bytes in pngBytesList)
            bw.Write(bytes);

        Console.WriteLine("ICO: " + icoPath);
    }
}
