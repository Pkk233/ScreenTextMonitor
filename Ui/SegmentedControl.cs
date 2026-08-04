namespace ScreenTextMonitor.Ui;

/// <summary>圆角分段控件（对应 Python 版 SegmentedControl）。</summary>
public class SegmentedControl : Control
{
    private readonly (string Label, string Value)[] _segments;
    private readonly int _radius = 12;
    private string _value;

    public event EventHandler SelectionChanged;

    public SegmentedControl((string, string)[] options, int height = 36, Font font = null)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint
                 | ControlStyles.ResizeRedraw, true);
        _segments = options;
        _value = options[0].Item2;
        Font = font ?? Theme.FontUiBold;
        Height = height;
        Width = 300;
        BackColor = Theme.Surface;
        Cursor = Cursors.Hand;
    }

    public string Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            if (Array.FindIndex(_segments, s => s.Value == value) < 0) return;
            _value = value;
            Invalidate();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>仅更新选中态，不触发回调（用于加载配置）。</summary>
    public void SetValueSilent(string value)
    {
        if (Array.FindIndex(_segments, s => s.Value == value) < 0) return;
        _value = value;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        int n = _segments.Length;
        double seg = (double)Width / n;
        int idx = Math.Max(0, Math.Min(n - 1, (int)(e.X / seg)));
        Value = _segments[idx].Value;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Theme.Smooth(g);
        using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, ClientRectangle);
        if (Width <= 1) return;

        var rSeg = new RectangleF(1, 1, Width - 2, Height - 2);
        Theme.FillRoundRectGradient(g, rSeg, _radius, Theme.HoverSoft, Theme.BorderLo);
        using (var penLo = new Pen(Theme.BorderLo, 1)) g.DrawPath(penLo, Theme.RoundRect(rSeg, _radius));
        var rSegIn = new RectangleF(rSeg.X + 1, rSeg.Y + 1, rSeg.Width - 2, rSeg.Height - 2);
        using (var penHi = new Pen(Theme.BorderHi, 1)) g.DrawPath(penHi, Theme.RoundRect(rSegIn, _radius - 1));

        int n = _segments.Length;
        float seg = (float)Width / n;
        for (int i = 0; i < n; i++)
        {
            var (label, val) = _segments[i];
            float x0 = i * seg;
            float x1 = (i + 1) * seg;
            Color fg;
            if (val == _value)
            {
                const float pad = 3;
                var segR = new RectangleF(x0 + pad, pad, x1 - x0 - 2 * pad, Height - 2 * pad);
                Theme.FillRoundRectGradient(g, segR, _radius - 2, Theme.Surface, Theme.SurfaceBot);
                using (var sLo = new Pen(Theme.BorderLo, 1)) g.DrawPath(sLo, Theme.RoundRect(segR, _radius - 2));
                var segIn = new RectangleF(segR.X + 1, segR.Y + 1, segR.Width - 2, segR.Height - 2);
                using (var sHi = new Pen(Theme.BorderHi, 1)) g.DrawPath(sHi, Theme.RoundRect(segIn, _radius - 3));
                fg = Theme.Accent;
            }
            else
            {
                fg = Theme.TextSub;
            }

            var rect = Rectangle.Round(new RectangleF(x0, 0, x1 - x0, Height));
            TextRenderer.DrawText(g, label, Font, rect, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }
    }
}
