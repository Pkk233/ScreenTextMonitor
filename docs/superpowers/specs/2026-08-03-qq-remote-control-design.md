# QQ 远程控制 · 设计规格（2026-08-03）

> 状态：已与用户确认，进入实现。
> 关联：复用现有 `Core/QqNotifier.cs`（NapCat HTTP 报警）、`Core/AppConfig.cs`、`MainForm.cs` 的 `StartMonitor` / `StopMonitor`。

## 1. 目标与范围

让本程序通过 QQ 私聊消息远程控制监控的启动与暂停：

- 收到私聊「启动检测」→ 启动监控（`StartMonitor`）
- 收到私聊「关闭检测」→ 关闭监控（`StopMonitor`）

**范围边界：**

- 仅处理 onebot `message` 事件的**私聊**（`message_type == "private"`），不处理群消息（除非后续扩展）。
- 仅支持「启动 / 关闭」两个命令，不做切换、状态查询等（设计预留扩展点）。
- 程序**本身不登录 QQ**，依赖已在运行的 NapCat 接入层（onebot 协议），通过「正向 WebSocket 事件」接收消息。

## 2. 架构

```
QQ 私聊
  │
  ▼
NapCat（用户本机，已运行）
  │  正向 WebSocket 事件（ws://127.0.0.1:3001，config: qq_ws_url）
  ▼
QqController（新增，ClientWebSocket，.NET 8 自带，零新依赖）
  │  解析 onebot message 事件 → 提取 user_id + message 文本
  │  鉴权（allowAny / 白名单 qq_target）
  │  命令解析（包含「启动检测」/「关闭检测」）
  ▼
MainForm（Invoke 回 UI 线程）→ StartMonitor / StopMonitor
  │
  ▼
执行回执：QqNotifier.SendPrivateAsync → 发给命令发送者 user_id
```

- **新增模块**：`Core/QqController.cs`
- **复用**：`Core/QqNotifier.SendPrivateAsync`（回执发送，走 HTTP API 地址 `qq_url`，与监听 WS 端口独立）
- **无新 NuGet 依赖**：`System.Net.WebSockets.ClientWebSocket` 随 .NET 8 自带。

## 3. 配置变更（AppConfig.cs）

新增两个字段，键名保持 snake_case 与现有风格一致：

| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `qq_ws_url` | string | `ws://127.0.0.1:3001` | NapCat 正向 WS 事件地址 |
| `qq_ctrl_allow_any` | bool | `true` | `true`=任意私聊可控制；`false`=仅 `qq_target` 可控制 |

需在 `AppConfig` 增加属性、`Load()`、`Save()` 三处同步（复刻现有 `qq_*` 写法）。

## 4. QqController 设计

```csharp
public sealed class QqController : IDisposable
{
    public QqController(string wsUrl, string token, string authorizedQq, bool allowAny,
                        Action onStart, Action onStop, Action<string> log);

    public void Start();    // 启动后台连接+重连循环
    public void Dispose();  // 取消令牌 + 关闭 WS + 停止重连
}
```

内部职责：

1. **连接循环 `ConnectLoopAsync`**：`ClientWebSocket.ConnectAsync` 到 `wsUrl`（带 `?access_token=token` 鉴权，token 为空则不加）。成功后进入接收循环；断开后等待 5s 重试，直到 `Dispose`。
2. **接收循环 `ReceiveLoopAsync`**：累积 WS 文本帧，按 onebot 以 `\n` 分隔的 JSON 行逐个解析。
3. **事件分发 `HandleEvent(json)`**：仅当 `post_type == "message"` 且 `message_type == "private"` 时继续；提取 `user_id` 与 `message` 文本（兼容字符串与 CQ 码数组两种形态，数组时拼接各 `text` 段）。
4. **鉴权**：`IsAuthorized(userId, authorizedQq, allowAny)` —— `allowAny` 为真直接通过；否则仅当 `userId == authorizedQq` 通过。
5. **命令解析**：`QqCommandParser.Parse(text)` 返回 `Start` / `Stop` / `None`（子串包含「启动检测」/「关闭检测」，忽略首尾空白）。
6. **回调**：命中命令后调用注入的 `onStart` / `onStop`（由 MainForm 提供，内部已 Invoke 回 UI 线程）。
7. **日志**：连接成功/失败/断开/鉴权拦截/命令命中均通过注入的 `log` 回调记录。

