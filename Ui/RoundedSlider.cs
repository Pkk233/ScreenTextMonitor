namespace ScreenTextMonitor.Ui;

/// <summary>圆角滑块（对应 Python 版 RoundedSlider，绑定 DoubleVar）。</summary>
public class RoundedSlider : Control
{
    private readonly double _from;
    private readonly double _to;
    private readonly double _step;
    private readonly int _trackH = 6;
    private readonly int _pad = 12;
    private double _value;

    public event EventHandler ValueChanged;

    public RoundedSlider(double from, double to, double step = 0, int height = 24)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint
                 | ControlStyles.ResizeRedraw, true);
        _from = from;
        _to = to;
        _step = step;
        _value = from;
        Height = height;
        Width = 160;
        BackColor = Theme.Surface;
        Cursor = Cursors.Hand;
    }

    public double Value
    {
        get => _value;
        set
        {
            double v = Math.Max(_from, Math.Min(_to, value));
            if (Math.Abs(v - _value) < 1e-9) return;
            _value = v;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private float ValueToX(double v)
    {
        double frac = Math.Abs(_to - _from) < 1e-12 ? 0 : (v - _from) / (_to - _from);
        return (float)(_pad + frac * (Width - 2 * _pad));
    }

    private double XToValue(int x)
    {
        int w = Width;
        double frac = w > 2 * _pad ? (double)(x - _pad) / (w - 2 * _pad) : 0;
        frac = Math.Max(0.0, Math.Min(1.0, frac));
        double v = _from + frac * (_to - _from);
        if (_step > 0) v = Math.Round(v / _step) * _step;
        return v;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left) Value = XToValue(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (e.Button == MouseButtons.Left) Value = XToValue(e.X);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Theme.Smooth(g);
        using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, ClientRectangle);

        if (Width <= 2 * _pad) return;

        float cy = Height / 2f;
        float th = _trackH;
        var trkR = new RectangleF(_pad, cy - th / 2, Width - 2 * _pad, th);
        Theme.FillRoundRectGradient(g, trkR, th / 2f, Theme.TrackBg, Theme.TrackBg);
        using (var pen = new Pen(Theme.BorderLo, 1)) g.DrawPath(pen, Theme.RoundRect(trkR, th / 2f));

        float vx = ValueToX(_value);
        if (vx > _pad + 1)
        {
            var fillR = new RectangleF(_pad, cy - th / 2, vx - _pad, th);
            Theme.FillRoundRectGradient(g, fillR, th / 2f, Theme.AccentTop, Theme.AccentBot);
        }

        float r = th + 4;
        using (var sb = new SolidBrush(Color.FromArgb(70, Theme.Shadow)))
            g.FillEllipse(sb, vx - r + 1, cy - r + 2, r * 2, r * 2);
        using (var kb = new SolidBrush(Theme.Accent))
        {
            g.FillEllipse(kb, vx - r, cy - r, r * 2, r * 2);
        }
        using (var pen = new Pen(Color.White, 2))
        {
            g.DrawEllipse(pen, vx - r, cy - r, r * 2, r * 2);
        }
    }
}
