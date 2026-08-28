# sZIP

[한국어](README.ko.md)

sZIP is a lightweight archive utility for Windows. It brings compression, extraction, automatic archive extraction, and Explorer integration together in one focused app.

It runs on Windows 10 and 11 and supports both Korean and English.

## What You Can Do

- Create ZIP and 7Z archives from files or folders.
- Open and extract ZIP, 7Z, RAR, TAR, GZ, and TGZ/TAR.GZ archives.
- Extract selected files and folders while preserving their paths.
- Rename files and folders inside ZIP and 7Z archives.
- Enter passwords for encrypted archives and cancel work in progress.
- Preserve nested and empty folders and avoid overwriting existing output.
- Follow operation progress with speed, throughput, and remaining time.

## Extraction Modes

- **Extract** places the archive contents directly in the selected destination.
- **Smart Extract** keeps a single top-level folder as-is. When an archive contains mixed items, it organizes them inside a folder named after the archive.
- **Extract Selected** extracts only the items selected in the archive list.

## Automatic Archive Extraction

sZIP can watch a folder and its subfolders for newly downloaded archives. Files within the configured size limit are extracted automatically after the download has finished.

Use the main ribbon to turn automatic archive extraction on or off. From Settings, you can choose the watch folder and size limit and decide whether the original archive should be deleted after successful extraction. The audit list shows completed and failed automatic operations.

## Windows Integration

Explorer integration adds an sZIP submenu for Smart Extract, Extract Here, opening archives, quick ZIP and 7Z compression, and compression settings. Multiple files can be compressed together, and supported archive formats can be associated with sZIP.

Closing the main window keeps sZIP available in the system tray. Use the tray menu to reopen the app, check for updates, or exit completely.

## Installation

Download the installer from [GitHub Releases](https://github.com/loselessss/sZIP/releases/latest). sZIP installs for the current user, so administrator privileges are normally not required. A portable ZIP is also provided.

The installer can add a desktop shortcut, launch sZIP with Windows, register Explorer menus, and associate supported archive formats.

## Language and Updates

In Settings, choose **Use Windows language**, **Korean**, or **English**. The selected language is also used for update information.

sZIP checks GitHub Releases for updates and verifies the downloaded installer size and SHA-256 digest before starting installation. Updates can be installed later or skipped by version.

## Project Information

- [Version history](CHANGELOG.md)
- [Release notes](RELEASE_NOTES.md)
- [Third-party notices](THIRD-PARTY-NOTICES.md)
- [MIT License](LICENSE)
