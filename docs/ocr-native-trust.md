# OCR trust and native review (#97)

## Scope and decision

The public wrapper remains Tesseract 5.2.0. Its approved x64 files report **Tesseract 5.0.0** and **Leptonica 1.82.0** via exports. A version string/package label does not establish the exact source commit or backport state. The approved hashes are in `scripts/security/native-versions.json`; `OcrNativeAssets` verifies them, file sizes, AMD64 PE headers and required C API exports before OCR.

The same NuGet package does not provide a verified drop-in update to the current upstream source releases. Source releases alone do not establish a compatible Windows binary or resolve advisories with no declared released fix. Replacing or renaming arbitrary newer DLLs would not establish compatibility/provenance. This change selects a **documented mitigation**, preserving the offline WPF/Tesseract architecture, rather than claiming that the existing binaries have been patched.

## Trust boundary

- Best/Fast keep the original English/Portuguese bytes, pinned upstream commits and SHA-256 values. All selected model files are validated for exact size and hash before use.
- `OcrModelLease` holds directory pins and read-only handles denying writes/deletion for the entire native engine lifetime. Reparse points, hardlinks, remote paths and ambiguous directory aliases are rejected by the existing local-file boundary.
- The private cache is `OcrModels-v1` under the selected app data directory. Cold restoration is serialized between processes; restored resources are verified before atomic publication. Corrupted regular cache files are replaced with embedded approved bytes. The old global temporary cache is neither trusted nor deleted.
- Alternate directories, including direct worker arguments, receive the same full checks; their files are never repaired or accepted based solely on their location. No arbitrary model/training interface is introduced.
- The parent holds its lease across the worker process lifetime; the worker independently verifies and holds a second lease. `LstmOnly` and normalized por/eng language choices remain fixed. `TESSDATA_PREFIX` is removed from the child environment.
- The worker runs in its own generated session directory. Approved native DLLs are verified before any is loaded; x64 libraries are loaded by absolute path, with dependency lookup restricted to System32. Leptonica is preloaded first. The wrapper's custom search root is the verified compiled/extracted bundle root, never a model directory or PATH. The pinned files remain available until engine cleanup.
- Parent process separation, re-encoded PNG, pixel limits, 45-second timeout, active cancellation, output draining and bounded cleanup remain. **The worker uses the account's permissions and is not an OS security sandbox.**

## Contextual advisory decisions

