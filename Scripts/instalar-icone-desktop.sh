#!/bin/bash
set -e
cd "$(dirname "$0")/.."

PROJECT_DIR="$(pwd)"
APPIMAGE_PATH="$PROJECT_DIR/GerenciadorMdiPortatil-x86_64.AppImage"
if [ ! -f "$APPIMAGE_PATH" ] && [ -f "$PROJECT_DIR/dist/release/GerenciadorMdiPortatil-x86_64.AppImage" ]; then
  APPIMAGE_PATH="$PROJECT_DIR/dist/release/GerenciadorMdiPortatil-x86_64.AppImage"
fi
if [ ! -f "$APPIMAGE_PATH" ]; then
  APPIMAGE_PATH="$PROJECT_DIR/dist/linux/AvaloniaMdiApp"
fi

chmod +x "$APPIMAGE_PATH" 2>/dev/null || true

ICON_DIR="$HOME/.local/share/icons/hicolor/256x256/apps"
PIXMAP_DIR="$HOME/.local/share/pixmaps"
APPS_DIR="$HOME/.local/share/applications"

mkdir -p "$ICON_DIR" "$PIXMAP_DIR" "$APPS_DIR"
cp -f "$PROJECT_DIR/Assets/app-icon.png" "$ICON_DIR/avaloniamdiapp.png"
cp -f "$PROJECT_DIR/Assets/app-icon.png" "$PIXMAP_DIR/avaloniamdiapp.png"

DESKTOP_CONTENT="[Desktop Entry]
Version=1.0
Type=Application
Name=Gerenciador MDI Portátil
GenericName=Workspace MDI e Fluxograma
Comment=Gerenciador de Janelas MDI, Planilhas, Fluxograma e Widgets
Exec=\"$APPIMAGE_PATH\" %U
Icon=$ICON_DIR/avaloniamdiapp.png
Terminal=false
Categories=Office;Utility;ProjectManagement;
StartupWMClass=AvaloniaMdiApp
StartupNotify=true"

echo "$DESKTOP_CONTENT" > "$APPS_DIR/AvaloniaMdiApp.desktop"
echo "$DESKTOP_CONTENT" > "$APPS_DIR/avaloniamdiapp.desktop"
chmod +x "$APPS_DIR/AvaloniaMdiApp.desktop" "$APPS_DIR/avaloniamdiapp.desktop"

for DESK_DIR in "$HOME/Área de trabalho" "$HOME/Área de Trabalho" "$HOME/Desktop"; do
  if [ -d "$DESK_DIR" ]; then
    SHORTCUT="$DESK_DIR/Gerenciador-MDI-Portatil.desktop"
    echo "$DESKTOP_CONTENT" > "$SHORTCUT"
    chmod +x "$SHORTCUT"
    gio set "$SHORTCUT" metadata::trusted true 2>/dev/null || true
    gio set "$APPIMAGE_PATH" metadata::custom-icon "file://$ICON_DIR/avaloniamdiapp.png" 2>/dev/null || true
    echo " -> Atalho criado na Área de Trabalho: $SHORTCUT"
    break
  fi
done

update-desktop-database "$APPS_DIR" 2>/dev/null || true
gtk-update-icon-cache -f -t "$HOME/.local/share/icons/hicolor" 2>/dev/null || true

echo "=========================================="
echo "Ícone instalado com sucesso no Ubuntu!"
echo " - Menu de Aplicativos / Barra de Tarefas: $APPS_DIR/AvaloniaMdiApp.desktop"
echo " - Ícone do Sistema: $ICON_DIR/avaloniamdiapp.png"
echo "=========================================="
