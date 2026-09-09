using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace WallSafe
{
    public static class IconBuilder
    {
        public static void GenerateIcoFile(string outputPath)
        {
            int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // ICO Header: 0, 1 (icon type), count
            bw.Write((short)0);
            bw.Write((short)1);
            bw.Write((short)sizes.Length);

            var pngBytesList = new System.Collections.Generic.List<byte[]>();

            foreach (var size in sizes)
            {
                using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
                using var g = Graphics.FromImage(bmp);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                // Draw modern rounded shield / wallpaper frame
                float margin = size * 0.08f;
                float rectSize = size - (margin * 2);

                // Background gradient
                using var path = new GraphicsPath();
                float r = size * 0.22f;
                path.AddArc(margin, margin, r, r, 180, 90);
                path.AddArc(margin + rectSize - r, margin, r, r, 270, 90);
                path.AddArc(margin + rectSize - r, margin + rectSize - r, r, r, 0, 90);
                path.AddArc(margin, margin + rectSize - r, r, r, 90, 90);
                path.CloseFigure();

                using (var brush = new LinearGradientBrush(
                    new PointF(margin, margin),
                    new PointF(margin + rectSize, margin + rectSize),
                    Color.FromArgb(255, 99, 102, 241), // Indigo #6366F1
                    Color.FromArgb(255, 168, 85, 247)  // Purple #A855F7
                ))
                {
                    g.FillPath(brush, path);
                }

                // Inner mountain / landscape sun artwork (representing wallpaper)
                float sunX = size * 0.35f;
                float sunY = size * 0.35f;
                float sunR = size * 0.20f;
                using (var sunBrush = new SolidBrush(Color.FromArgb(230, 254, 240, 138)))
                {
                    g.FillEllipse(sunBrush, sunX, sunY, sunR, sunR);
                }

                // Mountain silhouettes
                using (var mtnPath = new GraphicsPath())
                {
                    mtnPath.AddPolygon(new PointF[]
                    {
                        new PointF(margin, margin + rectSize * 0.85f),
                        new PointF(margin + rectSize * 0.45f, margin + rectSize * 0.40f),
                        new PointF(margin + rectSize * 0.75f, margin + rectSize * 0.75f),
                        new PointF(margin + rectSize * 0.90f, margin + rectSize * 0.58f),
                        new PointF(margin + rectSize, margin + rectSize * 0.75f),
                        new PointF(margin + rectSize, margin + rectSize),
                        new PointF(margin, margin + rectSize),
                    });

                    using var clip = path.Clone() as GraphicsPath;
                    g.SetClip(clip!);
                    using var mtnBrush1 = new SolidBrush(Color.FromArgb(200, 30, 27, 75));
                    g.FillPath(mtnBrush1, mtnPath);
                    g.ResetClip();
                }

                // Lock / Safe symbol overlay in corner (shield checkmark or safe glow)
                float lockX = margin + rectSize * 0.60f;
                float lockY = margin + rectSize * 0.60f;
                float lockS = rectSize * 0.38f;
                using (var badgePath = new GraphicsPath())
                {
                    badgePath.AddEllipse(lockX, lockY, lockS, lockS);
                    using var badgeBrush = new SolidBrush(Color.FromArgb(240, 16, 185, 129)); // Emerald Green
                    g.FillPath(badgeBrush, badgePath);

                    // Checkmark or shield
                    using var pen = new Pen(Color.White, Math.Max(1.2f, size * 0.045f))
                    {
                        StartCap = LineCap.Round,
                        EndCap = LineCap.Round
                    };
                    g.DrawLines(pen, new PointF[]
                    {
                        new PointF(lockX + lockS * 0.28f, lockY + lockS * 0.52f),
                        new PointF(lockX + lockS * 0.46f, lockY + lockS * 0.70f),
                        new PointF(lockX + lockS * 0.74f, lockY + lockS * 0.32f),
                    });
                }

                using var pngMs = new MemoryStream();
                bmp.Save(pngMs, ImageFormat.Png);
                pngBytesList.Add(pngMs.ToArray());
            }

            int offset = 6 + (16 * sizes.Length);

            for (int i = 0; i < sizes.Length; i++)
            {
                int s = sizes[i];
                byte[] data = pngBytesList[i];

                bw.Write((byte)(s >= 256 ? 0 : s)); // Width
                bw.Write((byte)(s >= 256 ? 0 : s)); // Height
                bw.Write((byte)0); // Color count
                bw.Write((byte)0); // Reserved
                bw.Write((short)1); // Planes
                bw.Write((short)32); // Bits per pixel
                bw.Write((int)data.Length); // Size of image data
                bw.Write((int)offset); // Offset of image data

                offset += data.Length;
            }

            foreach (var data in pngBytesList)
            {
                bw.Write(data);
            }

            File.WriteAllBytes(outputPath, ms.ToArray());
        }
    }
}
