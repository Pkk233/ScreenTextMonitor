using ScreenTextMonitor.Core;

namespace ScreenTextMonitor;

public sealed partial class MainForm
{
    // ==================================================================
    // Monitor Control (Start / Stop / Toggle)
    // ==================================================================

    private void ToggleMonitor()
    {
        if (_monitor?.IsRunning == true) StopMonitor();
        else StartMonitor();
    }

    private void StartMonitor()
    {
        MonitorConfig cfg;
        try
        {
            cfg = GetMonitorConfig();
            ValidateConfig(cfg);
        }
        catch (Exception ex)
        {
            Log($"配置错误: {ex.Message}", "red");
            return;
        }

        // Stop previous session if any
        _monitor?.Stop(2000);

        _statOcr = 0;
        _statSkip = 0;
        _statInterval = Math.Max(0.3, cfg.Interval);
        _cpuPrev = null;

        // Lower process priority to reduce CPU contention
        try
        {
            var hProc = NativeMethods.GetCurrentProcess();
            _origPriorityClass = NativeMethods.GetPriorityClass(hProc);
            NativeMethods.SetPriorityClass(hProc, NativeMethods.BELOW_NORMAL_PRIORITY_CLASS);
        }
        catch (Exception ex) { Log($"优先级调整失败（忽略）: {ex.Message}", "gray"); }

        _btnStart.SetText("⏹ 停止监控");
        _topBar.Pill.SetStatus("running", "运行中");
        _rail.SetStatus(true);

        Log(new string('=', 40), "bold");
        Log($"开始监控区域 ({cfg.RegionX}, {cfg.RegionY}, {cfg.RegionW}, {cfg.RegionH})", "green");
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
        Log($"检测间隔 {cfg.Interval:0.0}秒", "green");
        if (cfg.QqEnabled) Log($"QQ通知: 已启用 -> {cfg.QqUrl} (目标 {cfg.QqTarget})", "green");
        else Log("QQ通知: 未启用", "gray");
        Log(new string('=', 40), "bold");

        _monitor = new SceneMonitor(cfg,
            log: (msg, tag) => LogAsync(msg, tag),
            pushStats: stats => PushStats(stats.OcrCount, stats.SkipCount, stats.Interval),
            engineFactory: _ => new OcrEngine((m, t) => Log(m, t)));
        _monitor.Start();
    }

    private void StopMonitor()
    {
        _monitor?.Stop(2000);
        _monitor = null;

        // Restore process priority
        try
        {
            NativeMethods.SetPriorityClass(NativeMethods.GetCurrentProcess(),
                _origPriorityClass != 0 ? _origPriorityClass : NativeMethods.NORMAL_PRIORITY_CLASS);
        }
        catch (Exception ex) { Log($"优先级还原失败（忽略）: {ex.Message}", "gray"); }

        _btnStart.SetText("▶ 开始监控");
        _topBar.Pill.SetStatus("stopped", "停止");
        _rail.SetStatus(false);
        Log("监控已停止", "red");
    }
}
