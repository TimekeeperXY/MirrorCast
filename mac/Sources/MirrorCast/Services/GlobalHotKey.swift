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
    private static let signature = OSType(
        UInt32(ascii: "M") << 24
            | UInt32(ascii: "C") << 16
            | UInt32(ascii: "A") << 8
            | UInt32(ascii: "S"))

    nonisolated(unsafe) private static var actions: [UInt32: () -> Void] = [:]
    nonisolated(unsafe) private static var nextID: UInt32 = 1

    private var hotKeys: [String: EventHotKeyRef] = [:]
    private var ids: [String: UInt32] = [:]
    private var combinations: [String: HotKeyCombination] = [:]
    private var handlers: [String: () -> Void] = [:]
    private var eventHandler: EventHandlerRef?

    init() {
        installHandler()
    }

    @discardableResult
    func register(_ combination: HotKeyCombination,
                  action: @escaping () -> Void) -> Bool {
        register(key: "primary", combination: combination, action: action)
    }

    @discardableResult
    func register(key: String,
                  combination: HotKeyCombination,
                  action: @escaping () -> Void) -> Bool {
        unregister(key: key)
        installHandler()

        let id = Self.nextID
        Self.nextID += 1
        let identifier = EventHotKeyID(signature: Self.signature, id: id)
        var hotKey: EventHotKeyRef?
        let status = RegisterEventHotKey(
            combination.keyCode,
            combination.modifiers,
            identifier,
            GetApplicationEventTarget(),
            0,
            &hotKey)
        guard status == noErr, let hotKey else { return false }

        hotKeys[key] = hotKey
        ids[key] = id
        combinations[key] = combination
        handlers[key] = action
        Self.actions[id] = action
        return true
    }

    func replace(with combination: HotKeyCombination) -> Bool {
        guard let action = handlers["primary"],
              let previous = combinations["primary"]
        else { return false }

        if register(key: "primary", combination: combination, action: action) {
            return true
        }
        _ = register(key: "primary", combination: previous, action: action)
        return false
    }

    func unregister(key: String) {
        if let hotKey = hotKeys.removeValue(forKey: key) {
            UnregisterEventHotKey(hotKey)
        }
        if let id = ids.removeValue(forKey: key) {
            Self.actions.removeValue(forKey: id)
        }
        combinations.removeValue(forKey: key)
        handlers.removeValue(forKey: key)
    }

    func unregisterPresentationKeys() {
        ["f1", "f2", "f3", "f4", "escape"].forEach(unregister(key:))
    }

    func unregister() {
        for hotKey in hotKeys.values { UnregisterEventHotKey(hotKey) }
        for id in ids.values { Self.actions.removeValue(forKey: id) }
        hotKeys.removeAll()
        ids.removeAll()
        combinations.removeAll()
        handlers.removeAll()
        if let eventHandler {
            RemoveEventHandler(eventHandler)
            self.eventHandler = nil
        }
    }

    private func installHandler() {
        guard eventHandler == nil else { return }
        var eventType = EventTypeSpec(
            eventClass: OSType(kEventClassKeyboard),
            eventKind: UInt32(kEventHotKeyPressed))
        InstallEventHandler(
            GetApplicationEventTarget(),
            { _, event, _ in
                guard let event else { return OSStatus(eventNotHandledErr) }
                var identifier = EventHotKeyID()
                let status = GetEventParameter(
                    event,
                    EventParamName(kEventParamDirectObject),
                    EventParamType(typeEventHotKeyID),
                    nil,
                    MemoryLayout<EventHotKeyID>.size,
                    nil,
                    &identifier)
                guard status == noErr, identifier.signature == GlobalHotKey.signature,
                      let action = GlobalHotKey.actions[identifier.id]
                else { return OSStatus(eventNotHandledErr) }
                DispatchQueue.main.async { action() }
                return noErr
            },
            1,
            &eventType,
            nil,
            &eventHandler)
    }

    deinit {
        for hotKey in hotKeys.values { UnregisterEventHotKey(hotKey) }
        if let eventHandler { RemoveEventHandler(eventHandler) }
    }
}

private extension UInt32 {
    init(ascii character: Character) {
        self = character.asciiValue.map(UInt32.init) ?? 0
    }
}
