# Release Note Rules

## Purpose

Release notes are user-facing text shown in the app update window and on GitHub Releases. Include only the information a user needs to decide whether to update.

## CHANGELOG.md

- Describe only changes in the current release.
- For patch releases, include only what changed in that patch.
- Do not repeat major feature descriptions from older versions.
- Do not mention internal work such as CI, builds, tests, automated verification, or workflow changes.
- Include only user-visible features, fixes, and compatibility changes.

## RELEASE_NOTES.md

- This is the user-facing body shown in the update window.
- Keep English in `RELEASE_NOTES.md` and Korean in `RELEASE_NOTES.ko.md`.
- Update both language files for every release.
- GitHub Releases combine both files with language markers; the updater selects Korean for a Korean Windows display language and English otherwise.
- If a user may update directly to a patch release after skipping a previous feature release, include both the previous feature release highlights and the current patch fixes.
- Example: if `1.5.1` follows `1.5.0`, include `1.5.0 Highlights` and `1.5.1 Fixes`.
- Do not mention internal verification, CI, build environment changes, or workflow hardening.
- Do not include installer filenames, hashes, or test pass/fail details unless they directly help the user decide whether to update.
- If a release has no user-visible changes, say that directly instead of describing internal preparation as a feature.

## Style

- Write concise English.
- Include the version and release date in the title.
- Phrase entries from the user's point of view.
- Use clear verbs such as "Fixed", "Added", and "Supported".
- Prefer visible user impact over implementation details.
