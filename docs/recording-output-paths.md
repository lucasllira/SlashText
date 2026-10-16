# MP4 output path preflight (#154)

MP4 recording validates both the chosen final name and the private temporary
name before creating directories, normalizing recording preferences, reserving
the native session or constructing the native backend. The temporary name adds
44 UTF-16 code units to the final path; collision suffixes also consume space.
Runtime paths are resolved to absolute paths before the native queue, so a later
working-directory change cannot redirect them. Lengths also reject a long
supplied/extended spelling at the validation boundary. Existing
files, Unicode spelling, preferences and output templates are preserved.

The supported compatibility budget is 259 UTF-16 code units per complete path.
A path outside it produces an actionable localized message in Portuguese,
English or Spanish asking for a shorter folder/name. It leaves the service idle
and permits another attempt. Relative and extended-prefix spelling cannot bypass
the budget. The existing public media-path helper keeps its behavior for GIF and
other callers; only MP4 uses the deferred-directory preflight overload.

## Why a compatibility budget

The pinned 7.0.1 native code calls legacy `PathRemoveFileSpecW` and
`PathFindExtensionW`, whose documented input contracts use MAX_PATH. Windows
extended-file support alone does not prove the whole recorder/callback/recovery
and external viewer flow supports arbitrary long paths. The app does not change
registry policy, its manifest, saved paths or library binaries for this fix.

On this machine, a direct real native recording with the app's manifest succeeded
at 259 characters and failed at 260/261/350 without a prefix. Extended-prefix
recordings succeeded locally; isolated API probes hosted by Windows PowerShell
also accepted long paths. These are recorded observations, not a claim that the
prefix always fails or full extended/UNC support is implemented. The exact
failing native operation was not instrumented. This release uses a predictable
preflight contract rather than depending on that host-specific difference.

References: [PathRemoveFileSpecW](https://learn.microsoft.com/en-us/windows/win32/api/shlwapi/nf-shlwapi-pathremovefilespecw),
[PathFindExtensionW](https://learn.microsoft.com/en-us/windows/win32/api/shlwapi/nf-shlwapi-pathfindextensionw),
[Windows path limits](https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation).

## Validation

`--recording-output-path-smoke <output>` on the smoke executable verifies 143
checks: final/temporary 258/259/260/261/350 boundaries, UTF-16 including a surrogate
pair, UNC/extended strings without network access, blank-directory rejection,
localization, zero backend
creation and no rejected directories, unchanged preferences, collision-file
preservation and successful retry/finalization on the same service. These are
inert backend tests included in the general CI suite.

`SlashDesk.exe --recording-output-path-native-smoke <short-output-root>` is an
explicit native fixture. It records only its own generated opaque window, with
both audio sources disabled and verified absent from recorder options. ASCII and
Unicode cases at 260/261/350 are rejected without backend/directory creation;
259-unit temporaries produce real MP4s with a positive-duration video track, no
audio track and exclusive file access after stop. Use a fresh short fixture root
so the intended filename lengths fit. Real UNC shares, other media players and
all Windows versions are not certified by this local fixture. No personal media
or driver fault is needed; this scoped fix has no mandatory manual pilot test.
