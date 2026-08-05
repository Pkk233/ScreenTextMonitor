using System.Diagnostics;
using System.Threading;

namespace ScreenTextMonitor.Core;

/// <summary>Configuration snapshot for a single monitor session.</summary>
public sealed record MonitorConfig
{
    public required int RegionX { get; init; }
    public required int RegionY { get; init; }
    public required int RegionW { get; init; }
    public required int RegionH { get; init; }
    public required string[] Targets { get; init; }
    public required string AlertMode { get; init; }
    public int Freq { get; init; } = 1000;
    public int Dur { get; init; } = 1000;
    public string AudioPath { get; init; } = "";
    public string TtsText { get; init; } = "检测到目标文字，";
    public double Interval { get; init; } = 1.0;
    public bool SkipStatic { get; init; } = true;
    public bool SmartSkip { get; init; } = true;
    public bool PerfMode { get; init; }
    public bool AutoBackoff { get; init; } = true;
    public bool QqEnabled { get; init; }
    public string QqUrl { get; init; } = "";
    public string QqToken { get; init; } = "";
    public string QqTarget { get; init; } = "";
    public string QqMsg { get; init; } = "【警报】已检测到目标：{target}";
    public double ForceOcrIdle { get; init; } = 4.0;
    public double OcrThreshold { get; init; } = 6.0;
}

/// <summary>Statistics snapshot pushed to the UI thread.</summary>
public sealed record MonitorStats(int OcrCount, int SkipCount, double Interval);

/// <summary>Encapsulates the screen-monitor loop, keeping detection logic separate from UI.</summary>
public sealed class SceneMonitor : IDisposable
{
    private const int ChangeThumbW = 480;
    private const double ChangeThreshold = 2.0;
    private const int PerfMaxDim = 1000;
    private const int IdleBackoffBase = 3;
    private const double IdleBackoffStep = 0.6;
    private const double IdleBackoffCap = 5.0;

    private MonitorConfig _cfg;
    private readonly CancellationTokenSource _cts = new();
    private readonly GrayThumbCalc _thumbCalc = new(ChangeThumbW);

    private OcrEngine _engine;
    private Thread _thread;
    private byte[] _prevSmall;
    private bool _prevReady;
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private double _lastForceOcr;

    // External callbacks
    private readonly Action<string, string> _log;
    private readonly Action<MonitorStats> _pushStats;
    private readonly Func<MonitorConfig, OcrEngine> _engineFactory;

    public bool IsRunning => _thread?.IsAlive == true;

    /// <summary>Hot-swap the active config while a session is running; takes effect on the next loop iteration.</summary>
    public void UpdateConfig(MonitorConfig config) => Volatile.Write(ref _cfg, config);

    public SceneMonitor(
        MonitorConfig config,
        Action<string, string> log,
        Action<MonitorStats> pushStats,
        Func<MonitorConfig, OcrEngine> engineFactory = null)
    {
        _cfg = config;
        _log = log;
        _pushStats = pushStats;
        _engineFactory = engineFactory ?? (cfg => new OcrEngine((m, t) => log(m, t)));
    }

