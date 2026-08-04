namespace ScreenTextMonitor.Ui;

/// <summary>iOS 风格圆角开关（对应 Python 版 RoundedSwitch）。</summary>
public class RoundedSwitch : Control
{
    private bool _checked;
    private readonly int _trackW = 44;
    private readonly int _trackH;
    private readonly string _caption;

    public event EventHandler CheckedChanged;

    public RoundedSwitch(string text = "", bool value = false, int height = 24)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint
                 | ControlStyles.ResizeRedraw, true);
        _caption = text ?? string.Empty;
        _trackH = height;
        _checked = value;
        Font = Theme.FontUi;
        BackColor = Theme.Surface;
        ForeColor = Theme.Text;
        Cursor = Cursors.Hand;

        int textW = string.IsNullOrEmpty(_caption)
            ? 0
            : TextRenderer.MeasureText(_caption, Font).Width + 8;
        Width = _trackW + textW;
        Height = Math.Max(height, Font.Height + 4);
    }

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        // F12：整控件可点切换（含标签文字区域），不再限制只能点轨道。
        if (e.Button == MouseButtons.Left)
        {
            Checked = !Checked;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Theme.Smooth(g);
        using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, ClientRectangle);

        int top = (Height - _trackH) / 2;
        Color track = _checked ? Theme.Accent : Theme.TrackOff;
        var r = new RectangleF(1, top + 1, _trackW - 2, _trackH - 2);
        Theme.FillRoundRectGradient(g, r, _trackH / 2f,
            _checked ? Theme.AccentTop : Theme.TrackOff,
            _checked ? Theme.AccentBot : Theme.TrackOff);
        using (var pen = new Pen(Theme.BorderLo, 1)) g.DrawPath(pen, Theme.RoundRect(r, _trackH / 2f));

        float kx = _checked ? _trackW - _trackH / 2f - 2 : _trackH / 2f + 2;
        float ky = top + _trackH / 2f;
        float kr = _trackH / 2f - 3;
        using (var sb = new SolidBrush(Color.FromArgb(60, Theme.Shadow)))
            g.FillEllipse(sb, kx - kr + 1, ky - kr + 2, kr * 2, kr * 2);
        using (var wb = new SolidBrush(Color.White))
        {
            g.FillEllipse(wb, kx - kr, ky - kr, kr * 2, kr * 2);
        }

        if (!string.IsNullOrEmpty(_caption))
        {
            var rect = new Rectangle(_trackW + 8, 0, Width - _trackW - 8, Height);
            TextRenderer.DrawText(g, _caption, Font, rect, ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }
    }
}
