using System.Drawing.Drawing2D;

namespace ScreenTextMonitor.Ui;

/// <summary>
/// UI 设计令牌（统一配色 / 字体 / 圆角，便于整体美化与主题切换）。
/// 深色编辑器风：淡黑深蓝灰底 + 电光青蓝强调，贴近 IDE 视觉。
/// </summary>
public static class Theme
{
    public static readonly Color Bg = FromHex("#171d2b");            // 应用底色（淡黑深蓝灰）
    public static readonly Color Surface = FromHex("#1f2735");       // 卡片 / 输入框底色
    public static readonly Color Border = FromHex("#2c3850");        // 边框 / 分隔线
    public static readonly Color Text = FromHex("#e6edf3");          // 主文字（近白）
    public static readonly Color TextSub = FromHex("#9aa7bd");       // 次要文字 / 说明
    public static readonly Color Accent = FromHex("#38bdf8");        // 品牌强调色（电光青蓝）
    public static readonly Color AccentHover = FromHex("#7dd3fc");   // 强调色 hover
    public static readonly Color AccentPress = FromHex("#0ea5e9");   // 强调色按下
    public static readonly Color AccentSoft = FromHex("#0e2a3a");    // 强调色浅底（深青）
    public static readonly Color Success = FromHex("#3fb950");       // 成功 / 运行中
    public static readonly Color Warning = FromHex("#d29922");       // 警告
    public static readonly Color Danger = FromHex("#f85149");        // 错误 / 停止

    public static readonly Color TrackOff = FromHex("#33415c");      // 开关关态轨道
    public static readonly Color TrackBg = FromHex("#2c3850");
    public static readonly Color HoverSoft = FromHex("#243044");     // 悬停浅底
    public static readonly Color Disabled = FromHex("#5a6678");
    public static readonly Color ProgressTrough = FromHex("#283549");
    public static readonly Color Orange = FromHex("#d9822b");
    public static readonly Color Terminal = FromHex("#121826");      // 日志区终端深底（比 Bg 深一档）

    // —— 边框美化令牌（分层玻璃边框）——
    public static readonly Color SurfaceBot = FromHex("#232e4d");    // 卡片渐变底（比 Surface 略深）
    public static readonly Color BorderHi = FromHex("#3c4d72");      // 边框内亮边（玻璃斜面）
    public static readonly Color BorderLo = FromHex("#0e1422");      // 边框外暗边
    public static readonly Color AccentTop = FromHex("#7dd3fc");      // 强调渐变顶（更亮）
    public static readonly Color AccentBot = FromHex("#0ea5e9");      // 强调渐变底（更深）
    public static readonly Color AccentEdge = FromHex("#0e74a4");    // 强调边框外暗边
    public static readonly Color Shadow = FromHex("#0a0e17");        // 轻投影色

    // —— 导航栏令牌（手机 App 横屏：左侧导航轨比内容更深，制造层次）——
    public static readonly Color Rail = FromHex("#11161f");          // 导航轨底（深）
    public static readonly Color RailItem = FromHex("#1b2334");      // 导航项底（未选中悬停）
    public static readonly Color RailItemHi = FromHex("#28344e");     // 导航项高亮
    public static readonly Color RailBorder = FromHex("#2b3650");     // 导航轨右侧分隔线
    public static readonly Color RailText = FromHex("#8a98b0");       // 导航项文字（未选中）
    public static readonly Color RailTextOn = FromHex("#e6edf3");     // 导航项文字（选中/悬停）

    public static readonly Font FontUi = new("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font FontUiBold = new("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
    public static readonly Font FontTitle = new("Segoe UI", 16F, FontStyle.Bold, GraphicsUnit.Point);
    public static readonly Font FontSub = new("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font FontMono = new("Consolas", 9.5F, FontStyle.Regular, GraphicsUnit.Point);

    public static Color FromHex(string hex) => ColorTranslator.FromHtml(hex);

    /// <summary>构造圆角矩形路径（对应 Python 版的 _round_rect）。</summary>
    public static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        if (r.Width <= 0 || r.Height <= 0)
        {
            path.AddRectangle(r);
            return path;
        }

        float d = Math.Max(0.1f, Math.Min(radius, Math.Min(r.Width, r.Height) / 2f)) * 2f;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void FillRoundRect(Graphics g, RectangleF r, float radius, Color fill)
    {
        using var path = RoundRect(r, radius);
        using var brush = new SolidBrush(fill);
        g.FillPath(brush, path);
    }

    public static void DrawRoundRect(Graphics g, RectangleF r, float radius, Color fill, Color outline, float width = 1f)
    {
        using var path = RoundRect(r, radius);
        using (var brush = new SolidBrush(fill))
        {
            g.FillPath(brush, path);
        }
        using var pen = new Pen(outline, width);
        g.DrawPath(pen, path);
    }

    // —— 边框美化绘制方法（分层玻璃边框）——

    /// <summary>垂直渐变填充圆角矩形。</summary>
    public static void FillRoundRectGradient(Graphics g, RectangleF r, float radius, Color top, Color bottom)
    {
        using var path = RoundRect(r, radius);
        using var brush = new LinearGradientBrush(r, top, bottom, LinearGradientMode.Vertical);
        g.FillPath(brush, path);
    }

    /// <summary>轻投影：在卡片矩形右下方偏移绘制半透明阴影（调用方需在控件内留约 2px 边距）。</summary>
    public static void DrawSoftShadow(Graphics g, RectangleF r, float radius)
    {
        using var sb = new SolidBrush(Color.FromArgb(55, Shadow));
        g.FillPath(sb, RoundRect(new RectangleF(r.X + 1, r.Y + 2, r.Width, r.Height), radius));
    }

    /// <summary>分层玻璃卡片：渐变底 + 外暗边 + 内亮边（玻璃斜面）+ 顶部高光。</summary>
    public static void DrawCard(Graphics g, RectangleF r, float radius)
    {
        FillRoundRectGradient(g, r, radius, Surface, SurfaceBot);
        using (var penLo = new Pen(BorderLo, 1))
            g.DrawPath(penLo, RoundRect(r, radius));
        var rIn = new RectangleF(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
        using (var penHi = new Pen(BorderHi, 1))
            g.DrawPath(penHi, RoundRect(rIn, Math.Max(0f, radius - 1)));
        DrawTopHighlight(g, r, radius, 45);
    }

    /// <summary>强调渐变卡片（主按钮 / 聚焦输入框）：Accent 渐变 + 彩色外边 + 亮内边 + 顶高光。</summary>
    public static void DrawAccentCard(Graphics g, RectangleF r, float radius)
    {
        FillRoundRectGradient(g, r, radius, AccentTop, AccentBot);
        using (var penLo = new Pen(AccentEdge, 1))
            g.DrawPath(penLo, RoundRect(r, radius));
        var rIn = new RectangleF(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
        using (var penHi = new Pen(Color.FromArgb(180, 255, 255, 255), 1))
            g.DrawPath(penHi, RoundRect(rIn, Math.Max(0f, radius - 1)));
        DrawTopHighlight(g, r, radius, 120);
    }

    /// <summary>顶部 1px 低透明高光线，模拟玻璃顶边。</summary>
    private static void DrawTopHighlight(Graphics g, RectangleF r, float radius, int alpha)
    {
        if (r.Width <= 2 * radius) return;
        using var pen = new Pen(Color.FromArgb(alpha, 255, 255, 255), 1);
        float y = r.Y + 1.5f;
        g.DrawLine(pen, r.X + radius, y, r.X + r.Width - radius, y);
    }

    public static void Smooth(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
    }
}