    public void Start()
    {
        if (_thread?.IsAlive == true) return;

        _engine = _engineFactory(_cfg);
        _lastForceOcr = _sw.Elapsed.TotalSeconds;

        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "ScreenMonitor"
        };
        _thread.Start();
    }

    public void Stop(int timeoutMs = 2000)
    {
        _cts.Cancel();
        if (_thread?.IsAlive == true)
        {
            try { _thread.Join(timeoutMs); } catch { }
        }
        _engine?.Dispose();
        _engine = null;
    }

    public void Dispose()
    {
        Stop(2000);
        _cts.Dispose();
        _thumbCalc.Dispose();
        _engine?.Dispose();
    }

    private void Run()
    {
        var ct = _cts.Token;

        int ocrCount = 0, skipCount = 0, idleStreak = 0;
        _prevSmall = null;
        _prevReady = false;
        _lastForceOcr = _sw.Elapsed.TotalSeconds;

        while (!ct.IsCancellationRequested)
        {
            var cfg = Volatile.Read(ref _cfg);
            var targets = cfg.Targets;
            double baseInterval = Math.Max(0.3, cfg.Interval);
            bool skipStatic = cfg.SkipStatic;
            bool smartSkip = cfg.SmartSkip;
            bool perfMode = cfg.PerfMode;
            bool autoBackoff = cfg.AutoBackoff;
            string qqMsgTpl = string.IsNullOrEmpty(cfg.QqMsg) ? "【警报】已检测到目标：{target}" : cfg.QqMsg;

            Bitmap screenshot = null;
            Bitmap ocrInput = null;
            bool wasResized = false;

            try
            {
                screenshot = ScreenCapture.Grab(cfg.RegionX, cfg.RegionY, cfg.RegionW, cfg.RegionH);

                // ---- Change detection via grayscale thumbnail diff ----
                int thumbH = _thumbCalc.Compute(screenshot);
                byte[] cur = _thumbCalc.Buffer;
                if (_prevSmall is null || _prevSmall.Length < cur.Length)
                    _prevSmall = new byte[cur.Length];
                double diff = double.NaN;
                if (_prevReady)
                    diff = ScreenCapture.MeanAbsDiff(_prevSmall, cur);
                Buffer.BlockCopy(cur, 0, _prevSmall, 0, cur.Length);
                _prevReady = true;

                double elapsed = _sw.Elapsed.TotalSeconds;
                bool isStatic = diff < ChangeThreshold;
                bool forceOcr = (elapsed - _lastForceOcr) >= cfg.ForceOcrIdle;

                if (skipStatic && isStatic && !forceOcr)
                {
                    skipCount++;
                    continue;
                }

                // ---- Smart skip: skip if text unchanged from last OCR ----
                if (smartSkip && isStatic)
                {
                    skipCount++;
                    continue;
                }

                // ---- OCR ----
                ocrInput = screenshot;
                wasResized = false;
                if (perfMode)
                {
                    var resized = ScreenCapture.ResizeToMaxSide(screenshot, PerfMaxDim);
                    if (resized is not null)
                    {
                        ocrInput = resized;
                        wasResized = true;
                    }
                }

                OcrOutcome ocrResult = null;
                if (_engine is not null)
                {
                    ocrResult = _engine.Recognize(ocrInput);
                    ocrCount++;
                    _lastForceOcr = elapsed;
                }

                bool matched = false;
                if (ocrResult is not null)
                {
                    string allText = ocrResult.AllText;
                    foreach (var t in targets)
                    {
                        if (allText.Contains(t, StringComparison.Ordinal))
                        {
                            matched = true;
                            break;
                        }
                    }
                }

                if (matched)
                {
                    string matchedTarget = targets.FirstOrDefault(t => ocrResult.AllText.Contains(t, StringComparison.Ordinal)) ?? targets[0];
                    _log($"【匹配】检测到目标文字: {matchedTarget} (OCR耗时 {ocrResult.Elapsed:F1}s)", "green");
                    FireAlert(screenshot, qqMsgTpl, matchedTarget, cfg);
                    idleStreak = 0;
                }
                else
                {
                    idleStreak++;
                }

                // ---- Auto backoff ----
                double interval = baseInterval;
                if (autoBackoff && idleStreak >= IdleBackoffBase)
                {
                    int extra = idleStreak - IdleBackoffBase;
                    interval = Math.Min(baseInterval + extra * IdleBackoffStep, IdleBackoffCap);
                }

                // Push stats to UI
                _pushStats(new MonitorStats(ocrCount, skipCount, interval));

                // Wait
                int delayMs = (int)(interval * 1000);
                if (ct.WaitHandle.WaitOne(delayMs)) break;
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log($"监控异常: {ex.Message}", "red");
                if (ct.WaitHandle.WaitOne(1000)) break;
            }
            finally
            {
                if (wasResized && ocrInput != screenshot)
                    ocrInput?.Dispose();
                screenshot?.Dispose();
            }
        }
    }

    private void FireAlert(Bitmap screenshot, string qqMsgTpl, string matchedTarget, MonitorConfig cfg)
    {
        try
        {
            switch (cfg.AlertMode)
            {
                case "beep":
                    Alerts.Beep(cfg.Freq, cfg.Dur);
                    break;
                case "audio":
                    Alerts.PlayAudio(cfg.AudioPath);
                    break;
                default:
                    Alerts.Speak(cfg.TtsText);
                    break;
            }

            if (cfg.QqEnabled && !string.IsNullOrEmpty(cfg.QqUrl))
            {
                string msg = qqMsgTpl.Replace("{target}", matchedTarget);
                _ = Task.Run(() => QqNotifier.SendPrivateAsync(
                    cfg.QqUrl, cfg.QqToken, cfg.QqTarget, msg, screenshot));
            }
        }
        catch (Exception ex)
        {
            _log($"警报触发失败: {ex.Message}", "red");
        }
    }
}
