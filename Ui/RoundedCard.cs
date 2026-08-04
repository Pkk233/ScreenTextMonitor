namespace ScreenTextMonitor.Ui;

/// <summary>
/// 圆角卡片容器（对应 Python 版 RoundedCard）。
/// title 为分组标题；子控件加到 Body。卡片高度随 Body 内容自适应。
/// </summary>
public class RoundedCard : BufferedPanel
{
    private readonly int _radius;
    private readonly bool _fill;
    private bool _syncing;

    public Control Body { get; }

    /// <param name="fill">true=卡片填满父容器高度，内部 Body 跟随卡片伸缩（用于「运行日志」撑满剩余空间）。</param>
    public RoundedCard(string title = "", int radius = 14, bool fill = false)
    {
        _radius = radius;
        _fill = fill;
        BackColor = Theme.Bg;

        if (_fill)
        {
            // 填充模式：Body 是普通 Panel，由卡片在尺寸变化时手动定位到内描边区域，
            // 既保留圆角边框，又让日志区随窗口高度伸缩。
            Body = new Panel { BackColor = Theme.Surface, Padding = Padding.Empty };
        }
        else
        {
            Body = new StackPanel
            {
                BackColor = Theme.Surface,
                AutoHeight = true,
                Padding = Padding.Empty
            };
            Body.SizeChanged += (_, _) => SyncHeight();
        }
        Controls.Add(Body);

        if (!string.IsNullOrEmpty(title))
        {
            var lbl = Lbl.Make(title, Theme.Text, Theme.FontUiBold);
            lbl.Margin = new Padding(0, 0, 0, 8);
            if (_fill) lbl.Dock = DockStyle.Top;
            Body.Controls.Add(lbl);
        }

        if (!_fill) Height = Body.Height + 2 * _radius;
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (_fill)
        {
            // 填充模式：Body 跟随卡片内描边区域伸缩，卡片高度由父容器（Dock.Fill）决定。
            Body.SetBounds(_radius, _radius,
                Math.Max(0, Width - 2 * _radius),
                Math.Max(0, Height - 2 * _radius));
            Body.Invalidate();
        }
        else
        {
            SyncBody();
        }
    }

    private void SyncBody()
    {
        if (_syncing) return;
        _syncing = true;
        try
        {
            Body.SetBounds(_radius, _radius, Math.Max(0, Width - 2 * _radius), Body.Height);
        }
        finally
        {
            _syncing = false;
        }
        SyncHeight();
    }

    private void SyncHeight()
    {
        if (_fill) return;
        int want = Body.Height + 2 * _radius;
        if (Height != want) Height = want;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Theme.Smooth(g);
        using (var b = new SolidBrush(Parent?.BackColor ?? Theme.Bg))
        {
            g.FillRectangle(b, ClientRectangle);
        }
        if (Width <= 3 || Height <= 3) return;
        var r = new RectangleF(2, 2, Width - 4, Height - 4);
        Theme.DrawSoftShadow(g, r, _radius);
        Theme.DrawCard(g, r, _radius);
    }
}

/// <summary>顶部标题栏：圆角蓝色卡片 + 标题 / 副标题 + 状态胶囊。</summary>
public class HeaderCard : BufferedPanel
{
    private readonly string _title;
    private readonly string _subtitle;

    public StatusPill Pill { get; }

    public HeaderCard(string title, string subtitle)
    {
        _title = title;
        _subtitle = subtitle;
        BackColor = Theme.Bg;

        // 必须先创建 Pill 再设 Height：设 Height 会同步触发 OnSizeChanged，
        // 若 Pill 尚未实例化会抛 NullReferenceException 致启动崩溃。
        Pill = new StatusPill(96, 26) { BackColor = Theme.Accent };
        Controls.Add(Pill);

        Height = 72;
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (Pill is null) return;   // 双保险：构造期/卸载期访问空引用
        Pill.Location = new Point(Width - Pill.Width - (int)(Width * 0.03),
                                  (Height - Pill.Height) / 2);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Theme.Smooth(g);
        using (var b = new SolidBrush(Parent?.BackColor ?? Theme.Bg))
        {
            g.FillRectangle(b, ClientRectangle);
        }
        if (Width <= 1 || Height <= 1) return;

        var rH = new RectangleF(2, 2, Width - 4, Height - 4);
        Theme.DrawSoftShadow(g, rH, 16);
        Theme.DrawAccentCard(g, rH, 16);

        int x = (int)(Width * 0.03);
        var titleSize = TextRenderer.MeasureText(_title, Theme.FontTitle);
        int ty = (int)(Height * 0.36) - titleSize.Height / 2;
        TextRenderer.DrawText(g, _title, Theme.FontTitle, new Point(x, ty), Color.White,
            TextFormatFlags.NoPadding);

        var subSize = TextRenderer.MeasureText(_subtitle, Theme.FontSub);
        int sy = (int)(Height * 0.78) - subSize.Height / 2;
        TextRenderer.DrawText(g, _subtitle, Theme.FontSub, new Point(x, sy),
            Color.FromArgb(225, 255, 255, 255), TextFormatFlags.NoPadding);
    }
}
