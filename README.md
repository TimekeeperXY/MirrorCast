<div align="center">

# MirrorCast · 窗口镜像

**把主屏上任意一个程序窗口，实时镜像到副屏全屏显示。**

零 CPU / 零 GPU 开销 · 零延迟 · 常驻托盘 · 零学习成本

[![Platform](https://img.shields.io/badge/platform-Windows%2010%201809%2B%20%7C%2011-0078D4)](https://www.microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

</div>

---

## 这个工具解决什么问题

双屏讲课、直播、开会的时候，你大概率遇到过这些尴尬：

| 痛点 | 现状 |
|------|------|
| **想只投 PPT，不想暴露主屏** | Windows 自带的「复制显示器」是**整屏复制**，主屏上的备课笔记、微信消息、AI 对话框全都投出去了 |
| **把窗口拖到副屏就失控了** | 窗口一旦拖过去，主屏上就看不到了，翻页、切页面都得扭头看副屏，操作起来非常别扭 |
| **用 OBS 的「全屏投影源」** | 能做到，但为了投个窗口要长期开着一整个 OBS，太重了 |
| **会议里只想共享一个窗口** | 共享整个桌面容易误露隐私，共享单窗口又常常受软件限制 |

**MirrorCast 的做法**：主屏窗口留在原地不动，你照常操作；副屏独立全屏显示这个窗口的实时画面。两边互不干扰。

```
┌─────────── 主屏（你操作）───────────┐   ┌──── 副屏（观众看）────┐
│  PPT 编辑器 · 备课笔记 · 微信 · AI  │ → │      PPT 全屏画面      │
└─────────────────────────────────────┘   └────────────────────────┘
```

---

## 特色

**⚡ 真正的零开销**
基于 Windows **DWM Thumbnail API**，画面由系统合成器直接搬运，不经过截图、编码、渲染管线。实测镜像运行时 CPU 占用 **0.011%**，帧延迟 ≤ 1 帧，肉眼无感。

**🔄 不中断切换镜像窗口**
讲课中途要换个窗口投影？点「更换镜像窗口」，副屏画面**不会黑屏、不会闪烁**，直接原地切到新窗口。底层只替换缩略图源，镜像窗口本身全程不重建。

**📐 三种缩放模式，永不变形**
- **等比适应**（默认）— 保持宽高比，居中显示，边缘补黑边。PPT、视频首选
- **拉伸填满** — 强制铺满副屏
- **原始尺寸** — 1:1 像素级显示；源窗口大于副屏时等比缩小，绝不压扁拉长

拖动改变源窗口大小时，副屏画面**实时跟随**（33ms 检测），不会出现变形过渡。

**🖱 副屏合成鼠标指针**
DWM 缩略图本身**不含鼠标指针**（系统限制）。MirrorCast 实时读取真实光标位置与样式，按比例叠加到副屏画面上——观众能看到你的指针在动，箭头 / 手型 / 文本光标都会还原。可在设置里关闭。

**🎨 现代化界面，跟随系统主题**
Windows 11 原生 **Mica 云母材质**背景（Win10 自动降级为亚克力模糊），浅色 / 深色主题**实时跟随系统**切换，无需重启。

**🛡 贴心的异常处理**
- 源窗口最小化 → 副屏提示「源窗口已最小化」，恢复后自动继续
- 源窗口被关闭 → 自动停止镜像并提示
- 副屏被拔掉 → 自动停止镜像，回到主面板

**⌨️ 效率细节**
全局快捷键一键开关，**可自定义成你习惯的组合键** · 首次打开有分步操作引导 · 窗口列表实时搜索 · 常驻系统托盘 · 配置自动持久化，下次启动智能恢复上次选择 · 支持开机自启动 · **无需管理员权限**

---

## 系统要求

- **Windows 10 1809+ 或 Windows 11**（Mica 材质需 Windows 11，Win10 自动降级为亚克力）
- 至少 **2 个显示器**，且工作在**扩展模式**（不是复制模式）
- 从源码构建需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

---

## 安装

### 方式一：下载现成的（推荐）

前往 [Releases](../../releases) 下载最新的 `MirrorCast.exe`，**双击即可运行**。

单文件便携版，已内置 .NET 运行时，无需安装任何环境，不写注册表（除非你手动开启「开机自启动」），删掉文件即卸载。

> Windows SmartScreen 可能会提示「已保护你的电脑」——这是未购买代码签名证书的开源软件的常见现象。点「更多信息」→「仍要运行」即可。

### 方式二：从源码构建

```bash
git clone https://github.com/TimekeeperXY/MirrorCast.git
cd MirrorCast
dotnet publish src/MirrorCast -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

产物在 `src/MirrorCast/bin/Release/net8.0-windows/win-x64/publish/MirrorCast.exe`。

开发调试直接跑：

```bash
dotnet run --project src/MirrorCast
```

---

## 使用

### 基本流程

1. **确认双屏工作在扩展模式**（`Win + P` → 选择「扩展」）
2. **打开 MirrorCast**
3. **从列表选择要镜像的窗口**（支持搜索，列表每 2 秒自动刷新）
4. **选择目标显示器**（默认自动选中副屏）
5. **点击「开始镜像」**，副屏立刻全屏显示该窗口
6. 主屏继续自由操作，讲课结束点「停止镜像」或按 `Ctrl+Alt+M`

### 快捷键

| 快捷键 | 作用 |
|--------|------|
| `Ctrl + Alt + M` | 开始 / 停止镜像（全局，任意界面都能用）**可自定义** |
| `Ctrl + Alt + Shift + M` | 停止镜像并调出主控制面板 |
| `Esc` | 在副屏镜像窗口上按下，退出镜像 |

想换成别的组合键，点设置区里的「快捷键」按钮，然后直接按下新组合即可（需包含 Ctrl / Alt / Shift 之一，按 `Esc` 取消）。如果该组合已被其他程序占用，会提示并保留原设置。

### 讲课实战建议

- 勾选 **「只显示窗口内容（不含标题栏）」**，副屏画面更干净，观众看不到标题栏和边框
- PPT 建议用 **「等比适应」** 模式，任何比例都不会变形
- 关闭主面板不会退出程序，会最小化到托盘；托盘右键可快速开关镜像
- 配置会自动保存到 `%APPDATA%\MirrorCast\config.json`，下次启动自动恢复上次的窗口和显示器选择

---

## 已知限制

这些是 DWM Thumbnail 方案的**固有限制**，不是 bug，写在这里避免你踩坑：

- ❌ **副屏画面上的鼠标 / 键盘点击不会传回源窗口** — 副屏是「只读」的画面镜像。如需副屏可交互，得换 Windows Graphics Capture 方案
- ❌ **不支持录制画面、加特效** — DWM 缩略图不产生可访问的帧数据
- ❌ **全屏独占模式的 D3D 游戏可能无法镜像** — DWM 无法合成这类窗口（改用无边框窗口模式即可）
- ❌ **源窗口最小化时不会继续渲染** — 只显示黑屏或最后一帧，这是 DWM 特性
- ⚠️ **部分 UWP / WinUI 应用可能镜像失败** — 建议实测
- ⚠️ **以管理员身份运行的窗口（如任务管理器）可能枚举不到** — Windows UIPI 安全限制，属预期行为

---

## 技术原理

核心只有一句话：**用 `DwmRegisterThumbnail` 让系统合成器直接把源窗口的画面画到副屏窗口上。**

```
源窗口 HWND ──┐
              ├─→ DwmRegisterThumbnail ─→ DWM 合成器直接搬运画面 ─→ 副屏全屏窗口
副屏窗口 HWND ─┘
```

因为画面从头到尾没离开过 GPU、没经过任何截图/编码步骤，所以才能做到近乎零开销和零延迟。

关键实现点：
- **缩略图绘制在目标窗口内容之上**。这是最容易踩的坑：DWM 是在目标窗口自身的渲染结果**上层**合成缩略图的，所以你在镜像窗口里画的任何东西（光标、水印、角标）都会被镜像画面完全盖住，永远看不见。若需要叠加内容，必须放到**另一个独立的顶层窗口**里（本项目的 `CursorOverlayWindow` 就是为此存在）
- **`DwmUpdateThumbnailProperties`** 设置目标矩形，三种缩放模式就是在算这个矩形。**注意 DWM 会把源画面拉伸填满目标矩形而不是裁剪**，所以矩形必须严格保持源画面宽高比，否则画面变形
- **源窗口最小化时缩略图会继续显示冻结的最后一帧**，需要主动把 `fVisible` 设为 false 才能露出下层的提示文字
- **Per-Monitor DPI Aware V2**（在 `app.manifest` 声明），保证混合 DPI 多屏场景下坐标计算正确
- **`DwmSetWindowAttribute` + `DwmExtendFrameIntoClientArea`** 实现 Mica 背景；WPF 还需额外把 `HwndTarget.BackgroundColor` 设为透明，否则底层会填充不透明黑色

### 项目结构

```
src/MirrorCast/
├── Interop/          # DWM / User32 P/Invoke 声明
├── Services/         # 窗口枚举、显示器枚举、缩略图控制（核心）、配置、主题、快捷键
├── Models/           # 数据模型
├── ViewModels/       # MVVM 视图模型
├── Themes/           # 浅色 / 深色配色 + 控件样式
├── MainWindow.xaml         # 主控制面板
├── MirrorWindow.xaml       # 副屏全屏镜像窗口
└── CursorOverlayWindow.xaml # 指针叠加层（必须独立于镜像窗口，见上文）
```

---

## 参考资料

- [DWM Thumbnail Overview](https://learn.microsoft.com/en-us/windows/win32/dwm/thumbnail-ovw)
- [DwmRegisterThumbnail](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmregisterthumbnail)
- [DWM_THUMBNAIL_PROPERTIES](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ns-dwmapi-dwm_thumbnail_properties)

---

## 参与贡献

欢迎提 [Issue](../../issues) 反馈问题或建议新功能，也欢迎直接提 Pull Request。

---

## License

[MIT](LICENSE) — 随便用，商用也行。

---

<div align="center">

**created by [@晓阳的百宝箱](https://github.com/TimekeeperXY)**

如果这个工具帮到了你，点个 ⭐ 是最好的支持

</div>
