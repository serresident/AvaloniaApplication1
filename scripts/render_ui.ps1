param(
    [string]$ViewName = "MainWindow"
)

$ErrorActionPreference = "Stop"
Write-Host "[render_ui] Rendering view: $ViewName..." -ForegroundColor Cyan

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir

Set-Location $rootDir
dotnet run --project AvaloniaApplication1.UIValidation/AvaloniaApplication1.UIValidation.csproj -- $ViewName

Write-Host "[render_ui] Artifacts ready:" -ForegroundColor Green
Write-Host " - Screenshot: artifacts/ui_preview.png"
Write-Host " - Visual Tree: artifacts/ui_tree.json"
