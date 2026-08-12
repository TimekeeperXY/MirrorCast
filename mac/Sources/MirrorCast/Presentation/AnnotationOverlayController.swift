import AppKit
import SwiftUI

@MainActor
final class AnnotationOverlayController {
    private var panel: NSPanel?
    private let document: AnnotationDocument
    private let onExit: () -> Void

    init(document: AnnotationDocument, onExit: @escaping () -> Void) {
        self.document = document
        self.onExit = onExit
    }

    var isShowing: Bool { panel != nil }

    func show(sourceFrame: CGRect) {
        if panel == nil {
            let panel = NSPanel(
                contentRect: appKitRect(fromCaptureRect: sourceFrame),
                styleMask: [.borderless, .nonactivatingPanel],
                backing: .buffered,
                defer: false)
            panel.isOpaque = false
            panel.backgroundColor = .clear
            panel.hasShadow = false
            panel.level = .screenSaver
            panel.collectionBehavior = [.canJoinAllSpaces, .stationary,
                                        .fullScreenAuxiliary, .ignoresCycle]
            panel.hidesOnDeactivate = false
            panel.isMovable = false
            panel.contentView = NSHostingView(rootView: AnnotationOverlayView(
                document: document,
                onExit: onExit))
            panel.orderFrontRegardless()
            self.panel = panel
        }
        update(sourceFrame: sourceFrame)
    }

    func update(sourceFrame: CGRect) {
        panel?.setFrame(appKitRect(fromCaptureRect: sourceFrame), display: true)
    }

    func close() {
        panel?.orderOut(nil)
        panel?.contentView = nil
        panel = nil
    }

    private func appKitRect(fromCaptureRect rect: CGRect) -> NSRect {
        let mainTop = NSScreen.screens.first?.frame.maxY ?? 0
        return NSRect(x: rect.minX,
                      y: mainTop - rect.maxY,
                      width: rect.width,
                      height: rect.height)
    }
}

private struct AnnotationOverlayView: View {
    @ObservedObject var document: AnnotationDocument
    let onExit: () -> Void

    @State private var tool: AnnotationTool = .pen
    @State private var color = NSColor.systemRed
    @State private var thickness: CGFloat = 4
    @State private var draft: [AnnotationPoint] = []
    @State private var erasedIDs: Set<UUID> = []

    private let colors: [NSColor] = [.systemRed, .systemYellow, .systemBlue,
                                     .systemGreen, .white]

    var body: some View {
        GeometryReader { proxy in
            ZStack(alignment: .top) {
                Color.black.opacity(0.001)
                AnnotationCanvas(items: document.items.filter { !erasedIDs.contains($0.id) },
                                 draft: draftItem)
                    .contentShape(Rectangle())
                    .gesture(drawGesture(size: proxy.size))

                toolbar
                    .padding(.top, 10)
            }
        }
    }

    private var toolbar: some View {
        HStack(spacing: 5) {
            ForEach(AnnotationTool.allCases) { candidate in
                Button(candidate.title) { tool = candidate }
                    .buttonStyle(.bordered)
                    .tint(tool == candidate ? .accentColor : .secondary)
                    .controlSize(.small)
            }

            Divider().frame(height: 24)

            ForEach(colors, id: \.self) { candidate in
                Button { color = candidate } label: {
                    Circle().fill(Color(nsColor: candidate)).frame(width: 18, height: 18)
                        .overlay(Circle().stroke(.white.opacity(color == candidate ? 1 : 0.25), lineWidth: 2))
                }
                .buttonStyle(.plain)
            }

            Slider(value: $thickness, in: 2...12, step: 1).frame(width: 70)
            Button("撤销") { document.undo() }.disabled(!document.canUndo)
            Button("重做") { document.redo() }.disabled(!document.canRedo)
            Button("清空") { document.clear() }
            Button("退出") { onExit() }.keyboardShortcut(.cancelAction)
        }
        .padding(7)
        .background(.black.opacity(0.82), in: RoundedRectangle(cornerRadius: 8))
        .foregroundStyle(.white)
    }

    private var draftItem: AnnotationItem? {
        guard !draft.isEmpty, tool != .eraser else { return nil }
        return AnnotationItem(tool: tool,
                              points: draft,
                              color: color,
                              thickness: tool == .highlighter ? thickness * 3 : thickness,
                              opacity: tool == .highlighter ? 0.38 : 1)
    }

