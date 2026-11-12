using Avalonia.Platform;
using System;
using System.Diagnostics;
using System.IO;

namespace AvaloniaMdiApp
{
    public static class LinuxDesktopIntegration
    {
        public const string WmClass = "AvaloniaMdiApp";
        public const string DesktopFileName = "AvaloniaMdiApp.desktop";

        public static void GarantirIntegracaoDesktopLinux(bool criarAtalhoAreaDeTrabalho = false)
        {
            if (!OperatingSystem.IsLinux()) return;

            try
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrWhiteSpace(home) || !Directory.Exists(home)) return;

                // 1. Extrair o ícone para ~/.local/share/icons/hicolor/256x256/apps/avaloniamdiapp.png
                string iconDir = Path.Combine(home, ".local", "share", "icons", "hicolor", "256x256", "apps");
                Directory.CreateDirectory(iconDir);
                string iconPath = Path.Combine(iconDir, "avaloniamdiapp.png");

                using (var stream = AssetLoader.Open(new Uri("avares://AvaloniaMdiApp/Assets/app-icon.png")))
                using (var fs = File.Create(iconPath))
                {
                    stream.CopyTo(fs);
                }

                // Também copia para ~/.local/share/pixmaps para máxima compatibilidade com gerenciadores de janela
                string pixmapsDir = Path.Combine(home, ".local", "share", "pixmaps");
                Directory.CreateDirectory(pixmapsDir);
                File.Copy(iconPath, Path.Combine(pixmapsDir, "avaloniamdiapp.png"), overwrite: true);

                // 2. Determinar o caminho do executável (priorizando APPIMAGE se estiver rodando via AppImage)
                string? appImageEnv = Environment.GetEnvironmentVariable("APPIMAGE");
                string execPath = !string.IsNullOrWhiteSpace(appImageEnv) && File.Exists(appImageEnv)
                    ? appImageEnv
                    : Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "AvaloniaMdiApp");

                // Se existir o AppImage oficial na pasta dist/release do projeto, usa ele para atalhos da Área de Trabalho
                string projectAppImage = Path.Combine(AppContext.BaseDirectory, "GerenciadorMdiPortatil-x86_64.AppImage");
                if (File.Exists(projectAppImage))
                {
                    execPath = projectAppImage;
                }

                string desktopContent =
                    "[Desktop Entry]\n" +
                    "Version=1.0\n" +
                    "Type=Application\n" +
                    "Name=Gerenciador MDI Portátil\n" +
                    "GenericName=Workspace MDI e Fluxograma\n" +
                    "Comment=Gerenciador de Janelas MDI, Planilhas, Fluxograma e Widgets\n" +
                    $"Exec=\"{execPath}\" %U\n" +
                    $"Icon={iconPath}\n" +
                    "Terminal=false\n" +
                    "Categories=Office;Utility;ProjectManagement;\n" +
                    $"StartupWMClass={WmClass}\n" +
                    "StartupNotify=true\n";

                // 3. Registrar em ~/.local/share/applications para que a Barra de Tarefas (Ubuntu Dock) e Menu de Apps exibam o ícone
                string appsDir = Path.Combine(home, ".local", "share", "applications");
                Directory.CreateDirectory(appsDir);

                string userDesktopFile = Path.Combine(appsDir, DesktopFileName);
                File.WriteAllText(userDesktopFile, desktopContent);
                TornarExecutavel(userDesktopFile);

                string lowerDesktopFile = Path.Combine(appsDir, "avaloniamdiapp.desktop");
                File.WriteAllText(lowerDesktopFile, desktopContent);
                TornarExecutavel(lowerDesktopFile);

                // 4. Criar atalho na Área de Trabalho se solicitado
                if (criarAtalhoAreaDeTrabalho)
                {
                    CriarAtalhoNaAreaDeTrabalho(home, desktopContent);
                }
            }
            catch
            {
                // Ignora falhas de permissão de IO silenciosamente
            }
        }

        public static string? CriarAtalhoNaAreaDeTrabalho(string? home = null, string? desktopContent = null)
        {
            if (!OperatingSystem.IsLinux()) return null;

            try
            {
                home ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string? desktopDir = null;

                string[] candidatos =
                {
                    Path.Combine(home, "Área de trabalho"),
                    Path.Combine(home, "Área de Trabalho"),
                    Path.Combine(home, "Desktop")
                };

                foreach (var dir in candidatos)
                {
                    if (Directory.Exists(dir))
                    {
                        desktopDir = dir;
                        break;
                    }
                }

                if (desktopDir == null) return null;

                if (string.IsNullOrWhiteSpace(desktopContent))
                {
                    string appsFile = Path.Combine(home, ".local", "share", "applications", DesktopFileName);
                    if (File.Exists(appsFile))
                    {
                        desktopContent = File.ReadAllText(appsFile);
                    }
                }

                if (string.IsNullOrWhiteSpace(desktopContent)) return null;

                string shortcutPath = Path.Combine(desktopDir, "Gerenciador-MDI-Portatil.desktop");
                File.WriteAllText(shortcutPath, desktopContent);
                TornarExecutavel(shortcutPath);
                MarcarConfiavelGnome(shortcutPath);
                return shortcutPath;
            }
            catch
            {
                return null;
            }
        }

        private static void TornarExecutavel(string filePath)
        {
            try
            {
                if (OperatingSystem.IsLinux())
                {
                    File.SetUnixFileMode(filePath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }
            }
            catch { }
        }

        private static void MarcarConfiavelGnome(string shortcutPath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "gio",
                    Arguments = $"set \"{shortcutPath}\" metadata::trusted true",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(1500);
            }
            catch { }
        }
    }
}
