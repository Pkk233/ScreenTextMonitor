using System.Diagnostics;
using ScreenTextMonitor.Core;
using ScreenTextMonitor.Ui;

namespace ScreenTextMonitor;

public sealed class MainForm : Form
{
    // ---------------- 常量（与 Python 版一一对应）----------------
    private const int ChangeThumbW = 480;      // 静止/变化判定用的灰度缩略图宽度
    private const double ChangeThreshold = 2.0; // 低于此值视为「画面未变」，直接跳过 OCR
    private const int PerfMaxDim = 1000;       // 降采样模式下最长边上限
    private const int IdleBackoffBase = 3;     // 连续多少次无命中后开始自动降频
    private const double IdleBackoffStep = 0.6;// 每次降频叠加的秒数
    private const double IdleBackoffCap = 5.0; // 自动降频后的最大间隔（秒）

    // ---------------- 顶部 / Tab ----------------
    private HeaderCard _header;
    private RoundedButton _btnTabRun;
    private RoundedButton _btnTabSet;
    private ScrollStack _tabRun;
    private ScrollStack _tabSet;

    // ---------------- 运行 Tab 控件 ----------------
    private FlatTextBox _entryX, _entryY, _entryW, _entryH, _entryText;
    private RoundedButton _btnSelect, _btnStart, _btnPreview, _btnClear, _btnQq;
    private Label _labelCpu, _labelStats;
    private RoundedProgressBar _cpuBar;
    private LogBox _log;

    // ---------------- 设置 Tab 控件 ----------------
    private SegmentedControl _segAlert;
    private StackPanel _alertBody, _frameBeep, _frameAudio, _frameTts;
    private FlatTextBox _entryFreq, _entryDur, _entryAudio, _entryTts;
    private RoundedButton _btnBrowse;
    private RoundedSwitch _swQq, _swSkipStatic, _swSmartSkip, _swPerfMode, _swAutoBackoff;
    private FlatTextBox _entryQqUrl, _entryQqToken, _entryQqTarget, _entryQqMsg;
    private RoundedSlider _sliderInterval, _sliderDelay, _sliderSens;
    private Label _lblInterval, _lblDelay, _lblSens;

    // ---------------- 运行状态 ----------------
    private volatile bool _monitoring;
    private Thread _monitorThread;
    private OcrEngine _engine;
    private int _statOcr, _statSkip;
    private double _statInterval = 1.0;

    private volatile float _forceOcrIdle = 4.0f;   // 强制识别兜底间隔（秒），滑块实时调
    private volatile float _ocrThreshold = 6.0f;   // 变化判定上界，滑块实时调（越小越灵敏）

    private System.Windows.Forms.Timer _cpuTimer;
    private (double proc, double wall)? _cpuPrev;
    private readonly int _cpuNproc = Math.Max(1, Environment.ProcessorCount);
    private readonly bool _ttsAvailable = Alerts.TtsAvailable();

    // F1/F5/F6/F3 新增状态
    private uint _origPriorityClass;          // Start 时记录、Stop/关闭还原
    private readonly SemaphoreSlim _alertGate = new(1, 1);  // 串行提醒，避免蜂鸣/语音叠加
    private WheelMessageFilter _wheelFilter; // F3 滚轮过滤器

    public MainForm()
    {
        Text = "屏幕文字监控工具";
        ClientSize = new Size(620, 840);
        MinimumSize = new Size(600, 720);
        BackColor = Theme.Bg;
        Font = Theme.FontUi;
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        try
        {
            string ico = Path.Combine(AppConfig.AppDir, "app.ico");
            if (File.Exists(ico)) Icon = new Icon(ico);
        }
        catch { /* 图标缺失不影响运行 */ }

        BuildUi();
        LoadConfig();

        _cpuTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _cpuTimer.Tick += (_, _) => UpdateCpu();
    }

    // ==================================================================
    // UI 构建
    // ==================================================================

    private void BuildUi()
    {
        SuspendLayout();

        // ---- 顶部标题栏（圆角蓝卡 + 状态胶囊）----
        _header = new HeaderCard("屏幕文字监控工具", "实时检测 · 低占用 · 智能提醒");
        Controls.Add(_header);

        // ---- 圆角 Tab 栏 ----
        _btnTabRun = new RoundedButton("运行", ButtonVariant.Primary, 40) { AutoWidth = false };
        _btnTabRun.Command += (_, _) => SwitchTab("run");
        _btnTabSet = new RoundedButton("设置", ButtonVariant.Secondary, 40) { AutoWidth = false };
        _btnTabSet.Command += (_, _) => SwitchTab("set");
        Controls.Add(_btnTabRun);
        Controls.Add(_btnTabSet);

        // ---- 两个可滚动内容区 ----
        _tabRun = new ScrollStack();
        _tabSet = new ScrollStack();
        Controls.Add(_tabRun);
        Controls.Add(_tabSet);

        BuildRunTab(_tabRun);
        BuildSetTab(_tabSet);

        SwitchTab("run");
        ResumeLayout();
        LayoutRoot();
    }

