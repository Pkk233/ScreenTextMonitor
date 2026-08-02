using System.Drawing.Drawing2D;

namespace ScreenTextMonitor.Ui;

/// <summary>
/// UI 设计令牌（统一配色 / 字体 / 圆角，便于整体美化与主题切换）。
/// 与 Python 版 text.py 顶部的 C_* / FONT_* 常量逐项对应。
/// </summary>
public static class Theme
{
    public static readonly Color Bg = FromHex("#e9ecef");            // 应用底色（中性浅灰）
    public static readonly Color Surface = FromHex("#ffffff");       // 卡片 / 输入框底色（白）
    public static readonly Color Border = FromHex("#e2e8f0");        // 边框 / 分隔线
    public static readonly Color Text = FromHex("#1e293b");          // 主文字
    public static readonly Color TextSub = FromHex("#64748b");       // 次要文字 / 说明
    public static readonly Color Accent = FromHex("#3b82f6");        // 品牌强调色（蓝）
    public static readonly Color AccentHover = FromHex("#2563eb");   // 强调色 hover
    public static readonly Color AccentPress = FromHex("#1d4ed8");   // 强调色按下
    public static readonly Color AccentSoft = FromHex("#dbeafe");    // 强调色浅底
    public static readonly Color Success = FromHex("#10b981");       // 成功 / 运行中
    public static readonly Color Warning = FromHex("#f59e0b");       // 警告
    public static readonly Color Danger = FromHex("#ef4444");        // 错误 / 停止

    public static readonly Color TrackOff = FromHex("#cbd5e1");
    public static readonly Color TrackBg = FromHex("#e2e8f0");
    public static readonly Color HoverSoft = FromHex("#eef2f7");
    public static readonly Color Disabled = FromHex("#94a3b8");
    public static readonly Color ProgressTrough = FromHex("#e9eef5");
    public static readonly Color Orange = FromHex("#d9822b");

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

    public static void Smooth(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
    }
}
