import AppKit
import Combine
import SwiftUI

/// Main-actor isolated because every AppKit delegate callback arrives on the main thread
/// anyway, and both `AppState` and the mirror window controller are main-actor isolated.
@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {

    private var panelWindow: NSWindow?
    private let state = AppState()
    private let statusItem = StatusItemController()
    private let globalHotKey = GlobalHotKey()
    private var cancellables: Set<AnyCancellable> = []

    func applicationDidFinishLaunching(_ notification: Notification) {
        let hosting = NSHostingView(rootView: ControlPanelView(state: state))

        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 440, height: 680),
            styleMask: [.titled, .closable, .miniaturizable, .resizable],
            backing: .buffered,
            defer: false)

        window.title = "MirrorCast · 窗口镜像 — created by @晓阳的百宝箱"
        window.contentView = hosting
        window.center()
        window.isReleasedWhenClosed = false

        panelWindow = window
        configureStatusItem()
        configureGlobalHotKey()
        observeState()
        showPanel()
    }

    /// Re-check permission whenever the app comes back to the front — the user may have
    /// just flipped the switch in System Settings.
    func applicationDidBecomeActive(_ notification: Notification) {
        Task {
            state.refreshPermission()
            await state.refreshSources()
        }
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        false
    }

    func applicationWillTerminate(_ notification: Notification) {
        // Termination gives us no chance to await, so tear the mirror window down
        // synchronously. The capture stream dies with the process.
        globalHotKey.unregister()
        state.shutdown()
    }

    private func configureStatusItem() {
        statusItem.onShowPanel = { [weak self] in
            self?.showPanel()
        }
        statusItem.onToggleMirroring = { [weak self] in
            self?.toggleMirroring()
        }
        statusItem.onRefresh = { [weak self] in
            guard let self else { return }
            Task { await self.state.refreshSources() }
        }
        statusItem.onShowOnboarding = { [weak self] in
            self?.state.showOnboarding()
            self?.showPanel()
        }
        statusItem.onQuit = {
            NSApp.terminate(nil)
        }
    }

    private func configureGlobalHotKey() {
        let action: () -> Void = { [weak self] in
            guard let self else { return }
            self.toggleMirroring()
        }
        var registered = globalHotKey.register(state.hotKey, action: action)
        if !registered && state.hotKey != .defaultValue {
            registered = globalHotKey.register(.defaultValue, action: action)
            if registered {
                state.acceptDefaultHotKeyFallback()
            }
        }
        state.onHotKeyChangeRequested = { [weak self] combination in
            self?.globalHotKey.replace(with: combination) == true
        }
        if !registered {
            state.status = "全局快捷键 \(state.hotKey.displayName) 注册失败，可能与其他应用冲突"
        }
    }

    private func observeState() {
        state.objectWillChange
            .receive(on: RunLoop.main)
            .sink { [weak self] _ in
                DispatchQueue.main.async {
                    self?.updateStatusItem()
                }
            }
            .store(in: &cancellables)
        updateStatusItem()
    }

    private func updateStatusItem() {
        statusItem.update(isMirroring: state.isMirroring, canStart: state.canStart)
    }

    private func toggleMirroring() {
        if !state.isMirroring && !state.canStart {
            state.status = "请先完成权限授权，并选择源窗口和目标显示器"
            showPanel()
            return
        }

        Task {
            await state.toggleMirroring()
        }
    }

    private func showPanel() {
        guard let panelWindow else { return }
        panelWindow.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }
}
