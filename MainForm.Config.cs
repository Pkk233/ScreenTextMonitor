using ScreenTextMonitor.Core;

namespace ScreenTextMonitor;

public sealed partial class MainForm
{
    // ==================================================================
    // Config Load / Save
    // ==================================================================

    private void LoadConfig()
    {
        var cfg = AppConfig.Load();

        _entryX.Text = cfg.RegionX.ToString();
        _entryY.Text = cfg.RegionY.ToString();
        _entryW.Text = cfg.RegionW.ToString();
        _entryH.Text = cfg.RegionH.ToString();
        _entryText.Text = cfg.Targets;

        _segAlert.SetValueSilent(cfg.AlertMode);
        _entryFreq.Text = cfg.Freq.ToString();
        _entryDur.Text = cfg.Dur.ToString();
        _entryAudio.Text = cfg.AudioPath;
        _entryTts.Text = cfg.TtsText;

        _sliderInterval.Value = cfg.Interval;
        _sliderDelay.Value = cfg.ForceOcrIdle;
        _sliderSens.Value = (8.0 - cfg.OcrThreshold) / 6.0 * 100;
        OnSliderChange();
        OnDelayChange();
        OnSensChange();

        _swSkipStatic.Checked = cfg.SkipStatic;
        _swSmartSkip.Checked = cfg.SmartSkip;
        _swPerfMode.Checked = cfg.PerfMode;
        _swAutoBackoff.Checked = cfg.AutoBackoff;

        _swQq.Checked = cfg.QqEnabled;
        _entryQqUrl.Text = cfg.QqUrl;
        _entryQqToken.Text = cfg.QqToken;
        _entryQqTarget.Text = cfg.QqTarget;
        _entryQqMsg.Text = cfg.QqMsg;
        _entryQqWs.Text = cfg.QqWsUrl;
        _swQqCtrlLock.Checked = !cfg.QqCtrlAllowAny;
        _entryQqCmdStart.Text = cfg.QqCmdStart;
        _entryQqCmdStop.Text = cfg.QqCmdStop;

        if (_segClose is not null) _segClose.SetValueSilent(cfg.CloseAction);

        // Apply QQ controller after loading config
        ApplyQqController();
    }

    private void SaveConfig()
    {
        var (x, y, w, h) = GetRegion();
        var cfg = new AppConfig
        {
            RegionX = x,
            RegionY = y,
            RegionW = w,
            RegionH = h,
            Targets = _entryText.Text.Trim(),
            AlertMode = _segAlert.Value,
            Freq = int.TryParse(_entryFreq.Text.Trim(), out var freq) ? freq : 1000,
            Dur = int.TryParse(_entryDur.Text.Trim(), out var dur) ? dur : 1000,
            AudioPath = _entryAudio.Text.Trim(),
            TtsText = _entryTts.Text.Trim(),
            Interval = _sliderInterval.Value,
            SkipStatic = _swSkipStatic.Checked,
            SmartSkip = _swSmartSkip.Checked,
            PerfMode = _swPerfMode.Checked,
            AutoBackoff = _swAutoBackoff.Checked,
            ForceOcrIdle = _sliderDelay.Value,
            OcrThreshold = _ocrThreshold,
            QqEnabled = _swQq.Checked,
            QqUrl = _entryQqUrl.Text.Trim(),
            QqToken = _entryQqToken.Text.Trim(),
            QqTarget = _entryQqTarget.Text.Trim(),
            QqMsg = _entryQqMsg.Text.Trim(),
            QqWsUrl = _entryQqWs.Text.Trim(),
            QqCtrlAllowAny = !_swQqCtrlLock.Checked,
            QqCmdStart = _entryQqCmdStart.Text.Trim(),
            QqCmdStop = _entryQqCmdStop.Text.Trim(),
            CloseAction = _segClose?.Value ?? "minimize",
        };
        cfg.Save();
    }

    private (int X, int Y, int W, int H) GetRegion()
    {
        int x = int.TryParse(_entryX.Text.Trim(), out var vx) ? vx : 0;
        int y = int.TryParse(_entryY.Text.Trim(), out var vy) ? vy : 0;
        int w = int.TryParse(_entryW.Text.Trim(), out var vw) ? vw : 0;
        int h = int.TryParse(_entryH.Text.Trim(), out var vh) ? vh : 0;
        return (x, y, w, h);
    }

    private MonitorConfig GetMonitorConfig()
    {
        var (x, y, w, h) = GetRegion();
        return new MonitorConfig
        {
            RegionX = x,
            RegionY = y,
            RegionW = w,
            RegionH = h,
            Targets = _entryText.Text.Split(',')
                .Select(t => t.Trim()).Where(t => t.Length > 0).ToArray(),
            AlertMode = _segAlert.Value,
            Freq = int.TryParse(_entryFreq.Text.Trim(), out var freq) ? freq : 1000,
            Dur = int.TryParse(_entryDur.Text.Trim(), out var dur) ? dur : 1000,
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
            ForceOcrIdle = _sliderDelay.Value,
            OcrThreshold = _ocrThreshold,
        };
    }

    private void ValidateConfig(MonitorConfig cfg)
    {
        if (cfg.RegionW <= 0 || cfg.RegionH <= 0)
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
}
