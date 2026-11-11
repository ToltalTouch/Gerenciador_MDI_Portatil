using System;
using System.Collections.Generic;

namespace AvaloniaMdiApp
{
    public class TextFormatData
    {
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
        public double FontSize { get; set; } = 13;
        public string Alignment { get; set; } = "left";
        public string FontFamilyName { get; set; } = "default";
    }

    public class ChecklistItemData
    {
        public string Text { get; set; } = string.Empty;
        public bool IsChecked { get; set; }
        public string LinkedNoteContent { get; set; } = string.Empty;
        public TextFormatData LinkedNoteFormat { get; set; } = new();
    }

    public class WindowStateData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = string.Empty;
        public string WindowType { get; set; } = "default";
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string NoteContent { get; set; } = string.Empty;
        public TextFormatData TextFormat { get; set; } = new();
        public List<ChecklistItemData> ChecklistItems { get; set; } = new();
        public int AccessCount { get; set; } = 1;
        public DateTime LastUsedUtc { get; set; } = DateTime.UtcNow;
    }

    public class AppSettings
    {
        public bool IsDarkMode { get; set; } = true;
        public bool EnableNotifications { get; set; } = true;
        public List<WindowStateData> SavedWindows { get; set; } = new();
        public List<WindowStateData> SavedCatalog { get; set; } = new();
    }
}
