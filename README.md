# 屏幕文字监控工具（C# 版）

实时盯着屏幕某一块区域，用 OCR 识别里面的文字，一旦发现包含你指定的关键词就立刻提醒（蜂鸣 / 播放音频 / 语音播报），还能通过 QQ 发消息通知你。

本项目是 Python 版（Tkinter）的 **C# (.NET 8 WinForms) 重写版**，功能与 UI 完全一致：自绘圆角界面、OCR 文字检测、多种提醒方式、QQ 通知、性能优化一应俱全。

---

## ✨ 功能特性

- **屏幕区域框选**：一键进入全屏遮罩，鼠标拖拽框出要监控的区域，坐标自动回填。
- **目标文字匹配**：支持多个关键词（逗号分隔），命中任意一个即触发提醒。
- **三种提醒方式**：
  - `beep`：系统蜂鸣（`kernel32!Beep`，与 Python `winsound.Beep` 同源，无控制台也可靠）
  - `audio`：播放本地音频文件
  - `tts`：Windows 语音合成播报（System.Speech）
- **QQ 通知**：通过 NapCat 的 HTTP API 发送私聊消息，并附上命中时的屏幕截图。
- **QQ 远程控制**：通过 NapCat 的「正向 WebSocket 事件」接收 QQ 私聊消息，发「启动检测」启动监控、发「关闭检测」关闭监控（零额外依赖，.NET 8 自带 WebSocket）。
- **性能优化**：
  - 静止画面跳过识别、微小变动跳过识别
  - 截图降采样识别（最长边 1000px）
  - 空闲自动降频（最长 5s 一次）
  - 强制兜底识别间隔可调（1–10s），防止漏检
- **实时监控面板**：显示命中数、跳帧数、当前间隔、CPU 占用。
- **自绘 UI**：GDI+ 圆角控件库（按钮、开关、滑块、进度条、分段控件、状态胶囊、卡片等），与 Python 版视觉一致。

---

## 🖥️ 环境要求

- **操作系统**：Windows 10 / 11（x64）
- **运行时**（二选一，取决于你用哪个发布包）：
  - 免安装版：无需安装任何东西
  - 依赖运行时版：需安装 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

---

## 🚀 快速开始

### 方式一：用发布包（最简单）

项目根目录下有两个已打好的发布目录（默认不提交到仓库，需本地 `dotnet publish`，见下文）：

| 目录 | 说明 | 体积 |
|------|------|------|
| `发布/` | 免安装自包含版，双击 `ScreenTextMonitor.exe` 即开 | ~161 MB |
| `发布_需NET/` | 依赖 .NET 8 运行时版，需目标机已装 Desktop Runtime | ~38 MB |

> 这两个目录已写入 `.gitignore`，不会进入 Git 仓库。请在本机用下方命令自行生成，或单独分发。

### 方式二：从源码运行

```bash
# 需要已安装 .NET 8 SDK
cd 屏幕检测C
dotnet run -c Debug
```

---

## 📋 使用说明

程序主界面两个标签页：**运行** 与 **设置**。

### 运行标签
1. 点击 **「框选区域」**，拖拽鼠标选择要监控的屏幕区域，松开后坐标自动填入。
2. 在 **「目标文字」** 输入要监控的关键词（多个用逗号分隔，如 `收金,比例`）。
3. 点击 **「▶ 开始监控」**。命中时按「设置」里选的方式提醒，监控面板实时统计。
4. 点击「📷 预览」可查看最近一次命中截图。

### 设置标签
- **提醒方式**：蜂鸣 / 音频文件 / 语音播报（TTS），并可设频率、时长、播报文本。
- **QQ 通知**：启用后填写 NapCat 地址、token、目标 QQ 号、消息模板（`{target}` 会被替换为命中的关键词）。
- **QQ 远程控制**：在「QQ通知」卡片底部还有「WS事件地址」（NapCat 正向 WebSocket 事件地址，默认 `ws://127.0.0.1:3001`）和「仅允许授权QQ控制」开关。启用 QQ 通知后：
  - 用 QQ 私聊发送 **「启动检测」** 启动监控、**「关闭检测」** 关闭监控（任意好友均可控制，也可勾选开关限定只有「目标QQ」能控制）。
  - 命令执行后程序会回一条私聊给你确认。
  - **启用或修改 QQ 控制后需重启软件才生效**（设置仅在关闭时保存）。
- **检测间隔**：基础轮询间隔（秒）。
- **性能优化**：静止跳过、智能跳过、性能模式、空闲降频、强制识别间隔、变化灵敏度等开关与滑块。
- 滚轮可在标签内上下滚动内容。

---

## ⚙️ 配置文件（config.json）

