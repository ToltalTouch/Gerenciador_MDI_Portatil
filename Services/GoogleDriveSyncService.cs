using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace AvaloniaMdiApp
{
    public static class GoogleDriveSyncService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        /// <summary>
        /// Detecta automaticamente o caminho padrão da pasta do Google Drive no computador.
        /// </summary>
        public static string DetectarPastaPadraoGoogleDrive()
        {
            try
            {
                // Possíveis caminhos do Google Drive para Desktop no Windows
                string[] candidatosWindows =
                {
                    @"G:\Meu Drive",
                    @"G:\My Drive",
                    @"H:\Meu Drive",
                    @"H:\My Drive",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Google Drive"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Meu Drive"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Google Drive", "Meu Drive"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "GoogleDrive")
                };

                foreach (var path in candidatosWindows)
                {
                    if (Directory.Exists(path))
                    {
                        return Path.Combine(path, "Gerenciador MDI");
                    }
                }

                // Fallback amigável dentro da pasta do usuário
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Google Drive", "Gerenciador MDI");
            }
            catch
            {
                return Path.Combine(AppContext.BaseDirectory, "GoogleDriveBackup");
            }
        }

        /// <summary>
        /// Salva o estado completo do Workspace na pasta sincronizada do Google Drive.
        /// </summary>
        public static bool SincronizarWorkspaceNoGoogleDrive(
            WorkspaceData wsData,
            string pastaConfigurada,
            out string caminhoSalvo,
            out string mensagemErro)
        {
            caminhoSalvo = string.Empty;
            mensagemErro = string.Empty;

            try
            {
                string pastaFinal = string.IsNullOrWhiteSpace(pastaConfigurada)
                    ? DetectarPastaPadraoGoogleDrive()
                    : pastaConfigurada.Trim();

                if (!Directory.Exists(pastaFinal))
                {
                    Directory.CreateDirectory(pastaFinal);
                }

                // Nome seguro do arquivo baseado no workspace
                string nomeSeguro = SanitizarNomeArquivo(wsData.Name);
                if (string.IsNullOrWhiteSpace(nomeSeguro))
                {
                    nomeSeguro = "Workspace";
                }

                string nomeArquivoPrincipal = $"{nomeSeguro} - Nuvem Google Drive.mdiws";
                string caminhoFinal = Path.Combine(pastaFinal, nomeArquivoPrincipal);

                // Serializa o WorkspaceData
                string json = JsonSerializer.Serialize(wsData, JsonOptions);
                File.WriteAllText(caminhoFinal, json);

                // Salva também metadados de sincronização
                string metaPath = Path.Combine(pastaFinal, "sync_status.json");
                var metadata = new
                {
                    UltimaSincronizacaoUtc = DateTime.UtcNow,
                    UltimaSincronizacaoLocal = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                    WorkspaceNome = wsData.Name,
                    TotalJanelas = wsData.SavedWindows.Count,
                    TotalConexoes = wsData.Connections.Count,
                    TotalEtapasCalendario = wsData.CalendarLinks.Count
                };
                File.WriteAllText(metaPath, JsonSerializer.Serialize(metadata, JsonOptions));

                caminhoSalvo = caminhoFinal;
                return true;
            }
            catch (Exception ex)
            {
                mensagemErro = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Abre a pasta do Google Drive no explorador de arquivos do sistema.
        /// </summary>
        public static void AbrirPastaNoExplorador(string pasta)
        {
            try
            {
                if (!Directory.Exists(pasta))
                {
                    Directory.CreateDirectory(pasta);
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", pasta) { UseShellExecute = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start(new ProcessStartInfo("xdg-open", pasta) { UseShellExecute = true });
                }
            }
            catch { }
        }

        /// <summary>
        /// Abre a página oficial de download e configuração do Google Drive para Computador.
        /// </summary>
        public static void AbrirPaginaDownloadGoogleDrive()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "https://www.google.com/drive/download/",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch { }
        }

        private static string SanitizarNomeArquivo(string nome)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var chars = nome.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalidChars, chars[i]) >= 0)
                {
                    chars[i] = '_';
                }
            }
            return new string(chars).Trim();
        }
    }
}
