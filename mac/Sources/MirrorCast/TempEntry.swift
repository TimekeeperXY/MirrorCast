import AppKit

/// Application entry point.
///
/// Deliberately an explicit `@main` type with a `@MainActor` entry rather than top-level
/// code in `main.swift`: `AppDelegate` and `AppState` are main-actor isolated, and a
/// nonisolated entry point cannot construct them. Annotating the entry point is what
/// SwiftUI's own `App` protocol does, so the pattern is well supported.
///
/// Plain AppKit is used instead of the SwiftUI `App` protocol because the mirror is a
/// hand-built borderless NSWindow pinned to a specific display, which is much easier to
/// control outside SwiftUI's scene lifecycle.
@main
enum MirrorCastMain {

    @MainActor
    static func main() {
        let app = NSApplication.shared
        let delegate = AppDelegate()
        app.delegate = delegate
        app.setActivationPolicy(.regular)
        app.run()

        // Keeps the delegate alive for the whole run loop; without this the local
        // reference could be released while AppKit still holds only a weak `delegate`.
        withExtendedLifetime(delegate) {}
    }
}
