# sZIP 1.9.1 (2026-09-13)

[한국어](CHANGELOG.ko.md)

## Changes

- Removed manual extraction limits on entry count and expansion ratio. Safety errors now include an explanation before technical details.

- Fixed manual extraction being blocked when the total extracted size exceeded 2 GiB or a single file exceeded 1 GiB. Automatic extraction retains its existing safety limits.

## Known Issues

- sZIP does not appear in the Windows 11 primary context menu. Use **Show more options** to access the sZIP menu.
