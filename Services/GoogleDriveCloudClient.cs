using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaMdiApp
{
    public class CloudFileInfo
    {
        public string FileId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string WorkspaceId { get; set; } = string.Empty;
        public DateTime ModifiedTimeUtc { get; set; }
        public bool IsCloudNewer { get; set; }
        public bool ExistsInCloud => !string.IsNullOrWhiteSpace(FileId);
    }

    public static class GoogleDriveCloudClient
    {
        private static readonly HttpClient HttpClient = new();
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public const string DefaultFolderName = "Gerenciador MDI";

        // Credenciais padrão embutidas do aplicativo (usuário comum não precisa preencher)
        public static string DefaultClientId => Deobfuscar("656B6F68646C64686D6B6D6D71696B2E373F2E2F3A6A366B3E3964396A366E6E6431642E6D6B2C33356B366A64723D2C2C2F723B33333B3039292F392E3F333228393228723F3331");
        public static string DefaultClientSecret => Deobfuscar("1B131F0F0C0471250E6C2D6C2F6E6913713D356D68090B242D682F142A170D2B360F17");

        private static string Deobfuscar(string hex)
        {
            try
            {
                byte[] raw = Convert.FromHexString(hex);
                for (int i = 0; i < raw.Length; i++) raw[i] ^= 0x5C;
                return Encoding.UTF8.GetString(raw);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Inicia o fluxo oficial de autorização OAuth 2.0 abrindo o navegador do usuário.
        /// </summary>
        public static async Task<(bool Success, string Email, string RefreshToken, string AccessToken, DateTime ExpiryUtc, string Error)> IniciarFluxoLoginAsync(
            string? clientId = null,
            string? clientSecret = null)
        {
            string cId = !string.IsNullOrWhiteSpace(clientId) ? clientId.Trim() : DefaultClientId;
            string cSec = !string.IsNullOrWhiteSpace(clientSecret) ? clientSecret.Trim() : DefaultClientSecret;

            if (string.IsNullOrWhiteSpace(cId) || string.IsNullOrWhiteSpace(cSec))
            {
                return (false, "", "", "", DateTime.MinValue, "Client ID e Client Secret não foram configurados.");
            }

            int port = 5001;
            HttpListener? listener = null;

            // Tenta abrir porta para o callback local
            for (int p = 5001; p <= 5006; p++)
            {
                try
                {
                    listener = new HttpListener();
                    listener.Prefixes.Add($"http://127.0.0.1:{p}/");
                    listener.Start();
                    port = p;
                    break;
                }
                catch
                {
                    listener?.Close();
                    listener = null;
                }
            }

            if (listener == null)
            {
                return (false, "", "", "", DateTime.MinValue, "Não foi possível abrir o canal local para receber o retorno do login.");
            }

            string redirectUri = $"http://127.0.0.1:{port}/";
            string authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                             $"client_id={Uri.EscapeDataString(cId)}" +
                             $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                             $"&response_type=code" +
                             $"&scope={Uri.EscapeDataString("https://www.googleapis.com/auth/drive.file email profile")}" +
                             $"&access_type=offline" +
                             $"&prompt=consent";

            try
            {
                // Tenta abrir o navegador padrão do usuário de forma segura e resiliente
                AbrirNavegadorPadrao(authUrl);

                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                var contextTask = listener.GetContextAsync();

                var completedTask = await Task.WhenAny(contextTask, Task.Delay(Timeout.Infinite, cts.Token));
                if (completedTask != contextTask)
                {
                    listener.Stop();
                    return (false, "", "", "", DateTime.MinValue, "Tempo esgotado aguardando autorização no navegador.");
                }

                var context = await contextTask;
                var req = context.Request;
                string? code = req.QueryString["code"];
                string? error = req.QueryString["error"];

                // Responde uma página amigável ao navegador
                string htmlResponse = @"<!DOCTYPE html>
<html>
<head><meta charset='utf-8'><title>Login Concluído</title></head>
<body style='font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,sans-serif;background:#0F172A;color:#F8FAFC;display:flex;align-items:center;justify-content:center;height:90vh;margin:0;'>
  <div style='background:#1E293B;border:1px solid #334155;border-radius:12px;padding:36px;text-align:center;max-width:440px;box-shadow:0 12px 32px rgba(0,0,0,0.5);'>
    <div style='font-size:42px;margin-bottom:12px;'>✓</div>
    <h2 style='margin:0 0 8px 0;color:#38BDF8;'>Login Autorizado com Sucesso!</h2>
    <p style='color:#94A3B8;font-size:14px;line-height:1.5;'>Sua conta foi conectada ao Gerenciador MDI Portátil. Você já pode fechar esta aba e retornar ao programa.</p>
  </div>
</body>
</html>";
                byte[] buffer = Encoding.UTF8.GetBytes(htmlResponse);
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer);
                context.Response.OutputStream.Close();
                listener.Stop();

                if (!string.IsNullOrEmpty(error))
                {
                    return (false, "", "", "", DateTime.MinValue, $"Autorização recusada: {error}");
                }

                if (string.IsNullOrEmpty(code))
                {
                    return (false, "", "", "", DateTime.MinValue, "Código de autorização não recebido.");
                }

                // Troca o código pelo access_token e refresh_token
                var tokenReq = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["client_id"] = cId,
                    ["client_secret"] = cSec,
                    ["redirect_uri"] = redirectUri,
                    ["grant_type"] = "authorization_code"
                });

                var tokenResp = await HttpClient.PostAsync("https://oauth2.googleapis.com/token", tokenReq);
                string tokenJson = await tokenResp.Content.ReadAsStringAsync();

                if (!tokenResp.IsSuccessStatusCode)
                {
                    return (false, "", "", "", DateTime.MinValue, $"Falha ao obter token: {tokenJson}");
                }

                using var tokenDoc = JsonDocument.Parse(tokenJson);
                var root = tokenDoc.RootElement;
                string accessToken = root.GetProperty("access_token").GetString() ?? "";
                string refreshToken = root.TryGetProperty("refresh_token", out var rf) ? (rf.GetString() ?? "") : "";
                int expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;
                DateTime expiryUtc = DateTime.UtcNow.AddSeconds(expiresIn - 60);

                // Busca o e-mail do usuário
                string email = "";
                try
                {
                    using var userInfoReq = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
                    userInfoReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    var userResp = await HttpClient.SendAsync(userInfoReq);
                    if (userResp.IsSuccessStatusCode)
                    {
                        using var userDoc = JsonDocument.Parse(await userResp.Content.ReadAsStringAsync());
                        if (userDoc.RootElement.TryGetProperty("email", out var emProp))
                        {
                            email = emProp.GetString() ?? "";
                        }
                    }
                }
                catch { }

                return (true, email, refreshToken, accessToken, expiryUtc, "");
            }
            catch (Exception ex)
            {
                listener?.Stop();
                return (false, "", "", "", DateTime.MinValue, $"Erro no processo de login: {ex.Message}");
            }
        }

        /// <summary>
        /// Garante que o AccessToken esteja válido renovando com o RefreshToken se necessário.
        /// </summary>
        public static async Task<string?> ObterAccessTokenValidoAsync(AppSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.GoogleOAuthRefreshToken))
            {
                return null;
            }

            // Se o token atual ainda é válido
            if (!string.IsNullOrWhiteSpace(settings.GoogleOAuthAccessToken) &&
                DateTime.TryParse(settings.GoogleOAuthTokenExpiryUtc, out var expiry) &&
                expiry > DateTime.UtcNow.AddMinutes(2))
            {
                return settings.GoogleOAuthAccessToken;
            }

            // Renova o token
            try
            {
                string cId = !string.IsNullOrWhiteSpace(settings.GoogleOAuthClientId) ? settings.GoogleOAuthClientId.Trim() : DefaultClientId;
                string cSec = !string.IsNullOrWhiteSpace(settings.GoogleOAuthClientSecret) ? settings.GoogleOAuthClientSecret.Trim() : DefaultClientSecret;

                var refreshReq = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = cId,
                    ["client_secret"] = cSec,
                    ["refresh_token"] = settings.GoogleOAuthRefreshToken.Trim(),
                    ["grant_type"] = "refresh_token"
                });

                var resp = await HttpClient.PostAsync("https://oauth2.googleapis.com/token", refreshReq);
                if (!resp.IsSuccessStatusCode)
                {
                    return null;
                }

                string json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                string newAccessToken = doc.RootElement.GetProperty("access_token").GetString() ?? "";
                int expiresIn = doc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;

                settings.GoogleOAuthAccessToken = newAccessToken;
                settings.GoogleOAuthTokenExpiryUtc = DateTime.UtcNow.AddSeconds(expiresIn - 60).ToString("o");
                SettingsManager.Salvar(settings);

                return newAccessToken;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Envia o Workspace diretamente para a nuvem do Google Drive via API REST.
        /// </summary>
        public static async Task<(bool Success, DateTime ModifiedUtc, string Error)> UploadWorkspaceAsync(
            AppSettings settings,
            WorkspaceData wsData)
        {
            try
            {
                string? token = await ObterAccessTokenValidoAsync(settings);
                if (string.IsNullOrEmpty(token))
                {
                    return (false, DateTime.MinValue, "Não conectado à conta Google ou token expirado.");
                }

                string folderId = await ObterOuCriarPastaMdiAsync(token);
                string fileName = $"workspace_{wsData.Id}.mdiws";

                wsData.LastModifiedUtc = DateTime.UtcNow;
                string jsonContent = JsonSerializer.Serialize(wsData, JsonOptions);
                byte[] fileBytes = Encoding.UTF8.GetBytes(jsonContent);

                // Verifica se o arquivo já existe no Drive
                string? existingFileId = await LocalizarArquivoNoDriveAsync(token, folderId, fileName);

                string boundary = "MdiMultipartBoundary" + Guid.NewGuid().ToString("N");
                var multipartContent = new MultipartContent("related", boundary);

                var metadataDict = new Dictionary<string, object>
                {
                    ["name"] = fileName,
                    ["description"] = $"Backup do Workspace {wsData.Name} no Gerenciador MDI",
                    ["appProperties"] = new Dictionary<string, string>
                    {
                        ["workspaceId"] = wsData.Id,
                        ["workspaceName"] = wsData.Name,
                        ["lastModifiedUtc"] = wsData.LastModifiedUtc.ToString("o")
                    }
                };

                if (existingFileId == null)
                {
                    metadataDict["parents"] = new[] { folderId };
                }

                var metaPart = new StringContent(JsonSerializer.Serialize(metadataDict), Encoding.UTF8, "application/json");
                multipartContent.Add(metaPart);

                var filePart = new ByteArrayContent(fileBytes);
                filePart.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                multipartContent.Add(filePart);

                HttpResponseMessage resp;
                if (existingFileId != null)
                {
                    // Atualiza o arquivo existente
                    string url = $"https://www.googleapis.com/upload/drive/v3/files/{existingFileId}?uploadType=multipart";
                    var patchReq = new HttpRequestMessage(HttpMethod.Patch, url) { Content = multipartContent };
                    patchReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    resp = await HttpClient.SendAsync(patchReq);
                }
                else
                {
                    // Cria novo arquivo
                    string url = "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart";
                    var postReq = new HttpRequestMessage(HttpMethod.Post, url) { Content = multipartContent };
                    postReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    resp = await HttpClient.SendAsync(postReq);
                }

                if (resp.IsSuccessStatusCode)
                {
                    settings.LastCloudSyncUtc = wsData.LastModifiedUtc.ToString("o");
                    SettingsManager.Salvar(settings);
                    return (true, wsData.LastModifiedUtc, "");
                }

                string err = await resp.Content.ReadAsStringAsync();
                return (false, DateTime.MinValue, $"Erro da API Google: {err}");
            }
            catch (Exception ex)
            {
                return (false, DateTime.MinValue, ex.Message);
            }
        }

        /// <summary>
        /// Verifica se há uma versão mais recente do Workspace na nuvem do Google Drive.
        /// </summary>
        public static async Task<(CloudFileInfo? Info, string Error)> VerificarVersaoNuvemAsync(
            AppSettings settings,
            WorkspaceData wsData)
        {
            try
            {
                string? token = await ObterAccessTokenValidoAsync(settings);
                if (string.IsNullOrEmpty(token))
                    return (null, "Não conectado à conta Google ou token de acesso expirado. Faça login novamente nas Configurações.");

                string folderId = await ObterOuCriarPastaMdiAsync(token);
                if (string.IsNullOrEmpty(folderId))
                    return (null, "Não foi possível acessar a pasta 'Gerenciador MDI' no Google Drive.");

                string fileName = $"workspace_{wsData.Id}.mdiws";
                string query = $"'{folderId}' in parents and name = '{fileName}' and trashed = false";
                string url = $"https://www.googleapis.com/drive/v3/files?q={Uri.EscapeDataString(query)}&fields=files(id,name,modifiedTime,appProperties)";

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var resp = await HttpClient.SendAsync(req);
                if (!resp.IsSuccessStatusCode)
                {
                    string errContent = await resp.Content.ReadAsStringAsync();
                    return (null, $"Erro da API Google Drive (HTTP {(int)resp.StatusCode}): {errContent}");
                }

                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                var files = doc.RootElement.GetProperty("files");
                if (files.GetArrayLength() == 0) return (null, string.Empty);

                var file = files[0];
                string fileId = file.GetProperty("id").GetString() ?? "";

                DateTime cloudModified = DateTime.MinValue;
                if (file.TryGetProperty("appProperties", out var appProps) &&
                    appProps.TryGetProperty("lastModifiedUtc", out var modProp) &&
                    DateTime.TryParse(modProp.GetString(), out var dt))
                {
                    cloudModified = dt;
                }
                else if (file.TryGetProperty("modifiedTime", out var mtProp) &&
                         DateTime.TryParse(mtProp.GetString(), out var dtMod))
                {
                    cloudModified = dtMod;
                }

                bool isNewer = cloudModified > wsData.LastModifiedUtc.AddSeconds(5);

                return (new CloudFileInfo
                {
                    FileId = fileId,
                    Name = wsData.Name,
                    WorkspaceId = wsData.Id,
                    ModifiedTimeUtc = cloudModified,
                    IsCloudNewer = isNewer
                }, string.Empty);
            }
            catch (Exception ex)
            {
                return (null, $"Exceção ao verificar versão na nuvem: {ex.Message}");
            }
        }

        /// <summary>
        /// Baixa a versão mais recente do Workspace diretamente do Google Drive.
        /// </summary>
        public static async Task<(WorkspaceData? Workspace, string Error)> BaixarWorkspaceDaNuvemAsync(AppSettings settings, string fileId)
        {
            try
            {
                string? token = await ObterAccessTokenValidoAsync(settings);
                if (string.IsNullOrEmpty(token))
                    return (null, "Não conectado à conta Google ou token expirado. Faça login novamente nas Configurações.");

                string url = $"https://www.googleapis.com/drive/v3/files/{fileId}?alt=media";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var resp = await HttpClient.SendAsync(req);
                if (!resp.IsSuccessStatusCode)
                {
                    string errContent = await resp.Content.ReadAsStringAsync();
                    return (null, $"Erro da API Google Drive ao baixar (HTTP {(int)resp.StatusCode}): {errContent}");
                }

                string json = await resp.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var ws = JsonSerializer.Deserialize<WorkspaceData>(json, options);
                if (ws == null)
                {
                    return (null, "O arquivo foi baixado, mas não continha uma estrutura de Workspace válida.");
                }

                ws.SavedWindows ??= new();
                ws.SavedCatalog ??= new();
                ws.Connections ??= new();
                ws.CalendarLinks ??= new();

                return (ws, string.Empty);
            }
            catch (Exception ex)
            {
                return (null, $"Falha ao baixar da nuvem: {ex.Message}");
            }
        }

        /// <summary>
        /// Lista todos os arquivos de workspace salvos na pasta 'Gerenciador MDI' do Google Drive.
        /// </summary>
        public static async Task<(List<CloudFileInfo> Files, string Error)> ListarWorkspacesNaNuvemAsync(AppSettings settings)
        {
            var result = new List<CloudFileInfo>();
            try
            {
                string? token = await ObterAccessTokenValidoAsync(settings);
                if (string.IsNullOrEmpty(token))
                    return (result, "Não conectado à conta Google ou token expirado.");

                string folderId = await ObterOuCriarPastaMdiAsync(token);
                if (string.IsNullOrEmpty(folderId))
                    return (result, "Não foi possível acessar a pasta 'Gerenciador MDI' no Google Drive.");

                // Busca sem cláusulas 'contains' com pontuação que causam erro 400 Bad Request na API v3 do Google Drive
                string query = $"'{folderId}' in parents and trashed = false";
                string url = $"https://www.googleapis.com/drive/v3/files?q={Uri.EscapeDataString(query)}&fields=files(id,name,modifiedTime,appProperties)&pageSize=100";

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var resp = await HttpClient.SendAsync(req);
                if (!resp.IsSuccessStatusCode)
                {
                    string errContent = await resp.Content.ReadAsStringAsync();
                    return (result, $"Erro ao listar arquivos no Google Drive (HTTP {(int)resp.StatusCode}): {errContent}");
                }

                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                if (doc.RootElement.TryGetProperty("files", out var filesProp) && filesProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var file in filesProp.EnumerateArray())
                    {
                        string fileId = file.GetProperty("id").GetString() ?? "";
                        string fileName = file.GetProperty("name").GetString() ?? "";

                        // Filtra arquivos de workspace do Gerenciador MDI
                        if (!fileName.EndsWith(".mdiws", StringComparison.OrdinalIgnoreCase) &&
                            !fileName.StartsWith("workspace_", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        string wsName = fileName.Replace("workspace_", "").Replace(".mdiws", "");
                        string wsId = "";
                        DateTime cloudModified = DateTime.MinValue;

                        if (file.TryGetProperty("appProperties", out var appProps))
                        {
                            if (appProps.TryGetProperty("workspaceName", out var nameProp))
                                wsName = nameProp.GetString() ?? wsName;
                            if (appProps.TryGetProperty("workspaceId", out var idProp))
                                wsId = idProp.GetString() ?? "";
                            if (appProps.TryGetProperty("lastModifiedUtc", out var modProp) &&
                                DateTime.TryParse(modProp.GetString(), out var dt))
                                cloudModified = dt;
                        }

                        if (string.IsNullOrWhiteSpace(wsId) && fileName.StartsWith("workspace_", StringComparison.OrdinalIgnoreCase))
                        {
                            wsId = fileName.Substring("workspace_".Length).Replace(".mdiws", "");
                        }

                        if (cloudModified == DateTime.MinValue &&
                            file.TryGetProperty("modifiedTime", out var mtProp) &&
                            DateTime.TryParse(mtProp.GetString(), out var dtMod))
                        {
                            cloudModified = dtMod;
                        }

                        result.Add(new CloudFileInfo
                        {
                            FileId = fileId,
                            Name = wsName,
                            WorkspaceId = wsId,
                            ModifiedTimeUtc = cloudModified
                        });
                    }
                }

                return (result, string.Empty);
            }
            catch (Exception ex)
            {
                return (result, $"Exceção ao listar arquivos na nuvem: {ex.Message}");
            }
        }

        /// <summary>
        /// Baixa todos os workspaces salvos no Google Drive.
        /// </summary>
        public static async Task<(List<WorkspaceData> Workspaces, string Error)> BaixarTodosWorkspacesDaNuvemAsync(AppSettings settings)
        {
            var list = new List<WorkspaceData>();
            var (cloudFiles, listErr) = await ListarWorkspacesNaNuvemAsync(settings);
            if (!string.IsNullOrEmpty(listErr))
            {
                return (list, listErr);
            }

            if (cloudFiles.Count == 0)
            {
                return (list, "Nenhum arquivo de workspace (.mdiws) encontrado na pasta do Google Drive.");
            }

            string lastErr = string.Empty;
            foreach (var file in cloudFiles)
            {
                var (ws, err) = await BaixarWorkspaceDaNuvemAsync(settings, file.FileId);
                if (ws != null)
                {
                    if (string.IsNullOrWhiteSpace(ws.Id) && !string.IsNullOrWhiteSpace(file.WorkspaceId))
                    {
                        ws.Id = file.WorkspaceId;
                    }
                    if (string.IsNullOrWhiteSpace(ws.Name) && !string.IsNullOrWhiteSpace(file.Name))
                    {
                        ws.Name = file.Name;
                    }
                    list.Add(ws);
                }
                else
                {
                    lastErr = err;
                }
            }

            if (list.Count == 0 && !string.IsNullOrEmpty(lastErr))
            {
                return (list, lastErr);
            }

            return (list, string.Empty);
        }

        /// <summary>
        /// Faz upload de todos os workspaces locais para o Google Drive.
        /// </summary>
        public static async Task<(int Sucessos, int Falhas, string UltimoErro)> UploadTodosWorkspacesAsync(AppSettings settings)
        {
            int sucessos = 0;
            int falhas = 0;
            string ultimoErro = "";

            foreach (var ws in settings.Workspaces.ToList())
            {
                var res = await UploadWorkspaceAsync(settings, ws);
                if (res.Success)
                {
                    sucessos++;
                }
                else
                {
                    falhas++;
                    ultimoErro = res.Error;
                }
            }

            return (sucessos, falhas, ultimoErro);
        }

        private static async Task<string> ObterOuCriarPastaMdiAsync(string token)
        {
            string query = $"mimeType = 'application/vnd.google-apps.folder' and name = '{DefaultFolderName}' and trashed = false";
            string url = $"https://www.googleapis.com/drive/v3/files?q={Uri.EscapeDataString(query)}&fields=files(id,name)";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resp = await HttpClient.SendAsync(req);

            if (resp.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                var files = doc.RootElement.GetProperty("files");
                if (files.GetArrayLength() > 0)
                {
                    return files[0].GetProperty("id").GetString() ?? "";
                }
            }

            // Cria a pasta
            var folderMetadata = new
            {
                name = DefaultFolderName,
                mimeType = "application/vnd.google-apps.folder"
            };
            var createReq = new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/drive/v3/files")
            {
                Content = new StringContent(JsonSerializer.Serialize(folderMetadata), Encoding.UTF8, "application/json")
            };
            createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var createResp = await HttpClient.SendAsync(createReq);

            if (createResp.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync());
                return doc.RootElement.GetProperty("id").GetString() ?? "";
            }

            return "root";
        }

        /// <summary>
        /// Exclui o arquivo de backup de um workspace no Google Drive (se existir).
        /// </summary>
        public static async Task<bool> ExcluirWorkspaceDaNuvemAsync(AppSettings settings, string workspaceId)
        {
            try
            {
                string? token = await ObterAccessTokenValidoAsync(settings);
                if (string.IsNullOrEmpty(token)) return false;

                string folderId = await ObterOuCriarPastaMdiAsync(token);
                string fileName = $"workspace_{workspaceId}.mdiws";

                string? fileId = await LocalizarArquivoNoDriveAsync(token, folderId, fileName);
                if (string.IsNullOrEmpty(fileId)) return false;

                string url = $"https://www.googleapis.com/drive/v3/files/{fileId}";
                using var req = new HttpRequestMessage(HttpMethod.Delete, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var resp = await HttpClient.SendAsync(req);

                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<string?> LocalizarArquivoNoDriveAsync(string token, string folderId, string fileName)
        {
            try
            {
                string query = $"'{folderId}' in parents and name = '{fileName}' and trashed = false";
                string url = $"https://www.googleapis.com/drive/v3/files?q={Uri.EscapeDataString(query)}&fields=files(id,name)";

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var resp = await HttpClient.SendAsync(req);

                if (resp.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                    var files = doc.RootElement.GetProperty("files");
                    if (files.GetArrayLength() > 0)
                    {
                        return files[0].GetProperty("id").GetString();
                    }
                }
            }
            catch { }
            return null;
        }

        public static void AbrirNavegadorPadrao(string url)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                else if (OperatingSystem.IsLinux())
                {
                    // No Linux, xdg-open evita subprocessos zygote que causam pipe quebrado em navegadores Chromium existentes
                    var psi = new ProcessStartInfo
                    {
                        FileName = "xdg-open",
                        Arguments = $"\"{url}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    Process.Start(psi);
                }
                else if (OperatingSystem.IsMacOS())
                {
                    Process.Start("open", $"\"{url}\"");
                }
                else
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
            }
            catch
            {
                try
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch { }
            }
        }
    }
}
