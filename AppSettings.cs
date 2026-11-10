using System.Collections.Generic;

namespace AvaloniaMdiApp
{
    public class WindowStateData
    {
        public string Title { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    public class AppSettings
    {
        public bool IsDarkMode { get; set; } = true;
        public bool EnableNotifications { get; set; } = true;
        public List<WindowStateData> SavedWindows { get; set; } = new();
    }
}
