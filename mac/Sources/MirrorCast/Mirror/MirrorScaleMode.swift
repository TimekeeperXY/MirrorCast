import QuartzCore

enum MirrorScaleMode: String, CaseIterable, Identifiable {
    case fit
    case fill
    case stretch

    var id: String { rawValue }

    var title: String {
        switch self {
        case .fit: "适应"
        case .fill: "填满"
        case .stretch: "拉伸"
        }
    }

    var contentsGravity: CALayerContentsGravity {
        switch self {
        case .fit: .resizeAspect
        case .fill: .resizeAspectFill
        case .stretch: .resize
        }
    }
}