    private func drawGesture(size: CGSize) -> some Gesture {
        DragGesture(minimumDistance: 0, coordinateSpace: .local)
            .onChanged { value in
                let point = normalized(value.location, size: size)
                if tool == .eraser {
                    erase(at: point, size: size)
                } else if draft.isEmpty {
                    draft = [point]
                } else if tool == .pen || tool == .highlighter {
                    draft.append(point)
                } else if draft.count == 1 {
                    draft.append(point)
                } else {
                    draft[draft.count - 1] = point
                }
            }
            .onEnded { value in
                if tool == .eraser {
                    document.remove(ids: erasedIDs)
                    erasedIDs.removeAll()
                    return
                }
                let end = normalized(value.location, size: size)
                if draft.count == 1 { draft.append(end) }
                guard let item = draftItem else { return }
                document.add(item)
                draft.removeAll()
            }
    }

    private func normalized(_ point: CGPoint, size: CGSize) -> AnnotationPoint {
        AnnotationPoint(x: min(max(point.x / max(1, size.width), 0), 1),
                        y: min(max(1 - point.y / max(1, size.height), 0), 1))
    }

    private func erase(at point: AnnotationPoint, size: CGSize) {
        let tolerance = 14 / max(1, min(size.width, size.height))
        for item in document.items.reversed() where !erasedIDs.contains(item.id) {
            if hit(item, point: point, tolerance: tolerance) {
                erasedIDs.insert(item.id)
                return
            }
        }
    }

    private func hit(_ item: AnnotationItem, point: AnnotationPoint, tolerance: CGFloat) -> Bool {
        guard !item.points.isEmpty else { return false }
        if item.points.count == 1 {
            return hypot(item.points[0].x - point.x, item.points[0].y - point.y) <= tolerance
        }
        for index in 1..<item.points.count {
            if distance(point, segmentStart: item.points[index - 1], end: item.points[index]) <= tolerance {
                return true
            }
        }
        return false
    }

    private func distance(_ point: AnnotationPoint,
                          segmentStart start: AnnotationPoint,
                          end: AnnotationPoint) -> CGFloat {
        let dx = end.x - start.x
        let dy = end.y - start.y
        let lengthSquared = dx * dx + dy * dy
        if lengthSquared < 0.000001 { return hypot(point.x - start.x, point.y - start.y) }
        let t = min(max(((point.x - start.x) * dx + (point.y - start.y) * dy) / lengthSquared, 0), 1)
        return hypot(point.x - (start.x + t * dx), point.y - (start.y + t * dy))
    }
}

private struct AnnotationCanvas: View {
    let items: [AnnotationItem]
    let draft: AnnotationItem?

    var body: some View {
        Canvas { context, size in
            for item in items { draw(item, in: &context, size: size) }
            if let draft { draw(draft, in: &context, size: size) }
        }
    }

    private func draw(_ item: AnnotationItem, in context: inout GraphicsContext, size: CGSize) {
        guard let first = item.points.first else { return }
        let points = item.points.map { CGPoint(x: $0.x * size.width, y: (1 - $0.y) * size.height) }
        var path = Path()
        switch item.tool {
        case .pen, .highlighter:
            path.move(to: points[0])
            for point in points.dropFirst() { path.addLine(to: point) }
        case .line:
            guard let last = points.last else { return }
            path.move(to: points[0]); path.addLine(to: last)
        case .arrow:
            guard let last = points.last else { return }
            path.move(to: points[0]); path.addLine(to: last)
            let angle = atan2(last.y - points[0].y, last.x - points[0].x)
            let length = max(12, item.thickness * 4)
            path.move(to: last)
            path.addLine(to: CGPoint(x: last.x - length * cos(angle - .pi / 6),
                                     y: last.y - length * sin(angle - .pi / 6)))
            path.move(to: last)
            path.addLine(to: CGPoint(x: last.x - length * cos(angle + .pi / 6),
                                     y: last.y - length * sin(angle + .pi / 6)))
        case .rectangle, .ellipse:
            guard let last = points.last else { return }
            let rect = CGRect(x: min(points[0].x, last.x), y: min(points[0].y, last.y),
                              width: abs(points[0].x - last.x), height: abs(points[0].y - last.y))
            path = item.tool == .rectangle ? Path(rect) : Path(ellipseIn: rect)
        case .eraser:
            return
        }
        context.opacity = item.opacity
        context.stroke(path, with: .color(Color(nsColor: item.color)),
                       style: StrokeStyle(lineWidth: item.thickness,
                                          lineCap: .round, lineJoin: .round))
        _ = first
    }
}
