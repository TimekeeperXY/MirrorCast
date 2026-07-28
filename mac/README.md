# MirrorCast for macOS

Windows 版的 macOS 移植。**当前进度：M1（核心链路验证中，尚不可用于生产）。**

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
- Xcode Command Line Tools（`xcode-select --install`）
- 至少两台显示器，且处于**扩展**模式

## 构建与运行

```bash
cd mac
chmod +x build.sh
./build.sh --run
```

产物在 `mac/.build/bundle/MirrorCast.app`。

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

M2 起：三种缩放模式、源窗口尺寸变化跟随、副屏拔出处理。
M3 起：菜单栏常驻、全局快捷键、配置持久化、首次引导。

## 已知待办

- 源窗口改变大小后，捕获配置不会跟着更新（画面会被缩放，暂不影响可用性）。
  `SCStream.updateConfiguration` 需要 macOS 14+，兼容 13 需重启流，留到 M2。
- 尚未处理副屏被拔出的情况。
- 尚无应用图标。
