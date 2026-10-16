# GIF timing under capture load (#147)

The recording clock starts when the session starts, excludes pauses and freezes
when Stop is requested. The first available image now represents the interval
from zero, including startup/capture latency. This holds that image for time for
which no earlier image is available; it cannot reconstruct missed screen content.
Subsequent distinct images keep their captured timestamps. Repeated images and
missed scheduling slots extend the held image rather than shortening the movie.
Draining the queue after Stop does not extend the active recording duration.

Delays share one budget: the stopped active clock rounded to centiseconds, with
a 20 ms minimum for the entire GIF. Each retained image still has at least 20 ms.
`GifFrameTimeline` clamps transition boundaries between the preceding boundary
plus that minimum and the budget reserved for all remaining images. This keeps
30 FPS fractional periods and jitter/short bursts from accumulating delay. It
can move a close transition earlier or later to fit the minimum, including the
last image. The final saved duration equals that one budget, rather than adding
a fresh minimum to every independently rounded interval. Encoding and preview
use the same resulting images and delay list.

When all images fit the minimum-delay budget, every distinct image is retained.
If they cannot fit, retain an evenly spaced ordered subset of source indices;
the first and last remain whenever at least two images fit. If the entire clock
only fits one 20 ms image, retain the first. This explicit selection trades
unrepresentable transitions for clock accuracy instead of stretching the GIF.
The ordinary 10/20/30 FPS cadence fits without selection. Unselected PNGs are
deleted after both pipeline workers stop; cancellation and the existing frame,
queue and temporary-byte limits remain unchanged.

The reported regression has boundaries 0/40/50/90/100/140 ms and stops at 150 ms.
Six independent minima produced 180 ms. The common-budget delays are now
4/2/3/2/2/2 centiseconds, totaling 150 ms while preserving all six images. A
95.1 ms final transition followed by Stop at 95.2 ms gives delays 8/2, totaling
100 ms, with both images retained. Tests require exact centisecond-budget
equality; the previous 20 ms assertion tolerance was tightened, not expanded.

The WPF GIF encoder does not support frame metadata, so passing a delay through
BitmapFrame metadata did not preserve it in the saved file. After WPF encodes the
palettes/pixels, GifTimingMetadata walks the generated container blocks and writes
a Graphic Control Extension per image with its delay and disposal mode. It
streams image data through an 80 KiB buffer, preserving palettes and other
extensions and writing exactly one loop extension. It does not load the entire
GIF into memory. An owned staging file is flushed, closed and moved over the
private temporary input only after successful validation; failures clean staging
without modifying that input. Holds exceeding the
16-bit GIF delay are split across repeated image blocks with the same pixels,
without silently truncating the total or emitting a 10 ms remainder. Invalid
generated structures fail before replacing the temporary file. Publication still
moves the completed temporary file to the destination only after all processing.

Source: [Microsoft WPF GIF encoder](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.imaging.gifbitmapencoder?view=windowsdesktop-10.0)
(frame metadata unsupported); [Microsoft animated GIF sample](https://github.com/microsoft/Windows-classic-samples/blob/main/Samples/Win7Samples/multimedia/wic/wicanimatedgif/WicAnimatedGif.cpp)
(Graphic Control Extension delays are in 10 ms units);
[GIF89a specification](https://www.w3.org/Graphics/GIF/spec-gif89a.txt), sections 18–23
(container blocks, little-endian fields and disposal/delay layout).

`GifCaptureMetrics.ActiveDurationMilliseconds` records the stopped active clock,
alongside requested/effective FPS, captured/processed/stored/duplicate/dropped
frames and capture/spool/queue time. `TimingCoalescedFrames` separately counts
images omitted for an insufficient timing budget. Processed images equal stored
plus duplicate plus timing-coalesced images; scheduled-overload drops remain
separate. Temporary bytes continue to count all PNGs spooled before selection,
so removing images cannot hide resource use. The old combined deduplication/throughput
assertion is separated into labeled checks with the full metrics on failure.
The throughput floor (scheduled frames minus two slots of tolerance) applies
when there were no skipped slots and capture/spool/queue work consumed less than
20% of the active time. Otherwise the run is explicitly reported as overloaded,
not accepted as a throughput benchmark. Deduplication and timing always remain
mandatory, irrespective of that classification.

After a Release/x64 build, run:

```powershell
./scripts/gif-timeline-smoke.ps1 `
  -Executable tests/SlashText.SmokeTests/bin/x64/Release/net10.0-windows/SlashText.SmokeTests.exe `
  -OutputDirectory <new-evidence-directory>
```

The real pipeline uses only injected 16 × 12 red/blue bitmaps in a separate pilot
profile. Cases cover normal/static capture, 250/400 ms capture latency, a gated
late first frame, changing images at 10/20/30 FPS, pause and Stop with capture in
flight. They compare timeline duration to the actual stopped active clock,
decode the saved GIF to check each delay and pixel/order, and check long holds/malformed generated containers and resource
accounting/cleanup while an unrelated temporary session stays active. A separate
deterministic ten-second boundary test verifies rounding at each FPS independently
of runner speed. Eight saved GIF fixtures cover the reported burst, equal
timestamps, close final transitions, tiny/zero clocks and necessary image
selection, checking decoded delays and ordered endpoint pixels. Another 500
seeded traces verify budget, minimum delays and ordered endpoints independently
of scheduling. Existing cancellation regressions remain in the general suite.

Build Windows runs this matrix, retaining `metrics.json`, stdout/failure details
and synthetic GIFs even when it fails. Reports contain fixture data and timing
only; no personal clipboard, screen, microphone, device or document is read.
No manual test is required for this scoped fix. Actual hardware, DPI, screen
content and end-user capture flows remain part of candidate validation in #70.
