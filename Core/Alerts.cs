using System.Speech.Synthesis;

namespace ScreenTextMonitor.Core;

/// <summary>三种提醒方式：系统蜂鸣 / 自定义音频 / 语音播报。</summary>
public static class Alerts
{
    /// <summary>系统蜂鸣，等价 winsound.Beep(freq, dur)（走 kernel32!Beep，不依赖控制台）。</summary>
    public static void Beep(int freq, int durationMs)
    {
        int f = Math.Clamp(freq, 37, 32767);
        int d = Math.Max(1, durationMs);
        try
        {
            // 优先内核 Beep（与 Python winsound.Beep 同源）；失败再回退 Console.Beep
            if (!NativeMethods.Beep((uint)f, (uint)d))
            {
                Console.Beep(f, d);
            }
        }
        catch
        {
            // 部分虚拟机 / 无蜂鸣设备时忽略
        }
    }

    /// <summary>播放自定义音频（WAV），等价 winsound.PlaySound(SND_FILENAME | SND_ASYNC)。</summary>
    public static void PlayAudio(string path)
    {
        try
        {
            NativeMethods.PlaySound(path, IntPtr.Zero,
                NativeMethods.SND_FILENAME | NativeMethods.SND_ASYNC | NativeMethods.SND_NODEFAULT);
        }
        catch
        {
            // 与 Python 版一致：播放失败静默忽略
        }
    }

    /// <summary>语音播报，等价 pyttsx3.init() → say → runAndWait → del engine。</summary>
    public static void Speak(string text)
    {
        try
        {
            using var synth = new SpeechSynthesizer();
            synth.SetOutputToDefaultAudioDevice();
            synth.Speak(text);
        }
        catch
        {
            // 无 TTS 语音包时静默忽略
        }
    }

    /// <summary>检测当前系统是否具备可用的语音合成引擎。</summary>
    public static bool TtsAvailable()
    {
        try
        {
            using var synth = new SpeechSynthesizer();
            return synth.GetInstalledVoices().Any(v => v.Enabled);
        }
        catch
        {
            return false;
        }
    }
}