Reviewed 2026-10-15 against the [official Tesseract advisories](https://github.com/tesseract-ocr/tesseract/security/advisories) and linked upstream records. Machine-readable decisions: `scripts/security/ocr-advisory-decisions.json`. No adversarial model or exploit reproducer was executed.

| Record | Context and decision |
|---|---|
| [GHSA-9vwv-49m8-jv9m](https://github.com/tesseract-ocr/tesseract/security/advisories/GHSA-9vwv-49m8-jv9m) | Reported affected range is exactly 5.5.3; applicability to this binary is not established. Model-load trust is enforced regardless. |
| [GHSA-357f-833m-3f83](https://github.com/tesseract-ocr/tesseract/security/advisories/GHSA-357f-833m-3f83) | Requires legacy adaptation; the app uses LstmOnly and does not enable untrusted adapted templates. Fixed-model checks remain mandatory. |
| [GHSA-96jm-jc65-f968](https://github.com/tesseract-ocr/tesseract/security/advisories/GHSA-96jm-jc65-f968) | Legacy-engine precondition is excluded by LstmOnly. Record native patch uncertainty; do not claim the DLL is patched. |
| [GHSA-5j2p-r5vc-q7f3](https://github.com/tesseract-ocr/tesseract/security/advisories/GHSA-5j2p-r5vc-q7f3) | Legacy-engine precondition is excluded by LstmOnly. Record native patch uncertainty; do not claim the DLL is patched. |
| [GHSA-f6h7-cqr4-6fx4](https://github.com/tesseract-ocr/tesseract/security/advisories/GHSA-f6h7-cqr4-6fx4) | Crafted model/dictionary is the published precondition. SHA-256 allowlists and held file/directory leases mitigate this input route; LstmOnly alone is insufficient. |
| [GHSA-q44c-23p6-5mw6](https://github.com/tesseract-ocr/tesseract/security/advisories/GHSA-q44c-23p6-5mw6) | Crafted model/dictionary is the published precondition. SHA-256 allowlists and held file/directory leases mitigate this input route; LstmOnly alone is insufficient. |
| [GHSA-jgq8-pprg-vc68](https://github.com/tesseract-ocr/tesseract/security/advisories/GHSA-jgq8-pprg-vc68) | Crafted model/dictionary is the published precondition. SHA-256 allowlists and held file/directory leases mitigate this input route; LstmOnly alone is insufficient. |
| [GHSA-7v9h-3q3m-w68g](https://github.com/tesseract-ocr/tesseract/security/advisories/GHSA-7v9h-3q3m-w68g) | Crafted model/dictionary is the published precondition. SHA-256 allowlists and held file/directory leases mitigate this input route; LstmOnly alone is insufficient. |
| [GHSA-2hm8-q5c7-c373](https://github.com/tesseract-ocr/tesseract/security/advisories/GHSA-2hm8-q5c7-c373) | Crafted model/dictionary is the published precondition. SHA-256 allowlists and held file/directory leases mitigate this input route; LstmOnly alone is insufficient. |
| [GHSA-88qp-4g94-3rf3](https://github.com/tesseract-ocr/tesseract/security/advisories/GHSA-88qp-4g94-3rf3) | Legacy-engine precondition is excluded by LstmOnly. Record native patch uncertainty; do not claim the DLL is patched. |
| [GHSA-rphx-x795-5qjv](https://github.com/tesseract-ocr/tesseract/security/advisories/GHSA-rphx-x795-5qjv) | Legacy-engine precondition is excluded by LstmOnly. Record native patch uncertainty; do not claim the DLL is patched. |
| [GHSA-7j76-5rq5-5jg8](https://github.com/tesseract-ocr/tesseract/security/advisories/GHSA-7j76-5rq5-5jg8) | Crafted model/dictionary is the published precondition. SHA-256 allowlists and held file/directory leases mitigate this input route; LstmOnly alone is insufficient. |
| [GHSA-x3vq-7rr7-5x3h](https://github.com/tesseract-ocr/tesseract/security/advisories/GHSA-x3vq-7rr7-5x3h) | Crafted model/dictionary is the published precondition. SHA-256 allowlists and held file/directory leases mitigate this input route; LstmOnly alone is insufficient. |
| [OSV-2022-69](https://osv.dev/vulnerability/OSV-2022-69) | Upstream fix discusses a fuzzer-supplied invalid counting pointer and bounded clipping. The app does not supply such a pointer; complete native reachability/backports are not attested. |
| [OSV-2022-91](https://osv.dev/vulnerability/OSV-2022-91) | Affected pixel operator has a depth/call precondition. Re-encoding/bounds reduce input exposure, but do not prove all internal calls unreachable or the DLL patched. |
| [CVE-2022-38266](https://github.com/DanBloomberg/leptonica/commit/f062b42c0ea8dddebdc6a152fd16152de215d614) | Source fix and prior version triage do not attest the distributed binary. Retain explicit provenance/patch-level uncertainty. |

Some advisory metadata and prose disagree about patched versions; several list no released patch, and some name a later release. Do not convert that into a blanket fixed/unfixed claim. In particular, a model-deserialization advisory is not evidence of exploitation by an ordinary screenshot. Conversely, trusted models do not establish safety of all Leptonica pixel processing.

## Validation

`--ocr-trust-smoke <fresh-fixture-root>` covers default/alternate models, altered/truncated/missing/wrong-family data, held handles, cold concurrent writers, aliases and unapproved/x86 native files. It does not execute altered native files or models.

`--ocr-native-trust-smoke <fresh-fixture-root> [approved-baseline-exe]` additionally loads only approved binaries, verifies actual versions/ABI/module paths, recognizes generated por/eng text with Best/Fast, checks blank input, rejects direct worker bypass and exercises timeout/cancellation using an owned inert child. Optional baseline comparison records text error/confidence and timing; it never records the desktop or accesses the personal clipboard. The regular OCR WPF regression also runs in CI, including the published single-file app.

Run `scripts/languages-catalog-smoke.ps1`, the normal full smoke suite, and the security inventory/regression checks. NuGet audit availability and skipped native environments must remain explicit; a successful functional test is not an advisory remediation proof.

## Release gate and residual risk

The current DLL hashes are approved identities, **not a reproducible-source or security-patch attestation**. The application does not claim all advisory records are exploitable or fixed. Before #70 is accepted, record explicit acceptance of this remaining native/provenance risk, or replace the libraries with a separately verified compatible build and rerun the matrix. That acceptance is separate from an implementation review or a passed CI.

The x64 ABI/runtime was checked in available Windows environments. Hosted Windows CI already has prerequisites; it is not a clean bare-OS installation. A clean-machine VC++ x64 installed/missing prerequisite check belongs to the candidate gate (#70). No runtime installer is silently downloaded or run. x86 files may remain in the NuGet bundle for inventory/license review, but the x64 app rejects them and never selects the x86 loader path.

Follow-ups: #95 build authenticity/provenance; #96 complete transitive notices; #70 clean-machine/integrated candidate validation. A future source-built native update must pin source/patches/toolchain/dependencies, verify exports and dependencies, and compare OCR behavior before replacing this allowlist.

Primary references: [wrapper and prerequisites](https://github.com/charlesw/tesseract), [NuGet package](https://www.nuget.org/packages/Tesseract/5.2.0), [official loader](https://github.com/charlesw/tesseract/blob/master/src/Tesseract/Internal/InteropDotNet/LibraryLoader.cs), [Leptonica fix 69](https://github.com/DanBloomberg/leptonica/commit/748723a795e48ce0ec0ce2fa78082ef0c62aac9c), [Leptonica fix 91](https://github.com/DanBloomberg/leptonica/commit/f694a026b400a5f501f055e536eff2bfaa3c1bc1).
