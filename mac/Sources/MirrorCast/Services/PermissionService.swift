import AppKit
import CoreGraphics

/// Screen Recording (TCC) permission handling.
///
/// Unlike Windows, macOS refuses to hand over any window content until the user has
/// explicitly granted Screen Recording access in System Settings. `SCShareableContent`
/// simply fails without it, so this gate has to be cleared before anything else works.
enum PermissionService {

    static func hasScreenRecordingAccess() -> Bool {
        CGPreflightScreenCaptureAccess()
    }

    /// Shows the system prompt. macOS only offers it once per app identity; afterwards
    /// the user has to toggle the switch in System Settings by hand.
    @discardableResult
    static func requestScreenRecordingAccess() -> Bool {
        CGRequestScreenCaptureAccess()
    }

    static func openScreenRecordingSettings() {
        let url = URL(string:
            "x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture")!
        NSWorkspace.shared.open(url)
    }
}
