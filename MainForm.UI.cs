using ScreenTextMonitor.Core;
using ScreenTextMonitor.Ui;

namespace ScreenTextMonitor;

public sealed partial class MainForm
{
    // ==================================================================
    // UI Construction
    // ==================================================================

    private void BuildUi()
    {
        SuspendLayout();

        _rail = new NavRail();
        _rail.BrandImagePath = Path.Combine(AppConfig.AppDir, "assets", "brand.jpg");
        _rail.AddItem("run", "🏶", "运行");
        _rail.AddItem("set", "⚙️", "设置");
        _rail.Navigate += (_, key) => SwitchTab(key);
        Controls.Add(_rail);

        _topBar = new TopBar();
        Controls.Add(_topBar);

        _tabRun = new Panel { BackColor = Theme.Bg };
        _tabSet = new ScrollStack();
        Controls.Add(_tabRun);
        Controls.Add(_tabSet);

        BuildRunTab(_tabRun);
        BuildSetTab(_tabSet);

        SwitchTab("run");
        ResumeLayout();
        LayoutRoot();
    }

    private void BuildRunTab(Panel run)
    {
        var two = new TwoColPanel(12) { Dock = DockStyle.Fill };
        run.Controls.Add(two);

        // ---- Left column: Config ----
        var cardRegion = NewCard(two.LeftCol, "检测区域");
        var rowRegion = NewRow(34, Theme.Surface);
        _entryX = NewEntry(56, "38");
        _entryY = NewEntry(56, "1100");
        _entryW = NewEntry(56, "674");
        _entryH = NewEntry(56, "1363");
        _btnSelect = new RoundedButton("🖱 框选区域", ButtonVariant.Primary, 34);
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

        var cardText = NewCard(two.LeftCol, "目标文字（多个用逗号分隔）");
        _entryText = NewEntry(0, "收金,比例");
        cardText.Body.Controls.Add(_entryText);

        var rowMini = NewRow(42, Theme.Bg);
        rowMini.Margin = new Padding(0, 5, 0, 0);
        _btnPreview = new RoundedButton("📲 测试截图", ButtonVariant.Primary, 38);
        _btnPreview.Command += (_, _) => PreviewScreenshot();
        _btnClear = new RoundedButton("清空日志", ButtonVariant.Primary, 38);
        _btnClear.Command += (_, _) => ClearLog();
        rowMini.Add(_btnPreview, padL: 5, padR: 5).Add(_btnClear, right: true, padR: 5);
        two.LeftCol.Controls.Add(rowMini);

        // ---- Right column: Status + Control ----
        var cardMon = NewCard(two.RightCol, "实时监控");
        cardMon.Dock = DockStyle.Top;
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

        _btnStart = new RoundedButton("▶ 开始监控", ButtonVariant.Primary, 46) { AutoWidth = false };
        _btnStart.Margin = new Padding(0, 6, 0, 6);
        _btnStart.Dock = DockStyle.Top;
        _btnStart.Command += (_, _) => ToggleMonitor();
        two.RightCol.Controls.Add(_btnStart);

        var rowContact = NewRow(36, Theme.Bg);
        rowContact.Margin = new Padding(0, 3, 0, 3);
        _btnQq = new RoundedButton("👠 点击添加QQ好友", ButtonVariant.Secondary, 32);
        _btnQq.Command += (_, _) => OpenQqContact();
        rowContact.Add(Lbl.Make("联系爸爸: ", Theme.Text, bg: Theme.Bg)).Add(_btnQq);
        rowContact.Dock = DockStyle.Bottom;
        two.RightCol.Controls.Add(rowContact);

        // ---- Bottom: Full-width log ----
        _logCard = NewCard(run, "运行日志", fill: true);
        _logCard.Dock = DockStyle.Bottom;
        _log = new LogBox { Dock = DockStyle.Fill };
        _logCard.Body.Controls.Add(_log);
        run.SizeChanged += (_, _) => SyncLogHeight(run);
        SyncLogHeight(run);
    }

