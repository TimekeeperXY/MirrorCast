import AppKit

/// Owns the borderless full-screen window shown on the target display.
@MainActor
final class MirrorWindowController {

    private var window: NSWindow?
    private var mirrorView: MirrorLayerView?

    var isShowing: Bool { window != nil }

    func show(on screen: NSScreen) {
        close()

        let window = NSWindow(contentRect: screen.frame,
                              styleMask: .borderless,
                              backing: .buffered,
                              defer: false,
                              screen: screen)

        window.isOpaque = true
        window.backgroundColor = .black
        window.hasShadow = false
        // Above ordinary windows *and* above the menu bar / Dock on that display, so the
        // mirror really is full screen.
        window.level = .screenSaver
        // Stay put no matter which Space the presenter switches to on the main display,
        // and never take part in Mission Control or Cmd-Tab.
        window.collectionBehavior = [.canJoinAllSpaces, .stationary,
                                     .fullScreenAuxiliary, .ignoresCycle]
        // The mirror is a read-only picture; clicks must fall through to whatever is
        // actually on that screen.
        window.ignoresMouseEvents = true

        let view = MirrorLayerView(frame: NSRect(origin: .zero, size: screen.frame.size))
        view.autoresizingMask = [.width, .height]
        window.contentView = view

        window.setFrame(screen.frame, display: true)
        // orderFrontRegardless avoids stealing focus from whatever the presenter is doing.
        window.orderFrontRegardless()

        self.window = window
        self.mirrorView = view
    }

    func update(surface: IOSurfaceRef) {
        mirrorView?.update(surface: surface)
    }

    func setScaleMode(_ gravity: CALayerContentsGravity) {
        mirrorView?.setScaleMode(gravity)
    }

    func close() {
        window?.orderOut(nil)
        window?.contentView = nil
        window = nil
        mirrorView = nil
    }
}
