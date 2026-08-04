using System.Diagnostics;
using ScreenTextMonitor.Core;
using ScreenTextMonitor.Ui;

namespace ScreenTextMonitor;

public sealed partial class MainForm
{
    // ==================================================================
    // Layout & Navigation
    // ==================================================================

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        LayoutRoot();
    }

    private void LayoutRoot()
    {
        if (_rail is null) return;
        int w = ClientSize.Width;
        int h = ClientSize.Height;
        int railW = w < 860 ? 72 : 92;
        _rail.SetBounds(0, 0, railW, h);

        int contentX = railW;
        int contentW = Math.Max(20, w - railW);
        const int pad = 12;
        const int appBarH = 52;

        _topBar.SetBounds(contentX + pad, pad, contentW - 2 * pad, appBarH);

        int contentY = pad + appBarH + 10;
        var r = new Rectangle(contentX + pad, contentY,
            contentW - 2 * pad, Math.Max(20, h - contentY - pad));
        _tabRun.Bounds = r;
        _tabSet.Bounds = r;
    }

    private void SwitchTab(string which)
    {
        if (which == "run")
        {
            _tabSet.Visible = false;
            _tabRun.Visible = true;
            _rail.SetActive("run");
            _topBar.SetTitle("实时监控");
        }
        else
        {
            _tabRun.Visible = false;
            _tabSet.Visible = true;
            _rail.SetActive("set");
            _topBar.SetTitle("设置");
        }
    }

    // ==================================================================
    // Logging
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
    // Alert Mode Switch
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
            _entryAudio.Text = dlg.FileName;
    }

    // ==================================================================
    // Region Selection
    // ==================================================================

    private void SelectRegion()
    {
        var rect = RegionSelectorForm.Select(this);
        if (rect is null) return;
        _entryX.Text = rect.Value.X.ToString();
        _entryY.Text = rect.Value.Y.ToString();
        _entryW.Text = rect.Value.Width.ToString();
        _entryH.Text = rect.Value.Height.ToString();
    }

    private void PreviewScreenshot()
    {
        var (x, y, w, h) = GetRegion();
        if (w <= 0 || h <= 0) { Log("请先设置有效的检测区域", "red"); return; }
        try
        {
            using var bmp = ScreenCapture.Grab(x, y, w, h);
            var preview = new ImagePreviewForm(bmp, w, h);
            preview.Show(this);
        }
        catch (Exception ex) { Log($"截图失败: {ex.Message}", "red"); }
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
        catch { Log("无法打开 QQ 联系人", "red"); }
    }

    // ==================================================================
    // Slider Events
    // ==================================================================

    private void OnSliderChange()
    {
        double v = _sliderInterval.Value;
        _lblInterval.Text = $"{v:0.0}s";
    }

    private void OnDelayChange()
    {
        double v = _sliderDelay.Value;
        _forceOcrIdle = (float)v;
        _lblDelay.Text = $"{v:0.0}s";
    }

    private void OnSensChange()
    {
        double v = _sliderSens.Value;
        _ocrThreshold = (float)(8.0 - v / 100.0 * 6.0);
        _lblSens.Text = v < 25 ? "低" : v < 60 ? "中" : v < 85 ? "高" : "极高";
    }

    // ==================================================================
    // CPU Monitor
    // ==================================================================

    private void UpdateCpu()
    {
        NativeMethods.GetProcessTimes(NativeMethods.GetCurrentProcess(),
            out _, out _, out var kernel, out var user);
        double k = kernel.ToSeconds();
        double u = user.ToSeconds();
        double wall = (double)Environment.TickCount / 1000.0;

        if (_cpuPrev is { } prev)
        {
            double dk = k - prev.proc;
            double dw = wall - prev.wall;
            double pct = dw > 0 ? Math.Min(100.0, (dk / dw) * 100.0 / _cpuNproc) : 0.0;
            _labelCpu.Text = $"{pct:F0}%";
            _cpuBar.SetValue(pct);
        }
        _cpuPrev = (k + u, wall);
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

    private void UpdateStats()
    {
        _labelStats.Text = $"识别 {_statOcr} 次 · 跳过 {_statSkip} 次 · 当前间隔 {_statInterval:0.0}s";
    }

    // ==================================================================
    // QQ Remote Control
    // ==================================================================

    private void ApplyQqController()
    {
        if (!_swQq.Checked)
        {
            _qqCtrl?.Dispose();
            _qqCtrl = null;
            return;
        }

        _qqCtrl?.Dispose();
        _qqCtrl = null;

        string ws = _entryQqWs.Text.Trim();
        if (string.IsNullOrEmpty(ws)) ws = "ws://127.0.0.1:3001";

        _qqCtrl = new QqController(
            ws,
            _entryQqToken.Text.Trim(),
            _entryQqTarget.Text.Trim(),
            !_swQqCtrlLock.Checked,
            _entryQqCmdStart.Text.Trim(),
            _entryQqCmdStop.Text.Trim(),
            userId => InvokeQqCommand(true, userId),
            userId => InvokeQqCommand(false, userId),
            msg => Log(msg));
        _qqCtrl.Start();

        if (!_swQqCtrlLock.Checked)
            Log("⚠ 已开放任意私聊控制，存在被他人控制的风险", "red");
    }

    private void InvokeQqCommand(bool start, long senderId)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => InvokeQqCommand(start, senderId)));
            return;
        }

        if (start) StartMonitor();
        else StopMonitor();

        try
        {
            string httpUrl = _entryQqUrl.Text.Trim();
            string token = _entryQqToken.Text.Trim();
            string text = start ? "✅ 已启动监控" : "⏹ 已关闭监控";
            _ = Task.Run(() => QqNotifier.SendPrivateAsync(httpUrl, token, senderId.ToString(), text, null));
        }
        catch (Exception ex)
        {
            Log($"QQ 控制回执失败: {ex.Message}", "red");
        }
    }

    // ==================================================================
    // Lifecycle
    // ==================================================================

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        LayoutRoot();
        _tabRun.PerformLayout();
        _tabSet.PerformStackLayout();

        _wheelFilter ??= new WheelMessageFilter();
        Application.AddMessageFilter(_wheelFilter);

        Log("屏幕文字监控工具已启动", "bold");
        Log("提示: 点击「框选区域」→ 鼠标拖拉选择屏幕区域 → 松开自动填入坐标", "blue");
        Log("提示: 目标文字多个用逗号分隔，如 收金,比例", "blue");
        _cpuTimer.Start();
        ApplyQqController();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_wheelFilter is not null)
        {
            Application.RemoveMessageFilter(_wheelFilter);
            _wheelFilter = null;
        }

        _monitor?.Dispose();
        _cpuTimer?.Stop();
        SaveConfig();
        _qqCtrl?.Dispose();
        _qqCtrl = null;
        base.OnFormClosing(e);
    }
}