    private void BuildRunTab(ScrollStack run)
    {
        // ==================== 检测区域 ====================
        var cardRegion = NewCard(run, "检测区域");
        var rowRegion = NewRow(34, Theme.Surface);
        _entryX = NewEntry(56, "38");
        _entryY = NewEntry(56, "1100");
        _entryW = NewEntry(56, "674");
        _entryH = NewEntry(56, "1363");
        _btnSelect = new RoundedButton("🖱 框选区域", ButtonVariant.Secondary, 30);
        _btnSelect.Command += (_, _) => SelectRegion();
        rowRegion.Add(Lbl.Make("X:"))
                 .Add(_entryX, padL: 2, padR: 2)
                 .Add(Lbl.Make("Y:"))
                 .Add(_entryY, padL: 2, padR: 2)
                 .Add(Lbl.Make("宽:"))
                 .Add(_entryW, padL: 2, padR: 2)
                 .Add(Lbl.Make("高:"))
                 .Add(_entryH, padL: 2, padR: 2)
                 .Add(_btnSelect, padL: 10);
        cardRegion.Body.Controls.Add(rowRegion);

        // ==================== 目标文字 ====================
        var cardText = NewCard(run, "目标文字（多个用逗号分隔）");
        _entryText = NewEntry(0, "收金,比例");
        cardText.Body.Controls.Add(_entryText);

        // ==================== 控制按钮 ====================
        var rowCtrl = NewRow(42, Theme.Bg);
        rowCtrl.Margin = new Padding(0, 5, 0, 0);
        _btnStart = new RoundedButton("▶ 开始监控", ButtonVariant.Primary);
        _btnStart.Command += (_, _) => ToggleMonitor();
        _btnPreview = new RoundedButton("📷 测试截图", ButtonVariant.Secondary);
        _btnPreview.Command += (_, _) => PreviewScreenshot();
        _btnClear = new RoundedButton("清空日志", ButtonVariant.Secondary);
        _btnClear.Command += (_, _) => ClearLog();
        rowCtrl.Add(_btnStart, padL: 5, padR: 5)
               .Add(_btnPreview, padL: 5, padR: 5)
               .Add(_btnClear, right: true, padR: 5);
        run.Controls.Add(rowCtrl);

        // ==================== 实时监控 ====================
        var cardMon = NewCard(run, "实时监控");
        var rowCpu = NewRow(20, Theme.Surface);
        _labelCpu = Lbl.Make("—", Theme.TextSub);
        _labelCpu.AutoSize = false;
        _labelCpu.Width = 60;
        _cpuBar = new RoundedProgressBar(14);
        rowCpu.Add(Lbl.Make("CPU 占用:"))
              .Add(_labelCpu, padL: 5)
              .Add(_cpuBar, fill: true, padL: 5);
        cardMon.Body.Controls.Add(rowCpu);

        var rowStats = NewRow(22, Theme.Surface);
        rowStats.Margin = new Padding(0, 4, 0, 0);
        _labelStats = Lbl.Make("识别 0 次 · 跳过 0 次 · 当前间隔 1.0s", Theme.TextSub);
        rowStats.Add(_labelStats);
        cardMon.Body.Controls.Add(rowStats);

        // ==================== 日志区域 ====================
        var cardLog = NewCard(run, "运行日志");
        _log = new LogBox(170);
        cardLog.Body.Controls.Add(_log);

        // ==================== 联系作者 ====================
        var rowContact = NewRow(36, Theme.Bg);
        rowContact.Margin = new Padding(0, 3, 0, 3);
        _btnQq = new RoundedButton("👉 点击添加QQ好友", ButtonVariant.Secondary, 32);
        _btnQq.Command += (_, _) => OpenQqContact();
        rowContact.Add(Lbl.Make("联系爸爸: ", Theme.Text, bg: Theme.Bg)).Add(_btnQq);
        run.Controls.Add(rowContact);
    }

