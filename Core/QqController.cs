using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace ScreenTextMonitor.Core;

/// <summary>QQ remote control commands.</summary>
internal enum QqCommand { None, Start, Stop }

/// <summary>Pure-function command parser and authorizer (no network dependency).</summary>
internal static class QqCommandParser
{
    public static QqCommand Parse(string text, string startCmd, string stopCmd)
    {
        if (string.IsNullOrEmpty(text)) return QqCommand.None;
        var t = text.Trim();
        if (!string.IsNullOrWhiteSpace(startCmd) && t.Contains(startCmd)) return QqCommand.Start;
        if (!string.IsNullOrWhiteSpace(stopCmd) && t.Contains(stopCmd)) return QqCommand.Stop;
        return QqCommand.None;
    }

    public static bool IsAuthorized(long userId, string authorizedQq, bool allowAny)
    {
        if (allowAny) return true;
        return long.TryParse(authorizedQq, out var auth) && auth == userId;
    }
}

/// <summary>
/// Listens on NapCat's "forward WebSocket events" for QQ private messages,
/// parses start/stop commands, and invokes callbacks.
/// </summary>
public sealed class QqController : IDisposable
{
    private readonly string _wsUrl;
    private readonly string _token;
    private readonly string _authorizedQq;
    private readonly bool _allowAny;
    private readonly string _cmdStart;
    private readonly string _cmdStop;
    private readonly Action<long> _onStart;
    private readonly Action<long> _onStop;
    private readonly Action<string> _log;

    private readonly CancellationTokenSource _cts = new();
    private readonly object _wsLock = new();
    private ClientWebSocket _ws;
    private Task _loop;
    private byte[] _recvBuffer = new byte[8192];
    private readonly StringBuilder _sb = new();

    public QqController(
        string wsUrl, string token, string authorizedQq, bool allowAny,
        string cmdStart, string cmdStop,
        Action<long> onStart, Action<long> onStop, Action<string> log)
    {
        _wsUrl = wsUrl ?? string.Empty;
        _token = token ?? string.Empty;
        _authorizedQq = authorizedQq ?? string.Empty;
        _allowAny = allowAny;
        _cmdStart = cmdStart ?? string.Empty;
        _cmdStop = cmdStop ?? string.Empty;
        _onStart = onStart;
        _onStop = onStop;
        _log = log ?? (_ => { });
    }

    public void Start()
    {
        if (_loop is not null) return;
        _loop = Task.Run(() => ConnectLoopAsync(_cts.Token));
    }

    private async Task ConnectLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var url = _wsUrl;
                if (!string.IsNullOrEmpty(_token) &&
                    !url.Contains("access_token=", StringComparison.OrdinalIgnoreCase))
                {
                    url += (url.Contains('?') ? "&" : "?") + "access_token=" + Uri.EscapeDataString(_token);
                }

                var ws = new ClientWebSocket();
                lock (_wsLock) _ws = ws;

                _log($"Connecting QQ WebSocket: {_wsUrl}");
                await ws.ConnectAsync(new Uri(url), ct).ConfigureAwait(false);
                _log("QQ WebSocket connected, listening for private messages");
                await ReceiveLoopAsync(ws, ct).ConfigureAwait(false);
                _log("QQ WebSocket disconnected");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _log($"QQ WebSocket error: {ex.Message}");
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
            try { await Task.Delay(5000, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                _sb.Clear();

                do
                {
                    if (_recvBuffer.Length < 8192)
                        Array.Resize(ref _recvBuffer, Math.Max(_recvBuffer.Length * 2, 8192));

                    result = await ws.ReceiveAsync(new ArraySegment<byte>(_recvBuffer), ct).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        if (ws.State == WebSocketState.Open)
                            await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", ct).ConfigureAwait(false);
                        return;
                    }
                    _sb.Append(Encoding.UTF8.GetString(_recvBuffer, 0, result.Count));
                } while (!result.EndOfMessage);

                var text = _sb.ToString();
                // onebot JSON events (one per line)
                foreach (var line in text.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.Length == 0) continue;
                    HandleEvent(trimmed);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log($"QQ WebSocket receive error: {ex.Message}");
        }
    }

    private void HandleEvent(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return;

            string postType = root.TryGetProperty("post_type", out var pt) ? (pt.GetString() ?? "") : "";
            string msgType = root.TryGetProperty("message_type", out var mt) ? (mt.GetString() ?? "") : "";
            long userId = 0;
            if (root.TryGetProperty("user_id", out var uid)) userId = uid.GetInt64();
            var msg = ExtractText(root);
            if (string.IsNullOrEmpty(msg) && root.TryGetProperty("raw_message", out var rmEl))
                msg = rmEl.GetString() ?? "";
            _log($"QQ event: post={postType} type={msgType} user={userId} text='{msg}'");

            if (postType != "" && postType != "message") return;
            if (msgType != "" && msgType != "private") return;
            if (string.IsNullOrEmpty(msg)) return;

            if (!QqCommandParser.IsAuthorized(userId, _authorizedQq, _allowAny))
            {
                _log($"QQ command rejected: sender {userId} not authorized");
                return;
            }

            var cmd = QqCommandParser.Parse(msg, _cmdStart, _cmdStop);
            if (cmd == QqCommand.Start)
            {
                _log($"QQ command: start detection (from {userId})");
                _onStart?.Invoke(userId);
            }
            else if (cmd == QqCommand.Stop)
            {
                _log($"QQ command: stop detection (from {userId})");
                _onStop?.Invoke(userId);
            }
        }
        catch (Exception ex)
        {
            _log($"QQ event parse error: {ex.Message}");
        }
    }

    private static string ExtractText(JsonElement message)
    {
        if (message.ValueKind == JsonValueKind.String)
            return message.GetString() ?? string.Empty;

        if (message.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var item in message.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                if (item.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "text" &&
                    item.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Object &&
                    dataEl.TryGetProperty("text", out var textEl))
                {
                    sb.Append(textEl.GetString());
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
