@echo off
cd /d "%~dp0.."

if not exist "dist\windows" mkdir "dist\windows"
if not exist "dist\linux" mkdir "dist\linux"
if not exist "dist\release" mkdir "dist\release"

echo ==========================================
echo [1/3] Gerando executavel portatil (.exe) para Windows (win-x64)...
echo ==========================================
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o dist\windows

powershell -NoProfile -Command "Compress-Archive -Path 'dist\windows\*' -DestinationPath 'dist\release\GerenciadorMdiPortatil-win-x64.zip' -Force"

echo ==========================================
echo [2/3] Gerando binarios portateis para Linux (linux-x64)...
echo ==========================================
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o dist\linux
copy /Y "Assets\app-icon.png" "dist\linux\app-icon.png" >nul

echo ==========================================
echo [3/3] Gerando pacote .tar.gz para Linux...
echo ==========================================
tar -czf "dist\release\GerenciadorMdiPortatil-linux-x64.tar.gz" -C "dist\linux" .

echo ==========================================
echo Concluido com sucesso!
echo  - Executavel Windows: dist\windows\AvaloniaMdiApp.exe
echo  - Zip Windows (Release): dist\release\GerenciadorMdiPortatil-win-x64.zip
echo  - Pacote Linux (.tar.gz): dist\release\GerenciadorMdiPortatil-linux-x64.tar.gz
echo ==========================================
pause
