import AppKit

/// Black canvas that renders captured frames.
///
/// The frame's IOSurface is assigned straight to a CALayer's `contents`, so the pixels
/// go GPU → GPU with no CPU copy. `contentsGravity` gives us the three scale modes for
/// free — on Windows the same feature meant hand-computing destination rectangles, which
/// is where the aspect-ratio distortion bug came from.
final class MirrorLayerView: NSView {

    private let contentLayer = CALayer()
    private let spotlightLayer = CAShapeLayer()
    private let magnifierLayer = CALayer()
    private let magnifierFrameLayer = CAShapeLayer()
    private let annotationLayer = CAShapeLayer()
    private var sourcePixelSize = CGSize.zero
    private var zoomFactor: CGFloat = 1
    private var pointer = CGPoint(x: 0.5, y: 0.5)
    private var magnifierVisible = false
    private var spotlightVisible = false
    private var pointerEffectSize: CGFloat = 240
    private var annotations: [AnnotationItem] = []
    private var contentGravity: CALayerContentsGravity = .resizeAspect

    override init(frame frameRect: NSRect) {
        super.init(frame: frameRect)
        setUpLayers()
    }

    required init?(coder: NSCoder) {
        super.init(coder: coder)
        setUpLayers()
    }

    private func setUpLayers() {
        wantsLayer = true
        let root = CALayer()
        root.backgroundColor = NSColor.black.cgColor
        layer = root

        contentLayer.contentsGravity = .resizeAspect
        contentLayer.backgroundColor = NSColor.black.cgColor
        contentLayer.frame = bounds
        root.addSublayer(contentLayer)

        spotlightLayer.fillRule = .evenOdd
        spotlightLayer.fillColor = NSColor.black.withAlphaComponent(0.72).cgColor
        spotlightLayer.isHidden = true
        root.addSublayer(spotlightLayer)

        magnifierLayer.contentsGravity = .resizeAspectFill
        magnifierLayer.masksToBounds = true
        magnifierLayer.cornerRadius = 12
        magnifierLayer.isHidden = true
        root.addSublayer(magnifierLayer)

        magnifierFrameLayer.fillColor = NSColor.clear.cgColor
        magnifierFrameLayer.strokeColor = NSColor.white.cgColor
        magnifierFrameLayer.lineWidth = 3
        magnifierFrameLayer.isHidden = true
        root.addSublayer(magnifierFrameLayer)

        annotationLayer.fillColor = NSColor.clear.cgColor
        root.addSublayer(annotationLayer)
    }

    override func layout() {
        super.layout()
        // Implicit animations would make every resize visibly lag the source window.
        CATransaction.begin()
        CATransaction.setDisableActions(true)
        contentLayer.frame = bounds
        spotlightLayer.frame = bounds
        annotationLayer.frame = bounds
        updatePresentationLayers()
        CATransaction.commit()
    }

    func update(surface: IOSurfaceRef) {
        CATransaction.begin()
        CATransaction.setDisableActions(true)
        contentLayer.contents = surface
        magnifierLayer.contents = surface
        sourcePixelSize = CGSize(width: IOSurfaceGetWidth(surface),
                                 height: IOSurfaceGetHeight(surface))
        updatePresentationLayers()
        CATransaction.commit()
    }

    func setScaleMode(_ gravity: CALayerContentsGravity) {
        CATransaction.begin()
        CATransaction.setDisableActions(true)
        contentLayer.contentsGravity = gravity
        contentGravity = gravity
        magnifierLayer.contentsGravity = .resizeAspectFill
        updatePresentationLayers()
        CATransaction.commit()
    }

    func updatePresentation(pointer: CGPoint,
                            zoomFactor: CGFloat,
                            magnifierVisible: Bool,
                            spotlightVisible: Bool,
                            effectSize: CGFloat) {
        self.pointer = CGPoint(x: min(max(pointer.x, 0), 1),
                               y: min(max(pointer.y, 0), 1))
        self.zoomFactor = max(1, zoomFactor)
        self.magnifierVisible = magnifierVisible
        self.spotlightVisible = spotlightVisible
        pointerEffectSize = effectSize
        updatePresentationLayers()
    }

    func updateAnnotations(_ items: [AnnotationItem]) {
        annotations = items
        updateAnnotationLayers()
    }

