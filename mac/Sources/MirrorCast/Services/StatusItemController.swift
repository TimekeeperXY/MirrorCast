import AppKit

@MainActor
final class StatusItemController: NSObject {
    var onShowPanel: (() -> Void)?
    var onToggleMirroring: (() -> Void)?
    var onRefresh: (() -> Void)?
    var onShowOnboarding: (() -> Void)?
    var onQuit: (() -> Void)?

    private let statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
    private let toggleItem = NSMenuItem(title: "开始镜像", action: #selector(toggleMirroring), keyEquivalent: "")

    override init() {
        super.init()

        if let button = statusItem.button {
            button.image = NSImage(
                systemSymbolName: "rectangle.on.rectangle",
                accessibilityDescription: "MirrorCast")
            button.image?.isTemplate = true
        }

        let menu = NSMenu()
        menu.addItem(menuItem("显示控制面板", action: #selector(showPanel)))
        toggleItem.target = self
        menu.addItem(toggleItem)
        menu.addItem(menuItem("刷新窗口列表", action: #selector(refresh)))
        menu.addItem(.separator())
        menu.addItem(menuItem("重新显示使用教学", action: #selector(showOnboarding)))
        menu.addItem(.separator())
        menu.addItem(menuItem("退出 MirrorCast", action: #selector(quit), keyEquivalent: "q"))
        statusItem.menu = menu
    }

    func update(isMirroring: Bool, canStart: Bool) {
        toggleItem.title = isMirroring ? "停止镜像" : "开始镜像"
        toggleItem.isEnabled = isMirroring || canStart
        statusItem.button?.image = NSImage(
            systemSymbolName: isMirroring ? "rectangle.on.rectangle.angled" : "rectangle.on.rectangle",
            accessibilityDescription: isMirroring ? "MirrorCast 正在镜像" : "MirrorCast")
        statusItem.button?.image?.isTemplate = true
    }

    private func menuItem(_ title: String,
                          action: Selector,
                          keyEquivalent: String = "") -> NSMenuItem {
        let item = NSMenuItem(title: title, action: action, keyEquivalent: keyEquivalent)
        item.target = self
        return item
    }

    @objc private func showPanel() {
        onShowPanel?()
    }

    @objc private func toggleMirroring() {
        onToggleMirroring?()
    }

    @objc private func refresh() {
        onRefresh?()
    }

    @objc private func showOnboarding() {
        onShowOnboarding?()
    }

    @objc private func quit() {
        onQuit?()
    }
}
