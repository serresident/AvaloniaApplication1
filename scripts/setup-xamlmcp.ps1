# Скрипт быстрой настройки XamlMcp для AvaloniaApplication1
param(
    [switch]$InstallPackage = $true,
    [switch]$InstallRuntime = $false
)

$ErrorActionPreference = "Stop"

Write-Host "=== Настройка XamlMcp для AvaloniaApplication1 ===" -ForegroundColor Cyan

# 1. Проверка / Установка пакета XamlMcp.Avalonia в проект
if ($InstallPackage) {
    Write-Host "[1/3] Добавление NuGet-пакета XamlMcp.Avalonia в AvaloniaApplication1..." -ForegroundColor Yellow
    dotnet add "AvaloniaApplication1/AvaloniaApplication1.csproj" package XamlMcp.Avalonia --version 1.0.0-preview.3
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Пакет XamlMcp.Avalonia успешно подключен!" -ForegroundColor Green
    }
}

# 2. Проверка .NET 10 Runtime для MCP-сервера
Write-Host "[2/3] Проверка среды выполнения .NET 10 для XamlMcp.Server..." -ForegroundColor Yellow
$net10 = dotnet --list-runtimes | Select-String "Microsoft.NETCore.App 10\."
if ($net10) {
    Write-Host "✓ Обнаружен .NET 10 Runtime: $net10" -ForegroundColor Green
} else {
    Write-Warning "! Среда выполнения .NET 10 не найдена."
    Write-Host "Для работы MCP-сервера XamlMcp требуется .NET 10 Runtime."
    Write-Host "Выполните установку через winget:" -ForegroundColor Yellow
    Write-Host "  winget install Microsoft.DotNet.Runtime.10" -ForegroundColor White
    if ($InstallRuntime) {
        Write-Host "Запуск автоматической установки winget..." -ForegroundColor Yellow
        winget install Microsoft.DotNet.Runtime.10 --silent --accept-package-agreements --accept-source-agreements
    }
}

# 3. Инструкция по запуску
Write-Host "[3/3] Готово к использованию!" -ForegroundColor Cyan
Write-Host "Для запуска приложения с активным агентом XamlMcp выполните:"
Write-Host '  $env:XAML_MCP=1; dotnet run --project AvaloniaApplication1/AvaloniaApplication1.csproj' -ForegroundColor Green
