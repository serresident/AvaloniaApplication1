#!/bin/bash
# Скрипт сборки и публикации HMI проекта под панель RealLab NLcon-LXD12-IP65 (Debian 9/11 ARM)

set -e

ARCH="${1:-linux-arm}"
CONFIG="${2:-Release}"
OUTPUT_DIR="${3:-./publish/reallab-arm}"

echo "=========================================================="
echo " Сборка Avalonia HMI под панель RealLab ($ARCH)"
echo "=========================================================="

PROJECT_PATH="AvaloniaApplication1/AvaloniaApplication1.csproj"
if [ ! -f "$PROJECT_PATH" ]; then
    PROJECT_PATH="./AvaloniaApplication1.csproj"
fi

echo "1. Публикация автономного (self-contained) бинарника..."
dotnet publish "$PROJECT_PATH" \
    -c "$CONFIG" \
    -r "$ARCH" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:UseAppHost=true \
    -o "$OUTPUT_DIR"

echo "2. Копирование HMI конфигурации парка ММА..."
CONFIG_SOURCE="AvaloniaApplication1/config_mma_park.json"
if [ ! -f "$CONFIG_SOURCE" ]; then
    CONFIG_SOURCE="./config_mma_park.json"
fi

if [ -f "$CONFIG_SOURCE" ]; then
    cp "$CONFIG_SOURCE" "$OUTPUT_DIR/config.json"
    cp "$CONFIG_SOURCE" "$OUTPUT_DIR/config_mma_park.json"
    echo "Конфигурация скопирована в $OUTPUT_DIR/config.json"
fi

echo "=========================================================="
echo " Успешно! Пакет для панели подготовлен в: $OUTPUT_DIR"
echo "=========================================================="
