import SwiftUI

enum WalkthroughTarget: Hashable {
    case sourceWindow
    case targetDisplay
    case scaleMode
    case startMirror
}

enum WalkthroughStep: Int, CaseIterable {
    case sourceWindow
    case targetDisplay
    case scaleMode
    case startMirror
    case switchWindow

    var next: WalkthroughStep? {
        WalkthroughStep(rawValue: rawValue + 1)
    }

    var target: WalkthroughTarget {
        switch self {
        case .sourceWindow, .switchWindow: .sourceWindow
        case .targetDisplay: .targetDisplay
        case .scaleMode: .scaleMode
        case .startMirror: .startMirror
        }
    }

    var title: String {
        switch self {
        case .sourceWindow: "① 选择要镜像的窗口"
        case .targetDisplay: "② 选择目标显示器"
        case .scaleMode: "③ 选择缩放模式"
        case .startMirror: "④ 开始镜像"
        case .switchWindow: "⑤ 单击窗口即可切换"
        }
    }

    func body(hotKeyName: String) -> String {
        switch self {
        case .sourceWindow:
            "这里列出了当前可镜像的窗口。点击「刷新」可以重新读取窗口列表。"
        case .targetDisplay:
            "选择镜像画面要投放到哪块屏幕。两块显示器需要处于「扩展」模式。"
        case .scaleMode:
            "「适应」保留完整画面和黑边；「填满」可能裁切；「拉伸」会铺满整个副屏。"
        case .startMirror:
            "点击后，目标显示器会全屏显示所选窗口。也可以使用 \(hotKeyName) 开始或停止。"
        case .switchWindow:
            "这是 Mac 版的便捷操作：镜像开始后，无需先停止，直接单击列表中的另一个窗口，副屏会自动切换。"
        }
    }
}

struct WalkthroughFramePreferenceKey: PreferenceKey {
    static var defaultValue: [WalkthroughTarget: Anchor<CGRect>] = [:]

    static func reduce(value: inout [WalkthroughTarget: Anchor<CGRect>],
                       nextValue: () -> [WalkthroughTarget: Anchor<CGRect>]) {
        value.merge(nextValue(), uniquingKeysWith: { _, new in new })
    }
}

extension View {
    func walkthroughTarget(_ target: WalkthroughTarget) -> some View {
        anchorPreference(key: WalkthroughFramePreferenceKey.self, value: .bounds) {
            [target: $0]
        }
    }
}

struct WalkthroughOverlay: View {
    let step: WalkthroughStep
    let hotKeyName: String
    let targetFrame: CGRect?
    let onNext: () -> Void
    let onSkip: () -> Void

    var body: some View {
        GeometryReader { geometry in
            let cutout = targetFrame?.insetBy(dx: -6, dy: -6)
            let placeCardAtTop = (cutout?.midY ?? 0) > geometry.size.height / 2

            ZStack {
                SpotlightShape(cutout: cutout)
                    .fill(.black.opacity(0.72), style: FillStyle(eoFill: true))

                if let cutout {
                    RoundedRectangle(cornerRadius: 9)
                        .stroke(Color.accentColor, lineWidth: 3)
                        .frame(width: cutout.width, height: cutout.height)
                        .position(x: cutout.midX, y: cutout.midY)
                }

                VStack {
                    if !placeCardAtTop {
                        Spacer()
                    }

                    walkthroughCard

                    if placeCardAtTop {
                        Spacer()
                    }
                }
                .padding(14)
            }
        }
        .transition(.opacity)
        .zIndex(100)
    }

    private var walkthroughCard: some View {
        VStack(alignment: .leading, spacing: 9) {
            Text("第 \(step.rawValue + 1) / \(WalkthroughStep.allCases.count) 步")
                .font(.caption)
                .foregroundStyle(.secondary)

            Text(step.title)
                .font(.headline)

            Text(step.body(hotKeyName: hotKeyName))
                .font(.callout)
                .foregroundStyle(.secondary)
                .fixedSize(horizontal: false, vertical: true)

            HStack {
                Button("跳过", action: onSkip)
                Spacer()
                Button(step.next == nil ? "开始使用" : "下一步", action: onNext)
                    .buttonStyle(.borderedProminent)
            }
        }
        .padding(15)
        .frame(maxWidth: 370, alignment: .leading)
        .background(.regularMaterial, in: RoundedRectangle(cornerRadius: 8))
        .shadow(color: .black.opacity(0.28), radius: 14, y: 5)
    }
}

private struct SpotlightShape: Shape {
    let cutout: CGRect?

    func path(in rect: CGRect) -> Path {
        var path = Path()
        path.addRect(rect)
        if let cutout {
            path.addRoundedRect(in: cutout, cornerSize: CGSize(width: 9, height: 9))
        }
        return path
    }
}
