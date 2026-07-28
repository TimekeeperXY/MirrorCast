import AppKit

/// Black canvas that renders captured frames.
///
/// The frame's IOSurface is assigned straight to a CALayer's `contents`, so the pixels
/// go GPU → GPU with no CPU copy. `contentsGravity` gives us the three scale modes for
/// free — on Windows the same feature meant hand-computing destination rectangles, which
/// is where the aspect-ratio distortion bug came from.
final class MirrorLayerView: NSView {

    private let contentLayer = CALayer()

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
    }

    override func layout() {
        super.layout()
        // Implicit animations would make every resize visibly lag the source window.
        CATransaction.begin()
        CATransaction.setDisableActions(true)
        contentLayer.frame = bounds
        CATransaction.commit()
    }

    func update(surface: IOSurfaceRef) {
        CATransaction.begin()
        CATransaction.setDisableActions(true)
        contentLayer.contents = surface
        CATransaction.commit()
    }

    func setScaleMode(_ gravity: CALayerContentsGravity) {
        CATransaction.begin()
        CATransaction.setDisableActions(true)
        contentLayer.contentsGravity = gravity
        CATransaction.commit()
    }
}
