import AppKit
import Combine
import ScreenCaptureKit

/// One selectable source window, flattened out of `SCWindow` so SwiftUI can diff it.
struct WindowItem: Identifiable, Hashable {
    let id: CGWindowID
    let title: String
    let appName: String
}

/// One selectable target display.
struct ScreenItem: Identifiable, Hashable {
    let id: CGDirectDisplayID
    let name: String
    let resolution: String
    let isPrimary: Bool
}

@MainActor
final class AppState: ObservableObject {

    @Published private(set) var windows: [WindowItem] = []
    @Published private(set) var screens: [ScreenItem] = []
    @Published var selectedWindowID: CGWindowID?
    @Published var selectedScreenID: CGDirectDisplayID?
    @Published private(set) var isMirroring = false
    @Published var hasPermission = false
    @Published var status = ""
    @Published var showsCursor = true

    /// SwiftUI only ever sees `WindowItem`; the live SCWindow objects stay here because
    /// the capture filter needs the real thing.
    private var scWindows: [CGWindowID: SCWindow] = [:]

    private let capture = CaptureEngine()
    private let mirrorWindow = MirrorWindowController()

    var canStart: Bool {
        hasPermission && selectedWindowID != nil && selectedScreenID != nil && screens.count > 1
    }

    init() {
        capture.onSurface = { [weak self] surface in
            self?.mirrorWindow.update(surface: surface)
        }
        capture.onStopped = { [weak self] error in
            guard let self else { return }
            // The stream ends on its own when the source window disappears.
            self.finishMirroring()
            self.status = error == nil
                ? "镜像已停止"
                : "镜像已停止：源窗口可能已关闭（\(error!.localizedDescription)）"
        }
    }

    // MARK: - Permission

    func refreshPermission() {
        hasPermission = PermissionService.hasScreenRecordingAccess()
    }

    func requestPermission() {
        PermissionService.requestScreenRecordingAccess()
        refreshPermission()
        if !hasPermission {
            status = "请在「系统设置 → 隐私与安全性 → 屏幕录制」中勾选 MirrorCast，然后重新启动本程序"
            PermissionService.openScreenRecordingSettings()
        }
    }

    // MARK: - Enumeration

    func refreshSources() async {
        refreshScreens()

        guard hasPermission else {
            windows = []
            return
        }

        do {
            let content = try await SCShareableContent.excludingDesktopWindows(
                true, onScreenWindowsOnly: true)

            let selfPID = ProcessInfo.processInfo.processIdentifier
            var items: [WindowItem] = []
            var map: [CGWindowID: SCWindow] = [:]

            for window in content.windows {
                guard let title = window.title, !title.isEmpty,
                      let app = window.owningApplication,
                      app.processID != selfPID,
                      // Drop tool palettes, shadows and other chrome-sized surfaces.
                      window.frame.width >= 120, window.frame.height >= 120
                else { continue }

                items.append(WindowItem(id: window.windowID,
                                        title: title,
                                        appName: app.applicationName))
                map[window.windowID] = window
            }

            items.sort { ($0.appName, $0.title) < ($1.appName, $1.title) }

            windows = items
            scWindows = map

            if let selected = selectedWindowID, map[selected] == nil {
                selectedWindowID = items.first?.id
            } else if selectedWindowID == nil {
                selectedWindowID = items.first?.id
            }
        } catch {
            windows = []
            status = "获取窗口列表失败：\(error.localizedDescription)"
        }
    }

    private func refreshScreens() {
        let mainID = NSScreen.main.flatMap(Self.displayID(of:))

        screens = NSScreen.screens.enumerated().compactMap { index, screen in
            guard let id = Self.displayID(of: screen) else { return nil }
            let isPrimary = (id == mainID)
            // localizedName gives the real product name ("DELL U2720Q"), which is far more
            // useful than a raw display ID when picking a target.
            let label = screen.localizedName.isEmpty ? "显示器 \(index + 1)" : screen.localizedName
            return ScreenItem(
                id: id,
                name: isPrimary ? "\(label)（主）" : label,
                resolution: "\(Int(screen.frame.width))×\(Int(screen.frame.height))",
                isPrimary: isPrimary)
        }

        let selectionStillValid = selectedScreenID.map { current in
            screens.contains { $0.id == current }
        } ?? false

        if !selectionStillValid {
            // Default to the first non-primary display — that is the projector in practice.
            selectedScreenID = screens.first { !$0.isPrimary }?.id ?? screens.first?.id
        }
    }

    private static func displayID(of screen: NSScreen) -> CGDirectDisplayID? {
        let key = NSDeviceDescriptionKey("NSScreenNumber")
        return (screen.deviceDescription[key] as? NSNumber)?.uint32Value
    }

    // MARK: - Mirroring

    func startMirroring() async {
        guard let windowID = selectedWindowID,
              let window = scWindows[windowID],
              let screenID = selectedScreenID,
              let screen = NSScreen.screens.first(where: { Self.displayID(of: $0) == screenID })
        else {
            status = "请先选择要镜像的窗口和目标显示器"
            return
        }

        mirrorWindow.show(on: screen)

        do {
            try await capture.start(window: window,
                                    showsCursor: showsCursor,
                                    scale: screen.backingScaleFactor)
            isMirroring = true
            let title = windows.first(where: { $0.id == windowID })?.title ?? "所选窗口"
            status = "正在镜像：\(title)"
        } catch {
            mirrorWindow.close()
            status = "启动镜像失败：\(error.localizedDescription)"
        }
    }

    func stopMirroring() async {
        await capture.stop()
        finishMirroring()
        status = "镜像已停止"
    }

    private func finishMirroring() {
        mirrorWindow.close()
        isMirroring = false
    }
}
