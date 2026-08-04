using System.Drawing.Drawing2D;

namespace ScreenTextMonitor.Ui;

/// <summary>
/// 顶部应用栏（手机 App 风格顶栏）：左侧页面标题 + 右侧状态胶囊。
/// 底色与内容区一致，仅靠一条发丝分隔线与内容区分隔，保持横屏画面干净。
/// </summary>
public class TopBar : BufferedPanel
{
    private string _title = "实时监控";

    public StatusPill Pill { get; }

    public TopBar()
    {
        Height = 52;
        BackColor = Theme.Bg;
        Pill = new StatusPill(96, 26) { BackColor = Theme.Accent };
        Controls.Add(Pill);
    }

    public void SetTitle(string t)
    {
        if (_title == t) return;
        _title = t;
        Invalidate();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (Pill is null) return;
        Pill.Location = new Point(Width - Pill.Width - 12, (Height - Pill.Height) / 2);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Theme.Smooth(g);
        using (var b = new SolidBrush(Theme.Bg)) g.FillRectangle(b, ClientRectangle);
        // 底部发丝分隔线
        using (var pen = new Pen(Theme.Border, 1))
            g.DrawLine(pen, 0, Height - 1, Width, Height - 1);

        // 左侧页面标题（加粗白字，竖向居中）
        TextRenderer.DrawText(g, _title, Theme.FontTitle, new Point(6, 0), Color.White,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter
            | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
    }
}