    private func updatePresentationLayers() {
        guard bounds.width > 0, bounds.height > 0 else { return }
        CATransaction.begin()
        CATransaction.setDisableActions(true)

        let crop = sourceCrop()
        contentLayer.contentsRect = crop
        let projected = project(pointer, through: crop)

        let size = min(pointerEffectSize, min(bounds.width, bounds.height) * 0.65)
        let frame = CGRect(x: min(max(projected.x - size / 2, 8), bounds.width - size - 8),
                           y: min(max(projected.y - size / 2, 8), bounds.height - size - 8),
                           width: size, height: size)
        magnifierLayer.frame = frame
        let lensScale: CGFloat = 2
        let lensRect = CGRect(x: min(max(pointer.x - 0.5 / lensScale, 0), 1 - 1 / lensScale),
                              y: min(max(pointer.y - 0.5 / lensScale, 0), 1 - 1 / lensScale),
                              width: 1 / lensScale, height: 1 / lensScale)
        magnifierLayer.contentsRect = lensRect
        magnifierLayer.isHidden = !magnifierVisible
        magnifierFrameLayer.path = CGPath(roundedRect: frame, cornerWidth: 12,
                                          cornerHeight: 12, transform: nil)
        magnifierFrameLayer.isHidden = !magnifierVisible

        let spotlightPath = CGMutablePath()
        spotlightPath.addRect(bounds)
        let radius = max(40, pointerEffectSize / 2)
        spotlightPath.addEllipse(in: CGRect(x: projected.x - radius,
                                            y: projected.y - radius,
                                            width: radius * 2, height: radius * 2))
        spotlightLayer.path = spotlightPath
        spotlightLayer.isHidden = !spotlightVisible

        updateAnnotationLayers(crop: crop)
        CATransaction.commit()
    }

    private func sourceCrop() -> CGRect {
        guard zoomFactor > 1 else { return CGRect(x: 0, y: 0, width: 1, height: 1) }
        let width = 1 / zoomFactor
        let height = 1 / zoomFactor
        return CGRect(x: min(max(pointer.x - width / 2, 0), 1 - width),
                      y: min(max(pointer.y - height / 2, 0), 1 - height),
                      width: width, height: height)
    }

    private func project(_ normalized: CGPoint, through crop: CGRect) -> CGPoint {
        let imageRect = projectedImageRect()
        return CGPoint(x: imageRect.minX + (normalized.x - crop.minX) / crop.width * imageRect.width,
                       y: imageRect.minY + (normalized.y - crop.minY) / crop.height * imageRect.height)
    }

    private func projectedImageRect() -> CGRect {
        guard sourcePixelSize.width > 0, sourcePixelSize.height > 0,
              contentGravity != .resize else { return bounds }
        let scale = contentGravity == .resizeAspect
            ? min(bounds.width / sourcePixelSize.width, bounds.height / sourcePixelSize.height)
            : max(bounds.width / sourcePixelSize.width, bounds.height / sourcePixelSize.height)
        let size = CGSize(width: sourcePixelSize.width * scale,
                          height: sourcePixelSize.height * scale)
        return CGRect(x: (bounds.width - size.width) / 2,
                      y: (bounds.height - size.height) / 2,
                      width: size.width, height: size.height)
    }

    private func updateAnnotationLayers(crop: CGRect? = nil) {
        annotationLayer.sublayers?.forEach { $0.removeFromSuperlayer() }
        let crop = crop ?? sourceCrop()
        for item in annotations {
            let layer = CAShapeLayer()
            layer.fillColor = NSColor.clear.cgColor
            layer.strokeColor = item.color.withAlphaComponent(item.opacity).cgColor
            layer.lineWidth = item.thickness
            layer.lineCap = .round
            layer.lineJoin = .round
            layer.path = annotationPath(item, crop: crop)
            annotationLayer.addSublayer(layer)
        }
    }

    private func annotationPath(_ item: AnnotationItem, crop: CGRect) -> CGPath? {
        guard let first = item.points.first else { return nil }
        let points = item.points.map { project(CGPoint(x: $0.x, y: $0.y), through: crop) }
        let path = CGMutablePath()
        switch item.tool {
        case .pen, .highlighter:
            path.move(to: points[0])
            points.dropFirst().forEach { path.addLine(to: $0) }
        case .line:
            path.move(to: points[0]); path.addLine(to: points.last ?? points[0])
        case .arrow:
            let end = points.last ?? points[0]
            path.move(to: points[0]); path.addLine(to: end)
            let angle = atan2(end.y - points[0].y, end.x - points[0].x)
            let length = max(12, item.thickness * 4)
            path.move(to: end)
            path.addLine(to: CGPoint(x: end.x - length * cos(angle - .pi / 6),
                                     y: end.y - length * sin(angle - .pi / 6)))
            path.move(to: end)
            path.addLine(to: CGPoint(x: end.x - length * cos(angle + .pi / 6),
                                     y: end.y - length * sin(angle + .pi / 6)))
        case .rectangle, .ellipse:
            let end = points.last ?? points[0]
            let rect = CGRect(x: min(points[0].x, end.x), y: min(points[0].y, end.y),
                              width: abs(points[0].x - end.x), height: abs(points[0].y - end.y))
            if item.tool == .rectangle { path.addRect(rect) }
            else { path.addEllipse(in: rect) }
        case .eraser:
            return nil
        }
        _ = first
        return path
    }
}
