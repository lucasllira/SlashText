param(
    [Parameter(Mandatory)][string]$Executable,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path -LiteralPath $Executable).Path
$output = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) ('run-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8))
Write-Host 'Teste de gravacao MP4: quatro amostras de uma janela ficticia.'
Write-Host 'O teste usa audio do PC e microfone quando disponiveis e reproduz um tom.'
Write-Host 'Feche fontes de audio privadas; os MP4 ficam somente nesta pasta, sem upload.'
Write-Host 'Apos concluir, confira a reproducao. Este teste nao valida falhas de driver.'
[void](Read-Host 'Pressione ENTER quando estiver pronto, ou feche esta janela para cancelar')
New-Item -ItemType Directory -Path $output -Force | Out-Null
$exeHash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
$process = Start-Process $exe -ArgumentList "--recording-audio-smoke `"$output`" --native" -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(180000)) {
    $process.Kill()
    throw 'O processo exclusivo deste teste excedeu o limite e foi encerrado. Nao considere o teste aprovado.'
}
if ($process.ExitCode -ne 0) {
    Get-Content -LiteralPath (Join-Path $output 'failure.txt') -ErrorAction SilentlyContinue
    throw "Teste falhou. Preserve a pasta para diagnostico local: $output"
}
$metrics = Get-Content -LiteralPath (Join-Path $output 'native.json') -Raw | ConvertFrom-Json
# Windows PowerShell 5.1 emits a JSON array as a single pipeline object.
$metrics = @($metrics)
$expected = @('silent','computer','microphone','both')
if ($metrics.Count -ne 4 -or @($expected | Where-Object { $_ -notin $metrics.name }).Count) {
    throw 'Relatorio incompleto: faltam combinacoes de audio.'
}
$skipped = @($metrics | Where-Object { $_.PSObject.Properties['Skipped'] })
$passed = $skipped.Count -eq 0
[ordered]@{
    executableSha256 = $exeHash
    automaticChecks = $(if ($passed) { 'PASS' } else { 'INCOMPLETE' })
    playbackReview = 'PENDING'
    abnormalNativeStop = 'NOT_TESTED'
    samples = $metrics
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'summary.json') -Encoding UTF8
$metrics | Format-Table name,Elapsed,Bytes,Skipped -AutoSize
if (-not $passed) {
    Write-Host 'PENDENTE: fonte indisponivel. Veja quais combinacoes foram omitidas em native.json.'
} else {
    Write-Host 'PASS: quatro combinacoes, faixas, pausa, mute e duracoes verificadas automaticamente.'
}
Write-Host "Resultados e MP4: $output"
Write-Host 'Confira imagem e audio reproduzindo os arquivos. Nao envie gravacoes com conteudo pessoal.'
if (-not $passed) { exit 2 }
