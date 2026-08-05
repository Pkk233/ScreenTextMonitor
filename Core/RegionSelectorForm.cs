namespace ScreenTextMonitor.Core;

/// <summary>
/// 全屏遮罩框选屏幕区域（对应 Python 版 RegionSelector）。
/// 流程：左键按下→记录起点 → 实时显示选框 → 左键松开→记录终点 → 计算区域；ESC 取消。
/// </summary>
public sealed class RegionSelectorForm : Form
{
    private Point _start;
    private Point _current;
    private bool _dragging;
    private Rectangle? _result;

    private RegionSelectorForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
        BackColor = Color.Black;
        Opacity = 0.2;
        Cursor = Cursors.Cross;
        DoubleBuffered = true;
        KeyPreview = true;
    }

    /// <summary>返回屏幕绝对坐标的 (x, y, w, h)，取消或范围过小时返回 null。</summary>
    public static Rectangle? Select(IWin32Window owner)
    {
        using var f = new RegionSelectorForm();
        f.ShowDialog(owner);
        return f._result;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        _start = e.Location;
        _current = e.Location;
        _dragging = true;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging) return;
        _current = e.Location;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || !_dragging) return;
        _dragging = false;
        _result = CalcRegion(_start, e.Location);
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape)
        {
            _result = null;
            Close();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!_dragging) return;
        int x = Math.Min(_start.X, _current.X);
        int y = Math.Min(_start.Y, _current.Y);
        int w = Math.Abs(_current.X - _start.X);
        int h = Math.Abs(_current.Y - _start.Y);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var pen = new Pen(Color.FromArgb(0, 255, 0), 4)
        {
            DashStyle = System.Drawing.Drawing2D.DashStyle.Custom,
            DashPattern = new[] { 10f, 6f }
        };
        e.Graphics.DrawRectangle(pen, x, y, w, h);
    }

    /// <summary>把窗口内坐标换算成屏幕绝对坐标，宽高需大于 5 像素。</summary>
    private Rectangle? CalcRegion(Point p1, Point p2)
    {
        int x = Math.Min(p1.X, p2.X);
        int y = Math.Min(p1.Y, p2.Y);
        int w = Math.Abs(p2.X - p1.X);
        int h = Math.Abs(p2.Y - p1.Y);
        if (w <= 5 || h <= 5) return null;
        var origin = SystemInformation.VirtualScreen.Location;
        return new Rectangle(x + origin.X, y + origin.Y, w, h);
    }
}
