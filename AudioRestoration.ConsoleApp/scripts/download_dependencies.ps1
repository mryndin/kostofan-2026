$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$projectDir = $PSScriptRoot # Используем папку скрипта как базу
if ($projectDir -like "*scripts") { $projectDir = Split-Path $projectDir -Parent }

Write-Host "Project Directory: $projectDir" -ForegroundColor Cyan

# 1. Проверка FFmpeg
$ffmpegExe = Join-Path $projectDir "ffmpeg.exe"
$ffprobeExe = Join-Path $projectDir "ffprobe.exe"

if (-not (Test-Path $ffmpegExe) -or -not (Test-Path $ffprobeExe)) {
    Write-Host "FFmpeg binaries missing. Starting download..." -ForegroundColor Yellow
    $ffmpegUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip"
    $zipFile = Join-Path $projectDir "ffmpeg.zip"
    
    Invoke-WebRequest -Uri $ffmpegUrl -OutFile $zipFile -UseBasicParsing
    
    $dest = Join-Path $projectDir "temp_ffmpeg"
    if (-not (Test-Path $dest)) { New-Item -ItemType Directory -Path $dest | Out-Null }
    
    Expand-Archive $zipFile -DestinationPath $dest -Force
    
    (Get-ChildItem -Path $dest -Filter "ffmpeg.exe" -Recurse | Select-Object -First 1) | Copy-Item -Destination $projectDir
    (Get-ChildItem -Path $dest -Filter "ffprobe.exe" -Recurse | Select-Object -First 1) | Copy-Item -Destination $projectDir
    
    Remove-Item -Recurse -Force $dest
    Remove-Item $zipFile
    Write-Host "FFmpeg installed." -ForegroundColor Green
} else {
    Write-Host "FFmpeg already exists. Skipping." -ForegroundColor Gray
}

# 2. Проверка Модели
$modelFile = Join-Path $projectDir "model.onnx"

if (-not (Test-Path $modelFile)) {
    Write-Host "Model file missing. Downloading Demucs v4..." -ForegroundColor Yellow
    $modelUrl = "https://huggingface.co/MrCitron/demucs-v4-onnx/resolve/main/htdemucs_ft.onnx"
    
    try {
        Invoke-WebRequest -Uri $modelUrl -OutFile $modelFile -UseBasicParsing
        Write-Host "Model downloaded." -ForegroundColor Green
    } catch {
        Write-Error "Failed to download model. Error: $_"
        exit 1
    }
} else {
    Write-Host "Model already exists. Skipping." -ForegroundColor Gray
}

Write-Host "Dependencies satisfied successfully." -ForegroundColor Green
exit 0