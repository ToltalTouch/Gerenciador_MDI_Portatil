using System;
using System.IO;
using System.Text.Json;

namespace AvaloniaMdiApp
{
    public static class SettingsManager
    {
        private static readonly string FilePath = ObterCaminhoConfiguracao();
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        private static string ObterCaminhoConfiguracao()
        {
            string localPath = "settings.json";
            if (File.Exists(localPath))
                return Path.GetFullPath(localPath);

            return Path.Combine(AppContext.BaseDirectory, "settings.json");
        }

        public static AppSettings Carregar()
        {
            AppSettings settings;
            if (!File.Exists(FilePath))
            {
                settings = new AppSettings();
            }
            else
            {
                try
                {
                    string json = File.ReadAllText(FilePath);
                    settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                catch
                {
                    settings = new AppSettings();
                }
            }

            GarantirWorkspaces(settings);
            return settings;
        }

        public static void GarantirWorkspaces(AppSettings settings)
        {
            if (settings.Workspaces == null)
            {
                settings.Workspaces = new();
            }

            if (settings.Workspaces.Count == 0)
            {
                var ws = new WorkspaceData
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "Workspace 1",
                    BackgroundImagePath = settings.WorkspaceBackgroundImagePath ?? string.Empty,
                    SavedWindows = settings.SavedWindows != null ? new(settings.SavedWindows) : new(),
                    SavedCatalog = settings.SavedCatalog != null ? new(settings.SavedCatalog) : new()
                };
                settings.Workspaces.Add(ws);
                settings.ActiveWorkspaceId = ws.Id;
            }

            if (string.IsNullOrWhiteSpace(settings.ActiveWorkspaceId) ||
                !settings.Workspaces.Exists(w => w.Id == settings.ActiveWorkspaceId))
            {
                settings.ActiveWorkspaceId = settings.Workspaces[0].Id;
            }
        }

        public static void Salvar(AppSettings settings)
        {
            try
            {
                string json = JsonSerializer.Serialize(settings, JsonOptions);
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }
    }
}
