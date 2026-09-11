$ErrorActionPreference = "Stop"
Write-Host "[check-build] Building solution with XAMLIL diagnostics..." -ForegroundColor Cyan

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir

Set-Location $rootDir
dotnet build AvaloniaApplication1.sln --no-incremental -v:normal /p:AvaloniaShowTrace=true

Write-Host "[check-build] Build passed successfully with 0 errors." -ForegroundColor Green
