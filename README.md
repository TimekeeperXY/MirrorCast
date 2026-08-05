<div align="center">

# MirrorCast

**把主屏上的单个窗口实时镜像到副屏，全屏展示而不暴露整个桌面。**

[![Windows](https://img.shields.io/badge/Windows-10%201809%2B-0078D4?logo=windows)](https://github.com/TimekeeperXY/MirrorCast/releases)
[![macOS](https://img.shields.io/badge/macOS-13%2B-000000?logo=apple)](https://github.com/TimekeeperXY/MirrorCast/releases/tag/v1.2.0)
[![Release](https://img.shields.io/github/v/release/TimekeeperXY/MirrorCast)](https://github.com/TimekeeperXY/MirrorCast/releases)
[![License](https://img.shields.io/badge/license-MIT-2ea44f)](LICENSE)

[下载 Windows 版](https://github.com/TimekeeperXY/MirrorCast/releases/download/v1.1.1/MirrorCast.exe) ·
[下载 macOS DMG](https://github.com/TimekeeperXY/MirrorCast/releases/download/v1.2.0/MirrorCast-v1.2.0-macOS-arm64.dmg) ·
[查看全部版本](https://github.com/TimekeeperXY/MirrorCast/releases)

</div>

---

## MirrorCast 能做什么

在讲课、演示、直播或会议中，你通常只想让观众看到 PPT、浏览器或某个应用窗口，而不是整个桌面。

MirrorCast 会把选中的窗口留在主屏原位，同时将它实时显示在副屏上。主屏可以继续操作其他软件，副屏始终只展示镜像内容。

```text
┌──────────── 主屏：你操作 ────────────┐    ┌──── 副屏：观众观看 ────┐
│ PPT · 备课笔记 · 聊天软件 · 浏览器   │ ─→ │      指定窗口全屏       │
└──────────────────────────────────────┘    └────────────────────────┘
```

## 主要功能

- 单窗口镜像，不暴露整个桌面
- 副屏无边框全屏显示，支持不同分辨率和 DPI
- 多种缩放模式，可保留比例、填满或拉伸
- 可选择是否在副屏显示鼠标指针
- 副屏演示辅助：全屏放大、指针区域放大镜和指针聚光灯
- 镜像期间直接切换源窗口，无需退出副屏全屏
- 源窗口关闭或目标显示器断开时自动停止
- 自定义全局快捷键，一键开始或停止镜像
- 常驻系统托盘或菜单栏，自动保存常用设置
- 首次运行提供权限和操作流程引导

## 平台支持

| | Windows | macOS |
|---|---|---|
| 系统要求 | Windows 10 1809+ / Windows 11 | macOS 13+ |
| 支持架构 | x64 | Apple Silicon arm64、Intel x86_64 |
| 安装包 | 单文件 `MirrorCast.exe` | 按架构提供的 `MirrorCast-*-macOS-*.dmg` |
| 镜像技术 | DWM Thumbnail | ScreenCaptureKit + IOSurface |
| 后台入口 | 系统托盘 | 菜单栏 |
| 默认快捷键 | `Ctrl + Alt + M` | `Control + Option + M` |
| 额外权限 | 无 | 屏幕录制 |

两种实现都尽量让画面留在 GPU 路径中。Windows 使用 DWM 合成，开销接近系统原生缩略图；macOS 使用 ScreenCaptureKit 捕获流，资源占用会高于 Windows 版。

## 安装

### Windows

1. 下载 [MirrorCast v1.1.1 Windows EXE](https://github.com/TimekeeperXY/MirrorCast/releases/download/v1.1.1/MirrorCast.exe)。
2. 双击运行，无需安装 .NET 或其他运行库。
3. 如果 Windows SmartScreen 显示“已保护你的电脑”，点击“更多信息”后选择“仍要运行”。

MirrorCast 是便携应用。退出程序后删除 `MirrorCast.exe` 即可卸载。

### macOS

当前 v1.2.0 DMG 支持 Apple Silicon Mac，包括 M1、M2、M3、M4 及后续芯片。Intel 版已在源码和自动构建中支持，将从后续版本开始提供独立 DMG。

1. 下载 [MirrorCast v1.2.0 macOS DMG](https://github.com/TimekeeperXY/MirrorCast/releases/download/v1.2.0/MirrorCast-v1.2.0-macOS-arm64.dmg)。
2. 双击 DMG，将 `MirrorCast.app` 拖到 `Applications`。
3. 打开 Finder 的“应用程序”，按住 `Control` 单击 MirrorCast，选择“打开”。
4. 在确认窗口中再次点击“打开”。
5. 根据软件提示授予“屏幕录制”权限，然后完全退出并重新打开 MirrorCast。

> [!IMPORTANT]
> macOS 版使用 ad-hoc 签名，未经过 Apple 公证。如果系统没有提供“打开”按钮，请前往“系统设置 → 隐私与安全性”点击“仍要打开”。

如果系统提示“应用已损坏”，请先确认 DMG 来自本仓库官方 Release，然后执行：

```bash
xattr -dr com.apple.quarantine "/Applications/MirrorCast.app"
```

这条命令只移除 MirrorCast 的下载隔离标记。**不要全局关闭 Gatekeeper。**

完整的安装、授权、更新和卸载说明请参阅 [macOS 安装指南](mac/INSTALL.md)。

## 使用方法

1. 将两块显示器设置为“扩展”模式，而不是“复制”模式。
2. 打开 MirrorCast，选择要镜像的窗口。
3. 选择目标显示器、缩放模式和鼠标指针选项。
4. 点击“开始镜像”，或使用全局快捷键。

镜像开始后，可以直接在窗口列表中选择另一个窗口，副屏会自动切换。关闭控制面板不会退出程序；请通过系统托盘或菜单栏重新打开、停止镜像或退出。

### 自定义快捷键

点击界面中的快捷键按钮，然后按下新的组合键。组合键需要包含至少一个修饰键：

- Windows：`Ctrl`、`Alt` 或 `Shift`
- macOS：`Control`、`Option`、`Shift` 或 `Command`

按 `Esc` 取消录制。如果组合键被其他应用占用，MirrorCast 会保留原设置。

### 演示辅助（Windows）

镜像开始后，可以从运行状态面板或全局快捷键切换以下效果：

- 屏幕放大：以源窗口中的鼠标位置为中心放大整个副屏画面，默认 `Ctrl + Alt + Shift + Z`。
- 指针放大镜：在副屏鼠标位置显示局部放大区域，默认 `Ctrl + Alt + Shift + L`。
- 指针聚光灯：压暗副屏其他区域并突出鼠标附近，默认 `Ctrl + Alt + Shift + P`。

放大倍数、指针效果范围和三组快捷键均可在控制面板中调整。屏幕放大与指针放大镜互斥，聚光灯可以与任一模式组合。所有效果仅作用于副屏镜像，不会遮挡或拦截主屏操作。

## 缩放模式

| 平台 | 模式 | 效果 |
|---|---|---|
| Windows | 等比适应 | 保持宽高比并完整显示，空余区域补黑边 |
| Windows | 拉伸填满 | 强制铺满副屏，可能改变宽高比 |
| Windows | 原始尺寸 | 优先按 1:1 显示，超出副屏时等比缩小 |
| macOS | 适应 | 保持宽高比并完整显示，空余区域补黑边 |
| macOS | 填满 | 保持宽高比并铺满副屏，边缘可能裁切 |
| macOS | 拉伸 | 强制铺满副屏，可能改变宽高比 |

## 已知限制

### 通用

- MirrorCast 是只读镜像工具，不会把副屏点击或键盘操作传回源窗口。
- 不提供录制、推流、叠加特效或远程控制功能。
- 至少需要两块显示器，并处于扩展模式。

### Windows

- 全屏独占模式的游戏可能无法被 DWM 捕获，建议使用无边框窗口模式。
- 源窗口最小化后，画面可能冻结在最后一帧。
- 高权限窗口和部分 UWP / WinUI 窗口可能无法枚举。

### macOS

- v1.2.0 官方 DMG 仅提供 Apple Silicon arm64 版本；Intel x86_64 支持将在后续版本发布。
- 首次运行必须授予屏幕录制权限，并在授权后重启应用。
- 因未使用 Apple Developer ID，不同版本更新后可能需要重新授权。

## 从源码构建

### Windows

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)：

```bash
git clone https://github.com/TimekeeperXY/MirrorCast.git
cd MirrorCast
dotnet run --project src/MirrorCast
```

生成单文件版本：

```bash
dotnet publish src/MirrorCast -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

### macOS

需要 macOS 13+ 和 Xcode Command Line Tools：

```bash
git clone https://github.com/TimekeeperXY/MirrorCast.git
cd MirrorCast/mac
./build.sh --run
```

生成当前 Mac 架构的 DMG：

```bash
./package-dmg.sh
```

生成 Intel、Apple Silicon 或 Universal 2 DMG：

```bash
./package-dmg.sh --arch x86_64
./package-dmg.sh --arch arm64
./package-dmg.sh --arch universal
```

更多实现和构建信息请参阅 [macOS 开发文档](mac/README.md)。

## 技术概览

| 目录 | 内容 |
|---|---|
| `src/MirrorCast/` | Windows WPF 应用、DWM / User32 互操作和 MVVM 逻辑 |
| `mac/Sources/MirrorCast/` | macOS AppKit / SwiftUI 应用和 ScreenCaptureKit 捕获逻辑 |
| `mac/INSTALL.md` | macOS 安装与 Gatekeeper 排障 |
| `docs/plans/` | 功能设计与实施计划 |

## 参与贡献

欢迎通过 [Issues](https://github.com/TimekeeperXY/MirrorCast/issues) 反馈问题，也欢迎提交 Pull Request。反馈问题时请注明操作系统版本、显示器分辨率和复现步骤。

## License

MirrorCast 使用 [MIT License](LICENSE)。

<div align="center">

created by [@晓阳的百宝箱](https://github.com/TimekeeperXY)

</div>