### 命令解析纯函数（便于独立验证）

```csharp
internal enum QqCommand { None, Start, Stop }

internal static class QqCommandParser
{
    public static QqCommand Parse(string text);
    public static bool IsAuthorized(long userId, string authorizedQq, bool allowAny);
}
```

## 5. 鉴权与安全

- 默认 `qq_ctrl_allow_any = true`（按用户选择"任意私聊都能控制"）。
- 保留 `authorizedQq = qq_target` 白名单能力：将 `qq_ctrl_allow_any` 关闭即仅该号可控制，便于随时收紧。
- `QqController.Start()` 成功后，MainForm 日志明确打印：`⚠ 已开放任意私聊控制，存在被他人控制的风险`。

## 6. 生命周期与线程

- `qq_enabled == true` 时，MainForm `OnShown` 之后启动 `QqController`；`OnFormClosing` 时 `Dispose` 并停止重连。
- `qq_enabled == false` 时不连接（既无报警回执也无监听 —— 维持"总开关"语义）。
- WS 回调在后台线程，MainForm 注入的 `onStart` / `onStop` 必须先 `this.Invoke(...)` 回到 UI 线程再调用 `StartMonitor` / `StopMonitor`（前者会操作 `_btnStart` 等控件）。

## 7. 执行回执

命令执行后，用 `QqNotifier.SendPrivateAsync(qq_url, qq_token, senderUserId, "✅ 已启动监控" / "⏹ 已关闭监控", null)` 回给**命令发送者**（`user_id`，非固定 `qq_target`），给用户明确反馈。失败不影响主流程（catch 记日志）。

## 8. UI 变更（MainForm.cs 设置标签 QQ 卡片）

在现有「QQ通知」卡片内新增：

- 「WebSocket 事件地址」输入框（`_entryQqWs`，默认 `ws://127.0.0.1:3001`），绑定 `qq_ws_url`。
- 「仅允许授权 QQ 控制」开关（`_swQqCtrlLock`，默认关 = 允许任意），绑定 `qq_ctrl_allow_any` 的反义。

`SaveConfig` / `LoadConfig` 同步这两个字段。

## 9. 验证策略与限制

**限制（重要）**：沙箱环境无法访问用户本机的 NapCat（`127.0.0.1` 是用户机器），**无法做端到端实机验证**。

验证手段：

1. **编译**：`dotnet build -c Release` 通过（WebSocket 无需新依赖，`AllowUnsafeBlocks` 已开）。
2. **临时脚本验证核心逻辑**（红绿）：写一个临时控制台程序引用 `QqCommandParser` 与事件解析，覆盖——命令解析（启动/关闭/无关文本）、鉴权（allowAny / 白名单命中与拦截）、onebot 事件 JSON（字符串 message / CQ 数组 message / 群消息被忽略）。验证后脚本不进仓库。
3. **代码评审**：规格符合 + 代码质量两阶段。
4. **本机验证步骤**：向用户输出一份本机验证清单（需 NapCat 开启正向 WS 事件、配置 `qq_ws_url`、勾选启用、手机 QQ 发「启动检测」观察程序与回执）。

## 10. 自审清单

- [x] 占位符：无 TODO / XXX 遗留。
- [x] 一致性：`qq_ws_url` / `qq_ctrl_allow_any` 在属性、`Load`、`Save`、UI 四处命名一致。
- [x] 范围：仅私聊、仅启动/关闭两命令，群消息明确忽略。
- [x] 歧义：WS 默认端口 `3001` 为 NapCat 常见默认，已在 UI 提示与日志说明"如不一致请修改"；回执目标为发送者 `user_id` 已明确。

## 11. 取舍记录

- **正向 WS 而非反向 WS / Webhook**：正向 WS 客户端零依赖、不占用程序端口（免防火墙/权限麻烦），最契合现有 NapCat 配置。
- **复用 `qq_enabled` 作总开关**：少一个配置项；用户本就在用 NapCat 通知，控制顺带开启最省事。
- **子串包含而非全等匹配**：允许命令前后带字，且「启动检测」四字连写误触概率极低。
