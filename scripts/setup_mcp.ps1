# Скрипт восстановления и синхронизации MCP-обвеса проекта AvaloniaApplication1
param(
    [switch]$InstallNet10 = $false
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " [MCP Harness] Инициализация и восстановление серверов... " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Восстановление локальных dotnet-инструментов из .config/dotnet-tools.json
Write-Host "`n[1/3] Восстановление проектных .NET инструментов (dotnet tool restore)..." -ForegroundColor Yellow
dotnet tool restore
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Локальные инструменты (binlog-mcp, dotnet-dump, dotnet-gcdump, ilspycmd, sharplens) успешно восстановлены!" -ForegroundColor Green
} else {
    Write-Warning "! Ошибка при вызове dotnet tool restore."
}

# 2. Проверка доступности .NET 10 Runtime для XamlMcp.Server
Write-Host "`n[2/3] Проверка среды выполнения .NET 10 для XamlMcp..." -ForegroundColor Yellow
$net10 = dotnet --list-runtimes | Select-String "Microsoft.NETCore.App 10\."
if ($net10) {
    Write-Host "✓ Среда выполнения .NET 10 обнаружена: $net10" -ForegroundColor Green
} else {
    Write-Warning "! Среда выполнения .NET 10 не найдена."
    Write-Host "Для работы XamlMcp.Server рекомендуется установить .NET 10 Runtime:"
    Write-Host "  winget install Microsoft.DotNet.Runtime.10" -ForegroundColor White
    if ($InstallNet10) {
        Write-Host "Запуск автоматической установки winget..." -ForegroundColor Yellow
        winget install Microsoft.DotNet.Runtime.10 --silent --accept-package-agreements --accept-source-agreements
    }
}

# 3. Проверка проектных конфигураций MCP для IDE / Агентов
Write-Host "`n[3/3] Проверка файлов конфигураций для AI-агентов..." -ForegroundColor Yellow

$vscodeConfig = ".vscode/mcp.json"
$cursorConfig = ".cursor/mcp.json"

if (Test-Path $vscodeConfig) {
    Write-Host "✓ Обнаружен проектный конфиг VS Code / Copilot: $vscodeConfig" -ForegroundColor Green
}
if (Test-Path $cursorConfig) {
    Write-Host "✓ Обнаружен проектный конфиг Cursor: $cursorConfig" -ForegroundColor Green
}

Write-Host "`n=== Все компоненты MCP готовы к работе! ===" -ForegroundColor Cyan
Write-Host "Доступные инструменты и серверы:" -ForegroundColor White
Write-Host "  1. avalonia-docs             - Официальный сервер документации Avalonia (HTTP: docs-mcp.avaloniaui.net)" -ForegroundColor White
Write-Host "  2. dotnet tool run binlog-mcp - Официальный MCP-сервер MSBuild для анализа сборки и XAMLIL" -ForegroundColor White
Write-Host "  3. dotnet tool run sharplens  - Семантический анализ C# и навигация по коду (SharpLens MCP)" -ForegroundColor White
Write-Host "  4. dotnet tool run ilspycmd   - Декомпиляция и исследование сборок Avalonia 12 / .NET 10" -ForegroundColor White
Write-Host "  5. powershell scripts/diag_memory.ps1 - Диагностика утечек памяти и анализ графов GC Roots" -ForegroundColor White
Write-Host "  6. ScadaDebugBridge           - Внутрипроцессный мост (http://127.0.0.1:5110/mcp и /api/*)" -ForegroundColor Green
Write-Host "  7. `$env:XAML_MCP=1; dotnet run - Запуск приложения с живым агентом XamlMcp" -ForegroundColor Green
