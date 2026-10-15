# Offline notice maintenance (#96)

`GenerateThirdPartyNotices` runs before resource path assignment, after framework
resolution. It does not use the network. It verifies every reviewed source text,
NuGet reference version and the recorder's actual package license, then embeds
`SlashText.ThirdParty.catalog.json` and `SlashText.ThirdParty.notices.zip`.

Framework-dependent development builds include the reviewed application/assets
notices. Self-contained portable and installed builds additionally include
LICENSE and THIRD-PARTY-NOTICES files from the exact resolved
Microsoft.NETCore.App / Microsoft.WindowsDesktop.App runtime packs. The latter's
NuGet package omits its upstream WPF/Windows Forms transitive notice files, so
those reviewed upstream texts are included explicitly. Runtime 10.0.12 was
reviewed for this change. A different runtime patch fails the generator until
its desktop notices and `validatedDesktopRuntimeVersion` are reviewed/updated;
do not downgrade the runtime or bypass the gate to get a green build.

The catalog preserves component versions/source references and SHA-256 of each
license body in canonical UTF-8 without BOM, LF line endings. This normalization
is explicit; all other upstream text is retained. ZIP timestamps/order are
deterministic. No absolute machine/cache paths are written into the catalog.
The app verifies catalog/ZIP cardinality, identities, lengths and all digests
before showing text. The viewer does not navigate URLs or use HTML rendering.

`ScreenRecorderLib.txt` is copied from the exact 6.6.0 NuGet LICENSE, retaining
Sverre Skodje's original 2017 notice. The distribution NOTICE also preserves
package metadata crediting 2025, Tesseract/Charles Weld, InteropDotNet/Andrey
Akinshin and AvalonEdit/AlphaSierraPapa. Copyright years are not substituted with
the current year. IJG's required attribution is included in both the local
NOTICE and the root third-party index.
The 6.6.0 native backend also includes and uses `fifo_map.h`; its complete MIT
header, copyright 2015-2017 Niels Lohmann, is preserved as a separate document.

Native codec observations were taken from the SHA-verified x64 Leptonica
`getImagelibVersions` export without decoding images: GIF 5.2.1, JPEG API 6b /
libjpeg-turbo 2.1.4, PNG 1.6.37, TIFF 4.4.0 and zlib 1.2.13. Static x86 strings
also identify libjpeg-turbo 2.1.4, TIFF 4.4.0 and zlib 1.2.13; x86 bytes remain
inventoried but are not loaded in the x64 app. TIFF contains liblzma codec/error
strings, but the package supplies no version/build manifest for it. Its upstream
5.2.7 COPYING is a supplemental notice, explicitly not a claim that the DLL uses
that exact revision. No WebP/OpenJPEG/JBIG implementation is inferred merely from
format names/stubs in Leptonica/TIFF. Native source/backport provenance remains
the accepted limitation of #97; this change does not replace/upgrade native code.

For each dependency/runtime update:

1. Inspect the actual resolved/distributed package, its license file and its
   upstream notices at the corresponding version/commit. Preserve full texts and
   author attributions; investigate additional native/static dependencies.
2. Update the reviewed catalog/texts, source references and canonical hashes.
   A version label must not be substituted for package identity. Confirm the
   recorder text still equals the resolved package's LICENSE.
3. Build and publish **both profiles**. Run `security-inventory.ps1` against
   those exact outputs from the corresponding checkout. Its metadata reader
   expands notice resources without loading/executing the app. The inventory
   verifies embedded document hashes, source catalog versions, package coverage
   and runtime license hashes against the restored package cache; SBOM/inventory
   retain their source/license relationships.
4. Run `third-party-notices-regression.ps1`, inventory unit tests, and
   `third-party-notices-smoke.ps1` for the actual portable EXE and installed
   staging EXE. The WPF smoke invokes the real About handler, reads every license
   in pt-BR/en/es × Light/Dark/System, renders reference/compact sizes and verifies
   preference preservation. It uses isolated fictitious app data and no browser,
   clipboard or recording. Render scales are not physical-DPI approval.

The generator regression exercises modified/missing source text, changed package
version, a changed NuGet license and missing/stale runtime notices. Inventory
tests independently reject missing proof, stale source/version, altered archive
text and missing package coverage. CI runs these gates and both distribution
viewers automatically; no mandatory user test is needed for this delivery.

This is a technical distribution inventory, not a legal opinion or a proof of
complete native source provenance. Platform Windows APIs/Media Foundation and
externally installed VC++ prerequisites are not new redistributed installers or
encoder executables. The original SlashDesk license and update behavior remain.

Primary references are preserved per document in the reviewed catalog, including
the exact recorder package, AvalonEdit commit, Tesseract/Leptonica source tags,
codec license files, Noto/Unicode snapshots and .NET upstream runtime notices.