程序首次运行会在同目录生成 `config.json`，键名与 Python 版完全兼容，可直接互换：

| 键 | 类型 | 说明 |
|----|------|------|
| `region_x` / `region_y` / `region_w` / `region_h` | string | 监控区域坐标与宽高（屏幕像素） |
| `targets` | string | 目标关键词，逗号分隔 |
| `alert_mode` | string | 提醒方式：`beep` / `audio` / `tts` |
| `freq` / `dur` | string | 蜂鸣频率(Hz) / 时长(ms) |
| `audio_path` | string | 音频模式下的音频文件路径 |
| `tts_text` | string | 语音播报文本 |
| `interval` | string | 基础检测间隔（秒） |
| `skip_static` | bool | 静止画面跳过识别 |
| `smart_skip` | bool | 微小变动跳过识别 |
| `perf_mode` | bool | 性能模式（更低 CPU） |
| `auto_backoff` | bool | 空闲自动降频 |
| `force_ocr_idle` | number | 强制兜底识别间隔（秒） |
| `ocr_threshold` | number | 变化判定灵敏度（越小越灵敏） |
| `qq_enabled` | bool | 是否启用 QQ 通知 |
| `qq_url` | string | NapCat HTTP 地址，如 `http://127.0.0.1:3000` |
| `qq_token` | string | NapCat access_token（可选） |
| `qq_target` | string | 接收通知的 QQ 号 |
| `qq_msg` | string | 消息模板，`{target}` 替换为命中关键词 |
| `qq_ws_url` | string | NapCat 正向 WebSocket 事件地址，如 `ws://127.0.0.1:3001`（QQ 远程控制用，与 HTTP 通知地址是独立端口） |
| `qq_ctrl_allow_any` | bool | 是否允许任意 QQ 私聊控制（`true`=任意好友可控制；`false`=仅 `qq_target` 可控制） |

---

## 🔧 构建与发布

需要 .NET 8 SDK。

```bash
# 调试构建
dotnet build -c Debug

# 发布：免安装自包含单文件
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:PublishReadyToRun=true -o "发布"

# 发布：依赖 .NET 8 运行时版（体积小）
dotnet publish -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true -o "发布_需NET"
```

> 发布目录需**整体拷贝**到目标机（OCR 模型 `models_ppocrv6/` 与若干原生 dll 是运行必需的，不能只拿一个 exe）。

---

## 🧱 项目结构

```
屏幕检测C/
├── MainForm.cs            # 主窗口：双标签布局、监控线程、CPU 采样、配置读写
├── Program.cs             # 入口（STAThread + HighDPI）
├── Ui/                    # 自绘圆角控件库
│   ├── Theme.cs          # 设计令牌与绘图工具
│   ├── Layout.cs         # StackPanel / ScrollStack / HRow 等布局容器
│   ├── RoundedButton.cs  # 圆角按钮
│   ├── RoundedSwitch.cs  # iOS 风格开关
│   ├── RoundedSlider.cs  # 圆角滑块
│   ├── RoundedProgressBar.cs
│   ├── SegmentedControl.cs
│   ├── StatusPill.cs
│   ├── RoundedCard.cs    # RoundedCard / HeaderCard
│   ├── FlatTextBox.cs
│   └── LogBox.cs
├── Core/                  # 核心逻辑
│   ├── NativeMethods.cs  # P/Invoke（Beep / 进程优先级 / CPU 时间 / 音频）
│   ├── AppConfig.cs      # config.json 读写
│   ├── ScreenCapture.cs  # GDI+ 截屏 + 灰度差分
│   ├── OcrEngine.cs      # RapidOCR 封装（PP-OCRv6_tiny_rec）
│   ├── RegionSelectorForm.cs  # 全屏框选
│   ├── ImagePreviewForm.cs    # 截图预览
│   ├── Alerts.cs         # 蜂鸣 / 音频 / 语音
│   ├── QqNotifier.cs     # NapCat QQ 通知
│   └── QqController.cs   # QQ 远程控制（NapCat 正向 WS 监听 + 命令解析）
├── models_ppocrv6/       # OCR 模型（det / cls / v6 rec / 字典）
└── config.json
```

---

## 🧩 技术栈

- **.NET 8 / WinForms**（`net8.0-windows`，x64）
- **RapidOcrNet 3.0.0** + **Microsoft.ML.OnnxRuntime 1.27** —— OCR 推理（PP-OCRv6_tiny 识别模型）
- **SkiaSharp 3.119** —— 图像缩放与像素处理
- **System.Speech** —— TTS 语音播报
- **GDI+** —— 自绘圆角 UI

---

## 📄 许可证

未指定。如需开源发布，请自行添加许可证文件。
