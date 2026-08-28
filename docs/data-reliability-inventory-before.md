# SlashDesk data reliability inventory - before

Base used for this independent reliability batch: approved functional hotfix tree from PR #26 (`207d21f5b947e2c49a3869aa80b94e36641e6a21`). The hotfix is not in `main`, so this branch is stacked on `agent/slashdesk-functional-reliability`. No capture, Salesforce, expansion, suggestions, or redesign files are part of this inventory.

## Persistent data

| Data | Path under the selected data root | Reader | Writer | Current risk |
|---|---|---|---|---|
| Snippets and categories | `snippets.md` | `SnippetMarkdownRepository` | `SnippetMarkdownRepository` and import flow | Per-operation temporary file, but no shared per-path serialization; a malformed file throws during the single startup block. |
| Settings | `settings.json` | `JsonFileStore<AppSettings>` | `MainWindow` event handlers | Invalid JSON silently becomes defaults; independent `async void` handlers can save concurrently; slider changes cause repeated saves. |
| Usage | `usage.json` | `UsageService` | `UsageService` | Invalid JSON and I/O failures are silently ignored; its private lock does not coordinate with backup/restore. |
| Capture history | `capture-history.json` | `CaptureHistoryStore` | `CaptureService` | Invalid whole-file JSON silently becomes an empty history; multiple history mutations serialize mutable state without a shared coordinator. |
| Snippet images | `assets/**` | rich-text renderer/editor | image insertion and file system | Included by the current default backup path, but not represented by hashes in the manifest and not managed for orphans. |
| Backups | `Backups/SlashDesk-backup-*.zip` | `BackupService` | `BackupService` | ZIP is created directly at its final name; validation checks only manifest version and presence of one recognized file; restore overwrites active files incrementally. |
| Logs | `Logs/**` | support/diagnostics | `AppDiagnosticLog`, `SafeDiagnosticLog` | Technical local diagnostics; not part of backups. |
| Update state | `update-state.json`, `Updates/**` | updater | updater | Outside this PR's backup/restore scope. |

Installed data root is `%LocalAppData%/SlashDesk`; portable data root is `<SlashDesk.exe>/SlashDeskData`, resolved centrally by `AppDataEnvironment`/`AppPaths`.

## Startup order before the change

`App.OnStartup` detects the distribution, probes portable write access, runs `DataMigrationService`, initializes diagnostics, then opens `MainWindow`. `MainWindow_OnLoaded` performs settings, theme, usage, Quick Accent, capture history, snippets, daily backup, text monitoring, Quick Accent, capture hotkeys, onboarding, and update checks inside one `try`. Consequently, a snippet load failure skips later independent services and leaves startup only partially initialized.

## Backup and restore before the change

The default backup includes the four recognized root files plus `assets/**` and a schema-1 manifest. The manifest lists only root file names, with no size or SHA-256. The final ZIP is written in place. Restore creates a pre-restore backup and then extracts recognized entries directly over active data. It has a destination-root prefix check, but performs no all-entry staging, duplicate-entry rejection, aggregate size limit, JSON/snippet validation, hash verification, or transactional rollback. Legacy archives without a manifest cannot pass `ValidateSnapshot`, although restore itself does not require one.

Retention is implemented as the seven newest matching ZIP files; it is a count of backup files, not seven calendar days.

## Concurrency before the change

- `JsonFileStore`, `SnippetMarkdownRepository`, and `CaptureHistoryStore` each create unique temporary files, but have no shared per-path gate.
- `UsageService` serializes only its own record methods.
- `CaptureService` mutates one `List<CaptureRecord>` from capture, recording, delete, edit, cleanup, and load flows without a common gate or immutable render snapshot.
- Backup and restore do not coordinate with any store.
- Settings handlers are `async void`; Quick Accent slider movement writes on every value change and exceptions can reach the Dispatcher.

## Existing effective coverage

The smoke suite covers snippet round trips and trigger compatibility, basic usage persistence, creation/listing of daily and manual backups, basic manifest presence, portable/installed path resolution, migration of legacy `capture-history.json`, preservation of simultaneous old/new roots, and tolerance of one corrupt capture-history item. It does not exercise backup hashes, recursive assets round trip, transactional restore/rollback, path traversal, duplicate ZIP entries, uncompressed-size limits, corruption preservation, per-path write ordering, startup isolation, clipboard-only history actions, orphan assets, or multi-monitor Quick Accent placement.

## UI/service touch points allowed for this batch

- `MainWindow.xaml.cs`: resilient module startup, safe/debounced settings persistence, history action availability, backup/restore feedback, content search verification, and manual asset maintenance action.
- `MainWindow.xaml`: at most one maintenance action in the existing backup card; no layout redesign.
- `QuickAccentWindow`: placement only; appearance remains unchanged.
- Persistence, migration, backup, history, diagnostics, and test services under `Services/`.

