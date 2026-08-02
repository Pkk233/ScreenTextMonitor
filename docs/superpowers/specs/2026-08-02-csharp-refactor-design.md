# 屏幕文字监控工具 C# 版 — 优化与缺陷修复设计规格

- **日期**：2026-08-02
- **状态**：待人类伙伴复核
- **工程**：`D:\c\屏幕检测C`（.NET 8 WinForms，由 `D:\pyxm\屏幕检测v6` Python+Tkinter 移植）
- **方法论**：规格驱动 + 红-绿-重构 TDD；未经本规格获批不写实现代码

---

## 1. 背景与目标

移植版已编译通过（0 警告 0 错误）、OCR 运行时验证通过、GUI 可启动。本次目标：在**不改变功能与 UI**的前提下，修复代码审查发现的正确性缺陷，补齐性能与质量短板，使移植版在可靠性与体验上对齐甚至超过 Python 源版。

## 2. 范围

**In scope（全部覆盖）**

| 编号 | 类别 | 标题 |
|---|---|---|
| F1 | 正确性(高) | Start/Stop 竞态释放 OCR 引擎 |
| F2 | 正确性(高) | 蜂鸣在 WinForms 下可能无声 |
| F3 | 正确性(高) | 滚轮不滚动内容区 |
| F4 | 正确性(高) | OCR fallback 字典缺失（死且坏） |
| F5 | 正确性(中) | 进程优先级降级不还原 |
| F6 | 性能(中) | TTS/蜂鸣同步阻塞监控线程 |
| F7 | 正确性(中) | CPU 采样 `_cpuPrev` 残留致重启尖峰 |
| F8 | 质量(低) | 日志无上限增长 |
| F9 | 性能(低) | `GrayThumbnail` 托管循环吃 CPU |
| F10 | 性能(低) | 每帧 `new byte[]` 缩略图 GC 压力 |
| F11 | 质量(低) | 宽泛空 catch 吞异常 |
| F12 | UX(低) | `RoundedSwitch` 点标签不切换 |
| F13 | 质量(低/验证) | DPI：自绘内容高分屏不缩放 |
| F14 | 质量(低) | 死代码清理 |
| F15 | 优化(低) | 未用 v5 模型被拷入输出致膨胀 |

**Out of scope**：UI 视觉重设计、新增功能、更换 OCR 引擎、改 `config.json` 键名、改变监控算法语义。

## 3. 现状与问题清单（分级，附位置/根因）

### 🔴 高优先级
- **F1** — `MainForm.cs:776` `_engine?.Dispose()`；`MainForm.cs:797` `StopMonitor` 仅置 `_monitoring=false`，不 `Join`。旧监控线程可能仍卡在 `MainForm.cs:884` `_engine.Recognize()`；快速连按开始/停止 → `ObjectDisposedException`/野指针。
- **F2** — `Alerts.cs:13` `Console.Beep`。WinExe 无控制台时行为不确定，且不等价于 Python `winsound.Beep`（后者走 `kernel32!Beep`）。可能导致「系统蜂鸣」提醒模式静默失效。
- **F3** — `Ui/Layout.cs:137` `ScrollStack.ScrollBy` 为死代码；全工程无 `Application.AddMessageFilter`。鼠标悬停在卡片/输入框上时滚轮消息路由到子控件，ScrollStack 不滚动。
- **F4** — `Core/OcrEngine.cs:51` fallback 指向 `models_ppocrv6/ppocr_keys_v1.txt`，但该文件**本地不存在**（RapidOcrNet 3.0.0 包只内置 v5 latin dict，无 v1 dict；Python 源工程也未提供）。v6 rec 一旦缺失，fallback 会把不存在的 keys 路径传给 `InitModels`，抛出不易理解的异常。

