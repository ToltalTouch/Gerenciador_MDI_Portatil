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

    public class GalleryItemData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
    }

    public class SpreadsheetCellData
    {
        public string CellKey { get; set; } = string.Empty;
        public string RawValue { get; set; } = string.Empty;
    }

    public class ConditionalFormatRuleData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Range { get; set; } = "*";
        public string Operator { get; set; } = ">"; // ">", "<", ">=", "<=", "=", "!=", "between", "contains"
        public string Value1 { get; set; } = "0";
        public string Value2 { get; set; } = "";
        public string StyleKey { get; set; } = "green"; // "red", "green", "yellow", "blue", "purple"
    }

    public class WindowStateData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = string.Empty;
        public string WindowType { get; set; } = "default";
        public string CustomColor { get; set; } = string.Empty;
        public string CriticalityLevel { get; set; } = "none"; // "none", "low", "medium", "high", "critical"
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string NoteContent { get; set; } = string.Empty;
        public TextFormatData TextFormat { get; set; } = new();
        public List<ChecklistItemData> ChecklistItems { get; set; } = new();
        public List<GalleryItemData> GalleryItems { get; set; } = new();
        public int SpreadsheetRows { get; set; } = 12;
        public int SpreadsheetCols { get; set; } = 6;
        public List<SpreadsheetCellData> SpreadsheetCells { get; set; } = new();
        public List<ConditionalFormatRuleData> ConditionalRules { get; set; } = new();
        public int AccessCount { get; set; } = 1;
        public DateTime LastUsedUtc { get; set; } = DateTime.UtcNow;
    }

    public class WindowConnectionData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string FromWindowId { get; set; } = string.Empty;
        public string ToWindowId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string ColorHex { get; set; } = "#3B82F6";
    }

    public class WorkspaceData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Workspace 1";
        public string BackgroundImagePath { get; set; } = string.Empty;
        public double WorkspaceZoom { get; set; } = 1.0;
        public List<WindowStateData> SavedWindows { get; set; } = new();
        public List<WindowStateData> SavedCatalog { get; set; } = new();
        public List<WindowConnectionData> Connections { get; set; } = new();
    }

    public class AppSettings
    {
        public bool IsDarkMode { get; set; } = true;
        public bool EnableNotifications { get; set; } = true;
        public double LayoutScale { get; set; } = 1.0;
        public string MainWindowSizePreset { get; set; } = "1024x680";
        public string WidgetSizePreset { get; set; } = "normal";
        public string DefaultFlowchartLineColor { get; set; } = "#3B82F6";
        public bool AutoCheckUpdatesOnStartup { get; set; } = true;
        public string GitHubRepository { get; set; } = "ToltalTouch/Janela_multitarefa";
        public string LastInstalledTag { get; set; } = "v1.0.0";
        public string ActiveWorkspaceId { get; set; } = string.Empty;
        public List<WorkspaceData> Workspaces { get; set; } = new();

        // Propriedades legadas mantidas para migração automática ao carregar settings antigos
        public string WorkspaceBackgroundImagePath { get; set; } = string.Empty;
        public List<WindowStateData> SavedWindows { get; set; } = new();
        public List<WindowStateData> SavedCatalog { get; set; } = new();
    }
}
