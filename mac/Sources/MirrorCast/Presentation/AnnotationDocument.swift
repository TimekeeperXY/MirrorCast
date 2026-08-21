import AppKit
import Combine

enum AnnotationTool: String, CaseIterable, Identifiable {
    case pen, highlighter, line, arrow, rectangle, ellipse, eraser

    var id: String { rawValue }

    var title: String {
        switch self {
        case .pen: "画笔"
        case .highlighter: "荧光笔"
        case .line: "直线"
        case .arrow: "箭头"
        case .rectangle: "矩形"
        case .ellipse: "椭圆"
        case .eraser: "橡皮擦"
        }
    }
}

struct AnnotationPoint: Equatable {
    let x: CGFloat
    let y: CGFloat
}

struct AnnotationItem: Identifiable, Equatable {
    let id = UUID()
    let tool: AnnotationTool
    let points: [AnnotationPoint]
    let color: NSColor
    let thickness: CGFloat
    let opacity: CGFloat

    static func == (lhs: AnnotationItem, rhs: AnnotationItem) -> Bool {
        lhs.id == rhs.id
    }
}

@MainActor
final class AnnotationDocument: ObservableObject {
    @Published private(set) var items: [AnnotationItem] = []
    @Published private(set) var canUndo = false
    @Published private(set) var canRedo = false

    var onChange: (() -> Void)?

    private var history: [[AnnotationItem]] = [[]]
    private var historyIndex = 0

    func add(_ item: AnnotationItem) {
        commit(items + [item])
    }

    func remove(ids: Set<UUID>) {
        guard !ids.isEmpty else { return }
        let next = items.filter { !ids.contains($0.id) }
        guard next.count != items.count else { return }
        commit(next)
    }

    func undo() {
        guard historyIndex > 0 else { return }
        historyIndex -= 1
        publish()
    }

    func redo() {
        guard historyIndex + 1 < history.count else { return }
        historyIndex += 1
        publish()
    }

    func clear() {
        guard !items.isEmpty else { return }
        commit([])
    }

    func reset() {
        history = [[]]
        historyIndex = 0
        publish()
    }

    private func commit(_ next: [AnnotationItem]) {
        if historyIndex + 1 < history.count {
            history.removeSubrange((historyIndex + 1)..<history.count)
        }
        history.append(next)
        historyIndex += 1
        publish()
    }

    private func publish() {
        items = history[historyIndex]
        canUndo = historyIndex > 0
        canRedo = historyIndex + 1 < history.count
        onChange?()
    }
}
