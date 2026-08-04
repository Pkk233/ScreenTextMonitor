namespace ScreenTextMonitor.Ui;

/// <summary>圆角状态胶囊（对应 Python 版 StatusPill）。</summary>
public class StatusPill : Control
{
    private static readonly Dictionary<string, (Color Fg, Color Bg)> Map = new()
    {
        ["running"] = (Theme.Success, Theme.FromHex("#d1fae5")),
        ["stopped"] = (Theme.FromHex("#94a3b8"), Theme.FromHex("#e2e8f0")),
        ["error"] = (Theme.Danger, Theme.FromHex("#fee2e2")),
    };

    private string _status = "stopped";
    private string _label = "停止";
    private readonly int _radius = 13;

    public StatusPill(int width = 96, int height = 26)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint
                 | ControlStyles.ResizeRedraw, true);
        Width = width;
        Height = height;
        BackColor = Theme.Accent;
        Font = Theme.FontUiBold;
    }

    public void SetStatus(string status, string text)
    {
        _status = status;
        _label = text;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Theme.Smooth(g);
        using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, ClientRectangle);
        if (Width <= 1) return;

        var (fg, bg) = Map.TryGetValue(_status, out var v) ? v : Map["stopped"];
        Theme.FillRoundRectGradient(g, new RectangleF(1, 1, Width - 2, Height - 2), _radius, bg, bg);
        using (var pen = new Pen(Theme.BorderLo, 1)) g.DrawPath(pen, Theme.RoundRect(new RectangleF(1, 1, Width - 2, Height - 2), _radius));

        using (var dot = new SolidBrush(fg))
        {
            g.FillEllipse(dot, 12, Height / 2f - 4, 8, 8);
        }

        var rect = new Rectangle(26, 0, Width - 28, Height);
        TextRenderer.DrawText(g, _label, Font, rect, fg,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter
            | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
    }
}