    private void BuildSetTab(ScrollStack setf)
    {
        // ==================== 提醒方式 ====================
        var cardAlert = NewCard(setf, "提醒方式");
        _alertBody = cardAlert.Body;
        _segAlert = new SegmentedControl(new[]
        {
            ("系统蜂鸣", "beep"), ("自定义音频", "audio"), ("语音播报", "tts")
        });
        _segAlert.Margin = new Padding(0, 0, 0, 8);
        _segAlert.SelectionChanged += (_, _) => OnAlertModeChange();
        _alertBody.Controls.Add(_segAlert);

        // --- 蜂鸣设置 ---
        _frameBeep = NewSubStack(new Padding(0, 5, 0, 5));
        var rowFreq = NewRow(32, Theme.Surface);
        rowFreq.Margin = new Padding(0, 1, 0, 1);
        _entryFreq = NewEntry(90, "1000");
        rowFreq.Add(Lbl.Make("频率(Hz):")).Add(_entryFreq, padL: 5, padR: 5).Add(Lbl.Make("(100-5000)"));
        _frameBeep.Controls.Add(rowFreq);

        var rowDur = NewRow(32, Theme.Surface);
        rowDur.Margin = new Padding(0, 1, 0, 1);
        _entryDur = NewEntry(90, "1000");
        rowDur.Add(Lbl.Make("时长(ms):")).Add(_entryDur, padL: 5, padR: 5).Add(Lbl.Make("(200-5000)"));
        _frameBeep.Controls.Add(rowDur);
        _alertBody.Controls.Add(_frameBeep);

        // --- 自定义音频设置 ---
        _frameAudio = NewSubStack(new Padding(0, 5, 0, 5));
        var rowAudio = NewRow(34, Theme.Surface);
        _entryAudio = NewEntry(0, "");
        _btnBrowse = new RoundedButton("浏览...", ButtonVariant.Secondary, 30);
        _btnBrowse.Command += (_, _) => BrowseAudio();
        rowAudio.Add(Lbl.Make("音频文件:")).Add(_entryAudio, fill: true, padL: 5, padR: 5).Add(_btnBrowse);
        _frameAudio.Controls.Add(rowAudio);
        _alertBody.Controls.Add(_frameAudio);

        // --- 语音播报设置 ---
        _frameTts = NewSubStack(new Padding(0, 5, 0, 5));
        if (!_ttsAvailable)
        {
            var hint = Lbl.Make("(未检测到可用的系统语音引擎)", Theme.TextSub);
            hint.Margin = new Padding(0, 3, 0, 3);
            _frameTts.Controls.Add(hint);
        }
        var rowTts = NewRow(34, Theme.Surface);
        _entryTts = NewEntry(0, "检测到目标文字！");
        rowTts.Add(Lbl.Make("播报内容:")).Add(_entryTts, fill: true, padL: 5);
        _frameTts.Controls.Add(rowTts);
        _alertBody.Controls.Add(_frameTts);

        OnAlertModeChange();

        // ==================== QQ 通知 ====================
        var cardQq = NewCard(setf, "QQ通知（检测到目标时发送）");
        var rowQqEn = NewRow(28, Theme.Surface);
        _swQq = new RoundedSwitch("启用QQ消息通知");
        rowQqEn.Add(_swQq);
        cardQq.Body.Controls.Add(rowQqEn);

        _entryQqUrl = NewEntry(220, "http://127.0.0.1:3000");
        cardQq.Body.Controls.Add(QqRow("NapCat地址:", _entryQqUrl));

        _entryQqToken = NewEntry(220, "");
        var rowToken = QqRow("Token:", _entryQqToken);
        rowToken.Add(Lbl.Make("(可选，未设置留空)"), padL: 5);
        cardQq.Body.Controls.Add(rowToken);

        _entryQqTarget = NewEntry(150, "1414111902");
        cardQq.Body.Controls.Add(QqRow("目标QQ:", _entryQqTarget));

        _entryQqMsg = NewEntry(0, "【警报】已检测到目标：{target}");
        var rowMsg = NewRow(34, Theme.Surface);
        rowMsg.Margin = new Padding(0, 2, 0, 2);
        rowMsg.Add(Lbl.Make("消息内容:")).Add(_entryQqMsg, fill: true, padL: 5);
        cardQq.Body.Controls.Add(rowMsg);

        var qqHint = Lbl.Make("提示: {target} 会被替换为实际检测到的目标文字", Theme.TextSub);
        qqHint.Margin = new Padding(0, 2, 0, 2);
        cardQq.Body.Controls.Add(qqHint);

        // ==================== 检测间隔 ====================
        var cardInterval = NewCard(setf, "检测间隔");
        var rowInterval = NewRow(28, Theme.Surface);
        _sliderInterval = new RoundedSlider(0.5, 10.0, 0.5) { Value = 1.0 };
        _lblInterval = Lbl.Make("1.0s", Theme.Text, Theme.FontUiBold);
        _lblInterval.AutoSize = false;
        _lblInterval.Width = 42;
        _sliderInterval.ValueChanged += (_, _) =>
            _lblInterval.Text = $"{_sliderInterval.Value:0.0}s";
        rowInterval.Add(Lbl.Make("间隔:"))
                   .Add(_sliderInterval, fill: true, padL: 8, padR: 10)
                   .Add(_lblInterval)
                   .Add(Lbl.Make("(0.5-10秒)", Theme.TextSub), padL: 6);
        cardInterval.Body.Controls.Add(rowInterval);

        // ==================== 性能优化 ====================
        var cardPerf = NewCard(setf, "性能优化（降低 CPU 占用）");
        _swSkipStatic = NewPerfSwitch(cardPerf, "静止时跳过 OCR（画面没变就不识别）", true);
        _swSmartSkip = NewPerfSwitch(cardPerf, "忽略微小变动（光标/时钟等小变化也跳过 OCR，强烈推荐）", true);
        _swPerfMode = NewPerfSwitch(cardPerf, "降采样识别（变化时也缩小图片再识别，精度略降）", false);
        _swAutoBackoff = NewPerfSwitch(cardPerf, "空闲自动降频（长时间无命中自动拉长轮询间隔）", true);

        var rowDelay = NewRow(28, Theme.Surface);
        rowDelay.Margin = new Padding(10, 8, 10, 2);
        _sliderDelay = new RoundedSlider(1.0, 10.0, 0.5) { Value = 4.0 };
        _lblDelay = Lbl.Make("4.0s", Theme.Text, Theme.FontUiBold);
        _lblDelay.AutoSize = false;
        _lblDelay.Width = 42;
        _sliderDelay.ValueChanged += (_, _) => OnDelayChange();
        rowDelay.Add(Lbl.Make("最大检测延迟:"))
                .Add(_sliderDelay, fill: true, padL: 8, padR: 10)
                .Add(_lblDelay);
        cardPerf.Body.Controls.Add(rowDelay);

        var rowSens = NewRow(28, Theme.Surface);
        rowSens.Margin = new Padding(10, 6, 10, 2);
        _sliderSens = new RoundedSlider(0.0, 100.0, 1.0) { Value = 33.0 };
        _lblSens = Lbl.Make("中", Theme.Text, Theme.FontUiBold);
        _lblSens.AutoSize = false;
        _lblSens.Width = 42;
        _sliderSens.ValueChanged += (_, _) => OnSensChange();
        rowSens.Add(Lbl.Make("变化灵敏度:"))
               .Add(_sliderSens, fill: true, padL: 8, padR: 10)
               .Add(_lblSens);
        cardPerf.Body.Controls.Add(rowSens);

        var perfHint = Lbl.Make(
            "OCR 是最吃 CPU 的环节；以上开关都能减少识别次数。监控区域一直在动时建议勾选「降采样识别」",
            Theme.TextSub, wrap: true, width: 520);
        perfHint.Margin = new Padding(0, 6, 0, 0);
        cardPerf.Body.Controls.Add(perfHint);
    }