### 🟡 中优先级
- **F5** — `MainForm.cs:744` Start 降到 `BELOW_NORMAL_PRIORITY_CLASS`；`StopMonitor`/`OnFormClosing` 从不还原。
- **F6** — `Alerts.cs:13,40` `Console.Beep`/`SpeechSynthesizer.Speak` 同步执行在监控线程，命中提醒期间无法检测。
- **F7** — `MainForm.cs:1018` Start 不重置 `_cpuPrev`，重启后首帧按跨停顿时长算出 CPU 尖峰。

### 🟢 低优先级 / 优化 / 质量
- **F8** — `Ui/LogBox.cs` RichTextBox 无限增长。
- **F9** — `Core/ScreenCapture.cs:56` `GrayThumbnail` 逐行 `Marshal.Copy` + 托管像素循环，大区域（4K）每帧 8M+ 迭代。
- **F10** — `MainForm.cs:836` 每帧 `new byte[]` 灰度缩略图，GC 压力。
- **F11** — `Alerts`/`AppConfig`/`OnFormClosing` 多处宽泛空 catch 静默，掩盖问题。
- **F12** — `Ui/RoundedSwitch.cs:49` `OnMouseDown` 仅当 `e.X <= _trackW+4` 切换；点标签文字无效。
- **F13** — `MainForm.cs:63` `AutoScaleMode.Dpi` + PerMonitorV2 + 自绘硬编码像素，高分屏自绘内容可能不随 DPI 缩放。
- **F14** — 死代码：`Ui/Layout.cs` `ScrollBy`（F3 复用后保留）、`Ui/FlatTextBox.cs:29` `Inner` 属性未使用。
- **F15** — `~/.nuget/packages/rapidocrnet/3.0.0/build/RapidOcrNet.targets` 在 Build/Publish 后把未使用的 v5 模型（det/cls/latin rec/latin dict）拷到 `$(TargetDir)models\v5\`，膨胀产物。

## 4. 设计方案（逐项：根因 / 决策 / 涉及文件 / 验证）

### F1 Start/Stop 竞态（高）
- **根因**：停止不同步；引擎被并发释放。
- **决策**：
  1. `StopMonitor` 置 `_monitoring=false` 后调用 `_monitorThread?.Join(2000)`（监控循环 `SleepInterruptible` 切片 100ms，单次 OCR ≤1s，2s 足够退出）。
  2. `StartMonitor` 开头守卫：若 `_monitorThread?.IsAlive == true`，先置 `_monitoring=false` 再 `Join(2000)`，确认旧线程退出后才 `_engine?.Dispose()` + 新建。
  3. `OnFormClosing` 已有 `Join(800)`；与 StopMonitor 的 Join 统一为私有 `StopAndWait(int ms)`。
- **涉及**：`MainForm.cs`（StartMonitor/StopMonitor/OnFormClosing）。
- **验证**：单测——注入一个「Recognize 期间若被 Dispose 则抛」的桩引擎，模拟快速 toggle，断言无 `ObjectDisposedException` 且 Recognize 调用计数自洽。

### F2 蜂鸣可靠性（高）
- **根因**：`Console.Beep` 在无控制台 WinExe 下不确定。
- **决策**：`NativeMethods` 增 `kernel32!Beep(uint dwFreq, uint dwDuration)`（与 `winsound.Beep` 同源）；`Alerts.Beep` 优先调用它，失败回退 `Console.Beep`；`freq`/`dur` 仍 `Clamp`。
- **涉及**：`Core/NativeMethods.cs`、`Core/Alerts.cs`。
- **验证**：单测 `Beep(1000, 30)` 不抛、越界 freq 被夹紧到 [37,32767]（不断言有声）。

### F3 滚轮滚动（高）
- **根因**：无全局消息过滤器，`ScrollBy` 死代码。
- **决策**：实现 `WheelMessageFilter : IMessageFilter`（放 `Ui/Layout.cs`），`PreFilterMessage` 捕获 `WM_MOUSEWHEEL=0x020A`，取光标下控件、向上找最近 `ScrollStack`，调其 `ScrollBy(lines)`。`ScrollBy` 改为基于 `AutoScrollPosition` 平滑滚动（行高 36px）。`MainForm.OnShown` 注册、`OnFormClosed` 注销。
- **涉及**：`Ui/Layout.cs`、`MainForm.cs`。
- **验证**：单测 `ScrollBy` 在 `AutoScrollMinSize.Height > ClientSize.Height` 时改变 `AutoScrollPosition.Y`。

### F4 OCR fallback 字典（高）
- **根因**：fallback 指向不存在的 `ppocr_keys_v1.txt`；该文件本地/包内均无。
- **决策**：移除「v4 rec fallback」这条坏路径（Python 源工程本就只发 v6 rec + dict，依赖 rapidocr 内置 det/cls）。改为：**强制要求 v6 rec + dict**，缺失即抛清晰的 `FileNotFoundException("缺少 PP-OCRv6 识别模型或字典，请补齐 models_ppocrv6/PP-OCRv6_tiny_rec.onnx 与 ppocrv6_dict.txt")`；不再把坏路径传给 `InitModels`。附带：`ch_PP-OCRv4_rec_infer.onnx` 仅被该 fallback 引用，移除后该文件成死文件，可在 `models_ppocrv6/` 删除（~10MB）。**det/cls 仍用 v4/v2，不变。**
- **涉及**：`Core/OcrEngine.cs`、`models_ppocrv6/`。
- **验证**：单测——v6 在场时构造成功 `UsingV6Rec==true`；临时移走 v6 rec 构造抛清晰异常（非 InitModels 内部异常）。

### F5 进程优先级（中）
- **决策**：`NativeMethods` 增 `GetPriorityClass` + 常量 `NORMAL_PRIORITY_CLASS=0x20`；Start 记录原优先级；`StopMonitor`/`OnFormClosing` 还原。
- **涉及**：`Core/NativeMethods.cs`、`MainForm.cs`。
- **验证**：单测 Start→Stop 后 `GetPriorityClass` == 原值（用伪句柄或进程自身）。

### F6 提醒不阻塞监控（中）
- **根因**：Beep/Speak 同步在监控线程。
- **决策**：命中后用 `Task.Run` 包裹提醒调用（与 QQ 通知并发模型一致）；用 `SemaphoreSlim(1,0)` 串行提醒避免蜂鸣/语音叠加；`Alerts.Speak` 每次 new 独立 `SpeechSynthesizer`，可并行。
- **涉及**：`MainForm.cs` MonitorLoop 命中分支、新增 `SemaphoreSlim _alertGate`。
- **验证**：单测——命中后监控线程立即进入下一轮（用计时桩断言提醒在后台）。

### F7 CPU 采样残留（中）
- **决策**：`StartMonitor` 置 `_cpuPrev = null`。
- **涉及**：`MainForm.cs`。
- **验证**：单测 Start 后 `_cpuPrev==null`。

### F8 日志上限（低）
- **决策**：`LogBox.Append` 后若 `TextLength > 20000` 截掉前 60%，保最近段；用 `RichTextBox.Select(0,len)+Clear+...` 或重设 `Rtf`。
- **涉及**：`Ui/LogBox.cs`。
- **验证**：单测连续 Append 超限后 `TextLength <= ~20000`。

### F9 GrayThumbnail 提速（低）
- **决策**：csproj 开 `<AllowUnsafeBlocks>`；`GrayThumbnail` 改用 `BitmapData.Scan0` 指针直读 + 累加，省去逐行 `Marshal.Copy`；算法不变。
- **涉及**：`ScreenTextMonitor.csproj`、`Core/ScreenCapture.cs`。
- **验证**：单测同一图托管版与 unsafe 版输出 `MeanAbsDiff` 一致。

### F10 缩略图 buffer 复用（低）
- **决策**：MonitorLoop 双 buffer（`prevSmall`/`curSmall` 两个固定 `byte[]` 交换），不再每帧 new；`GrayThumbnail` 改为写入传入 buffer 的重载。
- **涉及**：`Core/ScreenCapture.cs`、`MainForm.cs`。
- **验证**：单测复用版与 new 版结果一致；监控循环无新分配（计断言可选）。

### F11 空 catch 改善（低）
- **决策**：关键路径（`OnFormClosing`、`Alerts`、`AppConfig.Load/Save`）的 catch 至少 `LogAsync`/debug 记录异常摘要；保留 Python-parity 的「不向用户报错」语义但加可观测性。
- **涉及**：上述文件。
- **验证**：构造异常路径，断言日志出现异常摘要。

### F12 RoundedSwitch 标签可点（低）
- **决策**：`OnMouseDown` 移除 `e.X <= _trackW+4` 限制，整控件可切换；左键即可。
- **涉及**：`Ui/RoundedSwitch.cs`。
- **验证**：单测点标签坐标触发 `CheckedChanged`。

### F13 DPI 自绘缩放（低 / 验证驱动）
- **现状**：`AutoScaleMode.Dpi` + PerMonitorV2 + 自绘硬编码像素。
- **决策**：**验证驱动**——先在目标高 DPI 屏真机截图；若自绘内容（按钮/滑块/胶囊）未随 DPI 缩放，再决策：方案 A 改 `AutoScaleMode.None` + `OnDpiChanged` 手动缩放 `Theme` 字号/半径；方案 B 让自绘控件按 `DeviceDpi` 缩放绘制参数。验证前不贸然改。
- **涉及**：`MainForm.cs`、`Ui/Theme.cs`（验证后定）。
- **验证**：真机截图对比 100%/150% DPI。

### F14 死代码清理（低）
- **决策**：`ScrollBy` 经 F3 复用保留；移除 `FlatTextBox.Inner`（确认全工程无引用，MainForm 用 `.Text` 包装）。
- **涉及**：`Ui/FlatTextBox.cs`。
- **验证**：编译通过；grep 无 `Inner` 残留。

### F15 未用 v5 模型膨胀（低）
- **根因**：`RapidOcrNet.targets` AfterTargets=Build/Publish 拷 v5 模型到输出。
- **决策**：csproj 加一个 `BeforeTargets="CopyModelsBuild"` 的 target 清空 `@(Models)`，阻止拷贝：
  ```xml
  <Target Name="_SuppressRapidOcrV5Models" BeforeTargets="CopyModelsBuild;CopyModelsPublish">
    <ItemGroup><Models Remove="@(Models)" /></ItemGroup>
  </Target>
  ```
- **涉及**：`ScreenTextMonitor.csproj`。
- **验证**：Build 后 `bin/.../models/v5/` 不存在；OCR 仍正常（用自有 v4 det/cls + v6 rec）。

## 5. 实现计划（TDD 任务拆分，每项 2-5 分钟，带文件路径与验证）

新增测试项目 `ScreenTextMonitor.Tests`（net8.0-windows，xUnit），ProjectReference 主工程。

| # | 任务（红→绿→重构） | 文件 | 验证 |
|---|---|---|---|
| T1 | F1 竞态：写失败测试（快速 toggle 不抛 ODE）→ StopMonitor 加 Join → StartMonitor 守卫 → 抽 `StopAndWait` | `MainForm.cs` | `dotnet test` 绿 |
| T2 | F2 蜂鸣：写不抛测试 → 加 `kernel32!Beep` P/Invoke → 调用优先级 | `NativeMethods.cs`/`Alerts.cs` | `dotnet test` 绿 |
| T3 | F5 优先级：加 `GetPriorityClass`/`NORMAL` → Stop 还原 | `NativeMethods.cs`/`MainForm.cs` | 测试+build |
| T4 | F4 fallback：移除坏分支 → 缺失抛清晰异常 → 删 v4 rec 文件 | `OcrEngine.cs`/models | 测试+build |
| T5 | F6 异步提醒 + SemaphoreSlim → 不阻塞测试 | `MainForm.cs` | `dotnet test` 绿 |
| T6 | F7 Start 重置 `_cpuPrev` | `MainForm.cs` | build |
| T7 | F3 `WheelMessageFilter` + `ScrollBy` → 注册/注销 → ScrollBy 测试 | `Ui/Layout.cs`/`MainForm.cs` | `dotnet test` 绿 |
| T8 | F8 LogBox 截断测试 → 实现 | `Ui/LogBox.cs` | `dotnet test` 绿 |
| T9 | F9 unsafe GrayThumbnail → 一致性测试 | `ScreenCapture.cs`/csproj | `dotnet test` 绿 |
| T10 | F10 双 buffer 复用 → 一致性测试 | `ScreenCapture.cs`/`MainForm.cs` | `dotnet test` 绿 |
| T11 | F11 catch 加日志 → 异常路径测试 | 多文件 | `dotnet test` 绿 |
| T12 | F12 Switch 标签可点 → 点击测试 | `Ui/RoundedSwitch.cs` | `dotnet test` 绿 |
| T13 | F15 csproj suppress v5 → build 后无 v5 目录 | `ScreenTextMonitor.csproj` | build+断言 |
| T14 | F14 删 `Inner` | `Ui/FlatTextBox.cs` | build+grep |
| T15 | F13 DPI 真机验证（手动，非自动化） | — | 截图对比 |
| T16 | 全量回归：`dotnet build -c Debug` 0 警告 0 错误 + GUI 启动 8s + OCR 冒烟 | — | 证据 |

## 6. 验证策略

1. **编译**：`dotnet build -c Debug` 必须 0 警告 0 错误。
2. **单测**：`dotnet test`，覆盖 F1/F2/F3(ScrollBy)/F4/F6/F8/F9/F10/F12 等可自动化项。
3. **集成**：OCR 冒烟（v6 rec 识别中文测试图）；GUI 启动存活 8s。
4. **真机**：框选→开始监控→命中提醒→滚轮滚动→关闭后优先级还原→日志不爆。

## 7. 风险与回滚

- **F9 unsafe**：需 `<AllowUnsafeBlocks>`；回滚即还原托管版（行为不变）。
- **F6 异步提醒并发**：`SemaphoreSlim` 防蜂鸣叠加；回滚即恢复同步。
- **F13 DPI**：验证驱动，不贸然改；无问题则仅记录。
- **F4 删 v4 rec 文件**：确认 grep 无引用后再删；保留也无害（仅占空间）。
- **F15 抑制 v5 拷贝**：不影响自有模型加载；回滚即移除 csproj target。

## 8. 自审清单（spec 完整性）

- **占位符**：无 TODO/XXX/FIXME 待补；F13 明确标「验证驱动」。
- **一致性**：`config.json` 键名/监控算法语义不变；det/cls 仍 v4/v2；不破坏 Python 配置互换。
- **范围**：15 项均落在 §2 In scope；无超范围（不改 UI 视觉、不加功能）。
- **歧义**：F13 唯一依赖真机结果，已显式标注；其余决策均给出可执行步骤与验证。
- **依赖关系**：F1/F5 都动 `StopMonitor`，按 T1→T3 顺序合并避免冲突；F9/F10 都动 `ScreenCapture`，按 T9→T10。

## 9. 待人类伙伴复核

1. 是否同意新增 `ScreenTextMonitor.Tests` 测试项目（增加工程复杂度但保证可自动化验证）？
2. F13 DPI 处理策略：**验证驱动**（先真机跑，无问题不动）vs **主动改造**（直接上 AutoScaleMode.None + 手动缩放）？
3. F4 是否同意移除 v4-rec fallback 与删除 `ch_PP-OCRv4_rec_infer.onnx`（~10MB 死文件）？

获批后按 §5 顺序逐任务走红-绿-重构。
