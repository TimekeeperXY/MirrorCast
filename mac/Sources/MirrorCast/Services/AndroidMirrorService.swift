import Foundation

struct AndroidDeviceInfo: Identifiable, Hashable {
    let serial: String
    let name: String
    let transport: String
    var id: String { serial }
    var displayName: String { "\(name) · \(transport) (\(serial))" }
}

struct AndroidMirrorOptions {
    let serial: String?
    let address: String?
    let port: UInt16
    let control: Bool
    let audio: Bool
    let turnScreenOff: Bool
    let maxFPS: Int
}

final class AndroidMirrorService {
    static let windowTitle = "MirrorCast · 安卓投屏"

    private var process: Process?

    var isAvailable: Bool {
        resolveTool("scrcpy") != nil && resolveTool("adb") != nil
    }

    func discoverDevices() async throws -> [AndroidDeviceInfo] {
        guard let adb = resolveTool("adb") else {
            throw MirrorError("未找到 ADB，请重新安装 MirrorCast 完整版。")
        }
        return try await Task.detached {
            let mdns = try Self.run(adb, ["mdns", "services"])
            for endpoint in Self.mdnsEndpoints(mdns.output) {
                _ = try? Self.run(adb, ["connect", endpoint])
            }
            let result = try Self.run(adb, ["devices", "-l"])
            guard result.status == 0 else {
                throw MirrorError(result.error.isEmpty ? "无法读取安卓设备列表。" : result.error)
            }
            return Self.parseDevices(result.output)
        }.value
    }

    func start(_ options: AndroidMirrorOptions) async throws {
        stop()
        guard let scrcpy = resolveTool("scrcpy"), let adb = resolveTool("adb") else {
            throw MirrorError("投屏组件不可用，请重新安装 MirrorCast 完整版。")
        }

        if let address = options.address, !address.isEmpty {
            guard Self.isValidAddress(address) else { throw MirrorError("无线地址格式不正确。") }
            let result = try await Task.detached {
                try Self.run(adb, ["connect", "\(address):\(options.port)"])
            }.value
            guard result.status == 0 else {
                throw MirrorError(result.error.isEmpty ? "无线 ADB 连接失败。" : result.error)
            }
        }

        var arguments = [
            "--window-title=\(Self.windowTitle)",
            "--max-size=1920",
            "--max-fps=\(min(max(options.maxFPS, 15), 240))",
            options.audio ? "--audio-source=output" : "--no-audio"
        ]
        if !options.control { arguments.append("--no-control") }
        if options.turnScreenOff { arguments.append("--turn-screen-off") }
        if let serial = options.serial, !serial.isEmpty {
            arguments.append("--serial=\(serial)")
        } else if let address = options.address, !address.isEmpty {
            arguments.append("--serial=\(address):\(options.port)")
        } else {
            arguments.append("--select-usb")
        }

        let child = Process()
        child.executableURL = URL(fileURLWithPath: scrcpy)
        child.arguments = arguments
        child.currentDirectoryURL = URL(fileURLWithPath: (scrcpy as NSString).deletingLastPathComponent)
        child.standardOutput = FileHandle.nullDevice
        child.standardError = FileHandle.nullDevice
        var environment = ProcessInfo.processInfo.environment
        environment["ADB"] = adb
        if let server = bundledTool("scrcpy-server") { environment["SCRCPY_SERVER_PATH"] = server }
        child.environment = environment
        try child.run()
        process = child
    }

    func ensureRunning() throws {
        guard let process, process.isRunning else {
            throw MirrorError("scrcpy 在创建窗口前退出，请确认手机已授权 USB/无线调试。")
        }
    }

    func stop() {
        guard let child = process else { return }
        process = nil
        if child.isRunning {
            child.terminate()
            DispatchQueue.global().asyncAfter(deadline: .now() + 1) {
                if child.isRunning { child.interrupt() }
            }
        }
    }

    private func resolveTool(_ name: String) -> String? {
        if let bundled = bundledTool(name) { return bundled }
        let pathCandidates = (ProcessInfo.processInfo.environment["PATH"] ?? "")
            .split(separator: ":").map { "\($0)/\(name)" }
        let candidates = pathCandidates + ["/opt/homebrew/bin/\(name)", "/usr/local/bin/\(name)", "/opt/local/bin/\(name)"]
        return candidates.first(where: FileManager.default.isExecutableFile(atPath:))
    }

    private func bundledTool(_ name: String) -> String? {
        guard let resources = Bundle.main.resourceURL else { return nil }
        let arch = ProcessInfo.processInfo.machineArchitecture
        let relative: String
        switch name {
        case "adb": relative = "android-tools/macos-aarch64/platform-tools/adb"
        case "scrcpy": relative = "android-tools/\(arch)/scrcpy/scrcpy"
        case "scrcpy-server": relative = "android-tools/\(arch)/scrcpy/scrcpy-server"
        default: return nil
        }
        let path = resources.appendingPathComponent(relative).path
        return FileManager.default.fileExists(atPath: path) ? path : nil
    }

    private static func run(_ executable: String, _ arguments: [String]) throws -> (status: Int32, output: String, error: String) {
        let process = Process()
        let output = Pipe()
        let error = Pipe()
        process.executableURL = URL(fileURLWithPath: executable)
        process.arguments = arguments
        process.standardOutput = output
        process.standardError = error
        try process.run()
        process.waitUntilExit()
        return (process.terminationStatus,
                String(decoding: output.fileHandleForReading.readDataToEndOfFile(), as: UTF8.self).trimmingCharacters(in: .whitespacesAndNewlines),
                String(decoding: error.fileHandleForReading.readDataToEndOfFile(), as: UTF8.self).trimmingCharacters(in: .whitespacesAndNewlines))
    }

    private static func parseDevices(_ output: String) -> [AndroidDeviceInfo] {
        output.split(whereSeparator: \.isNewline).compactMap { line in
            let parts = line.split(whereSeparator: \.isWhitespace).map(String.init)
            guard parts.count >= 2, parts[1] == "device" else { return nil }
            let serial = parts[0]
            let model = parts.first(where: { $0.hasPrefix("model:") }).map { String($0.dropFirst(6)).replacingOccurrences(of: "_", with: " ") } ?? "安卓设备"
            return AndroidDeviceInfo(serial: serial, name: model, transport: serial.contains(":") ? "Wi-Fi" : "USB")
        }.sorted { $0.name < $1.name }
    }

    private static func mdnsEndpoints(_ output: String) -> [String] {
        output.split(whereSeparator: \.isNewline).compactMap { line in
            let parts = line.split(whereSeparator: \.isWhitespace).map(String.init)
            return parts.count >= 3 && parts[1].contains("_adb-tls-connect._tcp") ? parts[2] : nil
        }
    }

    private static func isValidAddress(_ value: String) -> Bool {
        !value.isEmpty && value.count <= 253 && value.allSatisfy { $0.isASCII && ($0.isLetter || $0.isNumber || ".:-_[]".contains($0)) }
    }
}

private struct MirrorError: LocalizedError {
    let message: String
    init(_ message: String) { self.message = message }
    var errorDescription: String? { message }
}

private extension ProcessInfo {
    var machineArchitecture: String {
        #if arch(x86_64)
        return "macos-x86_64"
        #else
        return "macos-aarch64"
        #endif
    }
}
