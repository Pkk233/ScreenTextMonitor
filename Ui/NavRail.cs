using System.Drawing.Drawing2D;
using System.Collections.Generic;

namespace ScreenTextMonitor.Ui;

/// <summary>
/// 左侧导航轨（手机 App 横屏风格）：顶部品牌块 + 图标导航项 + 底部实时状态灯。
/// 窄轨（宽度 &lt; 80）自动隐藏文字、仅显示图标，适配更窄的窗口。
/// 选中项用强调渐变药丸 + 外发光 + 左侧色条，作为整支界面的「记忆点」。
/// </summary>
public class NavRail : BufferedPanel
{
    private sealed class Item
    {
        public string Key = "";
        public string Icon = "";
        public string Label = "";
        public Rectangle Slot;
    }

    private readonly List<Item> _items = new();
    private string _selected = "run";
    private int _hover = -1;
    private int _press = -1;
    private bool _running;

    public NavRail()
    {
        BackColor = Theme.Rail;
        Cursor = Cursors.Hand;
    }

    public event EventHandler<string> Navigate;

    public void AddItem(string key, string icon, string label)
    {
        _items.Add(new Item { Key = key, Icon = icon, Label = label });
        ComputeSlots();
        Invalidate();
    }

    public void SetActive(string key)
    {
        if (_selected == key) return;
        _selected = key;
        Invalidate();
    }

    /// <summary>反映监控运行状态，驱动底部状态灯。</summary>
    public void SetStatus(bool running)
    {
        if (_running == running) return;
        _running = running;
        Invalidate();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        ComputeSlots();
    }

    private bool Narrow => Width < 80;

    private void ComputeSlots()
    {
        int top = Narrow ? 68 : 76;
        int slotH = Narrow ? 58 : 64;
        int x = 6;
        int w = Math.Max(10, Width - 12);
        for (int i = 0; i < _items.Count; i++)
        {
            int y = top + i * slotH;
            _items[i].Slot = new Rectangle(x, y, w, Narrow ? 54 : 58);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int idx = HitTest(e.Location);
        if (idx != _hover) { _hover = idx; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hover != -1) { _hover = -1; Invalidate(); }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left) _press = HitTest(e.Location);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        int idx = HitTest(e.Location);
        _press = -1;
        if (idx >= 0)
        {
            var it = _items[idx];
            _selected = it.Key;
            Invalidate();
            Navigate?.Invoke(this, it.Key);
        }
    }

    private int HitTest(Point p)
    {
        for (int i = 0; i < _items.Count; i++)
            if (_items[i].Slot.Contains(p)) return i;
        return -1;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Theme.Smooth(g);

        // 导航轨底
        using (var b = new SolidBrush(Theme.Rail)) g.FillRectangle(b, ClientRectangle);
        // 右侧分隔线
        using (var pen = new Pen(Theme.RailBorder, 1))
            g.DrawLine(pen, Width - 1, 0, Width - 1, Height);

        // —— 品牌块（电光青蓝渐变方块 + 眼睛图标）——
        int bw = Narrow ? 40 : 44;
        int bx = (Width - bw) / 2;
        var brand = new RectangleF(bx, 14, bw, bw);
        Theme.DrawAccentCard(g, brand, 12);
        using (var fBrand = new Font("Segoe UI", Narrow ? 18 : 20, FontStyle.Bold))
            TextRenderer.DrawText(g, "👁", fBrand, new Rectangle(bx, 14, bw, bw), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        if (!Narrow)
        {
            TextRenderer.DrawText(g, "屏幕监控", Theme.FontSub,
                new Rectangle(0, 14 + bw + 4, Width, 16), Theme.TextSub,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
        }

        // —— 导航项 ——
        for (int i = 0; i < _items.Count; i++)
        {
            var it = _items[i];
            bool sel = it.Key == _selected;
            bool hov = i == _hover;
            var slot = new RectangleF(it.Slot.X, it.Slot.Y, it.Slot.Width, it.Slot.Height);

            if (sel)
            {
                // 外发光
                using (var gp = new SolidBrush(Color.FromArgb(55, Theme.Accent)))
                using (var glow = Theme.RoundRect(new RectangleF(slot.X - 2, slot.Y - 2, slot.Width + 4, slot.Height + 4), 16))
                    g.FillPath(gp, glow);
                // 选中药丸 + 左侧强调色条
                Theme.DrawAccentCard(g, slot, 14);
                using (var bar = new SolidBrush(Theme.AccentHover))
                using (var bp = Theme.RoundRect(new RectangleF(slot.X - 6, slot.Y + 10, 4, slot.Height - 20), 2))
                    g.FillPath(bar, bp);
            }
            else if (hov)
            {
                Theme.FillRoundRectGradient(g, slot, 12, Theme.RailItemHi, Theme.RailItem);
                using (var pen = new Pen(Theme.RailBorder, 1))
                using (var p = Theme.RoundRect(slot, 12))
                    g.DrawPath(pen, p);
            }

            int iconSize = sel ? 22 : 20;
            int iconBoxH = Narrow ? it.Slot.Height : 40;
            int iconY = it.Slot.Y + (Narrow ? (it.Slot.Height - iconBoxH) / 2 : 6);
            Color iconColor = sel ? Color.White : (hov ? Theme.RailTextOn : Theme.RailText);
            using (var fIcon = new Font("Segoe UI", iconSize, FontStyle.Regular))
                TextRenderer.DrawText(g, it.Icon, fIcon,
                    new Rectangle(it.Slot.X, iconY, it.Slot.Width, iconBoxH), iconColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            if (!Narrow)
            {
                int ly = it.Slot.Y + 44;
                Color lblColor = sel ? Color.White : (hov ? Theme.RailTextOn : Theme.RailText);
                TextRenderer.DrawText(g, it.Label, Theme.FontSub,
                    new Rectangle(it.Slot.X, ly, it.Slot.Width, 16), lblColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
            }
        }

        // —— 底部实时状态灯 ——
        int ledY = Height - 34;
        int ledX = Narrow ? Width / 2 - 5 : 16;
        Color led = _running ? Theme.Success : Theme.FromHex("#475569");
        if (_running)
        {
            using (var gp = new SolidBrush(Color.FromArgb(60, Theme.Success)))
                g.FillEllipse(gp, ledX - 3, ledY - 3, 16, 16);
        }
        using (var ld = new SolidBrush(led))
            g.FillEllipse(ld, ledX, ledY, 10, 10);

        if (!Narrow)
        {
            TextRenderer.DrawText(g, _running ? "运行中" : "已停止",
                Theme.FontSub, new Rectangle(ledX + 16, ledY - 3, Width - ledX - 20, 16),
                _running ? Theme.Success : Theme.TextSub,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}
