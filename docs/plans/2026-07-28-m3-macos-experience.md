# MirrorCast macOS M3 Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add menu bar operation, a global start/stop shortcut, persistent preferences, and a compact first-run guide to the macOS app.

**Architecture:** Keep the existing AppKit lifecycle and SwiftUI control panel. `AppDelegate` owns native menu bar and Carbon hotkey services, while `AppState` remains the source of truth for mirroring and persists user-facing preferences through `UserDefaults`.

**Tech Stack:** Swift 5.9, AppKit, SwiftUI, ScreenCaptureKit, Carbon, UserDefaults

---

### Task 1: Persist User Preferences

**Files:**
- Create: `mac/Sources/MirrorCast/Services/PreferencesStore.swift`
- Modify: `mac/Sources/MirrorCast/AppState.swift`
- Modify: `mac/Sources/MirrorCast/Mirror/MirrorScaleMode.swift`

**Steps:**
1. Define typed keys for cursor visibility, scale mode, target display, last source app/title, and onboarding completion.
2. Restore scale mode and cursor settings when `AppState` initializes.
3. Restore the target display and best matching source window after enumeration.
4. Save values whenever a user-facing preference or successful source selection changes.
5. Run `./mac/build.sh`; expect a successful release build.

### Task 2: Add Menu Bar Operation

**Files:**
- Create: `mac/Sources/MirrorCast/Services/StatusItemController.swift`
- Modify: `mac/Sources/MirrorCast/AppDelegate.swift`
- Modify: `mac/Sources/MirrorCast/Main.swift`

**Steps:**
1. Create an `NSStatusItem` with display, start/stop, refresh, onboarding, and quit actions.
2. Keep menu labels and icon state synchronized with `AppState`.
3. Make closing the control panel hide it while the app remains in the menu bar.
4. Run the app and verify the panel can be reopened from the menu.

### Task 3: Register the Global Shortcut

**Files:**
- Create: `mac/Sources/MirrorCast/Services/GlobalHotKey.swift`
- Modify: `mac/Sources/MirrorCast/AppDelegate.swift`

**Steps:**
1. Register the persisted shortcut, defaulting to `Control + Option + M`, with Carbon `RegisterEventHotKey`.
2. Add an AppKit-backed shortcut recorder that accepts modified key combinations and supports Escape to cancel.
3. Reject conflicts without replacing the currently working shortcut.
4. Persist the accepted key code, modifiers, and display label.
5. Route the callback to start/stop mirroring and keep walkthrough text synchronized.
6. When setup is incomplete, show the panel and expose the current status message.
7. Unregister the hotkey during termination.
8. Build and verify registration succeeds without Accessibility permission.

### Task 4: Add First-Run Guidance

**Files:**
- Create: `mac/Sources/MirrorCast/UI/OnboardingView.swift`
- Modify: `mac/Sources/MirrorCast/UI/ControlPanelView.swift`
- Modify: `mac/Sources/MirrorCast/AppState.swift`

**Steps:**
1. Before authorization, show a dedicated screen-recording permission page.
2. After authorization and restart, show a Windows-style spotlight walkthrough over the control panel.
3. Walk through source window, target display, scale mode, start mirroring, and Mac-only click-to-switch behavior.
4. Persist completion only after finishing or skipping the walkthrough.
5. Let the menu bar replay the walkthrough later.
6. Verify the spotlight and teaching card fit in the existing 440-point control panel.

### Task 5: Final Verification and Documentation

**Files:**
- Modify: `mac/README.md`

**Steps:**
1. Document M3 features and shortcut behavior.
2. Run `git diff --check`.
3. Run `./mac/build.sh`.
4. Launch the built app, inspect its control panel, and confirm the process remains alive after closing the panel.
