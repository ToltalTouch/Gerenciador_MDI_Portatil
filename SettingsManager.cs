using System.IO;
using System.Text.Json;

namespace AvaloniaMdiApp
{
    public static class SettingsManager
    {
        private static readonly string FilePath = "settings.json";

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
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }
    }
}
