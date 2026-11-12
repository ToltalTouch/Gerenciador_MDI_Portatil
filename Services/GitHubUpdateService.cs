using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaMdiApp
{
    public class GitHubReleaseInfo
    {
        public string TagName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string HtmlUrl { get; set; } = string.Empty;
        public string AssetName { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public long AssetSizeBytes { get; set; }
        public bool IsNewer { get; set; }
        public string CurrentVersion { get; set; } = string.Empty;
    }

    public class UpdateDownloadResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string DownloadedFilePath { get; set; } = string.Empty;
        public string ExtractedDirectory { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string TagName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Serviço leve para verificar tags/releases no GitHub e baixar automaticamente novas versões.
    /// </summary>
    public static class GitHubUpdateService
    {
        public const string DefaultRepository = "ToltalTouch/Janela_multitarefa";

        private static readonly HttpClient Http = CriarHttpClient();

        private static HttpClient CriarHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("GerenciadorMdiPortatil-Updater/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return client;
        }

        public static string ObterVersaoAtual()
        {
            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            if (ver == null) return "1.0.0";
            return $"{ver.Major}.{ver.Minor}.{Math.Max(0, ver.Build)}";
        }

        public static string NormalizarRepositorio(string? input)
        {
            string raw = (input ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
                return DefaultRepository;

            // Aceita URL completa (ex: https://github.com/ToltalTouch/Janela_multitarefa.git) ou "owner/repo"
            var match = Regex.Match(raw, @"github\.com/([^/\s]+/[^/\s\.]+)", RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1].Value;

            if (raw.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                raw = raw[..^4];

            return raw.Trim('/');
        }

        public static async Task<GitHubReleaseInfo?> VerificarUltimaReleaseAsync(
            string? repositorio,
            string? ultimaTagInstalada = null,
            CancellationToken ct = default)
        {
            string repo = NormalizarRepositorio(repositorio);
            string currentVer = ObterVersaoAtual();

            JsonElement releaseJson;
            string urlLatest = $"https://api.github.com/repos/{repo}/releases/latest";

            var response = await Http.GetAsync(urlLatest, ct);
            if (response.IsSuccessStatusCode)
            {
                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                releaseJson = doc.RootElement.Clone();
            }
            else
            {
                // Fallback para a lista de releases (inclui pre-releases)
                string urlList = $"https://api.github.com/repos/{repo}/releases?per_page=1";
                var listResp = await Http.GetAsync(urlList, ct);
                if (listResp.IsSuccessStatusCode)
                {
                    using var doc = await JsonDocument.ParseAsync(await listResp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                    {
                        releaseJson = doc.RootElement[0].Clone();
                    }
                    else
                    {
                        return await VerificarPorTagsAsync(repo, currentVer, ultimaTagInstalada, ct);
                    }
                }
                else
                {
                    return await VerificarPorTagsAsync(repo, currentVer, ultimaTagInstalada, ct);
                }
            }

            string tagName = releaseJson.TryGetProperty("tag_name", out var tagProp)
                ? (tagProp.GetString() ?? string.Empty)
                : string.Empty;

            if (string.IsNullOrWhiteSpace(tagName))
                return null;

            string title = releaseJson.TryGetProperty("name", out var nameProp) && !string.IsNullOrWhiteSpace(nameProp.GetString())
                ? nameProp.GetString()!
                : tagName;

            string notes = releaseJson.TryGetProperty("body", out var bodyProp)
                ? (bodyProp.GetString() ?? string.Empty)
                : string.Empty;

            string htmlUrl = releaseJson.TryGetProperty("html_url", out var htmlProp)
                ? (htmlProp.GetString() ?? $"https://github.com/{repo}/releases")
                : $"https://github.com/{repo}/releases";

            (string assetName, string downloadUrl, long sizeBytes) = SelecionarMelhorAssetParaSO(releaseJson, repo, tagName);

            bool isNewer = EhVersaoMaisNova(currentVer, ultimaTagInstalada, tagName);

            return new GitHubReleaseInfo
            {
                TagName = tagName,
                Title = title,
                Notes = notes,
                HtmlUrl = htmlUrl,
                AssetName = assetName,
                DownloadUrl = downloadUrl,
                AssetSizeBytes = sizeBytes,
                IsNewer = isNewer,
                CurrentVersion = currentVer
            };
        }

        private static async Task<GitHubReleaseInfo?> VerificarPorTagsAsync(
            string repo,
            string currentVer,
            string? ultimaTagInstalada,
            CancellationToken ct)
        {
            string urlTags = $"https://api.github.com/repos/{repo}/tags?per_page=1";
            var resp = await Http.GetAsync(urlTags, ct);
            if (!resp.IsSuccessStatusCode) return null;

            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                return null;

            var firstTag = doc.RootElement[0];
            string tagName = firstTag.TryGetProperty("name", out var nameProp)
                ? (nameProp.GetString() ?? string.Empty)
                : string.Empty;

            if (string.IsNullOrWhiteSpace(tagName))
                return null;

            string zipUrl = firstTag.TryGetProperty("zipball_url", out var zipProp)
                ? (zipProp.GetString() ?? $"https://github.com/{repo}/archive/refs/tags/{tagName}.zip")
                : $"https://github.com/{repo}/archive/refs/tags/{tagName}.zip";

            bool isNewer = EhVersaoMaisNova(currentVer, ultimaTagInstalada, tagName);

            return new GitHubReleaseInfo
            {
                TagName = tagName,
                Title = $"Tag {tagName}",
                Notes = "Pacote de versão baseado na tag do repositório no GitHub.",
                HtmlUrl = $"https://github.com/{repo}/releases/tag/{tagName}",
                AssetName = $"GerenciadorMdiPortatil-{tagName}.zip",
                DownloadUrl = zipUrl,
                AssetSizeBytes = 0,
                IsNewer = isNewer,
                CurrentVersion = currentVer
            };
        }

        private static (string AssetName, string DownloadUrl, long SizeBytes) SelecionarMelhorAssetParaSO(
            JsonElement releaseJson,
            string repo,
            string tagName)
        {
            bool isWin = OperatingSystem.IsWindows();
            bool isLinux = OperatingSystem.IsLinux();

            string bestName = string.Empty;
            string bestUrl = string.Empty;
            long bestSize = 0;
            int bestScore = -1;

            if (releaseJson.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assetsProp.EnumerateArray())
                {
                    string name = asset.TryGetProperty("name", out var n) ? (n.GetString() ?? string.Empty) : string.Empty;
                    string url = asset.TryGetProperty("browser_download_url", out var u) ? (u.GetString() ?? string.Empty) : string.Empty;
                    long size = asset.TryGetProperty("size", out var s) && s.TryGetInt64(out long sz) ? sz : 0;

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
                        continue;

                    string lower = name.ToLowerInvariant();
                    int score = 1;

                    if (isWin)
                    {
                        if (lower.Contains("win") && lower.EndsWith(".zip")) score = 100;
                        else if (lower.EndsWith(".exe")) score = 95;
                        else if (lower.EndsWith(".msi")) score = 85;
                        else if (lower.EndsWith(".zip") && !lower.Contains("linux")) score = 70;
                    }
                    else if (isLinux)
                    {
                        if (lower.EndsWith(".appimage")) score = 100;
                        else if (lower.Contains("linux") && (lower.EndsWith(".tar.gz") || lower.EndsWith(".zip"))) score = 90;
                        else if (lower.EndsWith(".deb")) score = 80;
                    }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestName = name;
                        bestUrl = url;
                        bestSize = size;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(bestUrl))
            {
                return (bestName, bestUrl, bestSize);
            }

            // Fallback caso a release não tenha binários anexados ainda
            string fallbackUrl = releaseJson.TryGetProperty("zipball_url", out var zb) && !string.IsNullOrWhiteSpace(zb.GetString())
                ? zb.GetString()!
                : $"https://github.com/{repo}/archive/refs/tags/{tagName}.zip";

            return ($"GerenciadorMdiPortatil-{tagName}.zip", fallbackUrl, 0);
        }

        public static bool EhVersaoMaisNova(string currentVersion, string? lastInstalledTag, string remoteTag)
        {
            if (string.IsNullOrWhiteSpace(remoteTag))
                return false;

            if (!string.IsNullOrWhiteSpace(lastInstalledTag) &&
                string.Equals(lastInstalledTag.Trim(), remoteTag.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Version? vCurrent = ExtrairSemVer(currentVersion);
            Version? vRemote = ExtrairSemVer(remoteTag);

            if (vCurrent != null && vRemote != null)
            {
                return vRemote > vCurrent;
            }

            return !string.Equals($"v{currentVersion}", remoteTag.Trim(), StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(currentVersion, remoteTag.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static Version? ExtrairSemVer(string input)
        {
            var match = Regex.Match(input ?? string.Empty, @"(\d+)\.(\d+)(?:\.(\d+))?(?:\.(\d+))?");
            if (!match.Success) return null;

            int major = int.Parse(match.Groups[1].Value);
            int minor = int.Parse(match.Groups[2].Value);
            int build = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
            int rev = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 0;

            return new Version(major, minor, build, rev);
        }

        public static async Task<UpdateDownloadResult> BaixarAtualizacaoAsync(
            GitHubReleaseInfo release,
            IProgress<int>? progress = null,
            CancellationToken ct = default)
        {
            try
            {
                string safeTag = Regex.Replace(release.TagName, @"[^a-zA-Z0-9._-]", "_");
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string updateDir = Path.Combine(baseDir, "Updates", safeTag);
                Directory.CreateDirectory(updateDir);

                string fileName = string.IsNullOrWhiteSpace(release.AssetName)
                    ? $"update-{safeTag}.zip"
                    : Path.GetFileName(release.AssetName);

                string targetFile = Path.Combine(updateDir, fileName);

                using var response = await Http.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? release.AssetSizeBytes;
                await using (var contentStream = await response.Content.ReadAsStreamAsync(ct))
                await using (var fileStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    byte[] buffer = new byte[81920];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                        totalRead += bytesRead;

                        if (totalBytes > 0)
                        {
                            int pct = (int)Math.Clamp((totalRead * 100L) / totalBytes, 0, 100);
                            progress?.Report(pct);
                        }
                    }
                }

                progress?.Report(100);

                string extractedDir = string.Empty;
                string executablePath = string.Empty;

                if (targetFile.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    extractedDir = Path.Combine(updateDir, "extraido");
                    if (Directory.Exists(extractedDir))
                    {
                        Directory.Delete(extractedDir, recursive: true);
                    }
                    ZipFile.ExtractToDirectory(targetFile, extractedDir, overwriteFiles: true);

                    if (OperatingSystem.IsWindows())
                    {
                        var exes = Directory.GetFiles(extractedDir, "*.exe", SearchOption.AllDirectories);
                        if (exes.Length > 0)
                        {
                            executablePath = exes[0];
                        }
                    }
                    else if (OperatingSystem.IsLinux())
                    {
                        var bins = Directory.GetFiles(extractedDir, "AvaloniaMdiApp", SearchOption.AllDirectories);
                        if (bins.Length > 0)
                        {
                            executablePath = bins[0];
                        }
                    }
                }
                else if (targetFile.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                         targetFile.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase))
                {
                    executablePath = targetFile;
                    if (OperatingSystem.IsLinux())
                    {
                        try
                        {
                            File.SetUnixFileMode(targetFile,
                                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                        }
                        catch { }
                    }
                }

                return new UpdateDownloadResult
                {
                    Success = true,
                    TagName = release.TagName,
                    DownloadedFilePath = targetFile,
                    ExtractedDirectory = extractedDir,
                    ExecutablePath = executablePath,
                    Message = $"Versão {release.TagName} baixada com sucesso em:\n{updateDir}"
                };
            }
            catch (Exception ex)
            {
                return new UpdateDownloadResult
                {
                    Success = false,
                    TagName = release.TagName,
                    Message = $"Falha ao baixar atualização: {ex.Message}"
                };
            }
        }

        public static bool IniciarInstalacaoAutomatica(UpdateDownloadResult resultado)
        {
            try
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(resultado.ExecutablePath) && File.Exists(resultado.ExecutablePath))
                {
                    string sourceDir = Path.GetDirectoryName(resultado.ExecutablePath) ?? resultado.ExtractedDirectory;
                    string exeName = Path.GetFileName(resultado.ExecutablePath);
                    string batPath = Path.Combine(Path.GetTempPath(), "atualizar_gerenciador_mdi.bat");

                    var sb = new StringBuilder();
                    sb.AppendLine("@echo off");
                    sb.AppendLine("timeout /t 2 /nobreak >nul");
                    if (!string.IsNullOrWhiteSpace(sourceDir) && Directory.Exists(sourceDir))
                    {
                        sb.AppendLine($"xcopy /E /Y /I \"{sourceDir}\\*\" \"{appDir}\\\" >nul");
                        sb.AppendLine($"start \"\" \"{Path.Combine(appDir, exeName)}\"");
                    }
                    else
                    {
                        sb.AppendLine($"copy /Y \"{resultado.ExecutablePath}\" \"{Path.Combine(appDir, exeName)}\" >nul");
                        sb.AppendLine($"start \"\" \"{Path.Combine(appDir, exeName)}\"");
                    }

                    File.WriteAllText(batPath, sb.ToString(), Encoding.Default);

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c \"{batPath}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    return true;
                }

                if (OperatingSystem.IsLinux() && !string.IsNullOrWhiteSpace(resultado.ExecutablePath) && File.Exists(resultado.ExecutablePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = resultado.ExecutablePath,
                        UseShellExecute = true
                    });
                    return true;
                }

                string pastaParaAbrir = !string.IsNullOrWhiteSpace(resultado.ExtractedDirectory) && Directory.Exists(resultado.ExtractedDirectory)
                    ? resultado.ExtractedDirectory
                    : (Path.GetDirectoryName(resultado.DownloadedFilePath) ?? appDir);

                Process.Start(new ProcessStartInfo
                {
                    FileName = pastaParaAbrir,
                    UseShellExecute = true
                });
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
