#!/bin/bash
set -e
cd "$(dirname "$0")/.."

echo "=========================================="
echo "Gerando pacote AppImage para Ubuntu/Linux (x86_64)..."
echo "=========================================="

mkdir -p dist/linux
mkdir -p dist/release

if [ ! -f "dist/linux/AvaloniaMdiApp" ]; then
  echo "Compilando binário Linux antes de empacotar o AppImage..."
  XDG_RUNTIME_DIR=/tmp dotnet publish -c Release -r ubuntu.26.04-x64 --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:DebugType=None \
    -p:DebugSymbols=false \
    --source /home/micael/.nuget/packages \
    -o dist/linux
fi

APPDIR="/tmp/GerenciadorMdiPortatil.AppDir"
rm -rf "$APPDIR"
mkdir -p "$APPDIR/usr/bin"
mkdir -p "$APPDIR/usr/share/applications"
mkdir -p "$APPDIR/usr/share/icons/hicolor/256x256/apps"
mkdir -p "$APPDIR/usr/share/metainfo"

# 1. Copiar executável e bibliotecas nativas
cp -r dist/linux/* "$APPDIR/usr/bin/"
chmod +x "$APPDIR/usr/bin/AvaloniaMdiApp"

# 2. Copiar ícones para a raiz do AppDir (.DirIcon e avaloniamdiapp.png) e usr/share/icons
cp Assets/app-icon.png "$APPDIR/avaloniamdiapp.png"
cp Assets/app-icon.png "$APPDIR/.DirIcon"
cp Assets/app-icon.png "$APPDIR/usr/share/icons/hicolor/256x256/apps/avaloniamdiapp.png"

# 3. Criar arquivo .desktop padrão dentro do AppDir
cat > "$APPDIR/avaloniamdiapp.desktop" << 'EOF'
[Desktop Entry]
Version=1.0
Type=Application
Name=Gerenciador MDI Portátil
GenericName=Workspace MDI e Fluxograma
Comment=Gerenciador de Janelas MDI, Planilhas, Fluxograma e Widgets
Exec=AvaloniaMdiApp %U
Icon=avaloniamdiapp
Terminal=false
Categories=Office;Utility;ProjectManagement;
StartupWMClass=AvaloniaMdiApp
StartupNotify=true
EOF

cp "$APPDIR/avaloniamdiapp.desktop" "$APPDIR/usr/share/applications/avaloniamdiapp.desktop"

# 4. Criar script AppRun (integra ícone na Barra de Tarefas/Dock do Ubuntu e inicia o app)
cat > "$APPDIR/AppRun" << 'EOF'
#!/bin/bash
HERE="$(dirname "$(readlink -f "${0}")")"
export APPDIR="${APPDIR:-$HERE}"

# Garante que o ícone e o .desktop estejam registrados na sessão do usuário para a Barra de Tarefas / Dock do Ubuntu
if [ -n "$HOME" ] && [ -d "$HOME" ]; then
  ICON_DEST_DIR="$HOME/.local/share/icons/hicolor/256x256/apps"
  PIXMAP_DEST_DIR="$HOME/.local/share/pixmaps"
  APPS_DEST_DIR="$HOME/.local/share/applications"
  mkdir -p "$ICON_DEST_DIR" "$PIXMAP_DEST_DIR" "$APPS_DEST_DIR" 2>/dev/null || true

  cp -f "$HERE/avaloniamdiapp.png" "$ICON_DEST_DIR/avaloniamdiapp.png" 2>/dev/null || true
  cp -f "$HERE/avaloniamdiapp.png" "$PIXMAP_DEST_DIR/avaloniamdiapp.png" 2>/dev/null || true

  EXEC_TARGET="${APPIMAGE:-$HERE/usr/bin/AvaloniaMdiApp}"
  cat > "$APPS_DEST_DIR/AvaloniaMdiApp.desktop" << DESKTOP_EOF
[Desktop Entry]
Version=1.0
Type=Application
Name=Gerenciador MDI Portátil
GenericName=Workspace MDI e Fluxograma
Comment=Gerenciador de Janelas MDI, Planilhas, Fluxograma e Widgets
Exec="$EXEC_TARGET" %U
Icon=$ICON_DEST_DIR/avaloniamdiapp.png
Terminal=false
Categories=Office;Utility;ProjectManagement;
StartupWMClass=AvaloniaMdiApp
StartupNotify=true
DESKTOP_EOF
  chmod +x "$APPS_DEST_DIR/AvaloniaMdiApp.desktop" 2>/dev/null || true
  cp -f "$APPS_DEST_DIR/AvaloniaMdiApp.desktop" "$APPS_DEST_DIR/avaloniamdiapp.desktop" 2>/dev/null || true
fi

exec "$HERE/usr/bin/AvaloniaMdiApp" "$@"
EOF
chmod +x "$APPDIR/AppRun"

# 5. Gerar imagem SquashFS compactada do AppDir
SQUASHFS_FILE="/tmp/avaloniamdiapp.squashfs"
rm -f "$SQUASHFS_FILE"
mksquashfs "$APPDIR" "$SQUASHFS_FILE" -root-owned -noappend -comp gzip >/dev/null

# 6. Compilar cabeçalho ELF Runtime Type 2 do AppImage (compatível com Ubuntu 22.04/24.04/26.04 sem exigir libfuse2)
RUNTIME_SRC="/tmp/appimage_runtime.c"
RUNTIME_BIN="/tmp/appimage_runtime"

cat > "$RUNTIME_SRC" << 'EOF'
#define _GNU_SOURCE
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <sys/stat.h>
#include <sys/wait.h>
#include <limits.h>
#include <errno.h>

static const volatile char appimage_marker[] = "APPIMAGE_SQUASHFS_OFFSET_PLACEHOLDER_000000000000";

static long get_squashfs_offset(const char *self_path) {
    FILE *f = fopen(self_path, "rb");
    if (!f) return -1;
    if (fseek(f, 0, SEEK_END) != 0) { fclose(f); return -1; }
    long total = ftell(f);
    fclose(f);

    long offset = atol(appimage_marker + 37);
    if (offset > 0 && offset < total) {
        return offset;
    }
    return -1;
}

int main(int argc, char **argv) {
    char self_path[PATH_MAX];
    ssize_t len = readlink("/proc/self/exe", self_path, sizeof(self_path) - 1);
    if (len <= 0) {
        perror("readlink /proc/self/exe");
        return 1;
    }
    self_path[len] = '\0';

    long offset = get_squashfs_offset(self_path);
    if (offset <= 0) {
        fprintf(stderr, "Erro: offset SquashFS inválido no AppImage.\n");
        return 1;
    }

    if (argc >= 2) {
        if (strcmp(argv[1], "--appimage-offset") == 0) {
            printf("%ld\n", offset);
            return 0;
        }
        if (strcmp(argv[1], "--appimage-version") == 0) {
            printf("AppImage Type 2 Runtime (FUSE-free Ubuntu compatible)\n");
            return 0;
        }
        if (strcmp(argv[1], "--appimage-help") == 0) {
            printf("Opções AppImage:\n");
            printf("  --appimage-extract          Extrai o conteúdo para ./squashfs-root\n");
            printf("  --appimage-extract-and-run  Extrai e executa o AppImage\n");
            printf("  --appimage-offset           Exibe o offset da imagem SquashFS\n");
            return 0;
        }
        if (strcmp(argv[1], "--appimage-extract") == 0) {
            char offset_str[32];
            snprintf(offset_str, sizeof(offset_str), "%ld", offset);
            execlp("unsquashfs", "unsquashfs", "-f", "-d", "squashfs-root", "-o", offset_str, self_path, NULL);
            perror("unsquashfs");
            return 1;
        }
    }

    struct stat st;
    if (stat(self_path, &st) != 0) {
        perror("stat self");
        return 1;
    }

    char cache_dir[PATH_MAX];
    snprintf(cache_dir, sizeof(cache_dir), "/tmp/.appimage_mdi_%u_%ld_%ld",
             (unsigned)getuid(), (long)st.st_size, (long)st.st_mtime);

    char apprun_path[PATH_MAX];
    snprintf(apprun_path, sizeof(apprun_path), "%s/AppRun", cache_dir);

    if (access(apprun_path, X_OK) != 0) {
        char offset_str[32];
        snprintf(offset_str, sizeof(offset_str), "%ld", offset);

        pid_t pid = fork();
        if (pid == 0) {
            freopen("/dev/null", "w", stdout);
            execlp("unsquashfs", "unsquashfs", "-f", "-d", cache_dir, "-o", offset_str, self_path, NULL);
            _exit(127);
        } else if (pid > 0) {
            int status = 0;
            waitpid(pid, &status, 0);
            if (!WIFEXITED(status) || WEXITSTATUS(status) != 0) {
                fprintf(stderr, "Falha ao descompactar AppImage com unsquashfs.\n");
                return 1;
            }
        } else {
            perror("fork");
            return 1;
        }
    }

    char cwd[PATH_MAX];
    if (getcwd(cwd, sizeof(cwd)) != NULL) {
        setenv("OWD", cwd, 1);
    }
    setenv("APPIMAGE", self_path, 1);
    setenv("APPDIR", cache_dir, 1);
    setenv("ARGV0", argv[0], 1);

    int shift_args = (argc >= 2 && strcmp(argv[1], "--appimage-extract-and-run") == 0) ? 1 : 0;
    int new_argc = argc - shift_args;
    char **new_argv = calloc(new_argc + 1, sizeof(char *));
    new_argv[0] = apprun_path;
    for (int i = 1; i < new_argc; i++) {
        new_argv[i] = argv[i + shift_args];
    }
    new_argv[new_argc] = NULL;

    execv(apprun_path, new_argv);
    perror("execv AppRun");
    return 1;
}
EOF

gcc -O2 -s "$RUNTIME_SRC" -o "$RUNTIME_BIN"

# 7. Gravar os bytes mágicos do AppImage Type 2 ("AI\x02" no offset 8 do cabeçalho ELF) e o offset exato do SquashFS
python3 - << 'PYEOF'
import os

runtime_path = "/tmp/appimage_runtime"
squashfs_path = "/tmp/avaloniamdiapp.squashfs"
output_path = "dist/release/GerenciadorMdiPortatil-x86_64.AppImage"

with open(runtime_path, "rb") as f:
    runtime_bytes = bytearray(f.read())

# Pad runtime to 4096-byte alignment
pad_len = (4096 - (len(runtime_bytes) % 4096)) % 4096
runtime_bytes.extend(b"\x00" * pad_len)
squashfs_offset = len(runtime_bytes)

# Set AppImage Type 2 magic bytes at ELF e_ident[8..10]: 'A', 'I', 0x02
runtime_bytes[8] = 0x41
runtime_bytes[9] = 0x49
runtime_bytes[10] = 0x02

# Patch placeholder offset string inside runtime binary
placeholder = b"APPIMAGE_SQUASHFS_OFFSET_PLACEHOLDER_000000000000"
idx = runtime_bytes.find(placeholder)
if idx < 0:
    raise RuntimeError("Placeholder not found in runtime binary")

patched_str = f"APPIMAGE_SQUASHFS_OFFSET_PLACEHOLDER_{squashfs_offset:012d}".encode("ascii")
runtime_bytes[idx:idx + len(placeholder)] = patched_str

with open(output_path, "wb") as out:
    out.write(runtime_bytes)
    with open(squashfs_path, "rb") as sq:
        while True:
            chunk = sq.read(1024 * 1024)
            if not chunk:
                break
            out.write(chunk)

os.chmod(output_path, 0o755)
print(f"AppImage criado com sucesso: {output_path} (SquashFS offset: {squashfs_offset})")
PYEOF

# Copia também para a raiz do projeto para fácil execução
cp -f dist/release/GerenciadorMdiPortatil-x86_64.AppImage ./GerenciadorMdiPortatil-x86_64.AppImage
chmod +x ./GerenciadorMdiPortatil-x86_64.AppImage

rm -rf "$APPDIR" "$SQUASHFS_FILE" "$RUNTIME_SRC" "$RUNTIME_BIN"
echo "=========================================="
echo "AppImage pronto em:"
echo " -> ./GerenciadorMdiPortatil-x86_64.AppImage"
echo " -> dist/release/GerenciadorMdiPortatil-x86_64.AppImage"
echo "=========================================="
