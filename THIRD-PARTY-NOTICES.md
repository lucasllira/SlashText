# Third-party notices

Open **Sobre → Avisos de terceiros** (About → Third-party notices) to read the
complete licenses and attributions inside SlashDesk, including while offline.
Select a component and use the read-only text area to read/select/copy its text.
The interface supports Portuguese, English and Spanish. Upstream license texts
remain in their original language and retain their authors and conditions.

This software is based in part on the work of the Independent JPEG Group.

The reviewed source catalog and document hashes are in
[`src/SlashText/Assets/ThirdParty/catalog.json`](src/SlashText/Assets/ThirdParty/catalog.json).
The build embeds that catalog, all reviewed texts, and notices from the exact
resolved self-contained runtime packs. The portable distribution remains one
`SlashDesk.exe`; the installed staging includes the same offline viewer.
No download of notices occurs when the application runs.

| Components | Included texts |
| --- | --- |
| ScreenRecorderLib 6.6.0 / fifo_map | Exact NuGet MIT license, package attribution and Niels Lohmann's original MIT notice from the native backend |
| AvalonEdit 6.3.1.120 | Upstream MIT license and package attribution |
| Tesseract .NET 5.2.0 / InteropDotNet | Apache 2.0, MIT, authors and original acknowledgements |
| Native Tesseract 5.0.0 / Leptonica 1.82.0 | Apache 2.0, authors and Leptonica BSD terms |
| GIF 5.2.1, JPEG API 6b / libjpeg-turbo 2.1.4, PNG 1.6.37, TIFF 4.4.0, zlib 1.2.13 | Original component licenses, including IJG attribution |
| liblzma codec diagnostics in TIFF | Supplemental XZ Utils COPYING; exact binary version is not attested |
| Noto Emoji / region flags / Unicode and CLDR metadata | Apache 2.0, Unicode terms, flag exemptions and original notices |
| Lucide / Feather paths | Original ISC/MIT notices |
| Self-contained .NET / WPF / Windows Forms | Resolved runtime pack licenses/transitive notices plus reviewed upstream desktop notices |

The source commit/backport/build graph of the supplied OCR DLLs remains
unattested, as documented in [OCR trust](docs/ocr-native-trust.md). Exported and
static version observations are not a native build attestation. The XZ text
distinguishes its public-domain library from separately licensed tools; SlashDesk
does not distribute XZ command-line tools. Do not infer a blanket license for
unknown platform components or change security decisions based on this index.

SlashDesk's own [MIT license](LICENSE) is unchanged. The project license is also
included in the offline catalog for convenience. Maintenance and package
verification: [third-party notice maintenance](docs/third-party-notices.md).
