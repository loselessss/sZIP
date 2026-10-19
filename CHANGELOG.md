# sZIP 1.11.4 (2026-10-19)

## Changes

- The main window title bar now shows the current version.
- Set the automatic extraction watch interval in seconds by typing a value or using the up and down arrows in Settings.
- Improved the size and text alignment of the language selector in Settings.
- The GitHub edition selects Explorer menu integration by default and remembers when you turn it off.
- Removed Check for updates from the Store edition's tray menu.

# sZIP 1.11.3 (2026-10-18)

## Changes

- Updated the archive engine to SharpCompress 0.50.5 for split ZIP compatibility, encrypted RAR5 improvements, and archive parsing fixes.

# sZIP 1.11.2 (2026-10-08)

## Changes

- Fixed opening and extracting split ZIP archives with matching .z01, .z02, ... and .zip parts.
- Incomplete extraction output now reports an error instead of leaving a truncated file.

# sZIP 1.11.1 (2026-10-08)

## Changes

- Closing and reopening the main window now shows an empty archive list and clears the previous archive and password after any active operation finishes.

# sZIP 1.11.0 (2026-10-08)

## Changes

- Double-click an archive entry, press Enter, or choose Preview to view file or folder contents.
- Added image/text/media previews and Windows preview-handler integration, with hexadecimal fallback for other formats and an explicit option to open document copies in associated apps.

# sZIP 1.10.2 (2026-10-07)

## Changes

- Retry failed, skipped, or cancelled extractions from automatic extraction history and enter a password when needed.
- Open output folders directly from history.
- Long paths are shortened to fit and shown in full on hover.

# sZIP 1.10.1 (2026-09-26)

## Changes

- Added a link to the public privacy policy in Settings.
- Archives created or edited by sZIP in a watched folder are no longer automatically extracted.
- Tightened the Settings window's vertical spacing and removed excess empty space below the language and automatic-extraction options.

# sZIP 1.10.0 (2026-09-13)

[한국어](CHANGELOG.ko.md)

## Changes

- Expanded the compression progress card with item count, processed size, speed, elapsed time, and estimated remaining time.
- Large 7Z files now update compression progress continuously while the current file is being processed.

## Known Issues

- sZIP does not appear in the Windows 11 primary context menu. Use **Show more options** to access the sZIP menu.
