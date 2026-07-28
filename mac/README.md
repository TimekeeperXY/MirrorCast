# MirrorCast for macOS

Windows 版的 macOS 移植。**当前进度：M4（封装与分发）。**

## 与 Windows 版的根本差异

Windows 版基于 DWM Thumbnail —— 告诉系统合成器「把窗口 X 画到这里」，画面全程不出 GPU，所以 CPU 占用只有 0.011%。

**macOS 没有对等的公开 API。** 这里改用 **ScreenCaptureKit**：它是真正的捕获流，会产出帧。开销靠「永不用 CPU 碰这些像素」压到最低——每帧的 `IOSurface` 直接赋给 `CALayer.contents`，数据始终留在 GPU 上。

代价是 CPU 占用达不到 Windows 版那个量级（预期低个位数 %），但换来两个好处：

| | Windows | macOS |
|---|---|---|
| 鼠标指针 | 需自行合成叠加（踩过两轮坑） | `showsCursor = true` 一行 |
| 缩放模式 | 手算目标矩形（踩过宽高比坑） | `contentsGravity` 枚举 |

## 环境要求

- macOS 13 (Ventura) 或更高
- 至少两台显示器，且处于**扩展**模式

## 下载与安装

由于当前版本没有使用付费的 Apple Developer ID 签名和公证，首次打开时会出现 macOS 安全提示。请按照 [macOS 安装指南](INSTALL.md) 完成安装、安全确认和屏幕录制授权。

## 构建与运行

从源码构建需要 Xcode Command Line Tools（`xcode-select --install`）。

```bash
cd mac
chmod +x build.sh
./build.sh --run
```

产物在 `mac/.build/bundle/MirrorCast.app`。

## 打包 DMG

当前发布包面向 Apple Silicon Mac（arm64）：

```bash
cd mac
chmod +x package-dmg.sh
./package-dmg.sh
```

产物在 `mac/dist/`，同时生成对应的 SHA-256 校验文件。

## 首次运行必须授权

macOS 不授权就完全读不到窗口画面。首次打开会看到授权提示：

1. 点「去授权」，系统会弹窗或直接跳转到设置
2. 在「系统设置 → 隐私与安全性 → 屏幕录制」中勾选 **MirrorCast**
3. **完全退出 MirrorCast 后重新打开**（这一步经常被漏掉，不重启的话即使勾了也读不到窗口）

### 关于「每次重新构建都要重新授权」

因为没有付费的 Apple Developer ID，只能用 ad-hoc 签名，而它的身份哈希**每次编译都会变**——macOS 会把新构建当成另一个 App。

如果出现「设置里明明勾了却还是没权限」：把列表里旧的 MirrorCast 条目用 `−` 删掉，再重新添加新构建的那个。

## M1 范围

- [x] 屏幕录制权限检测与申请
- [x] 窗口枚举（`SCShareableContent`）
- [x] 显示器枚举（`NSScreen`）
- [x] 单窗口捕获（`SCStream` + 窗口过滤器）
- [x] 副屏无边框全屏显示（`IOSurface` → `CALayer`）
- [x] 源窗口关闭时自动停止

## M2 范围

- [x] 三种缩放模式（适应、填满、拉伸）
- [x] 捕获分辨率按源窗口所在屏的 backing scale 配置
- [x] 源窗口尺寸变化跟随
- [x] 镜像期间直接切换源窗口
- [x] 副屏拔出时自动停止

## M3 范围

- [x] 菜单栏常驻与控制面板重新打开
- [x] 可自定义全局快捷键（默认 `Control + Option + M`）
- [x] 指针、缩放模式、目标屏及上次源窗口配置持久化
- [x] 授权前权限引导
- [x] 授权后五步聚光灯教学（含 Mac 独有的单击窗口热切换）
- [x] 菜单栏重新显示使用教学

## 已知待办

- 尚无应用图标。
