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

        var (bg, fg) = Colors();
        Color border = _variant == ButtonVariant.Secondary ? Theme.Border : bg;
        var r = new RectangleF(1, 1, Width - 2, Height - 2);
        Theme.DrawRoundRect(g, r, _radius, bg, border);

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
