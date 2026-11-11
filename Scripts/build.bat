@echo off
cd /d "%~dp0.."
echo ==========================================
echo Gerando executavel portatil (.exe) para Windows...
echo ==========================================
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist\windows
echo ==========================================
echo Concluido! O arquivo AvaloniaMdiApp.exe esta na pasta dist\windows
echo ==========================================
pause
