# Скрипт сборки и публикации HMI проекта под панель RealLab NLcon-LXD12-IP65 (Debian 9/11 ARM)
param(
    [string]$Architecture = "linux-arm", # linux-arm (32-bit armhf) или linux-arm64 (aarch64)
    [string]$Configuration = "Release",
    [string]$OutputDir = "./publish/reallab-arm"
)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Сборка Avalonia HMI под панель RealLab ($Architecture)" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$projectPath = "AvaloniaApplication1/AvaloniaApplication1.csproj"
if (-not (Test-Path $projectPath)) {
    $projectPath = "./AvaloniaApplication1.csproj"
}

if (-not (Test-Path $projectPath)) {
    Write-Error "Проект $projectPath не найден!"
    exit 1
}

Write-Host "1. Публикация автономного (self-contained) бинарника..." -ForegroundColor Yellow

dotnet publish $projectPath `
    -c $Configuration `
    -r $Architecture `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:UseAppHost=true `
    -o $OutputDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Ошибка сборки dotnet publish!"
    exit $LASTEXITCODE
}

Write-Host "2. Копирование HMI конфигурации парка ММА..." -ForegroundColor Yellow
$configSource = "AvaloniaApplication1/config_mma_park.json"
if (-not (Test-Path $configSource)) {
    $configSource = "./config_mma_park.json"
}

if (Test-Path $configSource) {
    Copy-Item $configSource "$OutputDir/config.json" -Force
    Copy-Item $configSource "$OutputDir/config_mma_park.json" -Force
    Write-Host "Конфигурация скопирована в $OutputDir/config.json" -ForegroundColor Green
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host " Успешно! Пакет для панели подготовлен в: $OutputDir" -ForegroundColor Green
Write-Host " Для отправки на панель выполните:" -ForegroundColor Cyan
Write-Host "   scp -r $OutputDir/* root@<IP_ПАНЕЛИ>:/opt/mma_hmi/" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Green
