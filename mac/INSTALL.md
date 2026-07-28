# MirrorCast macOS 安装指南

MirrorCast 当前没有使用付费的 Apple Developer ID 签名和公证。第一次打开时，macOS 可能提示“无法验证开发者”“Apple 无法检查其是否包含恶意软件”或“应用已损坏”。这是未公证应用的系统提示，不代表 MirrorCast 实际损坏。

请只从本项目的官方 GitHub Releases 页面下载 MirrorCast，不要运行第三方重新打包的版本。

当前 DMG 发布包面向 Apple Silicon Mac（M1、M2、M3、M4 及后续芯片），要求 macOS 13 或更高版本。

## 安装

1. 从 GitHub Releases 下载最新版 `MirrorCast-*-macOS-arm64.dmg`。
2. 双击 DMG 打开安装窗口。
3. 将 `MirrorCast.app` 拖到旁边的“Applications”快捷方式。
4. 在 Finder 侧边栏推出 MirrorCast 磁盘映像。
5. 不要直接双击。打开 Finder 的“应用程序”，按住 `Control` 单击 `MirrorCast`，选择“打开”。
6. 在随后出现的确认窗口中再次点击“打开”。

完成一次后，以后可以像普通应用一样启动 MirrorCast。

## 如果没有“打开”按钮

先直接尝试打开一次 MirrorCast，让 macOS 记录这次拦截，然后：

1. 打开“系统设置”。
2. 进入“隐私与安全性”。
3. 向下滚动到“安全性”区域。
4. 找到关于 MirrorCast 被阻止的提示，点击“仍要打开”。
5. 输入 Mac 登录密码或使用 Touch ID 确认。

## 如果提示“应用已损坏”

确认应用是从本项目官方 GitHub Releases 下载后，打开“终端”，执行：

```bash
xattr -dr com.apple.quarantine "/Applications/MirrorCast.app"
```

然后回到 Finder，按住 `Control` 单击 MirrorCast，再选择“打开”。

这条命令只移除 `MirrorCast.app` 的下载隔离标记，不会关闭 macOS 的全局安全检查。如果你把应用放在其他位置，请将命令中的路径替换为实际路径。

不要执行下面这类命令：

```bash
sudo spctl --master-disable
```

MirrorCast 不需要全局关闭 Gatekeeper。全局关闭会降低其他下载软件的安全保护。

## 授予屏幕录制权限

MirrorCast 必须获得屏幕录制权限才能读取窗口画面：

1. 打开 MirrorCast，点击“前往授权”。
2. 在“系统设置 → 隐私与安全性 → 屏幕录制”中启用 MirrorCast。
3. 从菜单栏退出 MirrorCast，确保程序完全结束。
4. 重新打开 MirrorCast。
5. 授权成功后，软件会自动显示五步使用教学。

MirrorCast 只在内存中处理画面，不会录制或保存捕获内容。

## 更新版本

1. 从 MirrorCast 菜单栏图标选择“退出 MirrorCast”。
2. 用新版本覆盖“应用程序”中的旧版 `MirrorCast.app`。
3. 按照上面的“右键打开”步骤启动新版本。
4. 如果窗口列表为空或仍提示没有权限，请在“屏幕录制”设置中删除旧的 MirrorCast 条目，再重新添加和授权新版本。

由于当前使用 ad-hoc 签名，不同构建可能被 macOS 识别为不同应用，因此更新后重新授权属于预期情况。

## 卸载

1. 从菜单栏退出 MirrorCast。
2. 将“应用程序”中的 `MirrorCast.app` 移到废纸篓。
3. 如需清除偏好设置，在终端执行：

```bash
defaults delete com.xiaoyang.mirrorcast
```

第三步是可选的，它只删除 MirrorCast 保存的显示器、缩放模式和快捷键设置。
