namespace ScreenTextMonitor.Ui;

/// <summary>开启双缓冲的基础面板，避免自绘控件闪烁。</summary>
public class BufferedPanel : Panel
{
    public BufferedPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint
                 | ControlStyles.ResizeRedraw, true);
        UpdateStyles();
    }
}

/// <summary>
/// 垂直堆叠容器，等价于 tkinter 的 pack(side="top", fill="x")。
/// 子控件按添加顺序自上而下排列，宽度自动撑满，Margin 作为间距。
/// AutoHeight=true 时容器高度随内容自适应（用于卡片内部）。
/// </summary>
public class StackPanel : BufferedPanel
{
    private bool _layouting;
    private bool _dirty;

    /// <summary>
    /// 显式隐藏的子控件集合。不能直接读 Control.Visible —— 窗体尚未显示时
    /// 它对所有子控件都返回 false，会导致构建期布局全部塌陷。
    /// </summary>
    private readonly HashSet<Control> _hidden = new();

    public bool AutoHeight { get; set; } = true;

    public StackPanel()
    {
        BackColor = Theme.Surface;
    }

    /// <summary>显示 / 隐藏某个直接子控件，并立即重排。</summary>
    public void SetChildVisible(Control child, bool visible)
    {
        if (visible) _hidden.Remove(child);
        else _hidden.Add(child);
        child.Visible = visible;
        PerformStackLayout();
    }

    private bool IsChildVisible(Control c) => !_hidden.Contains(c);

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        e.Control.SizeChanged += ChildChanged;
        e.Control.VisibleChanged += ChildChanged;
        PerformStackLayout();
    }

    protected override void OnControlRemoved(ControlEventArgs e)
    {
        e.Control.SizeChanged -= ChildChanged;
        e.Control.VisibleChanged -= ChildChanged;
        base.OnControlRemoved(e);
        PerformStackLayout();
    }

    private void ChildChanged(object sender, EventArgs e)
    {
        if (_layouting)
        {
            _dirty = true;
            return;
        }
        PerformStackLayout();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        PerformStackLayout();
    }

    public void PerformStackLayout()
    {
        if (_layouting || IsDisposed || Disposing) return;
        _layouting = true;
        try
        {
            int guard = 0;
            int contentHeight;
            do
            {
                _dirty = false;
                int offY = AutoScroll ? AutoScrollPosition.Y : 0;
                int y = Padding.Top;
                int availW = ClientSize.Width - Padding.Horizontal;

                foreach (Control c in Controls)
                {
                    if (!IsChildVisible(c)) continue;
                    y += c.Margin.Top;
                    int cw = Math.Max(0, availW - c.Margin.Horizontal);
                    c.SetBounds(Padding.Left + c.Margin.Left, y + offY, cw, c.Height);
                    y += c.Height + c.Margin.Bottom;
                }
                y += Padding.Bottom;
                contentHeight = y;
            } while (_dirty && ++guard < 6);

            if (AutoScroll)
            {
                var want = new Size(0, contentHeight);
                if (AutoScrollMinSize != want) AutoScrollMinSize = want;
            }
            else if (AutoHeight && Height != contentHeight)
            {
                Height = contentHeight;
            }
        }
        finally
        {
            _layouting = false;
        }
    }
}

/// <summary>可垂直滚动的堆叠容器（对应 Python 版 ScrollableFrame）。</summary>
public class ScrollStack : StackPanel
{
    public ScrollStack()
    {
        AutoHeight = false;
        AutoScroll = true;
        BackColor = Theme.Bg;
    }

    /// <summary>按像素滚动；正值向下、负值向上。供全局滚轮过滤器调用（F3）。</summary>
    public void ScrollBy(int deltaPixels)
    {
        // 直接操纵 VerticalScroll.Value：0 在顶部，向下滚动取值增大，
        // 有明确上下界。比 AutoScrollPosition（getter/setter 双重取负、
        // 极易算反）可靠。滚轮过滤器传入「正=向下」与这里一致。
        var vs = VerticalScroll;
        if (vs is null) return;
        int max = vs.Maximum - vs.LargeChange;
        if (max <= 0) return;
        int v = vs.Value + deltaPixels;
        v = Math.Max(vs.Minimum, Math.Min(max, v));
        if (v != vs.Value)
        {
            vs.Value = v;
            PerformStackLayout();
        }
    }
}

