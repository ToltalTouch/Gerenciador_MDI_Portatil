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
            if (!File.Exists(FilePath))
                return new AppSettings();

            try
            {
                string json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
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
