using RapidOcrNet;

namespace ScreenTextMonitor.Core;

/// <summary>OCR 识别结果。</summary>
public sealed class OcrOutcome
{
    public string AllText { get; init; } = string.Empty;
    public int LineCount { get; init; }
    public double Elapsed { get; init; }
}

/// <summary>
/// OCR 引擎封装（onnxruntime + PP-OCR 模型），对齐 Python 版 RapidOCR 配置：
///   use_cls=False（关闭方向分类，CPU 直降约 30%）
///   text_score=0.6（提高识别分数阈值）
///   rec 模型优先使用 models_ppocrv6/PP-OCRv6_tiny_rec.onnx + ppocrv6_dict.txt
/// </summary>
public sealed class OcrEngine : IDisposable
{
    private const string DetModel = "ch_PP-OCRv4_det_infer.onnx";
    private const string ClsModel = "ch_ppocr_mobile_v2.0_cls_infer.onnx";
    private const string V6Rec = "PP-OCRv6_tiny_rec.onnx";
    private const string V6Dict = "ppocrv6_dict.txt";

    private readonly RapidOcr _ocr = new();
    private readonly RapidOcrOptions _options;

    public bool UsingV6Rec { get; }

    /// <param name="modelDir">模型目录；默认 AppDir/models_ppocrv6（便于测试注入）。</param>
    public OcrEngine(Action<string, string> log, string modelDir = null)
    {
        modelDir ??= Path.Combine(AppConfig.AppDir, "models_ppocrv6");
        string det = Path.Combine(modelDir, DetModel);
        string cls = Path.Combine(modelDir, ClsModel);
        string rec = Path.Combine(modelDir, V6Rec);
        string keys = Path.Combine(modelDir, V6Dict);

        if (!File.Exists(det))
            throw new FileNotFoundException(
                $"缺少检测模型: {det}。请补齐 models_ppocrv6/{DetModel}", det);
        if (!File.Exists(rec) || !File.Exists(keys))
            throw new FileNotFoundException(
                $"缺少 PP-OCRv6 识别模型或字典，请补齐 models_ppocrv6/{V6Rec} 与 {V6Dict}", rec);

        UsingV6Rec = true;
        log?.Invoke("已启用 PP-OCRv6_tiny_rec 识别模型", "green");

        _ocr.InitModels(det, cls, rec, keys, numThread: 0);

        // PythonCompat 预设 = 短边自适应缩放到 736、无白边，与 Python rapidocr 预处理一致
        _options = RapidOcrOptions.PythonCompat with
        {
            DoAngle = false,
            TextScore = 0.6f
        };
    }

    public OcrOutcome Recognize(Bitmap bitmap)
    {
        var start = DateTime.UtcNow;
        using var sk = ScreenCapture.ToSKBitmap(bitmap);
        var result = _ocr.Detect(sk, _options);
        double elapsed = (DateTime.UtcNow - start).TotalSeconds;

        var blocks = result?.TextBlocks ?? Array.Empty<TextBlock>();
        string all = string.Concat(blocks.Select(b => b.Text ?? string.Empty));
        return new OcrOutcome { AllText = all, LineCount = blocks.Length, Elapsed = elapsed };
    }

    public void Dispose() => _ocr.Dispose();
}