    // ---------------- 构建小工具 ----------------

    private static RoundedCard NewCard(ScrollStack parent, string title)
    {
        var card = new RoundedCard(title) { Margin = new Padding(0, 5, 0, 5) };
        parent.Controls.Add(card);
        return card;
    }

    private static HRow NewRow(int height, Color bg)
    {
        return new HRow(height) { BackColor = bg, Margin = Padding.Empty };
    }

    private static StackPanel NewSubStack(Padding margin)
    {
        return new StackPanel
        {
            BackColor = Theme.Surface,
            AutoHeight = true,
            Margin = margin,
            Padding = Padding.Empty
        };
    }

    private static FlatTextBox NewEntry(int width, string text)
    {
        return new FlatTextBox(width > 0 ? width : 120, 30, text)
        {
            Margin = Padding.Empty
        };
    }

    private static HRow QqRow(string label, Control entry)
    {
        var row = NewRow(34, Theme.Surface);
        row.Margin = new Padding(0, 2, 0, 2);
        row.Add(Lbl.Make(label)).Add(entry, padL: 5);
        return row;
    }

    private static RoundedSwitch NewPerfSwitch(RoundedCard card, string text, bool value)
    {
        var sw = new RoundedSwitch(text, value) { Margin = new Padding(0, 3, 0, 3) };
        card.Body.Controls.Add(sw);
        return sw;
    }

