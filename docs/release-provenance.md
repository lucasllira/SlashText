# Release provenance and trust policy

## Decision for 3.3.0 (#95)

Use GitHub/Sigstore build provenance for future official releases. The maintainer
approved this strategy on 2026-10-15: verify before publication and provide a
verifier for manual downloads, without requiring GitHub CLI on application PCs.
No commercial certificate was purchased, no signing secret was added, and no
claim of Windows Authenticode signing or SmartScreen reputation is made.

| Strategy | Cost and operation | Compatibility / coverage |
| --- | --- | --- |
| GitHub artifact attestations (chosen) | Available for this public repository; short-lived OIDC identity, no maintained private signing key | Associates artifact digest with repository, workflow, tag and source commit; verified with GitHub CLI, not by Windows Explorer |
| Authenticode certificate/service | Provider, identity validation, key custody, renewal and pricing need separate evaluation before purchase | Windows publisher identity and timestamp verification; does not by itself identify the source commit or prove a build is safe |
| Hash/SBOM only | Existing integrity and inventory tooling | Detects corruption and inventories components; does not independently authenticate a producer |

## Producer

`Release Windows` must execute **from the release tag**, including manual dispatch.
Its selected ref must equal `refs/tags/<release_tag>`; its `GITHUB_SHA`, tag commit
and checked-out HEAD must match, and that commit must belong to main history.
Do not dispatch from main while checking out another commit: that would bind the
certificate's source identity to a different revision than the compiled code.

The validation job stays read-only. It builds/tests/publishes portable and
installed staging outputs and records SHA-256 for both ZIPs and the portable
EXE. The approval-protected publication job downloads those exact same-run
artifacts, compares their digests to the validated outputs, and checks the EXE's
single-file layout before generating provenance. There is no rebuild after approval.

Only `release.yml/publish-release` receives `contents: write`, `id-token: write`
and `attestations: write`, behind `release-approval`. `actions/attest` v4.2.2 is
pinned to `1e69f48acb82d1966a394da916b4c1698aa569d6`; its reviewed action entry
uses Node 24 directly, with no nested Actions. It creates SLSA v1 provenance
by default. No custom predicate or caller-supplied signing identity is used.

Subjects are the portable ZIP, installed staging ZIP and **the actual portable
SlashDesk.exe inside the ZIP**. Before `gh release create`, every subject is
cryptographically verified with the policy below. A failed/missing attestation
stops publication. The portable release publishes its ZIP, checksum and
`SlashDesk-<version>-provenance.json` bundle. Installed staging remains an Actions
validation artifact; no installer or installed auto-updater is introduced.

Repository allowlisting must explicitly add only the reviewed `actions/attest`
SHA, keeping existing restrictions. Editing `actions-policy.json` does not apply
the server setting; administrative authorization and API readback are separate.

## Consumer verification

Use a trusted checkout of `scripts/verify-release-provenance.ps1` and a trusted
GitHub CLI installation (tested with 2.97.0 using an explicit JSON bundle). Obtain
the expected tag and full source commit from the approved release decision or
independently reviewed repository tag, not from the unverified bundle itself.

```powershell
./scripts/verify-release-provenance.ps1 `
  -Artifact ./SlashDesk-3.3.0-portable-win-x64.zip `
  -Tag v3.3.0 -Commit '<approved 40-character lowercase commit>' `
  -Bundle ./SlashDesk-3.3.0-provenance.json
```

The same command can verify extracted `SlashDesk.exe`, or the installed staging
ZIP from Actions. It never installs or executes the subject. GitHub CLI verifies
the signature/trust chain, timestamps/transparency and digest; the script pins:

- Repository `lucasllira/SlashText` and signer workflow
  `lucasllira/SlashText/.github/workflows/release.yml`.
- Exact certificate identity ending `release.yml@refs/tags/<expected tag>` and
  source ref `refs/tags/<expected tag>`.
- Source commit **and signer workflow commit** equal the independently expected
  full commit; SLSA `https://slsa.dev/provenance/v1` predicate.
- GitHub-hosted runners; self-hosted runner provenance is refused.

Use the distributed JSON bundle where possible. Omitting `-Bundle` asks GitHub
CLI to find attestations online; API/CLI compatibility, authentication and
network failures are fatal, never a hash-only success. This is not a promise of
air-gapped verification: CLI may need trusted-root metadata from Sigstore/TUF.
Do not trust a replacement root supplied beside an untrusted artifact.

