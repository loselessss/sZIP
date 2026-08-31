# sZIP 1.9.0 Release Notes — Draft (2026-08-31)

## MSIX Preview

- Added an MSIX packaging preview for Microsoft Store and signed direct distribution, alongside the existing EXE installer.
- MSIX builds use their own update channel instead of launching the EXE updater.
- MSIX installation manages Explorer menus and file associations; automatic startup is controlled in Windows Settings.
- MSIX settings are stored independently of the installed package version.

This is not a published MSIX release. Signing, Store identity configuration and installed-package validation remain pending. Existing EXE installations are not automatically migrated or removed.
