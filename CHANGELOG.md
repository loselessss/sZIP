# sZIP 1.9.0 (2026-08-31)

[한국어](CHANGELOG.ko.md)

Available as an EXE installer and portable ZIP. Full-app MSIX distribution is not included, and the known Windows 11 primary context menu limitation for EXE installations remains.

## MSIX Preview

- Added an MSIX packaging preview for Microsoft Store and signed direct distribution, alongside the existing EXE installer.
- MSIX builds use their own update channel instead of launching the EXE updater.
- MSIX installation manages Explorer menus and file associations; automatic startup is controlled in Windows Settings.
- MSIX settings are stored independently of the installed package version.

MSIX production distribution is pending signing/Store setup and installed-package validation.
