namespace ScreenTextMonitor.Ui;

/// <summary>带颜色标签的只读日志区（对应 Python 版 scrolledtext + tag_configure）。</summary>
public class LogBox : BufferedPanel
{
    private readonly RichTextBox _rtb;
    /// <summary>日志字符上限，超过即截掉前段保最近内容，避免无限增长占内存（F8）。</summary>
    private const int MaxChars = 20000;

    public LogBox(int height = 170)
    {
        BackColor = Theme.Border;   // 作为 1px 外边框
        Padding = new Padding(1);
        Height = height;

        _rtb = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            ReadOnly = true,
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            Font = Theme.FontUi,
            WordWrap = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            DetectUrls = false,
            TabStop = false
        };
        Controls.Add(_rtb);
    }

    public void Append(string message, string tag = null)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => Append(message, tag)));
            return;
        }

        Color color = tag switch
        {
            "green" => Theme.Success,
            "red" => Theme.Danger,
            "blue" => Theme.Accent,
            "gray" => Theme.TextSub,
            _ => Theme.Text
        };
        Font font = tag == "bold" ? Theme.FontUiBold : Theme.FontUi;

        _rtb.SelectionStart = _rtb.TextLength;
        _rtb.SelectionLength = 0;
        _rtb.SelectionColor = color;
        _rtb.SelectionFont = font;
        _rtb.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        TrimIfNeeded();
        _rtb.SelectionStart = _rtb.TextLength;
        _rtb.ScrollToCaret();
    }

    /// <summary>超过上限时删前 60%，保最近段（F8）。</summary>
    private void TrimIfNeeded()
    {
        if (_rtb.TextLength <= MaxChars) return;
        int keepFrom = (int)(MaxChars * 0.4);
        _rtb.SelectionStart = 0;
        _rtb.SelectionLength = _rtb.TextLength - keepFrom;
        _rtb.SelectedText = string.Empty;
    }

    public void Clear() => _rtb.Clear();
}
