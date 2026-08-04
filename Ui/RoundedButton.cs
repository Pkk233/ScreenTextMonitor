namespace ScreenTextMonitor.Ui;

public enum ButtonVariant
{
    Primary,   // 蓝底白字
    Secondary  // 白底描边
}

/// <summary>圆角按钮：primary=蓝底白字，secondary=白底描边。</summary>
public class RoundedButton : Control
{
    private ButtonVariant _variant;
    private bool _pressed;
    private bool _hover;
    private bool _enabledEx = true;
    private readonly int _radius = 12;

    public bool AutoWidth { get; set; } = true;

    public RoundedButton(string text, ButtonVariant variant = ButtonVariant.Primary,
                         int height = 36, Font font = null)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint
                 | ControlStyles.ResizeRedraw
                 | ControlStyles.SupportsTransparentBackColor, true);
        _variant = variant;
        Font = font ?? Theme.FontUiBold;
        Height = height;
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        Text = text;
        ApplyAutoWidth();
    }

    public event EventHandler Command;

    public ButtonVariant Variant
    {
        get => _variant;
        set
        {
            if (_variant == value) return;
            _variant = value;
            Invalidate();
        }
    }

    public void SetText(string t)
    {
        Text = t;
        ApplyAutoWidth();
        Invalidate();
    }

    public void SetEnabled(bool enabled)
    {
        _enabledEx = enabled;
        Cursor = enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    private void ApplyAutoWidth()
    {
        if (!AutoWidth) return;
        int w = TextRenderer.MeasureText(Text ?? string.Empty, Font).Width;
        Width = w + 34;
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        _pressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!_enabledEx || e.Button != MouseButtons.Left) return;
        _pressed = true;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_enabledEx || e.Button != MouseButtons.Left) return;
        bool wasPressed = _pressed;
        _pressed = false;
        Invalidate();
        if (wasPressed && ClientRectangle.Contains(e.Location))
        {
            Command?.Invoke(this, EventArgs.Empty);
        }
    }

    private (Color bg, Color fg) Colors()
    {
        if (_variant == ButtonVariant.Primary)
        {
            Color bg = Theme.Accent;
            if (!_enabledEx) bg = Theme.Disabled;
            else if (_pressed) bg = Theme.AccentPress;
            else if (_hover) bg = Theme.AccentHover;
            return (bg, Color.White);
        }
        else
        {
            Color bg = Theme.Surface;
            if (!_enabledEx) bg = Theme.Border;
            else if (_pressed) bg = Theme.Border;
            else if (_hover) bg = Theme.HoverSoft;
            return (bg, Theme.Text);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Theme.Smooth(g);
        PaintBackgroundLikeParent(g);

        var r = new RectangleF(1, 1, Width - 2, Height - 2);
        var (bg, fg) = Colors();

        if (_variant == ButtonVariant.Primary)
        {
            if (!_enabledEx)
            {
                Theme.DrawRoundRect(g, r, _radius, Theme.Disabled, Theme.BorderLo);
            }
            else
            {
                Theme.DrawSoftShadow(g, r, _radius);
                Theme.FillRoundRectGradient(g, r, _radius, Theme.AccentTop, Theme.AccentBot);
                using (var penLo = new Pen(Theme.AccentEdge, 1))
                    g.DrawPath(penLo, Theme.RoundRect(r, _radius));
                var rIn = new RectangleF(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
                using (var penHi = new Pen(Color.FromArgb(170, 255, 255, 255), 1))
                    g.DrawPath(penHi, Theme.RoundRect(rIn, Math.Max(0f, _radius - 1)));
                if (r.Width > 2 * _radius)
                {
                    using var penTop = new Pen(Color.FromArgb(110, 255, 255, 255), 1);
                    g.DrawLine(penTop, r.X + _radius, r.Y + 1.5f, r.X + r.Width - _radius, r.Y + 1.5f);
                }
            }
        }
        else
        {
            Theme.FillRoundRectGradient(g, r, _radius, Theme.Surface, Theme.SurfaceBot);
            using (var penLo = new Pen(Theme.BorderLo, 1))
                g.DrawPath(penLo, Theme.RoundRect(r, _radius));
            var rIn = new RectangleF(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
            using (var penHi = new Pen(Theme.BorderHi, 1))
                g.DrawPath(penHi, Theme.RoundRect(rIn, Math.Max(0f, _radius - 1)));
            if (r.Width > 2 * _radius)
            {
                using var penTop = new Pen(Color.FromArgb(40, 255, 255, 255), 1);
                g.DrawLine(penTop, r.X + _radius, r.Y + 1.5f, r.X + r.Width - _radius, r.Y + 1.5f);
            }
        }

        if (_hover && _enabledEx)
        {
            using var ov = new SolidBrush(Color.FromArgb(_variant == ButtonVariant.Primary ? 28 : 16, 255, 255, 255));
            g.FillPath(ov, Theme.RoundRect(r, _radius));
        }
        if (_pressed && _enabledEx)
        {
            using var ov = new SolidBrush(Color.FromArgb(40, 0, 0, 0));
            g.FillPath(ov, Theme.RoundRect(r, _radius));
        }

        TextRenderer.DrawText(g, Text ?? string.Empty, Font, ClientRectangle, fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
            | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
    }

    private void PaintBackgroundLikeParent(Graphics g)
    {
        Color bg = Parent?.BackColor ?? Theme.Bg;
        using var b = new SolidBrush(bg);
        g.FillRectangle(b, ClientRectangle);
    }
}
