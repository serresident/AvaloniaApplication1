#!/usr/bin/env bash
set -e

VIEW_NAME="${1:-MainWindow}"
echo "[render_ui] Rendering view: $VIEW_NAME..."

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$ROOT_DIR"
dotnet run --project AvaloniaApplication1.UIValidation/AvaloniaApplication1.UIValidation.csproj -- "$VIEW_NAME"

echo "[render_ui] Artifacts ready:"
echo " - Screenshot: artifacts/ui_preview.png"
echo " - Visual Tree: artifacts/ui_tree.json"
