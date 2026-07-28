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
        .frame(minWidth: 420, minHeight: 620)
        .task {
            state.refreshPermission()
            await state.refreshSources()
        }
        .onChange(of: state.selectedWindowID) { _ in
            Task { await state.switchToSelectedWindow() }
        }
    }

    private var controlPanel: some View {
        VStack(alignment: .leading, spacing: 14) {
            header

            if !state.hasPermission {
                permissionBanner
            } else {
                windowSection
                screenSection
                scaleModePicker
                HotKeySettingView(
                    combination: state.hotKey,
                    onChange: state.updateHotKey,
                    onRestoreDefault: state.restoreDefaultHotKey)
                Toggle("在副屏显示鼠标指针", isOn: $state.showsCursor)
                    .disabled(state.isMirroring)
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
        }
        .walkthroughTarget(.startMirror)
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
