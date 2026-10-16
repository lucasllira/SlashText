# Release candidate validation (#70)

`Validate Release Candidate` publishes the normal Portable and Installed profiles,
without pilot flags or a version override. It runs on release-input pull requests
and can be dispatched after integration from the chosen source ref. It has only
`contents: read`; it does not create tags, attestations or GitHub Releases.

The workflow records the source commit and ZIP/EXE hashes, checks production
distribution startup in a fresh disposable CI account, then runs the actual
published executables through About, language, offline-notice and synthetic
native OCR probes. Component inventory/SBOM is collected from the same outputs.
Portable packaging must contain only SlashDesk.exe. Installed staging is a
directory layout, not an installer. Evidence and packages are retained as Actions
artifacts for 30 days; preserve them before expiration.

`scripts/candidate-package-smoke.ps1` refuses a version mismatch or a preexisting
CI startup profile. Locally, it skips normal startup to avoid migrating or
modifying a personal installed profile. Helper probes initialize their own
fixtures and never start input hooks or access personal screen/audio/clipboard.
Local startup unavailability remains explicit in the summary. A failed probe
fails the job; it does not publish a partial package as validated.

The ordinary Build Windows, CodeQL and Security Baseline checks remain separate.
Candidate package tests do not replace the domain/UI regressions they execute.
The candidate workflow intentionally does not run on every source edit; dispatch
it again when preparing the final integrated source or when candidate inputs
change. Pull-request merge refs and post-integration main refs have different
identities; record the actual commit instead of treating their artifacts as
interchangeable.

## Remaining acceptance gates

- Hosted runners already contain prerequisites. They do not prove OCR behavior
  on clean Windows with Visual C++ x64 missing and installed.
- Synthetic readers/renders do not prove physical DPI, screen-reader output,
  microphone release, device behavior or long-duration performance.
- Preserve/update/restore/rollback tests use representative synthetic data when
  the owner has no existing profile. Never invent a real-user-data acceptance.
- Before publication, validate the final source, runtime, metadata and artifacts;
  subsequent changes require a new identity and affected regression checks.
- The chosen provenance strategy still requires the first real SlashDesk
  tag/workflow/bundle validation under the existing approval policy. This
  read-only workflow does not satisfy that gate or authorize publication.
- #37 requires observation of an actual stable release by a previously running
  consumer; candidate artifacts and prereleases cannot satisfy it.

No manual pilot test should be requested before packages, hashes, fixtures,
expected outcomes and remaining physical checks are ready. #70 stays open until
its gates and owner acceptance are recorded; green CI alone does not close it.
