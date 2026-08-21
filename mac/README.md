# MirrorCast for macOS

MirrorCast 的 macOS 版本使用 ScreenCaptureKit 捕获单个窗口，通过 IOSurface 直接交给 CALayer 显示在目标屏幕上。

- 用户安装与权限处理：[macOS 安装指南](INSTALL.md)
- 项目总览与使用说明：[根 README](../README.md)
- 最新正式版 DMG：[MirrorCast v1.4.0（Apple Silicon / Intel）](https://github.com/TimekeeperXY/MirrorCast/releases/tag/v1.4.0)

## 系统要求

- macOS 13 Ventura 或更高版本
- Apple Silicon Mac 或 Intel Mac
- 至少两块显示器，并处于扩展模式

从源码构建还需要 Xcode Command Line Tools：

```bash
xcode-select --install
```

## macOS 版能力

- 单窗口捕获与副屏无边框全屏显示
- 适应、填满、拉伸三种缩放模式
- 按源窗口所在屏幕的 backing scale 配置捕获分辨率
- 源窗口改变大小时动态更新捕获配置
- 镜像期间单击窗口列表即可切换源窗口
- 鼠标指针显示开关
- GPU 路径的全屏放大、指针放大镜和指针聚光灯
- 主屏矢量标注工具栏，副屏同步显示画笔、荧光笔、图形和箭头
- 镜像期间使用 `F1`、`F2`、`F3`、`F4` 快速切换演示功能，按 `Esc` 逐层退出
- 源窗口关闭或目标显示器断开时自动停止
- 跨 Space 保持副屏镜像
- 副屏镜像窗口点击穿透
- 菜单栏常驻与自定义全局快捷键
- 显示器、缩放、指针、窗口和快捷键设置持久化
- 屏幕录制权限引导与五步使用教学
- 内置 ADB 与 scrcpy 的安卓 USB / 局域网投屏来源

部分 Mac 键盘默认将功能键用于亮度、音量等系统控制，此时需要按住 `Fn` 再按 `F1` 至 `F4`，或在系统设置中启用“将 F1、F2 等键用作标准功能键”。演示快捷模式可以在控制面板中关闭。

## 构建应用

```bash
cd mac
./build.sh
```

生成的应用位于：

```text
mac/.build/bundle/MirrorCast.app
```

构建并立即运行：

```bash
./build.sh --run
```

按目标架构构建：

```bash
./build.sh --arch arm64
./build.sh --arch x86_64
./build.sh --arch universal
```

`build.sh` 会进行 Release 编译、组装 `.app` 并使用 ad-hoc 签名。`universal` 会分别编译 arm64 和 x86_64，再用 `lipo` 合成为 Universal 2 应用。

## 打包 DMG

```bash
cd mac
./package-dmg.sh
```

默认生成当前 Mac 架构的 DMG。指定架构：

```bash
./package-dmg.sh --arch x86_64
./package-dmg.sh --arch arm64
./package-dmg.sh --arch universal
```

脚本会依次执行：

1. Release 构建
2. App 签名和 plist 校验
3. 目标架构检查
4. 创建包含 `MirrorCast.app` 和 Applications 快捷方式的压缩 DMG
5. DMG 完整性检查
6. 生成 SHA-256 文件

产物位于：

```text
mac/dist/MirrorCast-v<版本>-macOS-<架构>.dmg
mac/dist/MirrorCast-v<版本>-macOS-<架构>.dmg.sha256
```

Intel 架构名为 `x86_64`，Apple Silicon 为 `arm64`，双架构包为 `universal`。

每次修改 `mac/` 后，GitHub Actions 会在真实的 Intel macOS runner 上编译并打包 x86_64 DMG，避免只在 Apple Silicon 的交叉编译环境中验证。

## 实现结构

```text
mac/Sources/MirrorCast/
├── Capture/       # ScreenCaptureKit 捕获与帧输出
├── Mirror/        # 副屏窗口、CALayer 显示和缩放模式
├── Presentation/  # 放大、聚光灯和矢量标注
├── Services/      # 权限、偏好设置、菜单栏和全局快捷键
├── UI/            # 控制面板、权限引导和使用教学
├── AppDelegate.swift
├── AppState.swift
└── Main.swift
```

## 与 Windows 版的差异

| | Windows | macOS |
|---|---|---|
| 底层技术 | DWM Thumbnail | ScreenCaptureKit |
| 帧数据 | 系统合成器直接处理 | SCStream 输出 IOSurface |
| 鼠标指针 | 独立窗口合成 | ScreenCaptureKit 原生捕获 |
| 后台入口 | 系统托盘 | 菜单栏 |
| 权限 | 无额外权限 | 需要屏幕录制权限 |

macOS 没有与 DWM Thumbnail 对等的公开 API，因此资源占用会高于 Windows 版。实现中不读取 CPU 侧像素，每帧 IOSurface 直接交给 CALayer，尽量保持 GPU 路径。

## 分发限制

当前项目没有使用付费的 Apple Developer ID：

- App 使用 ad-hoc 签名，未经过 Apple 公证。
- 首次启动需要用户通过 Finder 右键“打开”或系统设置确认。
- 更新不同构建后，macOS 可能要求重新授予屏幕录制权限。
- v1.4.0 官方 Release 同时提供 Apple Silicon arm64 与 Intel x86_64 DMG。

不要建议用户全局关闭 Gatekeeper。完整、安全的处理方式见 [macOS 安装指南](INSTALL.md)。
