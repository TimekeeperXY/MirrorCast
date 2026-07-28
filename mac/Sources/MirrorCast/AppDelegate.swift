import AppKit
import SwiftUI

final class AppDelegate: NSObject, NSApplicationDelegate {

    private var panelWindow: NSWindow?
    private let state = AppState()

    func applicationDidFinishLaunching(_ notification: Notification) {
        let hosting = NSHostingView(rootView: ControlPanelView(state: state))

        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 440, height: 560),
            styleMask: [.titled, .closable, .miniaturizable, .resizable],
            backing: .buffered,
            defer: false)

        window.title = "MirrorCast · 窗口镜像 — created by @晓阳的百宝箱"
        window.contentView = hosting
        window.center()
        window.makeKeyAndOrderFront(nil)
        window.isReleasedWhenClosed = false

        panelWindow = window
        NSApp.activate(ignoringOtherApps: true)
    }

    /// Re-check permission whenever the app comes back to the front — the user may have
    /// just flipped the switch in System Settings.
    func applicationDidBecomeActive(_ notification: Notification) {
        Task { @MainActor in
            state.refreshPermission()
            await state.refreshSources()
        }
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        // M1 keeps this simple: closing the panel quits. The tray-style "keep running in
        // the background" behaviour arrives with the menu bar item in M3.
        true
    }

    func applicationWillTerminate(_ notification: Notification) {
        Task { @MainActor in await state.stopMirroring() }
    }
}
