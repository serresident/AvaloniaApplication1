#!/usr/bin/env bash
set -e

echo "[check-build] Building solution with XAMLIL diagnostics..."

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$ROOT_DIR"
dotnet build AvaloniaApplication1.sln --no-incremental -v:normal /p:AvaloniaShowTrace=true

echo "[check-build] Build passed successfully with 0 errors."