    // ==================================================================
    // 根布局
    // ==================================================================

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        LayoutRoot();
    }

    private void LayoutRoot()
    {
        if (_header is null) return;
        const int pad = 10;
        int w = ClientSize.Width;

        _header.SetBounds(pad, pad, Math.Max(10, w - 2 * pad), 72);

        int tabY = pad + 72 + 6;
        int inner = Math.Max(20, w - 2 * pad);
        int halfW = (inner - 8) / 2;
        _btnTabRun.SetBounds(pad, tabY, halfW, 40);
        _btnTabSet.SetBounds(pad + halfW + 8, tabY, inner - halfW - 8, 40);

        int contentY = tabY + 40 + 6;
        var r = new Rectangle(pad, contentY, inner, Math.Max(20, ClientSize.Height - contentY));
        _tabRun.Bounds = r;
        _tabSet.Bounds = r;
    }

    // ==================================================================
    // 圆角 Tab 切换
    // ==================================================================

    private void SwitchTab(string which)
    {
        if (which == "run")
        {
            _tabSet.Visible = false;
            _tabRun.Visible = true;
            _btnTabRun.Variant = ButtonVariant.Primary;
            _btnTabSet.Variant = ButtonVariant.Secondary;
        }
        else
        {
            _tabRun.Visible = false;
            _tabSet.Visible = true;
            _btnTabSet.Variant = ButtonVariant.Primary;
            _btnTabRun.Variant = ButtonVariant.Secondary;
        }
    }

    // ==================================================================
    // 日志
    // ==================================================================

    private void Log(string msg, string tag = null) => _log.Append(msg, tag);

    private void LogAsync(string msg, string tag = null)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(new Action(() => Log(msg, tag))); }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
    }

    private void ClearLog()
    {
        _log.Clear();
        Log("日志已清空");
    }

    // ==================================================================
    // 提醒方式切换
    // ==================================================================

    private void OnAlertModeChange()
    {
        string mode = _segAlert.Value;
        _alertBody.SetChildVisible(_frameBeep, mode == "beep");
        _alertBody.SetChildVisible(_frameAudio, mode == "audio");
        _alertBody.SetChildVisible(_frameTts, mode == "tts");
    }

    private void BrowseAudio()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "选择音频文件",
            Filter = "音频文件|*.wav;*.mp3;*.ogg;*.flac|所有文件|*.*"
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _entryAudio.Text = dlg.FileName;
        }
    }

    private void OpenQqContact()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "tencent://AddContact/?fromId=45&fromSubId=1&subcmd=all&uin=1414111902",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log($"打开 QQ 失败: {ex.Message}", "red");
        }
    }

    // ==================================================================
    // 配置保存 / 加载
    // ==================================================================

    private void SaveConfig()
    {
        var cfg = new AppConfig
        {
            RegionX = _entryX.Text,
            RegionY = _entryY.Text,
            RegionW = _entryW.Text,
            RegionH = _entryH.Text,
            Targets = _entryText.Text,
            AlertMode = _segAlert.Value,
            Freq = _entryFreq.Text,
            Dur = _entryDur.Text,
            AudioPath = _entryAudio.Text.Trim(),
            TtsText = _entryTts.Text.Trim(),
            Interval = _sliderInterval.Value.ToString("0.0#",
                System.Globalization.CultureInfo.InvariantCulture),
            SkipStatic = _swSkipStatic.Checked,
            SmartSkip = _swSmartSkip.Checked,
            PerfMode = _swPerfMode.Checked,
            AutoBackoff = _swAutoBackoff.Checked,
            ForceOcrIdle = _forceOcrIdle,
            OcrThreshold = _ocrThreshold,
            QqEnabled = _swQq.Checked,
            QqUrl = _entryQqUrl.Text.Trim(),
            QqToken = _entryQqToken.Text.Trim(),
            QqTarget = _entryQqTarget.Text.Trim(),
            QqMsg = _entryQqMsg.Text.Trim(),
        };
        cfg.Save();
    }

    private void LoadConfig()
    {
        if (!File.Exists(AppConfig.ConfigPath)) return;
        var cfg = AppConfig.Load();

        _entryX.Text = cfg.RegionX;
        _entryY.Text = cfg.RegionY;
        _entryW.Text = cfg.RegionW;
        _entryH.Text = cfg.RegionH;
        _entryText.Text = cfg.Targets;

        _segAlert.SetValueSilent(cfg.AlertMode);
        OnAlertModeChange();

        _entryFreq.Text = cfg.Freq;
        _entryDur.Text = cfg.Dur;
        _entryAudio.Text = cfg.AudioPath;
        _entryTts.Text = cfg.TtsText;

        if (double.TryParse(cfg.Interval, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var iv))
        {
            _sliderInterval.Value = iv;
            _lblInterval.Text = $"{_sliderInterval.Value:0.0}s";
        }

        _swSkipStatic.Checked = cfg.SkipStatic;
        _swSmartSkip.Checked = cfg.SmartSkip;
        _swPerfMode.Checked = cfg.PerfMode;
        _swAutoBackoff.Checked = cfg.AutoBackoff;

        _forceOcrIdle = (float)cfg.ForceOcrIdle;
        _ocrThreshold = (float)cfg.OcrThreshold;
        _sliderDelay.Value = cfg.ForceOcrIdle;
        OnDelayChange();
        _sliderSens.Value = Math.Max(0.0, Math.Min(100.0, (8.0 - cfg.OcrThreshold) / 6.0 * 100.0));
        OnSensChange();

        _entryQqUrl.Text = cfg.QqUrl;
        _entryQqToken.Text = cfg.QqToken;
        _entryQqTarget.Text = cfg.QqTarget;
        _entryQqMsg.Text = cfg.QqMsg;
        _swQq.Checked = cfg.QqEnabled;
    }

    // ==================================================================
    // 区域框选
    // ==================================================================

    private void SelectRegion()
    {
        Log("拖动鼠标左键选择区域... 松开即完成", "bold");
        WindowState = FormWindowState.Minimized;
        _btnSelect.SetText("框选中...");
        _btnSelect.SetEnabled(false);
        Application.DoEvents();
        Thread.Sleep(180);

        Rectangle? result = RegionSelectorForm.Select(this);

        WindowState = FormWindowState.Normal;
        _btnSelect.SetText("🖱 框选区域");
        _btnSelect.SetEnabled(true);
        Activate();
        TopMost = true;
        var t = new System.Windows.Forms.Timer { Interval = 100 };
        t.Tick += (s, _) => { TopMost = false; t.Stop(); t.Dispose(); };
        t.Start();

        if (result.HasValue)
        {
            var r = result.Value;
            _entryX.Text = r.X.ToString();
            _entryY.Text = r.Y.ToString();
            _entryW.Text = r.Width.ToString();
            _entryH.Text = r.Height.ToString();
            Log($"已选择区域: ({r.X}, {r.Y}) {r.Width}x{r.Height}", "green");
        }
        else
        {
            Log("区域选择失败（拖拽范围太小或已取消）", "red");
        }
    }

    // ==================================================================
    // 测试截图
    // ==================================================================

    private void PreviewScreenshot()
    {
        try
        {
            var (x, y, w, h) = GetRegion();
            using var img = ScreenCapture.Grab(x, y, w, h);
            var win = new ImagePreviewForm(img, w, h);
            win.Show(this);
            Log($"已弹出截图预览窗口 ({w}x{h})，关闭窗口即清理", "blue");
        }
        catch (Exception ex)
        {
            Log($"截图失败: {ex.Message}", "red");
        }
    }

    // ==================================================================
    // 读取配置
    // ==================================================================

    private (int x, int y, int w, int h) GetRegion()
    {
        return (int.Parse(_entryX.Text.Trim()),
                int.Parse(_entryY.Text.Trim()),
                int.Parse(_entryW.Text.Trim()),
                int.Parse(_entryH.Text.Trim()));
    }

    private sealed class MonitorConfig
    {
        public (int X, int Y, int W, int H) Region;
        public string[] Targets;
        public string AlertMode;
        public int Freq;
        public int Dur;
        public string AudioPath;
        public string TtsText;
        public double Interval;
        public bool SkipStatic;
        public bool SmartSkip;
        public bool PerfMode;
        public bool AutoBackoff;
        public bool QqEnabled;
        public string QqUrl;
        public string QqToken;
        public string QqTarget;
        public string QqMsg;
    }

    private MonitorConfig GetConfig()
    {
        var (x, y, w, h) = GetRegion();
        return new MonitorConfig
        {
            Region = (x, y, w, h),
            Targets = _entryText.Text.Split(',')
                .Select(t => t.Trim()).Where(t => t.Length > 0).ToArray(),
            AlertMode = _segAlert.Value,
            Freq = int.Parse(_entryFreq.Text.Trim()),
            Dur = int.Parse(_entryDur.Text.Trim()),
            AudioPath = _entryAudio.Text.Trim(),
            TtsText = _entryTts.Text.Trim(),
            Interval = _sliderInterval.Value,
            SkipStatic = _swSkipStatic.Checked,
            SmartSkip = _swSmartSkip.Checked,
            PerfMode = _swPerfMode.Checked,
            AutoBackoff = _swAutoBackoff.Checked,
            QqEnabled = _swQq.Checked,
            QqUrl = _entryQqUrl.Text.Trim(),
            QqToken = _entryQqToken.Text.Trim(),
            QqTarget = _entryQqTarget.Text.Trim(),
            QqMsg = _entryQqMsg.Text.Trim(),
        };
    }

    private void ValidateConfig(MonitorConfig cfg)
    {
        if (cfg.Region.W <= 0 || cfg.Region.H <= 0)
            throw new ArgumentException("区域宽高必须大于 0");
        if (cfg.Targets.Length == 0)
            throw new ArgumentException("目标文字不能为空");

        switch (cfg.AlertMode)
        {
            case "beep":
                if (cfg.Freq < 100 || cfg.Freq > 5000) throw new ArgumentException("频率范围 100-5000 Hz");
                if (cfg.Dur < 200 || cfg.Dur > 5000) throw new ArgumentException("时长范围 200-5000 ms");
                break;
            case "audio":
                if (string.IsNullOrEmpty(cfg.AudioPath)) throw new ArgumentException("请选择音频文件或填写路径");
                break;
            case "tts":
                if (!_ttsAvailable) throw new ArgumentException("语音播报需要系统安装可用的语音引擎");
                if (string.IsNullOrEmpty(cfg.TtsText)) throw new ArgumentException("播报内容不能为空");
                break;
        }

        if (cfg.Interval < 0.5 || cfg.Interval > 10)
            throw new ArgumentException("间隔范围 0.5-10 秒");

        if (cfg.QqEnabled)
        {
            if (string.IsNullOrEmpty(cfg.QqUrl)) throw new ArgumentException("QQ通知启用时 NapCat地址 不能为空");
            if (string.IsNullOrEmpty(cfg.QqTarget)) throw new ArgumentException("QQ通知启用时 目标QQ 不能为空");
        }
    }

    // ==================================================================
    // 开始 / 停止监控
    // ==================================================================

    private void ToggleMonitor()
    {
        if (_monitoring) StopMonitor();
        else StartMonitor();
    }

    private void StartMonitor()
    {
        MonitorConfig cfg;
        try
        {
            cfg = GetConfig();
            ValidateConfig(cfg);
        }
        catch (Exception ex)
        {
            Log($"配置错误: {ex.Message}", "red");
            return;
        }

        // F1：先确保上一轮监控线程已退出，再开新一轮——避免旧线程仍在用 _engine 时被 Dispose
        StopAndWait(2000);

        _monitoring = true;
        _statOcr = 0;
        _statSkip = 0;
        _statInterval = Math.Max(0.3, cfg.Interval);
        _cpuPrev = null;   // F7：重启后重新采样，避免跨停顿时长算出 CPU 尖峰

        // 降低进程优先级，减少与其它程序的 CPU 争抢（Windows）；记录原值以便停止还原（F5）
        try
        {
            var hProc = NativeMethods.GetCurrentProcess();
            _origPriorityClass = NativeMethods.GetPriorityClass(hProc);
            NativeMethods.SetPriorityClass(hProc, NativeMethods.BELOW_NORMAL_PRIORITY_CLASS);
        }
        catch (Exception ex) { Log($"优先级调整失败（忽略）: {ex.Message}", "gray"); }

        _btnStart.SetText("⏹ 停止监控");
        _header.Pill.SetStatus("running", "运行中");

        Log(new string('=', 40), "bold");
        Log($"开始监控区域: ({cfg.Region.X}, {cfg.Region.Y}, {cfg.Region.W}, {cfg.Region.H})", "green");
        Log($"目标文字: [{string.Join(", ", cfg.Targets.Select(t => $"'{t}'"))}]", "green");
        switch (cfg.AlertMode)
        {
            case "beep":
                Log($"提醒方式: 系统蜂鸣 {cfg.Freq}Hz / {cfg.Dur}ms", "green");
                break;
            case "audio":
                Log($"提醒方式: 自定义音频 - {cfg.AudioPath}", "green");
                break;
            default:
                Log($"提醒方式: 语音播报 - 「{cfg.TtsText}」", "green");
                break;
        }
        Log($"检测间隔: {cfg.Interval:0.0}秒", "green");
        if (cfg.QqEnabled) Log($"QQ通知: 已启用 -> {cfg.QqUrl} (目标 {cfg.QqTarget})", "green");
        else Log("QQ通知: 未启用", "gray");
        Log(new string('=', 40), "bold");

        // 加载 OCR 引擎（在主线程加载，避免线程问题）
        Log("正在加载 OCR 引擎...");
        try
        {
            _engine?.Dispose();
            _engine = new OcrEngine((m, t) => Log(m, t));
            Log("OCR 引擎加载成功！", "green");
        }
        catch (Exception ex)
        {
            Log($"OCR 引擎加载失败: {ex.Message}", "red");
            _monitoring = false;
            _btnStart.SetText("▶ 开始监控");
            _header.Pill.SetStatus("stopped", "停止");
            return;
        }

        _monitorThread = new Thread(() => MonitorLoop(cfg))
        {
            IsBackground = true,
            Name = "ScreenMonitor"
        };
        _monitorThread.Start();
    }

    private void StopMonitor()
    {
        _monitoring = false;
        // F5：还原进程优先级（线程退出与否不影响优先级还原；真正的线程等待在 Start/OnFormClosing 做）
        try
        {
            NativeMethods.SetPriorityClass(NativeMethods.GetCurrentProcess(),
                _origPriorityClass != 0 ? _origPriorityClass : NativeMethods.NORMAL_PRIORITY_CLASS);
        }
        catch (Exception ex) { Log($"优先级还原失败（忽略）: {ex.Message}", "gray"); }

        _btnStart.SetText("▶ 开始监控");
        _header.Pill.SetStatus("stopped", "停止");
        Log("监控已停止", "red");
    }

    /// <summary>F1：等待监控线程退出（不改动 _monitoring，调用方负责置位）。</summary>
    private void StopAndWait(int timeoutMs)
    {
        var t = _monitorThread;
        if (t is { IsAlive: true })
        {
            try { t.Join(timeoutMs); } catch { /* 忽略 */ }
        }
    }

    private void MonitorLoop(MonitorConfig cfg)
    {
        var region = cfg.Region;
        var targets = cfg.Targets;
        double baseInterval = Math.Max(0.3, cfg.Interval);
        bool skipStatic = cfg.SkipStatic;
        bool smartSkip = cfg.SmartSkip;
        bool perfMode = cfg.PerfMode;
        bool autoBackoff = cfg.AutoBackoff;

        string qqUrl = (cfg.QqUrl ?? string.Empty).TrimEnd('/');
        string qqMsgTpl = string.IsNullOrEmpty(cfg.QqMsg) ? "【警报】已检测到目标：{target}" : cfg.QqMsg;

        int count = 0;
        int ocrCount = 0;
        int skipCount = 0;
        int idleStreak = 0;
        var thumbCalc = new GrayThumbCalc(ChangeThumbW);  // F10：复用 accum/count/输出 buffer
        byte[] prevSmall = null;                           // 上一帧（独立内存，仅分配一次）
        bool prevReady = false;
        var sw = Stopwatch.StartNew();
        double lastForceOcr = sw.Elapsed.TotalSeconds;
        double effectiveInterval;

        while (_monitoring)
        {
            Bitmap screenshot = null;
            try
            {
                screenshot = ScreenCapture.Grab(region.X, region.Y, region.W, region.H);

                // ---- 变化判定：两帧灰度缩略图的平均像素差 ----
                double diff = double.NaN;
                // F10：灰度缩略图写入复用 buffer，避免每帧 new byte[]/long[]/int[]
                thumbCalc.Compute(screenshot);
                byte[] cur = thumbCalc.Buffer;
                if (prevSmall is null || prevSmall.Length < cur.Length)
                    prevSmall = new byte[cur.Length];
                if (prevReady)
                    diff = ScreenCapture.MeanAbsDiff(cur, prevSmall);
                Buffer.BlockCopy(cur, 0, prevSmall, 0, cur.Length);
                prevReady = true;

                double now = sw.Elapsed.TotalSeconds;
                double forceIdle = _forceOcrIdle;
                bool force = (now - lastForceOcr) >= forceIdle;

                // ---- 决定是否跑 OCR（OCR 是最吃 CPU 的环节）----
                bool runOcr = true;
                if (!force && !double.IsNaN(diff))
                {
                    if (skipStatic && diff < ChangeThreshold)
                    {
                        runOcr = false;                       // 画面基本没变
                    }
                    else if (smartSkip && diff >= ChangeThreshold && diff < _ocrThreshold)
                    {
                        runOcr = false;                       // 仅微小变动（光标/时钟等），跳过 OCR
                    }
                }

                if (!runOcr)
                {
                    skipCount++;
                    idleStreak++;
                    effectiveInterval = NextInterval(autoBackoff, idleStreak, baseInterval, forceIdle);
                    PushStats(ocrCount, skipCount, effectiveInterval);
                    screenshot.Dispose();
                    screenshot = null;
                    SleepInterruptible(effectiveInterval);
                    continue;
                }

                // ---- 需要识别：可选降采样加速 ----
                lastForceOcr = now;
                ocrCount++;
                Bitmap resized = null;
                var ocrImg = screenshot;
                if (perfMode)
                {
                    resized = ScreenCapture.ResizeToMaxSide(screenshot, PerfMaxDim);
                    if (resized is not null) ocrImg = resized;
                }

                OcrOutcome result;
                try
                {
                    result = _engine.Recognize(ocrImg);
                }
                finally
                {
                    resized?.Dispose();
                }

                bool found = false;
                string allText = result.AllText;
                if (!string.IsNullOrEmpty(allText))
                {
                    foreach (var target in targets)
                    {
                        if (!allText.Contains(target, StringComparison.Ordinal)) continue;

                        found = true;
                        count++;
                        idleStreak = 0;
                        string preview = allText.Length > 60 ? allText[..60] : allText;
                        LogAsync($"[{count}] 检测到「{target}」 识别内容: {preview}", "blue");

                        // QQ 消息通知（后台发送，避免阻塞检测；附检测区域截图）
                        if (cfg.QqEnabled)
                        {
                            string qqText = qqMsgTpl.Replace("{target}", target);
                            var snapshot = (Bitmap)screenshot.Clone();
                            _ = Task.Run(() => SendQqAsync(qqUrl, cfg.QqToken, cfg.QqTarget, qqText, snapshot));
                        }

                        // F6：提醒异步化，避免蜂鸣/语音阻塞监控线程；SemaphoreSlim 串行避免叠加
                        var am = cfg.AlertMode;
                        _ = Task.Run(async () =>
                        {
                            await _alertGate.WaitAsync().ConfigureAwait(false);
                            try
                            {
                                switch (am)
                                {
                                    case "beep": Alerts.Beep(cfg.Freq, cfg.Dur); break;
                                    case "audio": Alerts.PlayAudio(cfg.AudioPath); break;
                                    case "tts": Alerts.Speak(cfg.TtsText); break;
                                }
                            }
                            finally { _alertGate.Release(); }
                        });
                        break;
                    }
                }

                if (!found) idleStreak++;
                effectiveInterval = NextInterval(autoBackoff, idleStreak, baseInterval, _forceOcrIdle);
                PushStats(ocrCount, skipCount, effectiveInterval);
                screenshot.Dispose();
                screenshot = null;
                SleepInterruptible(effectiveInterval);
            }
            catch (Exception ex)
            {
                LogAsync($"检测异常: {ex.Message}", "red");
                screenshot?.Dispose();
                screenshot = null;
                SleepInterruptible(baseInterval);
            }
        }
    }

    /// <summary>分片睡眠，停止监控时能及时退出循环。</summary>
    private void SleepInterruptible(double seconds)
    {
        int total = (int)(seconds * 1000);
        const int slice = 100;
        int slept = 0;
        while (slept < total && _monitoring)
        {
            int step = Math.Min(slice, total - slept);
            Thread.Sleep(step);
            slept += step;
        }
    }

    private async Task SendQqAsync(string url, string token, string target, string msg, Bitmap image)
    {
        try
        {
            string body = await QqNotifier.SendPrivateAsync(url, token, target, msg, image);
            LogAsync($"QQ消息已发送: {body}", "green");
        }
        catch (Exception ex)
        {
            LogAsync($"QQ消息发送失败: {ex.Message}", "red");
        }
        finally
        {
            image?.Dispose();
        }
    }

    // ==================================================================
    // CPU 占用实时显示
    // ==================================================================

    private static (double proc, double wall) GetProcessTimes()
    {
        if (!NativeMethods.GetProcessTimes(NativeMethods.GetCurrentProcess(),
                out _, out _, out var kernel, out var user))
        {
            throw new InvalidOperationException("GetProcessTimes 调用失败");
        }
        double proc = kernel.ToSeconds() + user.ToSeconds();
        double wall = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        return (proc, wall);
    }

    /// <summary>每秒采样一次，按任务管理器口径（占总 CPU 算力百分比）更新面板。</summary>
    private void UpdateCpu()
    {
        try
        {
            var cur = GetProcessTimes();
            if (_cpuPrev.HasValue)
            {
                double dtProc = cur.proc - _cpuPrev.Value.proc;
                double dtWall = cur.wall - _cpuPrev.Value.wall;
                double pct = dtWall > 0
                    ? Math.Max(0.0, Math.Min(100.0, dtProc / (dtWall * _cpuNproc) * 100.0))
                    : 0.0;

                _labelCpu.Text = $"{pct:0}%";
                Color color = pct >= 70 ? Color.Red : (pct >= 30 ? Theme.Orange : Theme.Success);
                _cpuBar.SetValue(pct, color);
                _labelCpu.ForeColor = color;
            }
            else
            {
                _labelCpu.Text = "测量中…";
                _labelCpu.ForeColor = Theme.TextSub;
            }
            _cpuPrev = cur;
        }
        catch
        {
            _labelCpu.Text = "N/A";
            _labelCpu.ForeColor = Theme.TextSub;
        }
    }

    /// <summary>
    /// 根据空闲连击次数计算下一轮轮询间隔（自适应降频）。
    /// forceIdle 为「强制识别兜底间隔」：降频上限不得超过它，否则兜底间隔失去意义。
    /// </summary>
    private static double NextInterval(bool autoBackoff, int idleStreak, double baseInterval, double forceIdle)
    {
        double cap = Math.Min(IdleBackoffCap, forceIdle);
        if (autoBackoff && idleStreak >= IdleBackoffBase)
        {
            return Math.Min(cap, baseInterval + IdleBackoffStep * (idleStreak - IdleBackoffBase + 1));
        }
        return baseInterval;
    }

    /// <summary>滑块：最大检测延迟（秒）→ 强制识别兜底间隔，实时生效。</summary>
    private void OnDelayChange()
    {
        double v = _sliderDelay.Value;
        _forceOcrIdle = (float)v;
        _lblDelay.Text = $"{v:0.0}s";
    }

    /// <summary>滑块：变化灵敏度 0~100 → 阈值 8.0~2.0（越高越灵敏），实时生效。</summary>
    private void OnSensChange()
    {
        double v = _sliderSens.Value;
        _ocrThreshold = (float)(8.0 - v / 100.0 * 6.0);
        _lblSens.Text = v < 25 ? "低" : v < 60 ? "中" : v < 85 ? "高" : "极高";
    }

    private void PushStats(int ocrCount, int skipCount, double interval)
    {
        _statOcr = ocrCount;
        _statSkip = skipCount;
        _statInterval = interval;
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(new Action(UpdateStats)); }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
    }

    /// <summary>刷新「实时监控」里的识别 / 跳过次数与当前间隔。</summary>
    private void UpdateStats()
    {
        _labelStats.Text = $"识别 {_statOcr} 次 · 跳过 {_statSkip} 次 · 当前间隔 {_statInterval:0.0}s";
    }

    // ==================================================================
    // 生命周期
    // ==================================================================

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        LayoutRoot();
        _tabRun.PerformStackLayout();
        _tabSet.PerformStackLayout();

        // F3：注册全局滚轮过滤器，让悬停卡片/输入框时也能滚动内容区
        _wheelFilter ??= new WheelMessageFilter();
        Application.AddMessageFilter(_wheelFilter);

        Log("屏幕文字监控工具已启动", "bold");
        Log("提示: 点击「框选区域」→ 鼠标拖拽选择屏幕区域 → 松开自动填入坐标", "blue");
        Log("提示: 目标文字多个用逗号分隔，如 收金,比例", "blue");
        _cpuTimer.Start();   // 启动 CPU 占用实时显示
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // F3：注销滚轮过滤器
        if (_wheelFilter is not null)
        {
            Application.RemoveMessageFilter(_wheelFilter);
            _wheelFilter = null;
        }

        _monitoring = false;
        _cpuTimer?.Stop();
        SaveConfig();
        StopAndWait(2000);          // F1：等待监控线程退出，再释放引擎，避免并发 Dispose
        try { _engine?.Dispose(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"引擎释放异常: {ex.Message}"); }  // F11
        base.OnFormClosing(e);
    }
}
