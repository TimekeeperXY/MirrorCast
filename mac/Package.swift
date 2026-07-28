// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "MirrorCast",
    platforms: [
        // ScreenCaptureKit exists from 12.3, but early releases had window-capture
        // bugs; 13 is the first genuinely dependable floor.
        .macOS(.v13)
    ],
    targets: [
        .executableTarget(
            name: "MirrorCast",
            path: "Sources/MirrorCast"
        )
    ]
)
