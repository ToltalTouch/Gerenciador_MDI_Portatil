#!/bin/bash
set -e
cd "$(dirname "$0")/.."

echo "=========================================="
echo "Iniciando o build dos executáveis portáteis..."
echo "=========================================="

rm -rf dist
mkdir -p dist/linux
mkdir -p dist/windows

echo "[1/2] Compilando para Linux (x64)..."
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o dist/linux

echo "[2/2] Compilando para Windows (x64)..."
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist/windows

echo "=========================================="
echo "Build concluído com sucesso!"
echo "Binários salvos nas pastas:"
echo " -> Linux:   dist/linux/"
echo " -> Windows: dist/windows/"
echo "=========================================="
