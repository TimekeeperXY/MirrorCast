# MirrorCast macOS DMG Release Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Produce and publish a reproducible arm64 DMG for MirrorCast v1.2.0.

**Architecture:** Reuse the existing release app build, stage the signed bundle beside an Applications symlink, and create a compressed read-only DMG with `hdiutil`. Keep generated artifacts out of Git and publish the verified DMG plus SHA-256 checksum through GitHub Releases.

**Tech Stack:** Swift Package Manager, Bash, codesign, hdiutil, GitHub CLI

---

### Task 1: Prepare Release Metadata

**Files:**
- Modify: `mac/Resources/Info.plist`
- Modify: `.gitignore`

**Steps:**
1. Set the app version to `1.2.0`.
2. Mark the app as a menu-bar utility.
3. Ignore generated `mac/dist/` artifacts.
4. Validate the plist with `plutil -lint`.

### Task 2: Add the DMG Packaging Script

**Files:**
- Create: `mac/package-dmg.sh`

**Steps:**
1. Run the existing release app build.
2. Verify the app signature and arm64 executable architecture.
3. Stage `MirrorCast.app` and an Applications symlink in a temporary directory.
4. Create a compressed UDZO DMG using `hdiutil`.
5. Verify the DMG and write a SHA-256 checksum file.

### Task 3: Document Packaging and Distribution

**Files:**
- Modify: `mac/README.md`
- Modify: `mac/INSTALL.md`

**Steps:**
1. Document the packaging command and output path.
2. State that this release targets Apple Silicon.
3. Keep Gatekeeper and ad-hoc signing limitations explicit.

### Task 4: Verify and Publish

**Steps:**
1. Run `./mac/package-dmg.sh`.
2. Mount the DMG read-only and verify the app and Applications symlink.
3. Copy the app out of the mounted image and verify its signature.
4. Commit and push the packaging changes through a PR.
5. Merge the PR, tag `v1.2.0`, create the GitHub Release, and upload the DMG and checksum.
