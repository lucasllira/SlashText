# Offline OCR

Tesseract .NET wrapper 5.2.0: Copyright Charles Weld, Apache-2.0.
https://github.com/charlesw/tesseract
Includes InteropDotNet (Andrey Akinshin, MIT) and the native files distributed
by the Tesseract 5.2.0 NuGet package (Apache-2.0), with Leptonica (BSD-2-Clause).
The approved x64 DLL exports report Tesseract 5.0.0 and Leptonica 1.82.0;
the package/upstream description calls Tesseract 5.2.0. These are distinct
observations, not an attestation of native source commits or security backports.
SHA-256 identity and contextual advisory decisions: scripts/security/native-versions.json
and docs/ocr-native-trust.md in the source repository. Complete transitive notices
and licenses remain subject to the distribution review (#96).

Portuguese and English models: tesseract-ocr/tessdata_best, Apache-2.0,
commit e12c65a915945e4c28e237a9b52bc4a8f39a0cec.
https://github.com/tesseract-ocr/tessdata_best

Portuguese and English fast models: tesseract-ocr/tessdata_fast, Apache-2.0,
commit 87416418657359cb625c412a48b6e1d6d41c29bd.
https://github.com/tesseract-ocr/tessdata_fast

Model files are restored with SHA-256 verification during build and embedded
in the executable. Recognition does not access the network. No custom training.
