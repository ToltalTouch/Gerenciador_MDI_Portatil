#!/bin/bash
set -e
cd "$(dirname "$0")/.."

echo "=========================================="
echo "Iniciando o build dos executáveis portáteis..."
echo "=========================================="

rm -rf dist
mkdir -p dist/linux
mkdir -p dist/windows
mkdir -p dist/release

echo "[1/3] Compilando executável portátil Single-File para Linux (x64)..."
XDG_RUNTIME_DIR=/tmp dotnet publish -c Release -r ubuntu.26.04-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=None \
  -p:DebugSymbols=false \
  --source /home/micael/.nuget/packages \
  -o dist/linux

cp Assets/app-icon.png dist/linux/app-icon.png

echo "[2/4] Empacotando arquivos para GitHub Releases (Linux)..."
tar -czf dist/release/GerenciadorMdiPortatil-linux-x64.tar.gz -C dist/linux .
python3 -c 'import shutil; shutil.make_archive("dist/release/GerenciadorMdiPortatil-linux-x64", "zip", "dist/linux")'

echo "[3/4] Gerando AppImage (.AppImage) para Ubuntu/Linux..."
bash Scripts/build-appimage.sh

echo "[4/4] Tentando compilar para Windows (x64) se pacotes de runtime win-x64 estiverem disponíveis..."
if XDG_RUNTIME_DIR=/tmp dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=None \
  -p:DebugSymbols=false \
  -o dist/windows 2>/dev/null; then
  cp Assets/app-icon.ico dist/windows/app-icon.ico
  python3 -c '
import shutil
shutil.make_archive("dist/release/GerenciadorMdiPortatil-win-x64", "zip", "dist/windows")
'
  echo " -> Pacote Windows gerado em dist/release/GerenciadorMdiPortatil-win-x64.zip"
else
  echo " -> Aviso: Pacote de runtime win-x64 requer conexão com NuGet (execute fora de ambiente offline se desejar gerar o .exe de Windows)."
fi

# Restaura project.assets.json padrão para dotnet run / dotnet build rápido
XDG_RUNTIME_DIR=/tmp dotnet restore --source /home/micael/.nuget/packages >/dev/null 2>&1 || true

echo "=========================================="
echo "Build concluído com sucesso!"
echo "Executável Linux: dist/linux/AvaloniaMdiApp"
echo "Pacote GitHub Releases: dist/release/GerenciadorMdiPortatil-linux-x64.tar.gz"
echo "=========================================="
