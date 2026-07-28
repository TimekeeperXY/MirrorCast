import Foundation
import ScreenCaptureKit
import CoreMedia
import CoreVideo

/// Wraps a single ScreenCaptureKit stream that mirrors one window.
///
/// This is the macOS counterpart to the Windows build's `ThumbnailController`, but the
/// mechanism is fundamentally different: Windows hands the compositor a "draw window X
/// here" instruction and never sees pixels, whereas ScreenCaptureKit produces real frames.
/// The cost is kept near zero by never touching those pixels on the CPU — each frame's
/// IOSurface is handed straight to a CALayer, so the data stays on the GPU end to end.
final class CaptureEngine: NSObject {

    /// Delivered on the main actor, once per accepted frame.
    var onSurface: (@MainActor (IOSurfaceRef) -> Void)?

    /// Delivered on the main actor when the stream ends by itself — most commonly
    /// because the source window was closed.
    var onStopped: (@MainActor (Error?) -> Void)?

    private var stream: SCStream?
    private var configuredWidth = 0
    private var configuredHeight = 0
    private var configuredShowsCursor = true
    private let sampleQueue = DispatchQueue(label: "com.xiaoyang.mirrorcast.capture",
                                            qos: .userInitiated)

    var isRunning: Bool { stream != nil }

    func start(window: SCWindow, showsCursor: Bool, scale: CGFloat) async throws {
        await stop()

        // A window-only filter: nothing behind or in front of the window is captured,
        // which is what makes this a window mirror rather than a screen mirror.
        let filter = SCContentFilter(desktopIndependentWindow: window)

        let config = makeConfiguration(window: window,
                                       showsCursor: showsCursor,
                                       scale: scale)

        let stream = SCStream(filter: filter, configuration: config, delegate: self)
        try stream.addStreamOutput(self, type: .screen, sampleHandlerQueue: sampleQueue)
        try await stream.startCapture()
        self.stream = stream
        remember(config)
    }

    func updateConfiguration(window: SCWindow,
                             showsCursor: Bool,
                             scale: CGFloat) async throws {
        guard let stream else { return }

        let config = makeConfiguration(window: window,
                                       showsCursor: showsCursor,
                                       scale: scale)
        guard config.width != configuredWidth
                || config.height != configuredHeight
                || config.showsCursor != configuredShowsCursor
        else { return }

        try await stream.updateConfiguration(config)
        remember(config)
    }

    func stop() async {
        guard let stream else { return }
        self.stream = nil
        configuredWidth = 0
        configuredHeight = 0
        try? await stream.stopCapture()
    }

    private func makeConfiguration(window: SCWindow,
                                   showsCursor: Bool,
                                   scale: CGFloat) -> SCStreamConfiguration {
        let config = SCStreamConfiguration()
        // The source screen owns the window's backing store. Using the target screen's
        // scale here produces the wrong pixel dimensions when the displays have different DPI.
        config.width = max(2, Int(window.frame.width * scale))
        config.height = max(2, Int(window.frame.height * scale))
        config.pixelFormat = kCVPixelFormatType_32BGRA
        config.showsCursor = showsCursor
        config.minimumFrameInterval = CMTime(value: 1, timescale: 60)
        config.queueDepth = 5
        config.scalesToFit = true
        return config
    }

    private func remember(_ config: SCStreamConfiguration) {
        configuredWidth = config.width
        configuredHeight = config.height
        configuredShowsCursor = config.showsCursor
    }
}

// MARK: - Frame delivery

extension CaptureEngine: SCStreamOutput {

    func stream(_ stream: SCStream,
                didOutputSampleBuffer sampleBuffer: CMSampleBuffer,
                of outputType: SCStreamOutputType) {

        guard outputType == .screen, sampleBuffer.isValid else { return }

        // ScreenCaptureKit also emits "idle" and "blank" frames when nothing changed.
        // Rendering those would flash the mirror, so only complete frames are accepted.
        guard let attachments = CMSampleBufferGetSampleAttachmentsArray(sampleBuffer,
                                                                        createIfNecessary: false)
                as? [[SCStreamFrameInfo: Any]],
              let statusRaw = attachments.first?[.status] as? Int,
              let status = SCFrameStatus(rawValue: statusRaw),
              status == .complete
        else { return }

        guard let pixelBuffer = CMSampleBufferGetImageBuffer(sampleBuffer) else { return }

        // The task captures `pixelBuffer`, which keeps its IOSurface alive until the layer
        // has taken it. Extracting the surface here and posting only the raw pointer would
        // risk the buffer being recycled before the layer renders it.
        Task { @MainActor [weak self] in
            guard let surface = CVPixelBufferGetIOSurface(pixelBuffer)?.takeUnretainedValue() else { return }
            self?.onSurface?(surface)
        }
    }
}

// MARK: - Stream lifetime

extension CaptureEngine: SCStreamDelegate {

    func stream(_ stream: SCStream, didStopWithError error: Error) {
        Task { @MainActor [weak self] in
            guard self?.stream === stream else { return }
            self?.stream = nil
            self?.onStopped?(error)
        }
    }
}
