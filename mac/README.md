# MirrorCast for macOS

MirrorCast 的 macOS 版本使用 ScreenCaptureKit 捕获单个窗口，通过 IOSurface 直接交给 CALayer 显示在目标屏幕上。

- 用户安装与权限处理：[macOS 安装指南](INSTALL.md)
- 项目总览与使用说明：[根 README](../README.md)
- 最新 Apple Silicon DMG：[MirrorCast v1.2.0](https://github.com/TimekeeperXY/MirrorCast/releases/tag/v1.2.0)

## 系统要求

- macOS 13 Ventura 或更高版本
- Apple Silicon Mac
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
- 源窗口关闭或目标显示器断开时自动停止
- 跨 Space 保持副屏镜像
- 副屏镜像窗口点击穿透
- 菜单栏常驻与自定义全局快捷键
- 显示器、缩放、指针、窗口和快捷键设置持久化
- 屏幕录制权限引导与五步使用教学

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

`build.sh` 会进行 Release 编译、组装 `.app` 并使用 ad-hoc 签名。

## 打包 DMG

```bash
cd mac
./package-dmg.sh
```

脚本会依次执行：

1. Release 构建
2. App 签名和 plist 校验
3. arm64 架构检查
4. 创建包含 `MirrorCast.app` 和 Applications 快捷方式的压缩 DMG
5. DMG 完整性检查
6. 生成 SHA-256 文件

产物位于：

```text
mac/dist/MirrorCast-v<版本>-macOS-arm64.dmg
mac/dist/MirrorCast-v<版本>-macOS-arm64.dmg.sha256
```

## 实现结构

```text
mac/Sources/MirrorCast/
├── Capture/       # ScreenCaptureKit 捕获与帧输出
├── Mirror/        # 副屏窗口、CALayer 显示和缩放模式
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
- 当前官方 DMG 仅发布 arm64，不包含 Intel 版本。

不要建议用户全局关闭 Gatekeeper。完整、安全的处理方式见 [macOS 安装指南](INSTALL.md)。
