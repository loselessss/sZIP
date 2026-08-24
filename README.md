# sZIP 1.5.6

sZIP is a free archive utility for Windows. It is built on .NET Framework 4.8, so it can run on typical Windows 10/11 PCs without a C# development environment or .NET SDK. Archives smaller than the configured size limit can be extracted automatically.

## Key Features

- Create ZIP and 7Z archives.
- Open and safely extract ZIP, 7Z, RAR, TAR, GZ, and TGZ/TAR.GZ archives.
- `Extract`: place contents directly in the selected folder or next to the archive.
- `Smart Extract`: keep a single top-level folder as-is, or organize mixed contents into a folder named after the archive.
- `Extract Selected`: extract selected files or folders while preserving their paths.
- Preserve nested folders and empty folders inside archives.
- Watch the selected folder and subfolders, then auto-extract archives within the configured size limit.
- Check download completion stability and recover missed files every 10 seconds.
- Manual password input, cancellation, and conflict-free output naming.
- Tray resident mode, Windows startup support, and single-instance command routing.
- Fluent-style light/dark theme support with throughput, speed, and remaining-time progress.
- Explorer multi-select actions: `Compress with sZIP`, `sZIP Extract`, and `sZIP Smart Extract`.
- Native `IExplorerCommand` extension for the Windows 11 primary context menu.
- `Open with` registration for ZIP/7Z/RAR/TAR/GZ/TGZ extensions.
- GitHub Releases based automatic updates with SHA-256 verification before installation.
- Manual update checks from the main window and tray menu.

The window close button hides sZIP to the tray instead of exiting. To quit completely, right-click the tray icon and choose `Exit`.

## Installation and Distributions

The recommended installer is `sZIP_Setup_1.5.6.exe` from GitHub Releases. It installs to `%LOCALAPPDATA%\Programs\sZIP`, so administrator privileges are not required. `sZIP-1.5.6-net48.zip` is the portable distribution.

The installer can register a desktop shortcut, Windows startup, Explorer menus, and archive file associations. On Windows 11, it registers a sparse identity package and x64 `IExplorerCommand` extension for the primary context menu. On unsupported environments and Windows 10, legacy context menu entries remain available as a fallback.

## Update Policy

The installed app checks for updates 5 seconds after startup. After a successful check, it waits 24 hours before contacting GitHub again; while running, it re-evaluates every hour. Network failures are not recorded as successful checks, so the next hourly cycle can retry.

When an update is available, sZIP shows release notes and installer information. The installer is downloaded to a temporary `.part` file and only runs after its size and SHA-256 digest match the GitHub release asset. Users can install later, skip a specific version, or run `Check for Updates` from the tray menu at any time.

## Development and Automated Verification

Local installer builds require Inno Setup 6, Visual C++ x64 Build Tools, and the Windows 10/11 SDK.

Pushing a `v*.*.*` tag runs GitHub Actions to test, build the portable ZIP, build the Inno Setup installer, generate SHA-256 files, run an installer smoke test, and publish the GitHub Release.

## License

This project is licensed under the MIT License. Archive format support uses SharpCompress 0.50.1 under the MIT License; see `THIRD-PARTY-NOTICES.md` for details.

See [CHANGELOG.md](CHANGELOG.md) for version changes.
