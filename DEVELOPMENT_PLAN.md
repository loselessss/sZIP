# sZIP Development Plan and Completion Log

## Goal

Provide a free Windows archive app that works without requiring users to install a separate development runtime. sZIP supports manual compression and extraction, and it can safely extract small archives automatically while watching the Downloads folder and its subfolders.

## Chosen Stack

- C# / WPF / .NET Framework 4.8
- SharpCompress 0.50.1
- Per-user Inno Setup 6 installer
- GitHub Actions and GitHub Releases distribution

## Completed Releases

- `0.1.0`: Initial ZIP listing, safe extraction, and download-folder watching.
- `0.2.0`: Dedicated icon, tray support, and saved settings.
- `0.3.0`: ZIP creation, drag and drop, watcher recovery, startup registration, and single-instance behavior.
- `1.0.0`: Multiple archive formats, password UI, recursive automatic archive extraction, Explorer integration, and safety limits.
- `1.0.1`: Shortened missing Downloads folder recovery checks to 10 seconds.
- `1.1.0`: Explorer multi-select compression.
- `1.2.0`: Explorer multi-select extraction.
- `1.3.0`: Installer, file associations, Paper Organizer-style update flow, and GitHub tag release automation.
- `1.4.0`: Fluent UI, Extract, Smart Extract, Extract Selected, Paper Organizer-style update flow, and Windows 11 modern context menu integration.

## Automatic Update Flow

1. Check 5 seconds after startup, and skip automatic checks if the last successful check was within 24 hours.
2. While running, reevaluate the check time every hour; network failures are not recorded as successful checks.
3. Validate the latest GitHub release version, release URL, and exact-version installer asset.
4. Stream the installer to a `.part` file.
5. Validate the asset size and GitHub `sha256:` digest.
6. Move only a verified installer to its final name, run it, and exit the app.
7. Let the user install later or skip a specific version.

## UI Modernization Status

The reference mockup for the modern extraction progress screen is [`szip-modern-progress.html`](szip-modern-progress.html). Version 1.4.0 carried this direction into shared WPF resources and the main progress surface.

### Scope

1. Progress, processed size, speed, remaining time, and current file display - complete.
2. Shared WPF resources for buttons, spacing, radius, colors, and typography - complete.
3. Shared design treatment for the main window, password dialog, and update dialog - complete.
4. Automatic Windows light/dark app theme support - complete.
5. Multi-select files and folders, plus extraction of selected folder children - complete.
6. Layout review across window widths and Windows scaling - manual QA pending.
7. Pause/resume - follow-up after reviewing safe cancellation and resume support in the archive libraries.

### Next Checks

1. Verify Windows 11 modern menu registration and removal from the installer.
2. Manually review scaling, keyboard access, and theme switching.
3. Consider safe operation pause/resume as a follow-up candidate.

## Windows 11 Explorer Integration

### Windows 11 context menu repair (1.8.0, implementation pending release)

- Restore the unsigned-package publisher marker in both package and application identities; missing metadata caused registration error 0x80073D2C.
- Label the primary submenu "Compress with sZIP" for ordinary files and folders; keep archive extraction commands under "sZIP".
- Show read-only registration status and an explicit repair action in Settings, with Korean/English error details and reinstall guidance.
- Run checks and repair off the UI thread; guard duplicate actions and keep the window open while registration changes are running. Repair applies immediately, independently of Save/Cancel.
- Force re-registration during explicit repair even if the package version matches. Preserve the classic-menu fallback when modern registration fails.
- Capture registration errors and timeouts in diagnostics, and test matching identities and file/folder registrations. Test generated PowerShell commands with fake Appx functions only; never mutate installed shell registration from automated tests.
- Before release, build the native extension and sparse package and verify installation, upgrade, multi-selection, and removal on Windows 11. Local source tests do not replace this check.
- Production package signing remains a follow-up: Microsoft documents unsigned packages as a testing mechanism. A signed distribution needs a trusted signing identity and matching application manifest; do not install trust certificates or change machine policy automatically.

## Verification Policy

- Run the full automated test suite and WPF Release build before committing.
- The tag workflow checks that project, installer, and tag versions match.
- Manual QA is performed when requested for production release milestones.
