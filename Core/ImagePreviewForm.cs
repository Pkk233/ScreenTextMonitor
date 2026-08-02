using ScreenTextMonitor.Ui;

namespace ScreenTextMonitor.Core;

/// <summary>截图预览窗口：不落盘，关闭后释放图像资源（对应 Python 版 _show_image_preview）。</summary>
public sealed class ImagePreviewForm : Form
{
    private Bitmap _display;

    public ImagePreviewForm(Bitmap source, int rawW, int rawH)
    {
        Text = $"截图预览  {rawW}x{rawH}";
        BackColor = Theme.Bg;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedSingle;

        // 限制显示尺寸，超大图自动缩放以免撑爆窗口
        const int maxW = 960, maxH = 640;
        double scale = Math.Min(1.0, Math.Min((double)maxW / source.Width, (double)maxH / source.Height));
        _display = scale < 1.0
            ? ScreenCapture.Resize(source, (int)(source.Width * scale), (int)(source.Height * scale))
            : new Bitmap(source);

        var pic = new PictureBox
        {
            Image = _display,
            SizeMode = PictureBoxSizeMode.AutoSize,
            BackColor = Theme.Bg,
            Location = new Point(12, 12)
        };
        Controls.Add(pic);
        ClientSize = new Size(_display.Width + 24, _display.Height + 24);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        _display?.Dispose();
        _display = null;
    }
}
