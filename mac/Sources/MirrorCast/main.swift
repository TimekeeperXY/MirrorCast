import AppKit

// Plain AppKit entry point rather than the SwiftUI `App` protocol: the mirror window is a
// hand-built borderless NSWindow on a specific display, which is far easier to control
// outside SwiftUI's scene lifecycle. Top-level code in main.swift also sidesteps the
// `@main` conflict that SwiftPM executable targets run into.
let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.setActivationPolicy(.regular)
app.run()
