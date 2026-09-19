using System;

namespace WallSafeWinUI.Services
{
    /// <summary>
    /// Fast O(N) StackBlur algorithm (Mario Klingemann).
    /// Generates true photographic Gaussian-equivalent blur with zero pixelation or blockiness.
    /// </summary>
    public static class FastBlur
    {
        public static void ProcessBgra(byte[] pix, int w, int h, int radius)
        {
            if (radius < 1 || w < 1 || h < 1) return;

            int wm = w - 1;
            int hm = h - 1;
            int wh = w * h;
            int div = radius + radius + 1;

            int[] r = new int[wh];
            int[] g = new int[wh];
            int[] b = new int[wh];
            int[] a = new int[wh];

            int rsum, gsum, bsum, asum, x, y, i, p, yp, yi, yw;
            int[] vmin = new int[Math.Max(w, h)];

            int divsum = (div + 1) >> 1;
            divsum *= divsum;
            int[] dv = new int[256 * divsum];
            for (i = 0; i < 256 * divsum; i++)
            {
                dv[i] = i / divsum;
            }

            yw = yi = 0;

            int[][] stack = new int[div][];
            for (int s = 0; s < div; s++) stack[s] = new int[4];

            int stackpointer;
            int stackstart;
            int[] sir;
            int rbs;
            int r1 = radius + 1;
            int routsum, goutsum, boutsum, aoutsum;
            int rinsum, ginsum, binsum, ainsum;

            for (y = 0; y < h; y++)
            {
                rinsum = ginsum = binsum = ainsum = routsum = goutsum = boutsum = aoutsum = rsum = gsum = bsum = asum = 0;
                for (i = -radius; i <= radius; i++)
                {
                    p = (yi + Math.Min(wm, Math.Max(i, 0))) * 4;
                    sir = stack[i + radius];
                    sir[0] = pix[p + 2]; // R
                    sir[1] = pix[p + 1]; // G
                    sir[2] = pix[p];     // B
                    sir[3] = pix[p + 3]; // A

                    rbs = r1 - Math.Abs(i);
                    rsum += sir[0] * rbs;
                    gsum += sir[1] * rbs;
                    bsum += sir[2] * rbs;
                    asum += sir[3] * rbs;

                    if (i > 0)
                    {
                        rinsum += sir[0];
                        ginsum += sir[1];
                        binsum += sir[2];
                        ainsum += sir[3];
                    }
                    else
                    {
                        routsum += sir[0];
                        goutsum += sir[1];
                        boutsum += sir[2];
                        aoutsum += sir[3];
                    }
                }
                stackpointer = radius;

                for (x = 0; x < w; x++)
                {
                    r[yi] = dv[rsum];
                    g[yi] = dv[gsum];
                    b[yi] = dv[bsum];
                    a[yi] = dv[asum];

                    rsum -= routsum;
                    gsum -= goutsum;
                    bsum -= boutsum;
                    asum -= aoutsum;

                    stackstart = stackpointer - radius + div;
                    sir = stack[stackstart % div];

                    routsum -= sir[0];
                    goutsum -= sir[1];
                    boutsum -= sir[2];
                    aoutsum -= sir[3];

                    if (y == 0)
                    {
                        vmin[x] = Math.Min(x + radius + 1, wm);
                    }
                    p = (yw + vmin[x]) * 4;

                    sir[0] = pix[p + 2];
                    sir[1] = pix[p + 1];
                    sir[2] = pix[p];
                    sir[3] = pix[p + 3];

                    rinsum += sir[0];
                    ginsum += sir[1];
                    binsum += sir[2];
                    ainsum += sir[3];

                    rsum += rinsum;
                    gsum += ginsum;
                    bsum += binsum;
                    asum += ainsum;

                    stackpointer = (stackpointer + 1) % div;
                    sir = stack[stackpointer % div];

                    routsum += sir[0];
                    goutsum += sir[1];
                    boutsum += sir[2];
                    aoutsum += sir[3];

                    rinsum -= sir[0];
                    ginsum -= sir[1];
                    binsum -= sir[2];
                    ainsum -= sir[3];

                    yi++;
                }
                yw += w;
            }

            for (x = 0; x < w; x++)
            {
                rinsum = ginsum = binsum = ainsum = routsum = goutsum = boutsum = aoutsum = rsum = gsum = bsum = asum = 0;
                yp = -radius * w;
                for (i = -radius; i <= radius; i++)
                {
                    yi = Math.Max(0, yp) + x;

                    sir = stack[i + radius];
                    sir[0] = r[yi];
                    sir[1] = g[yi];
                    sir[2] = b[yi];
                    sir[3] = a[yi];

                    rbs = r1 - Math.Abs(i);
                    rsum += r[yi] * rbs;
                    gsum += g[yi] * rbs;
                    bsum += b[yi] * rbs;
                    asum += a[yi] * rbs;

                    if (i > 0)
                    {
                        rinsum += sir[0];
                        ginsum += sir[1];
                        binsum += sir[2];
                        ainsum += sir[3];
                    }
                    else
                    {
                        routsum += sir[0];
                        goutsum += sir[1];
                        boutsum += sir[2];
                        aoutsum += sir[3];
                    }

                    if (i < hm)
                    {
                        yp += w;
                    }
                }
                yi = x;
                stackpointer = radius;
                for (y = 0; y < h; y++)
                {
                    p = yi * 4;
                    pix[p + 2] = (byte)dv[rsum]; // R
                    pix[p + 1] = (byte)dv[gsum]; // G
                    pix[p]     = (byte)dv[bsum]; // B
                    pix[p + 3] = 255;            // A (force 100% solid opacity)

                    rsum -= routsum;
                    gsum -= goutsum;
                    bsum -= boutsum;
                    asum -= aoutsum;

                    stackstart = stackpointer - radius + div;
                    sir = stack[stackstart % div];

                    routsum -= sir[0];
                    goutsum -= sir[1];
                    boutsum -= sir[2];
                    aoutsum -= sir[3];

                    if (x == 0)
                    {
                        vmin[y] = Math.Min(y + r1, hm) * w;
                    }
                    p = x + vmin[y];

                    sir[0] = r[p];
                    sir[1] = g[p];
                    sir[2] = b[p];
                    sir[3] = a[p];

                    rinsum += sir[0];
                    ginsum += sir[1];
                    binsum += sir[2];
                    ainsum += sir[3];

                    rsum += rinsum;
                    gsum += ginsum;
                    bsum += binsum;
                    asum += ainsum;

                    stackpointer = (stackpointer + 1) % div;
                    sir = stack[stackpointer];

                    routsum += sir[0];
                    goutsum += sir[1];
                    boutsum += sir[2];
                    aoutsum += sir[3];

                    rinsum -= sir[0];
                    ginsum -= sir[1];
                    binsum -= sir[2];
                    ainsum -= sir[3];

                    yi += w;
                }
            }
        }
    }
}
