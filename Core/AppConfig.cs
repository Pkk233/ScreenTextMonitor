using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScreenTextMonitor.Core;

/// <summary>Strongly-typed config with JSON serialization, matching Python edition keys.</summary>
public class AppConfig
{
    // ---- Region ----
    [JsonPropertyName("region_x")] public int RegionX { get; set; } = 38;
    [JsonPropertyName("region_y")] public int RegionY { get; set; } = 1100;
    [JsonPropertyName("region_w")] public int RegionW { get; set; } = 674;
    [JsonPropertyName("region_h")] public int RegionH { get; set; } = 1363;

    // ---- Detection ----
    [JsonPropertyName("targets")] public string Targets { get; set; } = "收金,比例";
    [JsonPropertyName("alert_mode")] public string AlertMode { get; set; } = "beep";
    [JsonPropertyName("freq")] public int Freq { get; set; } = 1000;
    [JsonPropertyName("dur")] public int Dur { get; set; } = 1000;
    [JsonPropertyName("audio_path")] public string AudioPath { get; set; } = "";
    [JsonPropertyName("tts_text")] public string TtsText { get; set; } = "检测到目标文字，";
    [JsonPropertyName("interval")] public double Interval { get; set; } = 1.0;
    [JsonPropertyName("skip_static")] public bool SkipStatic { get; set; } = true;
    [JsonPropertyName("smart_skip")] public bool SmartSkip { get; set; } = true;
    [JsonPropertyName("perf_mode")] public bool PerfMode { get; set; }
    [JsonPropertyName("auto_backoff")] public bool AutoBackoff { get; set; } = true;
    [JsonPropertyName("force_ocr_idle")] public double ForceOcrIdle { get; set; } = 4.0;
    [JsonPropertyName("ocr_threshold")] public double OcrThreshold { get; set; } = 6.0;

    // ---- QQ Notify ----
    [JsonPropertyName("qq_enabled")] public bool QqEnabled { get; set; }
    [JsonPropertyName("qq_url")] public string QqUrl { get; set; } = "http://127.0.0.1:3000";
    [JsonPropertyName("qq_token")] public string QqToken { get; set; } = "";
    [JsonPropertyName("qq_target")] public string QqTarget { get; set; } = "1414111902";
    [JsonPropertyName("qq_msg")] public string QqMsg { get; set; } = "【警报】已检测到目标：{target}";
    [JsonPropertyName("qq_ws_url")] public string QqWsUrl { get; set; } = "ws://127.0.0.1:3001";
    [JsonPropertyName("qq_ctrl_allow_any")] public bool QqCtrlAllowAny { get; set; } = true;
    [JsonPropertyName("qq_cmd_start")] public string QqCmdStart { get; set; } = "启动检测";
    [JsonPropertyName("qq_cmd_stop")] public string QqCmdStop { get; set; } = "关闭检测";

    // ---- Window close behavior ----
    // "minimize" = 最小化到托盘（常驻）；"exit" = 退出应用
    [JsonPropertyName("close_action")] public string CloseAction { get; set; } = "minimize";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string AppDir => AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    public static string ConfigPath => Path.Combine(AppDir, "config.json");

    /// <summary>Load from config.json, falling back to defaults on failure.</summary>
    public static AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return new AppConfig();
            var json = File.ReadAllText(ConfigPath, System.Text.Encoding.UTF8);
            // STJ honors [JsonPropertyName] (region_x/qq_* etc.) and fills missing keys with defaults.
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppConfig.Load] Config load failed: {ex.Message}");
            return new AppConfig();
        }
    }

    /// <summary>Save to config.json.</summary>
    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, WriteOptions);
            File.WriteAllText(ConfigPath, json, System.Text.Encoding.UTF8);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppConfig.Save] Config write failed: {ex.Message}");
        }
    }

    // MergeTo removed: config is now loaded via JsonSerializer.Deserialize<AppConfig> (honors [JsonPropertyName]).
}
