# MirrorCast README Refresh Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the Windows-centric, duplicated documentation with a clear cross-platform GitHub README and concise platform references.

**Architecture:** Keep the root README focused on product value, downloads, installation, usage, platform differences, and contribution. Move macOS build details to `mac/README.md` and retain `mac/INSTALL.md` as the complete Gatekeeper and permission troubleshooting guide.

**Tech Stack:** GitHub-flavored Markdown

---

### Task 1: Rewrite the Root README

**Files:**
- Modify: `README.md`

**Steps:**
1. Present Windows and macOS with equal prominence.
2. Add current release links and system requirements.
3. Include the correct Windows EXE and macOS DMG installation flows.
4. Describe common usage and platform-specific behavior without mixing implementation details.
5. Remove stale milestones, Windows-only claims presented as universal, and repeated prose.

### Task 2: Simplify macOS Developer Documentation

**Files:**
- Modify: `mac/README.md`

**Steps:**
1. Remove M1-M4 progress history.
2. Keep ScreenCaptureKit architecture, source build, DMG packaging, and known distribution constraints.
3. Link to the root README and full installation guide.

### Task 3: Verify Documentation

**Steps:**
1. Check all relative and release links.
2. Confirm macOS filenames and commands match the v1.2.0 release.
3. Run `git diff --check`.
4. Review heading order, tables, and code fences.
