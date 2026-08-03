using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace ScreenTextMonitor.Core;

/// <summary>QQ 远程控制命令。</summary>
internal enum QqCommand
{
    None,
    Start,
    Stop
}

/// <summary>
/// 命令解析与鉴权纯函数，便于独立验证（不依赖网络）。
/// </summary>
internal static class QqCommandParser
{
    /// <summary>
    /// 子串包含匹配：消息包含「启动检测」→ Start，包含「关闭检测」→ Stop，否则 None。
    /// 用包含而非全等，容忍前后带字；四字连写日常误触概率极低。
    /// </summary>
    public static QqCommand Parse(string text)
    {
        if (string.IsNullOrEmpty(text)) return QqCommand.None;
        var t = text.Trim();
        if (t.Contains("启动检测")) return QqCommand.Start;
        if (t.Contains("关闭检测")) return QqCommand.Stop;
        return QqCommand.None;
    }

    /// <summary>
    /// 鉴权：allowAny 为真时任意发送者通过；否则仅当发送者 QQ 号等于 authorizedQq 时通过。
    /// </summary>
    public static bool IsAuthorized(long userId, string authorizedQq, bool allowAny)
    {
        if (allowAny) return true;
        return long.TryParse(authorizedQq, out var auth) && auth == userId;
    }
}

/// <summary>
/// 通过 NapCat 的「正向 WebSocket 事件」接收 QQ 私聊消息，解析为启动/关闭命令并回调。
/// 复用现有 qq_url/qq_token 之外的独立 WS 端口（qq_ws_url）。零额外依赖（.NET 8 自带 ClientWebSocket）。
/// </summary>
public sealed class QqController : IDisposable
{
    private readonly string _wsUrl;
    private readonly string _token;
    private readonly string _authorizedQq;
    private readonly bool _allowAny;
    private readonly Action<long> _onStart;
    private readonly Action<long> _onStop;
    private readonly Action<string> _log;

    private readonly CancellationTokenSource _cts = new();
    private readonly object _wsLock = new();
    private ClientWebSocket _ws;
    private Task _loop;

    public QqController(string wsUrl, string token, string authorizedQq, bool allowAny,
                        Action<long> onStart, Action<long> onStop, Action<string> log)
    {
        _wsUrl = wsUrl ?? string.Empty;
        _token = token ?? string.Empty;
        _authorizedQq = authorizedQq ?? string.Empty;
        _allowAny = allowAny;
        _onStart = onStart;
        _onStop = onStop;
        _log = log ?? (_ => { });
    }

    /// <summary>启动后台连接 + 重连循环。重复调用安全（仅首次生效）。</summary>
    public void Start()
    {
        if (_loop != null) return;
        _loop = Task.Run(() => ConnectLoopAsync(_cts.Token));
    }

    private async Task ConnectLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var url = _wsUrl;
                if (!string.IsNullOrEmpty(_token) && !url.Contains("access_token=", StringComparison.OrdinalIgnoreCase))
                {
                    url += (url.Contains('?') ? "&" : "?") + "access_token=" + Uri.EscapeDataString(_token);
                }

                var ws = new ClientWebSocket();
                lock (_wsLock) _ws = ws;

                // 日志打印不带 token，避免凭据泄露
                _log($"正在连接 QQ 控制 WebSocket: {_wsUrl}");
                await ws.ConnectAsync(new Uri(url), ct).ConfigureAwait(false);
                _log("QQ 控制 WebSocket 已连接，开始监听私聊命令");
                await ReceiveLoopAsync(ws, ct).ConfigureAwait(false);
                _log("QQ 控制 WebSocket 已断开");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log($"QQ 控制 WebSocket 连接失败: {ex.Message}");
            }
            finally
            {
                lock (_wsLock)
                {
                    _ws?.Dispose();
                    _ws = null;
                }
            }

            if (ct.IsCancellationRequested) break;
            try
            {
                await Task.Delay(5000, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[8192];
        var sb = new StringBuilder();
        try
        {
            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        if (ws.State == WebSocketState.Open)
                            await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", ct).ConfigureAwait(false);
                        return;
                    }
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                } while (!result.EndOfMessage);

                var text = sb.ToString();
                sb.Clear();

                // onebot 通常一行一个 JSON 事件，按换行切分后逐个处理
                foreach (var line in text.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.Length == 0) continue;
                    HandleEvent(trimmed);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常退出
        }
        catch (Exception ex)
        {
            _log($"QQ 控制 WebSocket 接收异常: {ex.Message}");
        }
    }

    private void HandleEvent(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            if (node is not JsonObject o) return;
            if (o["post_type"]?.ToString() != "message") return;
            if (o["message_type"]?.ToString() != "private") return;

            long.TryParse(o["user_id"]?.ToString(), out long userId);
            var msg = ExtractText(o["message"]);
            if (string.IsNullOrEmpty(msg)) return;

            if (!QqCommandParser.IsAuthorized(userId, _authorizedQq, _allowAny))
            {
                _log($"QQ 控制被拒绝: 发送者 {userId} 不在授权列表");
                return;
            }

            var cmd = QqCommandParser.Parse(msg);
            if (cmd == QqCommand.Start)
            {
                _log($"QQ 控制: 启动检测（来自 {userId}）");
                _onStart?.Invoke(userId);
            }
            else if (cmd == QqCommand.Stop)
            {
                _log($"QQ 控制: 关闭检测（来自 {userId}）");
                _onStop?.Invoke(userId);
            }
        }
        catch (Exception ex)
        {
            _log($"QQ 控制事件解析失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 提取消息文本：onebot 私聊纯文本时 message 为字符串；
    /// 含 CQ 码时为数组，取其中 type=text 段的 text 拼接。
    /// </summary>
    private static string ExtractText(JsonNode node)
    {
        if (node is null) return string.Empty;
        if (node is JsonValue v && v.GetValueKind() == JsonValueKind.String)
            return v.GetValue<string>();
        if (node is JsonArray arr)
        {
            var sb = new StringBuilder();
            foreach (var item in arr)
            {
                if (item is JsonObject seg && seg["type"]?.ToString() == "text")
                {
                    var data = seg["data"];
                    if (data is JsonObject d && d["text"] is JsonValue tv && tv.GetValueKind() == JsonValueKind.String)
                        sb.Append(tv.GetValue<string>());
                }
            }
            return sb.ToString();
        }
        return string.Empty;
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        try { _loop?.Wait(2000); } catch { }
        try { _cts.Dispose(); } catch { }
    }
}