/// <summary>
/// 全局滚轮过滤器（F3）：把 WM_MOUSEWHEEL 路由到光标下的 ScrollStack，
/// 而非默认发给焦点控件——否则悬停在卡片/输入框上时内容区不滚动。
/// 由 MainForm 在 OnShown 注册、OnFormClosed 注销。
/// </summary>
public sealed class WheelMessageFilter : IMessageFilter
{
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int WHEEL_DELTA = 120;
    private const int LineHeight = 36;

    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg != WM_MOUSEWHEEL) return false;

        var ss = FindStackUnderCursor();
        if (ss is null) return false;

        long wParam = m.WParam.ToInt64();
        short delta = (short)((wParam >> 16) & 0xFFFF);
        ss.ScrollBy(-(delta / WHEEL_DELTA) * LineHeight); // 滚轮向上(正 delta)→内容向上滚
        return true;   // 消费，阻止默认路由
    }

    private static ScrollStack FindStackUnderCursor()
    {
        var pos = Control.MousePosition;
        foreach (Form f in Application.OpenForms)
        {
            var client = f.PointToClient(pos);
            if (!f.ClientRectangle.Contains(client)) continue;

            var hit = ControlAtPoint(f, client);
            for (var c = hit; c is not null; c = c.Parent)
                if (c is ScrollStack s) return s;
        }
        return null;
    }

    private static Control ControlAtPoint(Control parent, Point pt)
    {
        foreach (Control c in parent.Controls)
        {
            if (!c.Visible) continue;
            if (c.Bounds.Contains(pt))
            {
                var sub = ControlAtPoint(c, new Point(pt.X - c.Left, pt.Y - c.Top));
                return sub ?? c;
            }
        }
        return null;
    }
}

/// <summary>
/// 水平行容器，等价于 tkinter 的 pack(side="left"/"right", fill="x", expand=True)。
/// </summary>
public class HRow : BufferedPanel
{
    private sealed class Item
    {
        public Control C;
        public bool Right;
        public bool Fill;
        public int PadL;
        public int PadR;
    }

    private readonly List<Item> _items = new();
    private bool _layouting;

    public HRow(int height = 32)
    {
        Height = height;
        BackColor = Theme.Surface;
    }

    public HRow Add(Control c, bool right = false, bool fill = false, int padL = 0, int padR = 0)
    {
        _items.Add(new Item { C = c, Right = right, Fill = fill, PadL = padL, PadR = padR });
        Controls.Add(c);
        c.SizeChanged += (_, _) => DoLayout();
        c.VisibleChanged += (_, _) => DoLayout();
        DoLayout();
        return this;
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        DoLayout();
    }

    private void DoLayout()
    {
        if (_layouting || IsDisposed) return;
        _layouting = true;
        try
        {
            int total = ClientSize.Width - Padding.Horizontal;
            int fixedW = 0, fillCount = 0;
            foreach (var it in _items)
            {
                fixedW += it.PadL + it.PadR;
                if (it.Fill) fillCount++;
                else fixedW += it.C.Width;
            }

            int remain = Math.Max(0, total - fixedW);
            int each = fillCount > 0 ? remain / fillCount : 0;

            int left = Padding.Left;
            int right = Padding.Left + total;

            foreach (var it in _items)
            {
                int w = it.Fill ? Math.Max(10, each) : it.C.Width;
                int h = it.C.Height;
                int y = Math.Max(0, (ClientSize.Height - h) / 2);
                if (it.Right)
                {
                    right -= it.PadR;
                    it.C.SetBounds(right - w, y, w, h);
                    right -= w + it.PadL;
                }
                else
                {
                    left += it.PadL;
                    it.C.SetBounds(left, y, w, h);
                    left += w + it.PadR;
                }
            }
        }
        finally
        {
            _layouting = false;
        }
    }
}

/// <summary>普通文本标签的快捷构造。</summary>
public static class Lbl
{
    public static Label Make(string text, Color? fg = null, Font font = null, Color? bg = null, bool wrap = false, int width = 0)
    {
        var l = new Label
        {
            Text = text,
            AutoSize = !wrap,
            Font = font ?? Theme.FontUi,
            ForeColor = fg ?? Theme.Text,
            BackColor = bg ?? Theme.Surface,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            TextAlign = ContentAlignment.MiddleLeft
        };
        if (wrap)
        {
            l.AutoSize = false;
            l.Width = width > 0 ? width : 540;
            l.Height = TextRenderer.MeasureText(text, l.Font, new Size(l.Width, 0),
                TextFormatFlags.WordBreak).Height + 2;
        }
        return l;
    }
}
