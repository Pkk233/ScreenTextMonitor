using System.Buffers;
using System.Drawing.Imaging;

namespace ScreenTextMonitor.Core;

/// <summary>
/// Reusable grayscale thumbnail calculator with buffer reuse.
/// Gray formula matches PIL convert("L"): L = 0.299R + 0.587G + 0.114B.
/// </summary>
public sealed class GrayThumbCalc : IDisposable
{
    private readonly int _thumbW;
    private long[] _accum;
    private int[] _count;
    private byte[] _buffer;
    private bool _disposed;

    public byte[] Buffer => _buffer ?? Array.Empty<byte>();

    public GrayThumbCalc(int thumbW) => _thumbW = Math.Max(1, thumbW);

    /// <summary>
    /// Compute a grayscale thumbnail for the given bitmap.
    /// Result is written to the reusable <see cref="Buffer"/>.
    /// Returns the computed thumbnail height.
    /// </summary>
    public int Compute(Bitmap bmp)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        int sw = bmp.Width, sh = bmp.Height;
        int thumbH = Math.Max(1, (int)((long)_thumbW * sh / sw));
        int n = _thumbW * thumbH;

        EnsureCapacity(ref _accum, n);
        EnsureCapacity(ref _count, n);
        EnsureCapacity(ref _buffer, n);
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
                        // Format32bppRgb: B,G,R,X
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
                _buffer[i] = _count[i] > 0 ? (byte)(_accum[i] / _count[i]) : (byte)0;
        }
        finally
        {
            bmp.UnlockBits(data);
        }
        return thumbH;
    }

    private static void EnsureCapacity<T>(ref T[] arr, int required)
    {
        if (arr is null || arr.Length < required)
            arr = new T[required];
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _accum = null;
        _count = null;
        _buffer = null;
    }
}