## Compatibility and residual risk

The application updater does **not** verify Sigstore or Authenticode internally.
Its existing stable-version selection, hash/PE/version/layout checks, portable
transaction, pilot isolation, data preservation and rollback remain. Downloads
now start at the exact official repository/tag/asset URL. Automatic HTTP
redirects are disabled: at most three redirects, HTTPS port 443, no credentials
or fragments, only official release CDN hosts/paths; a rejected hop is not sent.
Metadata API redirects fail closed too. These origin restrictions are not a
substitute for producer authentication.

Versions without attestations, including 3.2.0, keep their existing application
update compatibility. The provenance verifier refuses them rather than inventing
proof retrospectively. A manually selected historical rollback is an explicitly
legacy/unsigned artifact; use the known previous executable/hash and existing
backup procedure. Do not weaken the verifier to accept it as authenticated.
Installing the new version from an old updater does not retroactively upgrade
that old updater's trust checks.

An actor controlling the expected repository/workflow, approval authority, runner
or local account can still affect a release or its verification. Attestation
proves an identity/build association, not absence of vulnerabilities, reproducible
builds, SLSA Build Level 3, or an OS security sandbox. The first-party verification
is a release gate and manual consumer option, not an automatic-client signature
gate. This limitation and the deferred Authenticode decision must be recorded
explicitly in #70 before launch.

## Rotation and incident response

OIDC signing certificates are short-lived; GitHub/Sigstore and CLI's trusted-root
verification handle normal certificate/root rotation. Action upgrades and their
exact allowlist SHA require a reviewed PR plus authorized server change. Repository
ownership/path/workflow changes must update this trust policy through review;
there is no wildcard signer or automatic fallback to a new producer. Do not use
`--custom-trusted-root` to bypass verification errors.

If compromise is suspected, stop approval/publication, retain the artifact and
verification evidence, review source/run identities, revoke affected credentials
and access, and distribute a reviewed remediation notice. Do not overwrite an
immutable release tag. Publish a newly approved version after trust is restored;
keep the previous known-good executable/data backups available for rollback.

## Validation boundary

Automated regressions execute release input/source guards, permission mutations,
verification command policy, synthetic HTTP redirects and updater preparation /
rollback / data preservation. Mocked `gh` tests validate wiring and failure
propagation, **not cryptography**. Real CLI verification is recorded separately.
Real verification of the public RC1 and stable bundles is recorded in
[#70](https://github.com/lucasllira/SlashText/issues/70). RC1 binds source
`138e6bbdacf494d2d748e6db68b53c1d46008bc5`; the separately authorized
[stable v3.3.0](https://github.com/lucasllira/SlashText/releases/tag/v3.3.0)
binds `834d7878ef02e2fc90572bd17df6aee898c1d3fd`, built/published by
[run 39077264939](https://github.com/lucasllira/SlashText/actions/runs/39077264939).

For stable, all three subjects passed the trusted consumer script with the public
bundle; seven wrong-identity/policy cases, modified artifact/signature and missing
verification material were refused. A final unchanged positive control passed
(14 decisions). Public ZIP/checksum and extracted EXE match the approved hashes;
installed staging remains the same-run Actions artifact, not a public installer.

| Stable subject | SHA-256 |
| --- | --- |
| Portable ZIP | `4e2bd54778e45266c2bf77741a5980e11f349cc38731442af3b0ff311461fb20` |
| Actual portable SlashDesk.exe | `4e1ee92070262de5b5511e2b547ef76c6f4e9d4333fbb343be747b395e3c2e73` |
| Installed staging ZIP | `791d6b9f05863957e33326eeda38b87317a766a88472c1ee9c38d77cce6e4d9f` |

This proof does not cover later main commits or prove an end-user update popup/
transaction; that observation remains in #37. Each new published tag/artifact
requires its own authorized run, public bundle download, successful verification
and refused modified/wrong-identity cases. No production tag/release may be created
merely to test documentation or a code PR.

References: [GitHub artifact attestations](https://docs.github.com/en/actions/how-tos/secure-your-work/use-artifact-attestations/use-artifact-attestations),
[GitHub CLI verification policy](https://cli.github.com/manual/gh_attestation_verify),
[reviewed Action](https://github.com/actions/attest/tree/1e69f48acb82d1966a394da916b4c1698aa569d6).
