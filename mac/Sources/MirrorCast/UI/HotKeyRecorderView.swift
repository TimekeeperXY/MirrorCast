import AppKit
import Carbon
import SwiftUI

struct HotKeySettingView: View {
    let combination: HotKeyCombination
    let onChange: (HotKeyCombination) -> Void
    let onRestoreDefault: () -> Void

    @State private var isRecording = false

    var body: some View {
        HStack(spacing: 8) {
            Text("开始/停止快捷键")
                .font(.subheadline)
                .foregroundStyle(.secondary)

            Spacer()

            Button(isRecording ? "请按下组合键…" : combination.displayName) {
                isRecording = true
            }
            .controlSize(.small)

            Button(action: onRestoreDefault) {
                Image(systemName: "arrow.counterclockwise")
            }
            .buttonStyle(.borderless)
            .help("恢复默认快捷键")
            .disabled(combination == .defaultValue)

            HotKeyCaptureView(
                isActive: isRecording,
                onCapture: {
                    isRecording = false
                    onChange($0)
                },
                onCancel: {
                    isRecording = false
                })
                .frame(width: 0, height: 0)
        }
    }
}

private struct HotKeyCaptureView: NSViewRepresentable {
    let isActive: Bool
    let onCapture: (HotKeyCombination) -> Void
    let onCancel: () -> Void

    func makeNSView(context: Context) -> HotKeyCaptureNSView {
        let view = HotKeyCaptureNSView()
        view.onCapture = onCapture
        view.onCancel = onCancel
        return view
    }

    func updateNSView(_ view: HotKeyCaptureNSView, context: Context) {
        view.onCapture = onCapture
        view.onCancel = onCancel

        guard isActive else {
            if view.window?.firstResponder === view {
                view.window?.makeFirstResponder(nil)
            }
            return
        }

        DispatchQueue.main.async {
            view.window?.makeFirstResponder(view)
        }
    }
}

private final class HotKeyCaptureNSView: NSView {
    var onCapture: ((HotKeyCombination) -> Void)?
    var onCancel: (() -> Void)?

    override var acceptsFirstResponder: Bool { true }

    override func keyDown(with event: NSEvent) {
        if event.keyCode == UInt16(kVK_Escape) {
            onCancel?()
            return
        }

        let modifiers = carbonModifiers(from: event.modifierFlags)
        guard modifiers != 0,
              let keyLabel = keyLabel(for: event)
        else {
            NSSound.beep()
            return
        }

        onCapture?(HotKeyCombination(
            keyCode: UInt32(event.keyCode),
            modifiers: modifiers,
            keyLabel: keyLabel))
    }

    private func carbonModifiers(from flags: NSEvent.ModifierFlags) -> UInt32 {
        var modifiers: UInt32 = 0
        if flags.contains(.control) { modifiers |= UInt32(controlKey) }
        if flags.contains(.option) { modifiers |= UInt32(optionKey) }
        if flags.contains(.shift) { modifiers |= UInt32(shiftKey) }
        if flags.contains(.command) { modifiers |= UInt32(cmdKey) }
        return modifiers
    }

    private func keyLabel(for event: NSEvent) -> String? {
        let specialKeys: [UInt16: String] = [
            UInt16(kVK_Space): "Space",
            UInt16(kVK_Return): "Return",
            UInt16(kVK_Tab): "Tab",
            UInt16(kVK_Delete): "Delete",
            UInt16(kVK_ForwardDelete): "Forward Delete",
            UInt16(kVK_LeftArrow): "←",
            UInt16(kVK_RightArrow): "→",
            UInt16(kVK_UpArrow): "↑",
            UInt16(kVK_DownArrow): "↓",
            UInt16(kVK_F1): "F1",
            UInt16(kVK_F2): "F2",
            UInt16(kVK_F3): "F3",
            UInt16(kVK_F4): "F4",
            UInt16(kVK_F5): "F5",
            UInt16(kVK_F6): "F6",
            UInt16(kVK_F7): "F7",
            UInt16(kVK_F8): "F8",
            UInt16(kVK_F9): "F9",
            UInt16(kVK_F10): "F10",
            UInt16(kVK_F11): "F11",
            UInt16(kVK_F12): "F12"
        ]

        if let special = specialKeys[event.keyCode] {
            return special
        }

        guard let characters = event.charactersIgnoringModifiers?
                .trimmingCharacters(in: .whitespacesAndNewlines),
              !characters.isEmpty
        else { return nil }

        return characters.uppercased()
    }
}
