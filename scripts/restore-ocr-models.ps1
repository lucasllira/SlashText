$ErrorActionPreference = 'Stop'
$folder = Join-Path (Split-Path $PSScriptRoot -Parent) 'src/SlashText/Assets/Ocr'
$commit = 'e12c65a915945e4c28e237a9b52bc4a8f39a0cec'
$models = @{
    eng = '8280aed0782fe27257a68ea10fe7ef324ca0f8d85bd2fd145d1c2b560bcb66ba'
    por = '711de9dbb8052067bd42f16b9119967f30bada80d57e2ef24f65d09f531adb04'
}
New-Item $folder -ItemType Directory -Force | Out-Null
foreach ($language in $models.Keys) {
    $path = Join-Path $folder "$language.traineddata"
    if ((Test-Path $path) -and (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant() -eq $models[$language]) { continue }
    $temporary = "$path.$([Guid]::NewGuid().ToString('N')).tmp"
    try {
        Invoke-WebRequest "https://raw.githubusercontent.com/tesseract-ocr/tessdata_best/$commit/$language.traineddata" -OutFile $temporary
        if ((Get-FileHash $temporary -Algorithm SHA256).Hash.ToLowerInvariant() -ne $models[$language]) { throw "OCR model hash mismatch: $language" }
        Move-Item -LiteralPath $temporary -Destination $path -Force
    } finally { if (Test-Path $temporary) { Remove-Item -LiteralPath $temporary } }
}
