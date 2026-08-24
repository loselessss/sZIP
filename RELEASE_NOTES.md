# sZIP 1.5.6 Release Notes

Release date: 2026-08-24

## 1.5.0 Highlights

- Choose the auto-extract watch folder.
- Set the maximum auto-extract archive size in MB.
- Optionally delete the source archive after auto extraction completes.
- Review auto-extract results in a separate audit window.
- Improved filename recovery for ZIP files created on macOS.

## 1.5.1 Fixes

- Fixed cases where the installer did not appear after the update download step.
- Show the installer path and error message when launching the installer fails.

## 1.5.2 UI Cleanup

- Removed the large icon area from the top of the main window.
- Reorganized archive open, create, extract, update, audit, and auto-extract actions into a ribbon toolbar.
- Made the auto-extract state visible as `On` / `Off`.

## 1.5.3 Fixes

- Fixed a dialog close issue that prevented the installer launch step after update download completed.

## 1.5.4 UI Cleanup

- Added crisp vector icons to the main ribbon toolbar actions.

## 1.5.5 UI Cleanup

- Aligned the auto-extract toggle with the ribbon button row.
- Reduced blurry ribbon edges and thin lines at HiDPI scaling.

## 1.5.6 Explorer Menu and Installer Cleanup

- Added `sZIP Extract` and `sZIP Smart Extract` to the archive context menu.
- Manual `Smart Extract` now sends the window to the tray after the operation succeeds.
- Installers downloaded by the updater are cleaned up after installation completes.
