using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ScreenTextMonitor.Core;

/// <summary>
/// config.json 读写，键名与 Python 版完全一致，保证两边配置文件可互换。
/// </summary>
public class AppConfig
{
    public string RegionX { get; set; } = "38";
    public string RegionY { get; set; } = "1100";
    public string RegionW { get; set; } = "674";
    public string RegionH { get; set; } = "1363";
    public string Targets { get; set; } = "收金,比例";
    public string AlertMode { get; set; } = "beep";
    public string Freq { get; set; } = "1000";
    public string Dur { get; set; } = "1000";
    public string AudioPath { get; set; } = "";
    public string TtsText { get; set; } = "检测到目标文字！";
    public string Interval { get; set; } = "1.0";
    public bool SkipStatic { get; set; } = true;
    public bool SmartSkip { get; set; } = true;
    public bool PerfMode { get; set; }
    public bool AutoBackoff { get; set; } = true;
    public double ForceOcrIdle { get; set; } = 4.0;
    public double OcrThreshold { get; set; } = 6.0;
    public bool QqEnabled { get; set; }
    public string QqUrl { get; set; } = "http://127.0.0.1:3000";
    public string QqToken { get; set; } = "";
    public string QqTarget { get; set; } = "1414111902";
    public string QqMsg { get; set; } = "【警报】已检测到目标：{target}";
    public string QqWsUrl { get; set; } = "ws://127.0.0.1:3001";
    public bool QqCtrlAllowAny { get; set; } = true;

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string AppDir => AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

    public static string ConfigPath => Path.Combine(AppDir, "config.json");

    public static AppConfig Load()
    {
        var cfg = new AppConfig();
        try
        {
            if (!File.Exists(ConfigPath)) return cfg;
            var node = JsonNode.Parse(File.ReadAllText(ConfigPath, System.Text.Encoding.UTF8));
            if (node is not JsonObject o) return cfg;

            cfg.RegionX = Str(o, "region_x", cfg.RegionX);
            cfg.RegionY = Str(o, "region_y", cfg.RegionY);
            cfg.RegionW = Str(o, "region_w", cfg.RegionW);
            cfg.RegionH = Str(o, "region_h", cfg.RegionH);
            cfg.Targets = Str(o, "targets", cfg.Targets);
            cfg.AlertMode = Str(o, "alert_mode", cfg.AlertMode);
            cfg.Freq = Str(o, "freq", cfg.Freq);
            cfg.Dur = Str(o, "dur", cfg.Dur);
            cfg.AudioPath = Str(o, "audio_path", cfg.AudioPath);
            cfg.TtsText = Str(o, "tts_text", cfg.TtsText);
            cfg.Interval = Str(o, "interval", cfg.Interval);
            cfg.SkipStatic = Bool(o, "skip_static", cfg.SkipStatic);
            cfg.SmartSkip = Bool(o, "smart_skip", cfg.SmartSkip);
            cfg.PerfMode = Bool(o, "perf_mode", cfg.PerfMode);
            cfg.AutoBackoff = Bool(o, "auto_backoff", cfg.AutoBackoff);
            cfg.ForceOcrIdle = Dbl(o, "force_ocr_idle", cfg.ForceOcrIdle);
            cfg.OcrThreshold = Dbl(o, "ocr_threshold", cfg.OcrThreshold);
            cfg.QqEnabled = Bool(o, "qq_enabled", cfg.QqEnabled);
            cfg.QqUrl = Str(o, "qq_url", cfg.QqUrl);
            cfg.QqToken = Str(o, "qq_token", cfg.QqToken);
            cfg.QqTarget = Str(o, "qq_target", cfg.QqTarget);
            cfg.QqMsg = Str(o, "qq_msg", cfg.QqMsg);
            cfg.QqWsUrl = Str(o, "qq_ws_url", cfg.QqWsUrl);
            cfg.QqCtrlAllowAny = Bool(o, "qq_ctrl_allow_any", cfg.QqCtrlAllowAny);
        }
        catch (Exception ex)
        {
            // F11：配置损坏时静默回退默认值（Python 版一致），但留 Debug 可观测
            System.Diagnostics.Debug.WriteLine($"[AppConfig.Load] 配置读取失败，回退默认值: {ex.Message}");
        }
        return cfg;
    }

    public void Save()
    {
        try
        {
            var o = new JsonObject
            {
                ["region_x"] = RegionX,
                ["region_y"] = RegionY,
                ["region_w"] = RegionW,
                ["region_h"] = RegionH,
                ["targets"] = Targets,
                ["alert_mode"] = AlertMode,
                ["freq"] = Freq,
                ["dur"] = Dur,
                ["audio_path"] = AudioPath,
                ["tts_text"] = TtsText,
                ["interval"] = Interval,
                ["skip_static"] = SkipStatic,
                ["smart_skip"] = SmartSkip,
                ["perf_mode"] = PerfMode,
                ["auto_backoff"] = AutoBackoff,
                ["force_ocr_idle"] = ForceOcrIdle,
                ["ocr_threshold"] = OcrThreshold,
                ["qq_enabled"] = QqEnabled,
                ["qq_url"] = QqUrl,
                ["qq_token"] = QqToken,
                ["qq_target"] = QqTarget,
                ["qq_msg"] = QqMsg,
                ["qq_ws_url"] = QqWsUrl,
                ["qq_ctrl_allow_any"] = QqCtrlAllowAny,
            };
            File.WriteAllText(ConfigPath, o.ToJsonString(WriteOptions), System.Text.Encoding.UTF8);
        }
        catch (Exception ex)
        {
            // F11：保存失败不影响退出流程，但留 Debug 可观测
            System.Diagnostics.Debug.WriteLine($"[AppConfig.Save] 配置写入失败: {ex.Message}");
        }
    }

    private static string Str(JsonObject o, string key, string def)
    {
        if (!o.TryGetPropertyValue(key, out var v) || v is null) return def;
        return v.GetValueKind() switch
        {
            JsonValueKind.String => v.GetValue<string>(),
            JsonValueKind.Number => v.ToJsonString(),
            _ => def
        };
    }

    private static bool Bool(JsonObject o, string key, bool def)
    {
        if (!o.TryGetPropertyValue(key, out var v) || v is null) return def;
        return v.GetValueKind() switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(v.GetValue<string>(), out var b) ? b : def,
            _ => def
        };
    }

    private static double Dbl(JsonObject o, string key, double def)
    {
        if (!o.TryGetPropertyValue(key, out var v) || v is null) return def;
        return v.GetValueKind() switch
        {
            JsonValueKind.Number => v.GetValue<double>(),
            JsonValueKind.String => double.TryParse(v.GetValue<string>(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : def,
            _ => def
        };
    }
}
