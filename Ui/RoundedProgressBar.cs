namespace ScreenTextMonitor.Ui;

/// <summary>圆角进度条（对应 Python 版 RoundedProgressBar）。</summary>
public class RoundedProgressBar : Control
{
    private readonly double _max;
    private readonly int _pad = 2;
    private double _value;
    private Color _color = Theme.Accent;

    public RoundedProgressBar(int height = 14, double maximum = 100)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint
                 | ControlStyles.ResizeRedraw, true);
        _max = maximum;
        Height = height;
        Width = 120;
        BackColor = Theme.Surface;
    }

    public void SetValue(double value, Color? color = null)
    {
        _value = Math.Max(0, Math.Min(_max, value));
        if (color.HasValue) _color = color.Value;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Theme.Smooth(g);
        using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, ClientRectangle);
        if (Width <= 2) return;

        float cy = Height / 2f;
        float th = Height - 2 * _pad;
        Theme.DrawRoundRect(g, new RectangleF(_pad, cy - th / 2, Width - 2 * _pad, th),
            th / 2f, Theme.ProgressTrough, Theme.ProgressTrough);

        double frac = _max > 0 ? _value / _max : 0;
        float fw = (float)(_pad + frac * (Width - 2 * _pad));
        if (fw > _pad + 1)
        {
            Theme.DrawRoundRect(g, new RectangleF(_pad, cy - th / 2, fw - _pad, th),
                th / 2f, _color, _color);
        }
    }
}
