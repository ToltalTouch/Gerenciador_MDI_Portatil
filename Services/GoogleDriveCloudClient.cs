using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
                // Abre o navegador padrão do usuário
                Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

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

                string boundary = "-------MdiMultipartBoundary" + Guid.NewGuid().ToString("N");
                var multipartContent = new MultipartFormDataContent(boundary);

                var metadataObj = new
                {
                    name = fileName,
                    description = $"Backup do Workspace {wsData.Name} no Gerenciador MDI",
                    parents = existingFileId == null ? new[] { folderId } : null,
                    appProperties = new Dictionary<string, string>
                    {
                        ["workspaceId"] = wsData.Id,
                        ["workspaceName"] = wsData.Name,
                        ["lastModifiedUtc"] = wsData.LastModifiedUtc.ToString("o")
                    }
                };

                var metaPart = new StringContent(JsonSerializer.Serialize(metadataObj), Encoding.UTF8, "application/json");
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
        public static async Task<CloudFileInfo?> VerificarVersaoNuvemAsync(
            AppSettings settings,
            WorkspaceData wsData)
        {
            try
            {
                string? token = await ObterAccessTokenValidoAsync(settings);
                if (string.IsNullOrEmpty(token)) return null;

                string folderId = await ObterOuCriarPastaMdiAsync(token);
                string fileName = $"workspace_{wsData.Id}.mdiws";

                string query = $"'{folderId}' in parents and name = '{fileName}' and trashed = false";
                string url = $"https://www.googleapis.com/drive/v3/files?q={Uri.EscapeDataString(query)}&fields=files(id,name,modifiedTime,appProperties)";

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var resp = await HttpClient.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return null;

                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                var files = doc.RootElement.GetProperty("files");
                if (files.GetArrayLength() == 0) return null;

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

                return new CloudFileInfo
                {
                    FileId = fileId,
                    Name = wsData.Name,
                    ModifiedTimeUtc = cloudModified,
                    IsCloudNewer = isNewer
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Baixa a versão mais recente do Workspace diretamente do Google Drive.
        /// </summary>
        public static async Task<WorkspaceData?> BaixarWorkspaceDaNuvemAsync(AppSettings settings, string fileId)
        {
            try
            {
                string? token = await ObterAccessTokenValidoAsync(settings);
                if (string.IsNullOrEmpty(token)) return null;

                string url = $"https://www.googleapis.com/drive/v3/files/{fileId}?alt=media";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var resp = await HttpClient.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return null;

                string json = await resp.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<WorkspaceData>(json);
            }
            catch
            {
                return null;
            }
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
    }
}