    private void SyncLogHeight(Panel run)
    {
        if (_logCard is null || run.ClientSize.Height <= 0) return;
        int want = Math.Max(200, run.ClientSize.Height / 2);
        if (_logCard.Height != want) _logCard.Height = want;
    }

    private void BuildSetTab(ScrollStack set)
    {
        // ---- Alert Settings ----
        var cardAlert = NewCard(set, "警报设置");
        _segAlert = new SegmentedControl(new[]
        {
            ("系统蜂鸣", "beep"),
            ("自定义音频", "audio"),
            ("语音播报", "tts")
        });
        _segAlert.Margin = new Padding(0, 0, 0, 8);
        _segAlert.SelectionChanged += (_, _) => OnAlertModeChange();
        cardAlert.Body.Controls.Add(_segAlert);

        _alertBody = NewSubStack(new Padding(0, 6, 0, 0));
        cardAlert.Body.Controls.Add(_alertBody);

        // Beep
        _frameBeep = NewSubStack(Padding.Empty);
        _entryFreq = NewEntry(100, "1000");
        _entryDur = NewEntry(100, "1000");
        _frameBeep.Controls.Add(NewRow(34, Theme.Surface)
            .Add(Lbl.Make("频率 (Hz):")).Add(_entryFreq, padL: 6)
            .Add(Lbl.Make("时长 (ms):")).Add(_entryDur, padL: 6));
        _alertBody.Controls.Add(_frameBeep);

        // Audio
        _frameAudio = NewSubStack(Padding.Empty);
        _entryAudio = NewEntry(0, "");
        _btnBrowse = new RoundedButton("浏览...", ButtonVariant.Secondary, 30);
        _btnBrowse.Command += (_, _) => BrowseAudio();
        _frameAudio.Controls.Add(NewRow(34, Theme.Surface)
            .Add(Lbl.Make("音频文件:")).Add(_entryAudio, fill: true, padL: 6)
            .Add(_btnBrowse, padL: 6));
        _alertBody.Controls.Add(_frameAudio);

        // TTS
        _frameTts = NewSubStack(Padding.Empty);
        _entryTts = NewEntry(0, "");
        _frameTts.Controls.Add(NewRow(34, Theme.Surface)
            .Add(Lbl.Make("播报内容:")).Add(_entryTts, fill: true, padL: 6));
        _alertBody.Controls.Add(_frameTts);

        OnAlertModeChange();

        // ---- Detection Settings ----
        var cardDetect = NewCard(set, "检测设置");

        var descInterval = Lbl.Make("检测间隔：截图后多久进行下一次 OCR 识别", Theme.TextSub, Theme.FontSub);
        descInterval.Margin = new Padding(0, 4, 0, 2);
        cardDetect.Body.Controls.Add(descInterval);
        _sliderInterval = new RoundedSlider(0.5, 10.0, 0.5) { Value = 1.0 };
        cardDetect.Body.Controls.Add(_sliderInterval);
        _lblInterval = Lbl.Make("1.0s", Theme.TextSub);
        _lblInterval.Margin = new Padding(0, 0, 0, 8);
        cardDetect.Body.Controls.Add(_lblInterval);

        var descDelay = Lbl.Make("提醒间隔：两次警报之间的最小间隔", Theme.TextSub, Theme.FontSub);
        descDelay.Margin = new Padding(0, 2, 0, 2);
        cardDetect.Body.Controls.Add(descDelay);
        _sliderDelay = new RoundedSlider(1.0, 10.0, 0.5) { Value = 4.0 };
        cardDetect.Body.Controls.Add(_sliderDelay);
        _lblDelay = Lbl.Make("4.0s", Theme.TextSub);
        _lblDelay.Margin = new Padding(0, 0, 0, 8);
        cardDetect.Body.Controls.Add(_lblDelay);

        var descSens = Lbl.Make("相似度阈值：判断画面是否静止的灵敏度", Theme.TextSub, Theme.FontSub);
        descSens.Margin = new Padding(0, 2, 0, 2);
        cardDetect.Body.Controls.Add(descSens);
        _sliderSens = new RoundedSlider(0.0, 100.0, 1.0) { Value = 33.0 };
        cardDetect.Body.Controls.Add(_sliderSens);
        _lblSens = Lbl.Make("中", Theme.TextSub);
        cardDetect.Body.Controls.Add(_lblSens);

        // ---- Performance Settings ----
        var cardPerf = NewCard(set, "性能优化");
        _swSkipStatic = NewPerfSwitch(cardPerf, "跳过静态画面", true);
        _swSmartSkip = NewPerfSwitch(cardPerf, "智能跳过", true);
        _swPerfMode = NewPerfSwitch(cardPerf, "降采样识别", false);
        _swAutoBackoff = NewPerfSwitch(cardPerf, "自动降频", true);

        var perfHint = Lbl.Make(
            "OCR 是最吃 CPU 的环节；以上开关都能减少识别次数。监控区域一直在动时建议勾选「降采样识别」。",
            Theme.TextSub, wrap: true, width: 520);
        perfHint.Margin = new Padding(0, 6, 0, 0);
        cardPerf.Body.Controls.Add(perfHint);

        // ---- Close Behavior ----
        var cardClose = NewCard(set, "关闭窗口时");
        var descClose = Lbl.Make("点击右上角关闭按钮时的默认行为（可在设置中更改，自动保存）", Theme.TextSub, Theme.FontSub);
        descClose.Margin = new Padding(0, 4, 0, 2);
        cardClose.Body.Controls.Add(descClose);
        _segClose = new SegmentedControl(new[]
        {
            ("最小化到托盘", "minimize"),
            ("退出应用", "exit")
        });
        _segClose.Margin = new Padding(0, 4, 0, 0);
        cardClose.Body.Controls.Add(_segClose);

        // ---- QQ Notification ----
        var cardQq = NewCard(set, "QQ 通知");
        _swQq = new RoundedSwitch("启用 QQ 通知", false);
        cardQq.Body.Controls.Add(_swQq);

        _entryQqUrl = NewEntry(0, "http://127.0.0.1:3000");
        cardQq.Body.Controls.Add(QqRow("NapCat 地址:", _entryQqUrl));
        _entryQqToken = NewEntry(0, "");
        cardQq.Body.Controls.Add(QqRow("Token:", _entryQqToken));
        _entryQqTarget = NewEntry(0, "1414111902");
        cardQq.Body.Controls.Add(QqRow("目标 QQ:", _entryQqTarget));
        _entryQqMsg = NewEntry(0, "【警报】已检测到目标：{target}");
        cardQq.Body.Controls.Add(QqRow("消息模板:", _entryQqMsg));

        // ---- QQ Remote Control ----
        var cardQqCtrl = NewCard(set, "QQ 远程控制");
        _swQqCtrlLock = new RoundedSwitch("仅允许授权", false);
        cardQqCtrl.Body.Controls.Add(_swQqCtrlLock);
        _entryQqWs = NewEntry(0, "ws://127.0.0.1:3001");
        cardQqCtrl.Body.Controls.Add(QqRow("WS 地址:", _entryQqWs));
        _entryQqCmdStart = NewEntry(0, "启动检测");
        cardQqCtrl.Body.Controls.Add(QqRow("启动命令:", _entryQqCmdStart));
        _entryQqCmdStop = NewEntry(0, "关闭检测");
        cardQqCtrl.Body.Controls.Add(QqRow("关闭命令:", _entryQqCmdStop));
    }

    // ---------------- UI Helpers ----------------

    private static RoundedCard NewCard(Control parent, string title, bool fill = false)
    {
        var card = new RoundedCard(title, 14, fill) { Margin = new Padding(0, 5, 0, 5) };
        parent.Controls.Add(card);
        return card;
    }

    private static HRow NewRow(int height, Color bg)
        => new HRow(height) { BackColor = bg, Margin = Padding.Empty };

    private static StackPanel NewSubStack(Padding margin)
        => new StackPanel { BackColor = Theme.Surface, AutoHeight = true, Margin = margin, Padding = Padding.Empty };

    private static FlatTextBox NewEntry(int width, string text)
        => new FlatTextBox(width > 0 ? width : 120, 30, text) { Margin = Padding.Empty };

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
}
