# ScreenTextMonitor 界面焕新设计规格（2026-08-04）

> UI 设计师产出。目标：在保留现有功能布局与"深色编辑器风"基调的前提下，
> 将**边框**从"单色平描边"升级为"分层玻璃质感边框"，并整体提升精致度与可用性。
> 全部方案均可在现有 WinForms + GDI+ 框架内实现（不引入 Web / 不新增依赖）。

## 1. 设计上下文（Design Context）

- **产品**：ScreenTextMonitor —— Windows 屏幕文字检测 / OCR 桌面工具。
- **受众**：中文 power user（游戏 / 自动化场景），长时间盯屏使用，技术敏感。
- **核心任务**：框选屏幕区域 + 设关键词 → 启停监控 → 命中时报警（蜂鸣 / 语音 / QQ）；远程 QQ 控制。
- **品牌调性**：IDE / 仪表盘风，精准、冷静、专业；本次在"冷静"基础上加"精致仪表感"（premium instrument panel），不花哨。

## 2. 设计令牌（Design Tokens）

### 2.1 色彩（在现有深色编辑器风上做层次化）
| 角色 | 色值 | 用途 |
|------|------|------|
| `Bg` | `#171d2b` | 应用底色（保持） |
| `Surface` | `#1e2740` | 卡片底（原 `#1f2735`，略提亮增加层次） |
| `SurfaceBot` | `#232e4d` | 卡片底部（与 Surface 做垂直渐变） |
| `Border` | `#2c3850` | 边框基准（保持） |
| `BorderHi` | `#3c4d72` | 边框高光（顶部内描边，制造玻璃斜边） |
| `BorderLo` | `#0e1422` | 边框暗部（底部外描边，制造下沉感） |
| `Accent` | `#38bdf8` | 强调色（保持） |
| `AccentTop` | `#7dd3fc` | 强调渐变上沿（按钮 / 边框高光） |
| `AccentBot` | `#0ea5e9` | 强调渐变下沿 |
| `Text` | `#e6edf3` | 主文字 |
| `TextSub` | `#9aa7bd` | 次文字 |
| `Success/Danger/Warning` | `#3fb950` / `#f85149` / `#d29922` | 语义色（保持） |
| `Terminal` | `#0e1422` | 日志终端底（比 Bg 更深一档） |
| `Shadow` | `rgba(0,0,0,0.35)` | 卡片投影（GDI+ 用半透明偏移圆角矩形模拟） |

### 2.2 字体（保持中文兼容）
- 界面：`Segoe UI`（中文回退微软雅黑）—— 标题用 Bold 13–16pt，正文 10pt。
- 等宽（日志 / 数值 / 状态）：`Consolas` 9.5pt。
- 新增：标题加 `letter-spacing: 0.5px`（GDI+ 用 TextFormatFlags 近似）提升精致度。

### 2.3 间距与半径
- 基础单位 4px；卡片内边距 16–20px；卡片间距 12px。
- 卡片圆角 `14px`、按钮 `12px`、输入框 `10px`、状态胶囊 `999px`（保持圆润语言）。

## 3. 边框系统（本次核心改动）

新增 `Theme.DrawBeautifulBorder`，替代各处单色 `DrawRoundRect`：

1. **底色渐变**：卡片用 `Surface → SurfaceBot` 垂直 `LinearGradientBrush`。
2. **双描边斜边**：先画 1px `BorderLo`（外下暗），再画 1px `BorderHi`（内上亮）→ 经典玻璃斜面。
3. **顶部高光线**：距顶 1px 处画一段 `BorderHi` 低透明度横线（仅上边），制造"玻璃顶边"。
4. **强调渐变边框**：主按钮 / 聚焦输入框用 `AccentTop → AccentBot` 描边。
5. **焦点态**：输入框获焦时边框转 `Accent` + 内部 1px 低透明青色辉光（非外发光，克制）。
6. **投影（可选）**：卡片绘制前先画一个向下偏移 2px、半透明 `Shadow` 圆角矩形，做"悬浮"层次。

> 关键：避免"纯黑发光 / 通用圆角+阴影"的 AI 通用感；用**斜面 + 顶高光**而非外发光来体现质感。

## 4. 组件规格

- **卡片 RoundedCard**：渐变底 + 双描边斜边 + 顶部高光 + 轻投影；标题 `Text` Bold。
- **顶部头 HeaderCard**：`AccentTop → AccentBot` 渐变填充，白字标题，状态胶囊（绿=监测中）。
- **按钮 RoundedButton**：
  - Primary：强调渐变 + 双描边斜边 + 顶部高光；hover 提亮、press 压暗。
  - Secondary：Surface 渐变 + Border 双描边；hover `HoverSoft`。
- **开关 RoundedSwitch**：轨道 `TrackOff →` 渐变；开启时 `AccentTop → AccentBot` 渐变 + 滑块软投影。
- **输入框 FlatTextBox**：Surface 渐变底 + 1px Border；聚焦转强调边框 + 内辉光。
- **日志区 LogBox**：Terminal 深底 + 双描边斜边 + Consolas 等宽 + 彩色高亮（[命中]紫 / [提示]青 / 状态绿）。

## 5. 布局（沿用现有，不重构功能）

- 运行页：顶部 Header（标题 + 状态胶囊）；左列配置卡（区域 / 目标 / 间隔 / 提醒）；右列状态卡 + 大号"开始监控"主按钮 + 终端日志面板。
- 设置页：各分组卡片（检测 / 提醒 / QQ通知）沿用现有字段，仅换边框与间距。
- 横屏桌面优先，固定宽度可滚动，符合现有形态。

## 6. 可访问性 / 可用性

- 对比度保持：文字 `#e6edf3` on `#1e2740` 对比 ≈ 11:1（远超 WCAG AA 4.5:1）。
- 焦点可见：输入框 / 按钮聚焦有明确强调边框，键盘可达。
- 触控目标 ≥ 36px（按钮高度保持）。
- 状态用语精简：如"监测中 / 已停止"，不重复界面已有信息。

## 7. 实现落点（后续确认后执行）

- `Ui/Theme.cs`：新增 `SurfaceBot / BorderHi / BorderLo / AccentTop / AccentBot / Shadow` 令牌 + `DrawBeautifulBorder` / `FillRoundRectGrad` 辅助方法。
- `Ui/RoundedCard.cs`：改用渐变底 + 双描边 + 顶高光 + 轻投影。
- `Ui/RoundedButton.cs`：主/次按钮改用渐变 + 斜边 + 顶高光。
- `Ui/RoundedSwitch.cs`、`Ui/FlatTextBox.cs`：聚焦/开启态套用新边框。
- `Ui/LogBox.cs`：Terminal 底 + 双描边。
- 重新 `dotnet build` + 打包 `发布\` 与 `发布_需NET\`。

## 8. 验证

- 编译 0 警告 0 错误；本机双击预览（沙箱无 GUI，需真机验证视觉）。
- 复用现有功能不改动，仅视觉层升级。
