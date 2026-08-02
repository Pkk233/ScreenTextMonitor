namespace ScreenTextMonitor.Ui;

/// <summary>白底 / 细边框 / 聚焦高亮的输入框（对应 ttk 的 TEntry 样式）。</summary>
public class FlatTextBox : BufferedPanel
{
    private readonly TextBox _inner;
    private bool _focused;

    public FlatTextBox(int width = 120, int height = 30, string text = "")
    {
        BackColor = Theme.Surface;

        // 必须先建 _inner 再设尺寸：设置 Width/Height 会同步触发 OnSizeChanged→LayoutInner，
        // 若 _inner 尚未实例化会抛 NullReferenceException 致启动崩溃。
        _inner = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Font = Theme.FontUi,
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            Text = text
        };
        _inner.GotFocus += (_, _) => { _focused = true; Invalidate(); };
        _inner.LostFocus += (_, _) => { _focused = false; Invalidate(); };
        Controls.Add(_inner);

        Width = width;
        Height = height;
        LayoutInner();
    }

    public override string Text
    {
        get => _inner.Text;
        set => _inner.Text = value;
    }

    public bool UseSystemPasswordChar
    {
        get => _inner.UseSystemPasswordChar;
        set => _inner.UseSystemPasswordChar = value;
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        LayoutInner();
    }

    private void LayoutInner()
    {
        if (_inner is null) return;   // 双保险：构造期/卸载期
        int th = _inner.PreferredHeight;
        _inner.SetBounds(6, Math.Max(1, (Height - th) / 2), Math.Max(10, Width - 12), th);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        using (var b = new SolidBrush(Theme.Surface)) g.FillRectangle(b, ClientRectangle);
        using var pen = new Pen(_focused ? Theme.Accent : Theme.Border, 1);
        g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _inner.Focus();
    }
}
