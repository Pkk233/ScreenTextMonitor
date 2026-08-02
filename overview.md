# 概览：屏幕文字监控工具 C# 版 — 优化与缺陷修复（已实现）

## 状态
设计规格获批后，F1-F15 **全部实现完毕**，死文件/死代码全清。编译 0 警告 0 错误，单测 11/11 通过，GUI 启动验证通过。

## 本轮实现（对应规格 §4 决策）
| 编号 | 修复 | 证据 |
|---|---|---|
| F1 | Start/Stop 竞态释放引擎：`StopAndWait(2000)` + Start 守卫 + OnFormClosing Join | build + 逻辑审查 |
| F2 | 蜂鸣改 `kernel32!Beep` P/Invoke（与 winsound.Beep 同源），回退 Console.Beep | 单测不抛 |
| F3 | `WheelMessageFilter` 全局滚轮路由 + `ScrollBy` 真实滚动，OnShown 注册/OnFormClosing 注销 | build + 代码审查 |
| F4 | 移除坏 v4-rec fallback；强制 v6 rec+dict，缺则清晰报错；**删 `ch_PP-OCRv4_rec_infer.onnx`（~10MB）** | 单测（缺失报错 + 成功加载） |
| F5 | 进程优先级记录原值、Stop/关闭还原（`GetPriorityClass`/`NORMAL`） | build |
| F6 | 提醒异步化 `Task.Run` + `SemaphoreSlim(1)` 串行，不再阻塞监控线程 | build |
| F7 | Start 重置 `_cpuPrev`，消除重启 CPU 尖峰 | build |
| F8 | LogBox 超 20000 字截前 60% | 代码审查 |
| F9 | `GrayThumbCalc` unsafe 指针直读，省行拷贝 | 单测（均匀灰度/一致性） |
| F10 | 灰度缩略图 scratch 数组跨帧复用，MonitorLoop 双 buffer swap | 单测（同引用） |
| F11 | AppConfig/OnFormClosing catch 加 Debug 可观测 | build |
| F12 | RoundedSwitch 整控件可点切换 | 代码审查 |
| F13 | DPI：验证驱动，留待真机截图复核（不改代码） | — |
| F14 | **删 `FlatTextBox.Inner` 死代码** | grep 无引用 |
| F15 | csproj `BeforeTargets` 清空 `@(Models)`，阻止未用 v5 模型进输出 | bin 无 models/v5 |

## 🔴 意外发现（本应用此前从未真正启动成功）
可靠启动验证（PID + 输出文件，而非 tasklist 模糊匹配）暴露两个**原有构造期 NRE**，均已修复并加 STA 防回归测试：
1. `HeaderCard`：构造时 `Height=72` 在 `Pill` 创建前 → OnSizeChanged 空引用（`RoundedCard.cs`）。
2. `FlatTextBox`：构造时 `Width` 在 `_inner` 创建前 → LayoutInner 空引用（`FlatTextBox.cs`）。
修复模式：**先建子控件、再设尺寸，并加 null 双保险**。

## 验证证据
- `dotnet build -c Debug` → 0 警告 0 错误
- `dotnet test` → 11/11 通过（F2/F4/F9/F10/MeanAbsDiff/构造回归）
- GUI 启动：进程存活 7s+，输出文件无异常堆栈
- bin 产物：`models_ppocrv6` 仅含 det/cls/v6 rec/dict/inference.yml；无 v5、无 v4 rec

## 遗留 / 待办
- **F13 DPI**：需真机高 DPI 截图复核后再决定是否改缩放方案（规格 §4 决策不变）。
- 工程仍非 git 仓库；如需版本跟踪可 `git init`（规格文档在 `docs/superpowers/specs/`）。
- 真机端到端：框选→开始监控→命中提醒→滚轮滚动→关闭还原优先级，建议用户桌面验证。
