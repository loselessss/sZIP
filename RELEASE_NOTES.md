# sZIP 1.9.0 Release Notes (2026-08-31)

## Available Downloads

- This release provides the EXE installer and portable ZIP. Existing EXE installations keep their current update method.
- Microsoft Store and full-app MSIX downloads are not available yet. This release does not resolve the known Windows 11 primary context menu limitation for EXE installations.

## Upcoming MSIX Support

- Added an MSIX packaging preview for Microsoft Store and signed direct distribution, alongside the existing EXE installer.
- MSIX builds use their own update channel instead of launching the EXE updater.
- MSIX installation manages Explorer menus and file associations; automatic startup is controlled in Windows Settings.
- MSIX settings are stored independently of the installed package version.

This is not a published MSIX release. Signing, Store identity configuration and installed-package validation remain pending. Existing EXE installations are not automatically migrated or removed.
