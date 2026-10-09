# Offline OCR

Tesseract .NET wrapper 5.2.0: Copyright Charles Weld, Apache-2.0.
https://github.com/charlesw/tesseract
Includes InteropDotNet (Andrey Akinshin, MIT), Tesseract 5.2.0 (Apache-2.0)
and Leptonica 1.82.0 (BSD-2-Clause).

Portuguese and English models: tesseract-ocr/tessdata_best, Apache-2.0,
commit e12c65a915945e4c28e237a9b52bc4a8f39a0cec.
https://github.com/tesseract-ocr/tessdata_best

Model files are restored with SHA-256 verification during build and embedded
in the executable. Recognition does not access the network. No custom training.
