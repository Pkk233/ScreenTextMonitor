using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace ScreenTextMonitor.Core;

/// <summary>屏幕截取 + 灰度缩略图变化判定（对应 Python 版 ImageGrab + numpy 差分）。</summary>
public static class ScreenCapture
{
    /// <summary>按屏幕绝对坐标截取指定区域。</summary>
    public static Bitmap Grab(int x, int y, int w, int h)
    {
        w = Math.Max(1, w);
        h = Math.Max(1, h);
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(x, y, 0, 0, new Size(w, h), CopyPixelOperation.SourceCopy);
        return bmp;
    }

    /// <summary>GDI+ Bitmap 转 SkiaSharp 位图（零编码，直接拷贝像素）。</summary>
    public static SKBitmap ToSKBitmap(Bitmap bmp)
    {
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        byte[] buffer;
        int stride = data.Stride;
        try
        {
            buffer = new byte[stride * bmp.Height];
            Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);
        }
        finally
        {
            bmp.UnlockBits(data);
        }

        var info = new SKImageInfo(bmp.Width, bmp.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        var sk = new SKBitmap();
        if (!sk.InstallPixels(info, handle.AddrOfPinnedObject(), stride,
                (_, _) => handle.Free()))
        {
            handle.Free();
            sk.Dispose();
            throw new InvalidOperationException("无法转换位图像素缓冲区");
        }
        return sk;
    }

    /// <summary>
    /// 生成灰度缩略图（宽度固定 thumbW，高度等比），用于静止 / 微小变动判定。
    /// 灰度公式与 PIL 的 convert("L") 一致：L = 0.299R + 0.587G + 0.114B。
    /// 内部委托 <see cref="GrayThumbCalc"/>（unsafe 指针直读，复用 scratch 数组）。
    /// </summary>
    public static byte[] GrayThumbnail(Bitmap bmp, int thumbW, out int thumbH)
    {
        var calc = new GrayThumbCalc(thumbW);
        thumbH = calc.Compute(bmp);
        return calc.Buffer;
    }

    /// <summary>两帧灰度缩略图的平均绝对像素差（0-255 尺度）。</summary>
    public static double MeanAbsDiff(byte[] a, byte[] b)
    {
        if (a is null || b is null || a.Length != b.Length || a.Length == 0) return double.NaN;
        long sum = 0;
        for (int i = 0; i < a.Length; i++)
        {
            int d = a[i] - b[i];
            sum += d < 0 ? -d : d;
        }
        return (double)sum / a.Length;
    }

    /// <summary>等比缩放（降采样识别 / 预览缩略图用）。</summary>
    public static Bitmap Resize(Bitmap src, int w, int h)
    {
        var dst = new Bitmap(Math.Max(1, w), Math.Max(1, h), PixelFormat.Format32bppRgb);
        using var g = Graphics.FromImage(dst);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.DrawImage(src, new Rectangle(0, 0, dst.Width, dst.Height));
        return dst;
    }

    /// <summary>限制最长边后缩放，返回新图；若无需缩放返回 null。</summary>
    public static Bitmap ResizeToMaxSide(Bitmap src, int maxSide)
    {
        int w = src.Width, h = src.Height;
        double scale = (double)maxSide / Math.Max(w, h);
        if (scale >= 1.0) return null;
        return Resize(src, (int)(w * scale), (int)(h * scale));
    }
}

/// <summary>
/// 可复用的灰度缩略图计算器（F9/F10）：unsafe 指针直读像素、scratch 数组跨帧复用，
/// 避免每帧 Marshal.Copy 行拷贝与 new long[]/int[]/byte[] 的 GC 压力。
/// 灰度公式与 PIL convert("L") 一致：L = 0.299R + 0.587G + 0.114B。
/// </summary>
public sealed class GrayThumbCalc
{
    private readonly int _thumbW;
    private long[] _accum;
    private int[] _count;
    public byte[] Buffer;

    public GrayThumbCalc(int thumbW) => _thumbW = Math.Max(1, thumbW);

    /// <summary>计算一帧灰度缩略图，结果写入复用的 <see cref="Buffer"/>；返回当前高度。</summary>
    public int Compute(Bitmap bmp)
    {
        int sw = bmp.Width, sh = bmp.Height;
        int thumbH = Math.Max(1, (int)((long)_thumbW * sh / sw));
        int n = _thumbW * thumbH;
        if (Buffer is null || Buffer.Length < n) Buffer = new byte[n];
        if (_accum is null || _accum.Length < n) _accum = new long[n];
        if (_count is null || _count.Length < n) _count = new int[n];
        Array.Clear(_accum, 0, n);
        Array.Clear(_count, 0, n);

        var rect = new Rectangle(0, 0, sw, sh);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        try
        {
            int stride = data.Stride;
            unsafe
            {
                byte* basePtr = (byte*)data.Scan0;
                for (int y = 0; y < sh; y++)
                {
                    byte* row = basePtr + y * stride;
                    int ty = (int)((long)y * thumbH / sh);
                    if (ty >= thumbH) ty = thumbH - 1;
                    int rowBase = ty * _thumbW;
                    for (int x = 0; x < sw; x++)
                    {
                        byte* px = row + (x << 2);
                        // Format32bppRgb 字节序：B,G,R,X
                        int gray = (px[2] * 299 + px[1] * 587 + px[0] * 114) / 1000;
                        int tx = (int)((long)x * _thumbW / sw);
                        if (tx >= _thumbW) tx = _thumbW - 1;
                        int idx = rowBase + tx;
                        _accum[idx] += gray;
                        _count[idx]++;
                    }
                }
            }
            for (int i = 0; i < n; i++)
                Buffer[i] = _count[i] > 0 ? (byte)(_accum[i] / _count[i]) : (byte)0;
        }
        finally
        {
            bmp.UnlockBits(data);
        }
        return thumbH;
    }
}
