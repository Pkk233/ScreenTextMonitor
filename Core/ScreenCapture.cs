using System.Buffers;
using SkiaSharp;

namespace ScreenTextMonitor.Core;

/// <summary>
/// Screen capture, grayscale thumbnail diff, and resize utilities.
/// Mirrors the Python edition's ImageGrab + numpy diff logic.
/// </summary>
public static class ScreenCapture
{
    /// <summary>Capture a screen region by absolute coordinates.</summary>
    public static Bitmap Grab(int x, int y, int w, int h, System.Drawing.Imaging.PixelFormat format = System.Drawing.Imaging.PixelFormat.Format32bppRgb)
    {
        w = Math.Max(1, w);
        h = Math.Max(1, h);
        var bmp = new Bitmap(w, h, format);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(x, y, 0, 0, new Size(w, h), CopyPixelOperation.SourceCopy);
        return bmp;
    }

    /// <summary>Convert GDI+ Bitmap to SkiaSharp bitmap (zero-copy via pixel install).</summary>
    public static SKBitmap ToSKBitmap(Bitmap bmp)
    {
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
        int stride = data.Stride;
        int bufferLen = stride * bmp.Height;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferLen);
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buffer, 0, bufferLen);
            var info = new SKImageInfo(bmp.Width, bmp.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
            var sk = new SKBitmap();
            if (!sk.InstallPixels(info, handle.AddrOfPinnedObject(), stride,
                    (_, _) => handle.Free()))
            {
                handle.Free();
                sk.Dispose();
                throw new InvalidOperationException("Failed to install SKBitmap pixels");
            }
            // The SKBitmap now owns the pinned buffer; prevent double-free
            return sk;
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(buffer);
            throw;
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    /// <summary>
    /// Mean absolute pixel diff between two grayscale thumbnail buffers (0-255 scale).
    /// </summary>
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

    /// <summary>
    /// Resize a bitmap to the given dimensions using high-quality bicubic interpolation.
    /// </summary>
    public static Bitmap Resize(Bitmap src, int w, int h)
    {
        var dst = new Bitmap(Math.Max(1, w), Math.Max(1, h), System.Drawing.Imaging.PixelFormat.Format32bppRgb);
        using var g = Graphics.FromImage(dst);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        g.DrawImage(src, new Rectangle(0, 0, dst.Width, dst.Height));
        return dst;
    }

    /// <summary>Resize so the longest side does not exceed maxSide; returns null if no resize needed.</summary>
    public static Bitmap ResizeToMaxSide(Bitmap src, int maxSide)
    {
        int w = src.Width, h = src.Height;
        double scale = (double)maxSide / Math.Max(w, h);
        if (scale >= 1.0) return null;
        return Resize(src, (int)(w * scale), (int)(h * scale));
    }
}
