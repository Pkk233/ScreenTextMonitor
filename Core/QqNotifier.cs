using System.Drawing.Imaging;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ScreenTextMonitor.Core;

/// <summary>
/// Send QQ private messages via NapCat HTTP API (mirrors Python _send_qq_msg).
/// </summary>
public static class QqNotifier
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Convert a Bitmap to a NapCat-recognizable base64 CQ image code.
    /// Longest side is capped at maxSide to keep message size manageable.
    /// </summary>
    public static string ImageToCq(Bitmap img, int maxSide = 1280)
    {
        using var scaled = ScreenCapture.ResizeToMaxSide(img, maxSide);
        var use = scaled ?? img;
        using var ms = new MemoryStream();
        use.Save(ms, ImageFormat.Png);
        string b64 = Convert.ToBase64String(ms.ToArray());
        return $"[CQ:image,file=base64://{b64}]";
    }

    /// <summary>
    /// Send a private message. If image is not null, the screenshot is prepended.
    /// </summary>
    public static async Task<string> SendPrivateAsync(
        string baseUrl, string token, string targetQq,
        string msg, Bitmap image)
    {
        string url = baseUrl.TrimEnd('/') + "/send_private_msg";
        string message = msg ?? string.Empty;

        if (image is not null)
        {
            string cq = ImageToCq(image);
            message = string.IsNullOrEmpty(msg) ? cq : $"{cq}\n{msg}";
        }

        long userId = long.TryParse(targetQq, out var uid) ? uid : 0;

        using var payload = new MemoryStream();
        using (var writer = new Utf8JsonWriter(payload))
        {
            writer.WriteStartObject();
            writer.WriteNumber("user_id", userId);
            writer.WriteString("message", message);
            writer.WriteEndObject();
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new ByteArrayContent(payload.ToArray(), 0, (int)payload.Length)
            {
                Headers = { ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" } }
            }
        };

        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var resp = await Http.SendAsync(req).ConfigureAwait(false);
        return await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
    }
}
