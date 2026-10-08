# sZIP

[한국어](README.ko.md)

sZIP is a lightweight archive utility for Windows. It creates and extracts archives, automatically extracts new downloads from a folder you choose, and integrates with File Explorer.

It runs on Windows 10 and 11 and supports both Korean and English.

[Get it from the Microsoft Store](https://apps.microsoft.com/detail/9NX8XSVB054T?hl=en-us&gl=KR&ocid=pdpshare)

## What You Can Do

- Create ZIP and 7Z archives from files or folders.
- Open and extract ZIP, 7Z, RAR, TAR, GZ, and TGZ/TAR.GZ archives.
- Automatically extract new archives from a folder and its subfolders that you choose to watch.
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

From the history list, retry failed or skipped extractions, enter a password when needed, or open an existing output folder. Retries use Smart Extract and follow the original archive deletion setting.

## Windows Integration

Explorer integration adds an sZIP submenu for Smart Extract, Extract Here, opening archives, quick ZIP and 7Z compression, and compression settings. Multiple files can be compressed together, and supported archive formats can be associated with sZIP.

Closing the main window keeps sZIP available in the system tray. Use the tray menu to reopen the app, check for updates, or exit completely.

## Installation

Install sZIP from the [Microsoft Store](https://apps.microsoft.com/detail/9NX8XSVB054T?hl=en-us&gl=KR&ocid=pdpshare) or [GitHub Releases](https://github.com/loselessss/sZIP/releases/latest). The Microsoft Store provides stable releases only. GitHub Releases also provides a per-user EXE installer, which normally does not require administrator privileges, and a portable ZIP. Matching source, SHA-256 checksums and dependency/build information are bundled in the extras ZIP in the same release Assets.

The installer can add a desktop shortcut, launch sZIP with Windows, register Explorer menus, and associate supported archive formats.

## Language and Updates

In Settings, choose **Use Windows language**, **Korean**, or **English**. The selected language is also used for update information.

sZIP checks GitHub Releases for updates and verifies the downloaded installer size and SHA-256 digest before starting installation. Updates can be installed later or skipped by version.

## Project Information

- [Version history](CHANGELOG.md)
- [Release notes](RELEASE_NOTES.md)
- [Third-party notices](THIRD-PARTY-NOTICES.md)
- [MIT License](LICENSE)


## MSIX distribution preview

The development branch includes full-MSIX packaging for Microsoft Store and signed direct downloads. It does not replace the EXE installer yet. See the [MSIX distribution guide](packaging/msix/README.md) for signing requirements and remaining installation checks. Preview/test MSIX artifacts are not ready for end-user installation.
