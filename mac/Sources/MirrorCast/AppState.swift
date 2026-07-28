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
    @Published var selectedScreenID: CGDirectDisplayID? {
        didSet {
            if let selectedScreenID {
                preferences.targetDisplayID = selectedScreenID
            }
        }
    }
    @Published private(set) var isMirroring = false
    @Published var hasPermission = false
    @Published var status = ""
    @Published var showsCursor = true {
        didSet { preferences.showsCursor = showsCursor }
    }
    @Published var scaleMode: MirrorScaleMode = .fit {
        didSet {
            preferences.scaleMode = scaleMode
            mirrorWindow.setScaleMode(scaleMode.contentsGravity)
        }
    }
    @Published var showsPermissionOnboarding = false
    @Published var walkthroughStep: WalkthroughStep?
    @Published private(set) var hotKey = HotKeyCombination.defaultValue

    var onHotKeyChangeRequested: ((HotKeyCombination) -> Bool)?

    /// SwiftUI only ever sees `WindowItem`; the live SCWindow objects stay here because
    /// the capture filter needs the real thing.
    private var scWindows: [CGWindowID: SCWindow] = [:]

    private let capture = CaptureEngine()
    private let mirrorWindow = MirrorWindowController()
    private let preferences = PreferencesStore()
    private var mirrorMonitorTask: Task<Void, Never>?
    private var activeWindowID: CGWindowID?
    private var isSwitchingSource = false

    var canStart: Bool {
        hasPermission && selectedWindowID != nil && selectedScreenID != nil && screens.count > 1
    }

    init() {
        showsCursor = preferences.showsCursor
        scaleMode = preferences.scaleMode
        hotKey = preferences.hotKey
        hasPermission = PermissionService.hasScreenRecordingAccess()
        showsPermissionOnboarding = !hasPermission
        walkthroughStep = hasPermission && !preferences.completedWalkthrough ? .sourceWindow : nil

        capture.onSurface = { [weak self] surface in
            self?.mirrorWindow.update(surface: surface)
        }
        capture.onStopped = { [weak self] error in
            guard let self, self.isMirroring else { return }
            // The stream ends on its own when the source window disappears.
            self.finishMirroring()
            if let error {
                self.status = "镜像已停止：源窗口可能已关闭（\(error.localizedDescription)）"
            } else {
                self.status = "镜像已停止"
            }
        }
    }

    // MARK: - Permission

    func refreshPermission() {
        hasPermission = PermissionService.hasScreenRecordingAccess()
        if hasPermission {
            showsPermissionOnboarding = false
            if !preferences.completedWalkthrough && walkthroughStep == nil {
                walkthroughStep = .sourceWindow
            }
        } else {
            showsPermissionOnboarding = true
            walkthroughStep = nil
        }
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
                      !app.applicationName.isEmpty,
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

            if let selected = selectedWindowID, map[selected] != nil {
                return
            }

            selectedWindowID = preferredWindow(in: items)?.id ?? items.first?.id
        } catch {
            windows = []
            status = "获取窗口列表失败：\(error.localizedDescription)"
        }
    }

    private func refreshScreens() {
        let mainID = CGMainDisplayID()

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
            let savedTarget = preferences.targetDisplayID.flatMap { savedID in
                screens.first { $0.id == savedID }?.id
            }
            selectedScreenID = savedTarget
                ?? screens.first { !$0.isPrimary }?.id
                ?? screens.first?.id
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
        mirrorWindow.setScaleMode(scaleMode.contentsGravity)

        do {
            let sourceScale = backingScaleFactor(for: window)
            try await capture.start(window: window,
                                    showsCursor: showsCursor,
                                    scale: sourceScale)
            isMirroring = true
            activeWindowID = windowID
            rememberSource(windowID)
            startMirrorMonitor(windowID: windowID, targetScreenID: screenID)
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

    func switchToSelectedWindow() async {
        guard isMirroring, !isSwitchingSource else { return }

        isSwitchingSource = true
        defer { isSwitchingSource = false }

        // Selection can change again while ScreenCaptureKit is restarting. Keep going until
        // the active stream catches up with the user's latest click.
        while isMirroring,
              let windowID = selectedWindowID,
              windowID != activeWindowID {
            guard let window = scWindows[windowID],
                  let screenID = selectedScreenID
            else {
                status = "切换失败：所选窗口已不可用"
                return
            }

            mirrorMonitorTask?.cancel()
            mirrorMonitorTask = nil

            let title = windows.first(where: { $0.id == windowID })?.title ?? "所选窗口"
            status = "正在切换到：\(title)"

            do {
                try await capture.start(
                    window: window,
                    showsCursor: showsCursor,
                    scale: backingScaleFactor(for: window))
                guard isMirroring else { return }
                activeWindowID = windowID
                rememberSource(windowID)
                startMirrorMonitor(windowID: windowID, targetScreenID: screenID)
                status = "正在镜像：\(title)"
            } catch {
                finishMirroring()
                status = "切换镜像失败：\(error.localizedDescription)"
                return
            }
        }
    }

    private func finishMirroring() {
        mirrorMonitorTask?.cancel()
        mirrorMonitorTask = nil
        activeWindowID = nil
        mirrorWindow.close()
        isMirroring = false
    }

    func toggleMirroring() async {
        if isMirroring {
            await stopMirroring()
        } else {
            await startMirroring()
        }
    }

    func showOnboarding() {
        if hasPermission {
            showsPermissionOnboarding = false
            walkthroughStep = .sourceWindow
        } else {
            walkthroughStep = nil
            showsPermissionOnboarding = true
        }
    }

    func advanceWalkthrough() {
        guard let walkthroughStep else { return }
        if let next = walkthroughStep.next {
            self.walkthroughStep = next
        } else {
            finishWalkthrough()
        }
    }

    func finishWalkthrough() {
        preferences.completedWalkthrough = true
        walkthroughStep = nil
    }

    func updateHotKey(_ combination: HotKeyCombination) {
        guard combination != hotKey else { return }
        guard onHotKeyChangeRequested?(combination) == true else {
            status = "快捷键 \(combination.displayName) 注册失败，可能与其他应用冲突"
            return
        }

        hotKey = combination
        preferences.hotKey = combination
        status = "快捷键已更新为 \(combination.displayName)"
    }

    func restoreDefaultHotKey() {
        updateHotKey(.defaultValue)
    }

    func acceptDefaultHotKeyFallback() {
        hotKey = .defaultValue
        preferences.hotKey = .defaultValue
        status = "上次使用的快捷键已被占用，已恢复为 \(hotKey.displayName)"
    }

    private func startMirrorMonitor(windowID: CGWindowID,
                                    targetScreenID: CGDirectDisplayID) {
        mirrorMonitorTask?.cancel()
        mirrorMonitorTask = Task { [weak self] in
            while !Task.isCancelled {
                try? await Task.sleep(nanoseconds: 500_000_000)
                guard let self, self.isMirroring, !Task.isCancelled else { return }

                guard NSScreen.screens.contains(where: {
                    Self.displayID(of: $0) == targetScreenID
                }) else {
                    await self.stopMirroring(reason: "镜像已停止：目标显示器可能已断开")
                    return
                }

                do {
                    let content = try await SCShareableContent.excludingDesktopWindows(
                        true, onScreenWindowsOnly: false)
                    guard !Task.isCancelled else { return }
                    guard let window = content.windows.first(where: {
                        $0.windowID == windowID
                    }) else {
                        await self.stopMirroring(reason: "镜像已停止：源窗口可能已关闭")
                        return
                    }

                    self.scWindows[windowID] = window
                    try await self.capture.updateConfiguration(
                        window: window,
                        showsCursor: self.showsCursor,
                        scale: self.backingScaleFactor(for: window))
                } catch {
                    guard !Task.isCancelled else { return }
                    self.status = "更新镜像画面失败：\(error.localizedDescription)"
                }
            }
        }
    }

    private func stopMirroring(reason: String) async {
        finishMirroring()
        await capture.stop()
        status = reason
    }

    private func backingScaleFactor(for window: SCWindow) -> CGFloat {
        let sourceScreen = NSScreen.screens.max { left, right in
            Self.intersectionArea(window.frame, Self.captureFrame(of: left))
                < Self.intersectionArea(window.frame, Self.captureFrame(of: right))
        }
        return sourceScreen?.backingScaleFactor ?? 1
    }

    private static func captureFrame(of screen: NSScreen) -> CGRect {
        guard let displayID = displayID(of: screen) else { return screen.frame }
        // ScreenCaptureKit uses the Core Graphics global coordinate space. NSScreen flips
        // the vertical axis, which matters when displays are arranged above one another.
        return CGDisplayBounds(displayID)
    }

    private static func intersectionArea(_ first: CGRect, _ second: CGRect) -> CGFloat {
        let intersection = first.intersection(second)
        guard !intersection.isNull else { return 0 }
        return max(0, intersection.width) * max(0, intersection.height)
    }

    private func preferredWindow(in items: [WindowItem]) -> WindowItem? {
        guard let saved = preferences.sourceIdentity else { return nil }
        return items.first {
            $0.appName == saved.appName && $0.title == saved.title
        } ?? items.first {
            $0.appName == saved.appName
        }
    }

    private func rememberSource(_ windowID: CGWindowID) {
        guard let item = windows.first(where: { $0.id == windowID }) else { return }
        preferences.sourceIdentity = (item.appName, item.title)
    }

    /// Synchronous teardown for app termination, where awaiting is not an option.
    func shutdown() {
        finishMirroring()
    }
}
