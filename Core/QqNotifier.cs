using System.Drawing.Imaging;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;

namespace ScreenTextMonitor.Core;

/// <summary>通过 NapCat 的 HTTP API 发送 QQ 私聊消息（对应 Python 版 _send_qq_msg）。</summary>
public static class QqNotifier
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    private static readonly System.Text.Json.JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// 把位图转成 NapCat 可识别的 base64 图片 CQ 码。
    /// 为控制单条消息体积，最长边超过 maxSide 时先等比缩小。
    /// </summary>
    public static string ImageToCq(Bitmap img, int maxSide = 1280)
    {
        Bitmap scaled = null;
        try
        {
            scaled = ScreenCapture.ResizeToMaxSide(img, maxSide);
            var use = scaled ?? img;
            using var ms = new MemoryStream();
            use.Save(ms, ImageFormat.Png);
            string b64 = Convert.ToBase64String(ms.ToArray());
            return $"[CQ:image,file=base64://{b64}]";
        }
        finally
        {
            scaled?.Dispose();
        }
    }

    /// <summary>发送私聊消息；image 不为 null 时把截图附在文字前一起发送。</summary>
    public static async Task<string> SendPrivateAsync(string baseUrl, string token, string targetQq,
                                                      string msg, Bitmap image)
    {
        string url = baseUrl.TrimEnd('/') + "/send_private_msg";
        string message = msg ?? string.Empty;
        if (image is not null)
        {
            string cq = ImageToCq(image);
            message = string.IsNullOrEmpty(msg) ? cq : $"{cq}\n{msg}";
        }

        var payload = new JsonObject
        {
            ["user_id"] = long.TryParse(targetQq, out var uid) ? uid : 0,
            ["message"] = message
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload.ToJsonString(JsonOpts), Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(token))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var resp = await Http.SendAsync(req).ConfigureAwait(false);
        string body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        return body;
    }
}
