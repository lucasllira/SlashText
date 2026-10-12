# Recording stop recovery (#106)

## Contract

MP4 startup, pause/resume, audio changes, Stop and Dispose stay on a single native queue. A recording service is single-use. A process-wide reservation starts before queuing native startup and is released only after Dispose returns successfully (or no backend was created). New sessions are rejected while that reservation exists.

After a Stop request, or a completion/failure callback that starts cleanup, a 12-second watchdog checks native release. A missing callback, blocked native call or failed Dispose keeps the completion task pending and enters `StopUnconfirmed`. `IsRecording` remains true. The active diagnostic marker records that state where storage is writable. Dispose failure immediately enters uncertainty without retrying Dispose or dropping the backend reference. Late pause/resume responses cannot restore an apparently safe state.

The floating control stays visible, identifies possible continued screen/audio capture, disables recording commands and exposes **Exit SlashDesk**. Closing the control or requesting normal app exit cannot hide a pending MP4 session. Stop remains available during startup. GIF behavior is unchanged.

Emergency exit requires a themed confirmation with Cancel as the default, an explicit Exit SlashDesk action, the temporary path and a warning about unsaved edits and an incomplete video. Acceptance is rechecked against the same still-uncertain session. The action terminates **only the current process**, without calling the potentially blocked native cleanup queue. It does not delete the temporary file or automatically restart the app. If termination throws, the warning remains and directs the user to Task Manager.

If native cleanup eventually succeeds, the control can close, the reservation is released and the temporary MP4 remains at its original path. It is not promoted to a successful capture after a timeout. Normal success still validates and moves the MP4 before adding history.

## Decision and limits

The existing recorder runs inside the app. A user-confirmed emergency process exit is the smallest containment mechanism that addresses this issue without moving capture, audio controls and codec configuration to a new IPC architecture immediately. The cost is losing unsaved in-app edits and possibly an incomplete MP4; confirmation explicitly explains both. Isolating the recorder in a supervised worker would allow killing it without exiting the app and remains a possible later architectural improvement.

Windows termination stops the process threads and requests cancellation of pending I/O. Driver cleanup can take time; this is not a guarantee about defective hardware/kernel drivers. See [Microsoft's TerminateProcess contract](https://learn.microsoft.com/windows/win32/api/processthreadsapi/nf-processthreadsapi-terminateprocess). Preserving bytes does not guarantee a playable MP4. No safe emergency path promises to finish its encoder normally.

## Automated evidence

- Full smoke suite: 12 fresh-process recording scenarios using inert files, including normal completion, duplicate callbacks, absent callback, blocked startup/Stop/Dispose/pause/resume, callback without user Stop, failure callback, Dispose exception and emergency self-termination.
- Each uncertain case checks pending completion, the persistent marker, blocked new sessions and serialized cleanup. Late release permits a new session. The parent observes actual emergency child exit and can reopen the preserved file exclusively.
- Recording audio UI smoke additionally checks the recovery control in pt-BR/en/es, light/dark, system-close blocking, exit intent without implicit acceptance, layout bounds and late cleanup. No real capture is used in these automated fault cases.

## Pilot validation before acceptance

Use a fresh isolated pilot folder and a synthetic window with no private content. For each available configuration (no audio, PC only, microphone only, both), record a short sample, pause/resume, toggle each configured source, stop, then play the MP4 and check timing and expected tracks. Start a second recording after normal stop. Check the Windows microphone-use indicator after stop, allowing for other apps that may also use it. Record unavailable devices as skipped, not passed.

The automated abnormal cases do not establish release of physical microphones/loopback under a real driver failure. That hardware validation remains an explicit acceptance gate; do not deliberately hang drivers or record ambient/private material as an automatic test. Use an approved controlled environment for native failure injection, then verify process exit and device activity. Until that evidence exists, report implementation and automated validation as complete, with physical validation pending.
