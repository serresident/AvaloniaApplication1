# Скрипт автоматизированной диагностики памяти и поиска утечек в 24/7 SCADA
param(
    [string]$ProcessName = "AvaloniaApplication1",
    [string]$OutputDir = "./artifacts/dumps"
)

$ErrorActionPreference = "Stop"

Write-Host "=== [Memory Diagnostics] Анализ кучи и утечек памяти CLR (.NET 10) ===" -ForegroundColor Cyan

# 1. Поиск целевого процесса
$proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) {
    Write-Warning "Процесс '$ProcessName' не обнаружен."
    Write-Host "Доступные .NET процессы для сбора дампов (dotnet-gcdump ps):" -ForegroundColor Yellow
    dotnet tool run dotnet-gcdump -- ps
    Write-Host "`nДля запуска приложения в отладочном режиме выполните:"
    Write-Host "  dotnet run --project AvaloniaApplication1/AvaloniaApplication1.csproj" -ForegroundColor Green
    return
}

Write-Host "✓ Обнаружен целевой процесс: $ProcessName (PID: $($proc.Id), Рабочий набор: $([math]::Round($proc.WorkingSet64 / 1MB, 2)) МБ)" -ForegroundColor Green

# 2. Создание директории и фиксация имени дампа
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$dumpPath = "$OutputDir/${ProcessName}_$timestamp.gcdump"

# 3. Снятие легковесного GC-дампа (без длительной заморозки UI)
Write-Host "Снятие снимка кучи GC (dotnet-gcdump collect)..." -ForegroundColor Yellow
dotnet tool run dotnet-gcdump -- collect -p $proc.Id -o $dumpPath

if (Test-Path $dumpPath) {
    Write-Host "✓ GC-дамп успешно сохранен: $dumpPath" -ForegroundColor Green
    
    # 4. Генерация аналитического отчета
    Write-Host "`nАнализ удерживаемых объектов и GC Roots (Топ-25 типов):" -ForegroundColor Cyan
    $report = dotnet tool run dotnet-gcdump -- report $dumpPath
    $report | Select-Object -First 30
    
    # 5. Проверка специфичных типов SCADA
    Write-Host "`nПроверка ключевых компонентов мнемосхемы и подписок:" -ForegroundColor Yellow
    $scadaTypes = @("WidgetViewModel", "DashboardPanel", "CompositeDisposable", "TrendPenViewModel", "ValveControl", "PipeControl")
    foreach ($type in $scadaTypes) {
        $matches = $report | Select-String $type
        if ($matches) {
            Write-Host "  $matches" -ForegroundColor White
        }
    }
    Write-Host "`nДиагностика завершена успешно!" -ForegroundColor Green
} else {
    Write-Error "Не удалось сформировать файл дампа памяти."
}
