import CoreGraphics
import Foundation

final class PreferencesStore {
    private enum Key {
        static let showsCursor = "showsCursor"
        static let scaleMode = "scaleMode"
        static let targetDisplayID = "targetDisplayID"
        static let sourceAppName = "sourceAppName"
        static let sourceWindowTitle = "sourceWindowTitle"
        static let completedWalkthrough = "completedWalkthroughV3"
        static let hotKeyCode = "hotKeyCode"
        static let hotKeyModifiers = "hotKeyModifiers"
        static let hotKeyLabel = "hotKeyLabel"
    }

    private let defaults = UserDefaults.standard

    var showsCursor: Bool {
        get {
            defaults.object(forKey: Key.showsCursor) == nil
                ? true
                : defaults.bool(forKey: Key.showsCursor)
        }
        set { defaults.set(newValue, forKey: Key.showsCursor) }
    }

    var scaleMode: MirrorScaleMode {
        get {
            guard let rawValue = defaults.string(forKey: Key.scaleMode),
                  let mode = MirrorScaleMode(rawValue: rawValue)
            else { return .fit }
            return mode
        }
        set { defaults.set(newValue.rawValue, forKey: Key.scaleMode) }
    }

    var targetDisplayID: CGDirectDisplayID? {
        get {
            guard defaults.object(forKey: Key.targetDisplayID) != nil else { return nil }
            return CGDirectDisplayID(defaults.integer(forKey: Key.targetDisplayID))
        }
        set {
            if let newValue {
                defaults.set(Int(newValue), forKey: Key.targetDisplayID)
            } else {
                defaults.removeObject(forKey: Key.targetDisplayID)
            }
        }
    }

    var sourceIdentity: (appName: String, title: String)? {
        get {
            guard let appName = defaults.string(forKey: Key.sourceAppName),
                  let title = defaults.string(forKey: Key.sourceWindowTitle)
            else { return nil }
            return (appName, title)
        }
        set {
            defaults.set(newValue?.appName, forKey: Key.sourceAppName)
            defaults.set(newValue?.title, forKey: Key.sourceWindowTitle)
        }
    }

    var completedWalkthrough: Bool {
        get { defaults.bool(forKey: Key.completedWalkthrough) }
        set { defaults.set(newValue, forKey: Key.completedWalkthrough) }
    }

    var hotKey: HotKeyCombination {
        get {
            guard defaults.object(forKey: Key.hotKeyCode) != nil,
                  defaults.object(forKey: Key.hotKeyModifiers) != nil,
                  let label = defaults.string(forKey: Key.hotKeyLabel)
            else { return .defaultValue }

            return HotKeyCombination(
                keyCode: UInt32(defaults.integer(forKey: Key.hotKeyCode)),
                modifiers: UInt32(defaults.integer(forKey: Key.hotKeyModifiers)),
                keyLabel: label)
        }
        set {
            defaults.set(Int(newValue.keyCode), forKey: Key.hotKeyCode)
            defaults.set(Int(newValue.modifiers), forKey: Key.hotKeyModifiers)
            defaults.set(newValue.keyLabel, forKey: Key.hotKeyLabel)
        }
    }
}
