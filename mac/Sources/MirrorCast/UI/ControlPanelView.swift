import SwiftUI

struct ControlPanelView: View {

    @ObservedObject var state: AppState

    var body: some View {
        Group {
            if state.showsPermissionOnboarding {
                PermissionOnboardingView(
                    onRequestPermission: state.requestPermission,
                    onRecheckPermission: {
                        state.refreshPermission()
                        Task { await state.refreshSources() }
                    })
            } else {
                controlPanel
            }
        }
        .frame(minWidth: 440, minHeight: 720)
        .task {
            state.refreshPermission()
            await state.refreshSources()
        }
        .onChange(of: state.selectedWindowID) { _ in
            Task { await state.switchToSelectedWindow() }
        }
    }

    private var controlPanel: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 14) {
                header

                if !state.hasPermission {
                    permissionBanner
                } else {
                    windowSection
                    androidSection
                    screenSection
                    scaleModePicker
                    HotKeySettingView(
                        combination: state.hotKey,
                        onChange: state.updateHotKey,
                        onRestoreDefault: state.restoreDefaultHotKey)
                    Toggle("在副屏显示鼠标指针", isOn: $state.showsCursor)
                        .disabled(state.isMirroring)
                    presentationSettings
                }

                Spacer(minLength: 0)

                if !state.status.isEmpty {
                    Text(state.status)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                }

                actionSection
            }
            .padding(18)
        }
        .overlayPreferenceValue(WalkthroughFramePreferenceKey.self) { anchors in
            GeometryReader { proxy in
                if let step = state.walkthroughStep {
                    WalkthroughOverlay(
                        step: step,
                        hotKeyName: state.hotKey.displayName,
                        targetFrame: anchors[step.target].map { proxy[$0] },
                        onNext: state.advanceWalkthrough,
                        onSkip: state.finishWalkthrough)
                }
            }
        }
    }

    private var header: some View {
        VStack(alignment: .leading, spacing: 2) {
            HStack(alignment: .firstTextBaseline, spacing: 8) {
                Text("MirrorCast").font(.system(size: 21, weight: .bold))
                Text("窗口镜像").font(.system(size: 13)).foregroundStyle(.secondary)
            }
            Text("created by @晓阳的百宝箱")
                .font(.system(size: 11))
                .foregroundStyle(.secondary)
        }
    }

    private var permissionBanner: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text("需要「屏幕录制」权限")
                .font(.headline)
            Text("macOS 要求先授权才能读取窗口画面。点击下方按钮授权，"
                 + "如果系统设置里已经勾选但这里仍提示，请完全退出本程序后重新打开。")
                .font(.callout)
                .foregroundStyle(.secondary)
                .fixedSize(horizontal: false, vertical: true)
            HStack {
                Button("去授权") { state.requestPermission() }
                    .keyboardShortcut(.defaultAction)
                Button("我已授权，重新检测") {
                    state.refreshPermission()
                    Task { await state.refreshSources() }
                }
            }
        }
        .padding(14)
        .background(.quaternary, in: RoundedRectangle(cornerRadius: 10))
    }

    private var windowSection: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack {
                Text("第一步：选择要镜像的窗口")
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
                Spacer()
                Button("刷新") { Task { await state.refreshSources() } }
                    .controlSize(.small)
            }

            List(state.windows, selection: $state.selectedWindowID) { item in
                HStack(spacing: 6) {
                    Text(item.title).lineLimit(1)
                    Text("(\(item.appName))")
                        .foregroundStyle(.secondary)
                        .lineLimit(1)
                }
            }
            .frame(height: 200)
        }
        .walkthroughTarget(.sourceWindow)
    }

    private var androidSection: some View {
        DisclosureGroup("安卓设备投屏") {
            VStack(alignment: .leading, spacing: 9) {
                HStack {
                    Picker("设备", selection: $state.selectedAndroidSerial) {
                        Text("选择已授权设备").tag(String?.none)
                        ForEach(state.androidDevices) { device in
                            Text(device.displayName).tag(Optional(device.serial))
                        }
                    }
                    .labelsHidden()
                    Button("刷新设备") { Task { await state.refreshAndroidDevices() } }
                        .disabled(state.isAndroidBusy || state.isMirroring)
                }

                HStack {
                    TextField("无线调试 IP 或主机名（可选）", text: $state.androidAddress)
                    TextField("端口", value: $state.androidPort, format: .number)
                        .frame(width: 72)
                }

                HStack {
                    Picker("帧率", selection: $state.androidMaxFPS) {
                        ForEach([30, 60, 90, 120, 165], id: \.self) { value in
                            Text("\(value) FPS").tag(value)
                        }
                    }
                    Toggle("允许控制", isOn: $state.androidControl)
                    Toggle("电脑播放声音", isOn: $state.androidAudio)
                }

                Toggle("投屏后关闭手机显示屏", isOn: $state.androidTurnScreenOff)
                Button(state.isAndroidBusy ? "正在连接…" : "投到副屏") {
                    Task { await state.startAndroidMirroring() }
                }
                .frame(maxWidth: .infinity)
                .buttonStyle(.borderedProminent)
                .disabled(!state.canStartAndroid || state.isMirroring)
            }
            .padding(.top, 8)
        }
    }

    private var screenSection: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("第二步：选择目标显示器")
                .font(.subheadline)
                .foregroundStyle(.secondary)

            if state.screens.count <= 1 {
                Text("仅检测到一个显示器，请接上副屏并确认处于「扩展」模式")
                    .font(.callout)
                    .foregroundStyle(.red)
                    .fixedSize(horizontal: false, vertical: true)
            }

            List(state.screens, selection: $state.selectedScreenID) { item in
                HStack(spacing: 8) {
                    Text(item.name)
                    Text(item.resolution).foregroundStyle(.secondary)
                }
            }
            .frame(height: 80)
            .disabled(state.isMirroring)
        }
        .walkthroughTarget(.targetDisplay)
    }

    private var actionSection: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("第四步：开始镜像")
                .font(.subheadline)
                .foregroundStyle(.secondary)

            Button {
                Task {
                    if state.isMirroring {
                        await state.stopMirroring()
                    } else {
                        await state.startMirroring()
                    }
                }
            } label: {
                Text(state.isMirroring ? "停止镜像" : "开始镜像")
                    .frame(maxWidth: .infinity)
                    .padding(.vertical, 6)
            }
            .controlSize(.large)
            .buttonStyle(.borderedProminent)
            .disabled(!state.isMirroring && !state.canStart)

            if state.isMirroring {
                presentationActions
                    .padding(.top, 8)
            }
        }
        .walkthroughTarget(.startMirror)
    }

    private var presentationSettings: some View {
        VStack(alignment: .leading, spacing: 9) {
            Divider()
            Text("演示辅助")
                .font(.subheadline)
                .foregroundStyle(.secondary)
            Toggle("镜像时启用 F1-F4 演示快捷模式", isOn: $state.presentationKeyMode)
            HStack {
                Text("放大倍数")
                Slider(value: $state.presentationZoomFactor, in: 1.25...5, step: 0.25)
                Text("\(state.presentationZoomFactor, specifier: "%.2f")x")
                    .monospacedDigit().frame(width: 48, alignment: .trailing)
            }
            HStack {
                Text("指针范围")
                Slider(value: $state.pointerEffectSize, in: 120...480, step: 20)
                Text("\(Int(state.pointerEffectSize)) px")
                    .monospacedDigit().frame(width: 58, alignment: .trailing)
            }
            Text("F1 屏幕放大 · F2 指针放大镜 · F3 指针聚光灯 · F4 屏幕标注 · Esc 逐层退出")
                .font(.caption)
                .foregroundStyle(.secondary)
                .fixedSize(horizontal: false, vertical: true)
        }
    }

    private var presentationActions: some View {
        VStack(spacing: 6) {
            HStack(spacing: 6) {
                presentationButton(state.isScreenZoomActive ? "关闭屏幕放大" : "F1 屏幕放大",
                                   active: state.isScreenZoomActive,
                                   action: state.toggleScreenZoom)
                presentationButton(state.isMagnifierActive ? "关闭放大镜" : "F2 指针放大镜",
                                   active: state.isMagnifierActive,
                                   action: state.toggleMagnifier)
            }
            HStack(spacing: 6) {
                presentationButton(state.isSpotlightActive ? "关闭聚光灯" : "F3 指针聚光灯",
                                   active: state.isSpotlightActive,
                                   action: state.toggleSpotlight)
                presentationButton(state.isAnnotationActive ? "退出标注" : "F4 屏幕标注",
                                   active: state.isAnnotationActive,
                                   action: state.toggleAnnotations)
            }
        }
    }

    private func presentationButton(_ title: String,
                                    active: Bool,
                                    action: @escaping () -> Void) -> some View {
        Button(title, action: action)
            .frame(maxWidth: .infinity)
            .buttonStyle(.bordered)
            .tint(active ? .accentColor : .secondary)
    }

    private var scaleModePicker: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("第三步：选择缩放模式")
                .font(.subheadline)
                .foregroundStyle(.secondary)
            Picker("缩放模式", selection: $state.scaleMode) {
                ForEach(MirrorScaleMode.allCases) { mode in
                    Text(mode.title).tag(mode)
                }
            }
            .labelsHidden()
            .pickerStyle(.segmented)
        }
        .walkthroughTarget(.scaleMode)
    }
}
