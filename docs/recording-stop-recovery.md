# Recording stop recovery (#106)

## Contract

MP4 startup, pause/resume, audio changes, Stop and Dispose stay on a single native queue. A recording service is single-use. A process-wide reservation starts before queuing native startup and is released only after Dispose returns successfully (or no backend was created). New sessions are rejected while that reservation exists.

After a Stop request, or a completion/failure callback that starts cleanup, a 12-second watchdog checks native release. A missing callback, blocked native call or failed Dispose keeps the completion task pending and enters `StopUnconfirmed`. `IsRecording` remains true. The private per-session marker in `RecordingRecovery-v1` records that state where storage is writable; paths and filenames remain excluded from diagnostic logs. Dispose failure immediately enters uncertainty without retrying Dispose or dropping the backend reference. Late pause/resume responses cannot restore an apparently safe state.

The floating control stays visible, identifies possible continued screen/audio capture, disables recording commands and exposes **Exit SlashDesk**. Closing the control or requesting normal app exit cannot hide a pending MP4 session. Stop remains available during startup. GIF behavior is unchanged.

Emergency exit requires a themed confirmation with Cancel as the default, an explicit Exit SlashDesk action, the temporary path and a warning about unsaved edits and an incomplete video. Acceptance is rechecked against the same still-uncertain session. The action terminates **only the current process**, without calling the potentially blocked native cleanup queue. It does not delete the temporary file or automatically restart the app. If termination throws, the warning remains and directs the user to Task Manager.

If native cleanup eventually succeeds, the control can close, the reservation is released and the temporary MP4 remains at its original path. It is not promoted to a successful capture after a timeout. Normal success still validates and moves the MP4 before adding history.

## Decision and limits

The existing recorder runs inside the app. A user-confirmed emergency process exit is the smallest containment mechanism that addresses this issue without moving capture, audio controls and codec configuration to a new IPC architecture immediately. The cost is losing unsaved in-app edits and possibly an incomplete MP4; confirmation explicitly explains both. Isolating the recorder in a supervised worker would allow killing it without exiting the app and remains a possible later architectural improvement.

Windows termination stops the process threads and requests cancellation of pending I/O. Driver cleanup can take time; this is not a guarantee about defective hardware/kernel drivers. See [Microsoft's TerminateProcess contract](https://learn.microsoft.com/windows/win32/api/processthreadsapi/nf-processthreadsapi-terminateprocess). Preserving bytes does not guarantee a playable MP4. No safe emergency path promises to finish its encoder normally.

## Automated evidence

- Full smoke suite: 12 fresh-process recording scenarios using inert files, including normal completion, duplicate callbacks, absent callback, blocked startup/Stop/Dispose/pause/resume, callback without user Stop, failure callback, Dispose exception and emergency self-termination.
- Each uncertain case checks pending completion, the private marker, blocked new sessions and serialized cleanup. Successful completion removes its own marker before notifying callers; late failure marks the partial as interrupted without deleting it. Late release permits a new session. The parent observes actual emergency child exit and can reopen the preserved file exclusively.
- Recording audio UI smoke additionally checks the recovery control in pt-BR/en/es, light/dark, system-close blocking, exit intent without implicit acceptance, layout bounds and late cleanup. No real capture is used in these automated fault cases.

- A real MainWindow/toolbar fixture with an inert backend exercises the current operation reservation: while stop is uncertain, screen/tray commands and maintenance stay blocked; late cleanup releases the operation without adding successful history. This runs in all three languages and both themes, alongside the existing preparation/cancellation and MP4 destination regressions.

## Pilot validation before acceptance

Use a fresh isolated pilot folder and a synthetic window with no private content. For each available configuration (no audio, PC only, microphone only, both), record a short sample, pause/resume, toggle each configured source, stop, then play the MP4 and check timing and expected tracks. Start a second recording after normal stop. Check the Windows microphone-use indicator after stop, allowing for other apps that may also use it. Record unavailable devices as skipped, not passed.

The automated abnormal cases do not establish release of physical microphones/loopback under a real driver failure. That hardware validation remains an explicit acceptance gate; do not deliberately hang drivers or record ambient/private material as an automatic test. Use an approved controlled environment for native failure injection, then verify process exit and device activity. Until that evidence exists, report implementation and automated validation as complete, with physical validation pending.

## Native controlled stop gate with ScreenRecorderLib 7.0.1

The #151 pilot's normal four-source audio and region/window checks were accepted
by the user. This branch retains that migration and verifies the remaining stop
recovery paths separately. `scripts/recording-native-stop-smoke.ps1` uses fresh
owned helper processes, each recording only its own generated opaque window.
It never attaches to or terminates another SlashDesk instance.

Three scenarios exercise the real native recorder: hold Stop at a test adapter
before forwarding it, then release it late; hold it and self-terminate through
the production emergency method; start and normally stop a fresh successor.
The adapter does not fake the native recording callbacks or file. The checks
require persistent uncertainty, visible controls despite system close, blocked
successors, a real native file lock before exit, exact child process lifetime,
exclusive file reopening after exit, preserved partial bytes and session marker,
and a successful native successor. Late/normal final files must contain the
expected video/audio tracks. Emergency partials may remain unplayable.

Default automation records no audio; `-Unattended` is permitted only without
audio. `-AudioBoth` requires interactive Enter consent and adds the same three
scenarios with PC/microphone enabled. Missing hardware reports INCOMPLETE. The
user should use fictitious sound/voice, then check the Windows microphone-use
indicator after completion, accounting for other applications using it. Files
stay local; share only the textual outcome.

This is controlled failure injection with a real active native recorder; the
injected hold is in the adapter. It does not reproduce a defective kernel
driver or prove instantaneous physical driver cleanup. Such faults are not a
safe end-user test. Production UI confirmation/cancel, normal-exit blocking,
and reservation integration remain covered by the inert WPF matrix. These
different evidence levels must stay explicit in the issue and release review.
