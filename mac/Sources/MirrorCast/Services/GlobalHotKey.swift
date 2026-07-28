import Carbon
import Foundation

struct HotKeyCombination: Equatable {
    let keyCode: UInt32
    let modifiers: UInt32
    let keyLabel: String

    static let defaultValue = HotKeyCombination(
        keyCode: UInt32(kVK_ANSI_M),
        modifiers: UInt32(controlKey | optionKey),
        keyLabel: "M")

    var displayName: String {
        var parts: [String] = []
        if modifiers & UInt32(controlKey) != 0 { parts.append("Control") }
        if modifiers & UInt32(optionKey) != 0 { parts.append("Option") }
        if modifiers & UInt32(shiftKey) != 0 { parts.append("Shift") }
        if modifiers & UInt32(cmdKey) != 0 { parts.append("Command") }
        parts.append(keyLabel)
        return parts.joined(separator: " + ")
    }
}

@MainActor
final class GlobalHotKey {
    nonisolated(unsafe) private static var action: (() -> Void)?

    private var hotKey: EventHotKeyRef?
    private var eventHandler: EventHandlerRef?
    private var currentCombination: HotKeyCombination?

    @discardableResult
    func register(_ combination: HotKeyCombination,
                  action: @escaping () -> Void) -> Bool {
        unregister()
        Self.action = action

        var eventType = EventTypeSpec(
            eventClass: OSType(kEventClassKeyboard),
            eventKind: UInt32(kEventHotKeyPressed))

        let handlerStatus = InstallEventHandler(
            GetApplicationEventTarget(),
            { _, _, _ in
                DispatchQueue.main.async {
                    GlobalHotKey.action?()
                }
                return noErr
            },
            1,
            &eventType,
            nil,
            &eventHandler)
        guard handlerStatus == noErr else {
            Self.action = nil
            return false
        }

        let signature = OSType(
            UInt32(ascii: "M") << 24
                | UInt32(ascii: "C") << 16
                | UInt32(ascii: "A") << 8
                | UInt32(ascii: "S"))
        let identifier = EventHotKeyID(signature: signature, id: 1)

        let hotKeyStatus = RegisterEventHotKey(
            combination.keyCode,
            combination.modifiers,
            identifier,
            GetApplicationEventTarget(),
            0,
            &hotKey)
        guard hotKeyStatus == noErr else {
            unregister()
            return false
        }

        currentCombination = combination
        return true
    }

    func replace(with combination: HotKeyCombination) -> Bool {
        guard let action = Self.action,
              let previousCombination = currentCombination
        else { return false }

        if register(combination, action: action) {
            return true
        }

        _ = register(previousCombination, action: action)
        return false
    }

    func unregister() {
        if let hotKey {
            UnregisterEventHotKey(hotKey)
            self.hotKey = nil
        }
        if let eventHandler {
            RemoveEventHandler(eventHandler)
            self.eventHandler = nil
        }
        currentCombination = nil
        Self.action = nil
    }

    deinit {
        if let hotKey {
            UnregisterEventHotKey(hotKey)
        }
        if let eventHandler {
            RemoveEventHandler(eventHandler)
        }
    }
}

private extension UInt32 {
    init(ascii character: Character) {
        self = character.asciiValue.map(UInt32.init) ?? 0
    }
}
