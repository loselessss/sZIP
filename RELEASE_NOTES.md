# sZIP 1.11.4 Release Notes (2026-10-19)

## 1.11.4 Changes

- The main window title bar now shows the current version.
- Set the automatic extraction watch interval in Settings, starting at 1 second. Type a value or adjust it with the up and down arrows.
- Improved the size and text alignment of the language selector in Settings.
- The GitHub edition selects Explorer menu integration by default and remembers when you turn it off.
- Removed Check for updates from the Store edition's tray menu. Microsoft Store manages its updates.

## 1.11.3 Fixes

- Updated SharpCompress to 0.50.5, including fixes for uppercase split ZIP filenames, encrypted RAR5 header/comment handling and asynchronous RAR5 extraction.
- The updated engine reuses encrypted RAR5 keys to reduce repeated processing, strengthens GZip/7Z header validation, and fixes TAR symbolic-link handling and internal stream positioning.

## 1.11.2 Fixes

- Fixed opening and extracting split ZIP archives with .z01, .z02, ... and .zip parts in the same folder.
- Missing split files now display a helpful message. Incomplete extraction output is detected and removed.

## 1.11.1 Fixes

- Closing and reopening the main window now starts with an empty archive list. The previous archive and password are cleared after any active operation finishes.

## 1.11.0 Highlights

- Double-click a file or folder inside an archive, press Enter, or choose Preview to inspect its contents.
- Images, text, and media can be viewed directly. PDF and Office previews use compatible Windows preview handlers installed on your PC.
- Other formats display file information and hexadecimal contents. Document copies can be opened in associated apps.
- Previewing leaves the original archive unchanged. Executable files and scripts are not launched from the preview window.

## Known issue — GitHub edition only

- On Windows 11, sZIP's compression/extraction commands may appear only under **Show more options**. Install the [Microsoft Store edition](https://apps.microsoft.com/detail/9NX8XSVB054T?hl=en-us) to resolve this and use sZIP from the default right-click menu. The Store edition is not affected by this issue.
