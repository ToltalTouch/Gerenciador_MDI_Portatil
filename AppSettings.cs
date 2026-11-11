using Avalonia.Media;
using System.Collections.Generic;

namespace AvaloniaMdiApp
{
    public class ChecklistItemData
    {
        public string Text { get; set; } = string.Empty;
        public bool IsChecked { get; set; }
    }

    public class WindowStateData
    {
        public string Title { get; set; } = string.Empty;
        public string WindowType { get; set; } = "default";
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string NoteContent { get; set; } = string.Empty;
        public List<ChecklistItemData> ChecklistItems { get; set; } = new();
    }

    public class AppSettings
    {
        public bool IsDarkMode { get; set; } = true;
        public bool EnableNotifications { get; set; } = true;
        public List<WindowStateData> SavedWindows { get; set; } = new();
    }

    /// <summary>
    /// Paleta estática em cache para evitar alocações repetidas de Brushes e reduzir consumo de memória/CPU.
    /// </summary>
    public static class ThemePalette
    {
        // Cores de destaque (Accent)
        public static readonly IBrush Accent = SolidColorBrush.Parse("#2563EB").ToImmutable();
        public static readonly IBrush AccentLight = SolidColorBrush.Parse("#3B82F6").ToImmutable();
        public static readonly IBrush AccentSubtle = SolidColorBrush.Parse("#1E3A8A").ToImmutable();
        public static readonly IBrush Danger = SolidColorBrush.Parse("#DC2626").ToImmutable();

        // Tema Escuro (Dark)
        public static readonly IBrush DarkWorkspaceBg = SolidColorBrush.Parse("#0F172A").ToImmutable();
        public static readonly IBrush DarkBarBg = SolidColorBrush.Parse("#1E293B").ToImmutable();
        public static readonly IBrush DarkWindowBg = SolidColorBrush.Parse("#1E293B").ToImmutable();
        public static readonly IBrush DarkWindowHeaderBg = SolidColorBrush.Parse("#26354D").ToImmutable();
        public static readonly IBrush DarkWindowHeaderActiveBg = SolidColorBrush.Parse("#1D4ED8").ToImmutable();
        public static readonly IBrush DarkContentBg = SolidColorBrush.Parse("#0F172A").ToImmutable();
        public static readonly IBrush DarkBorder = SolidColorBrush.Parse("#334155").ToImmutable();
        public static readonly IBrush DarkBorderActive = SolidColorBrush.Parse("#3B82F6").ToImmutable();
        public static readonly IBrush DarkTextPrimary = SolidColorBrush.Parse("#F8FAFC").ToImmutable();
        public static readonly IBrush DarkTextSecondary = SolidColorBrush.Parse("#94A3B8").ToImmutable();
        public static readonly IBrush DarkButtonBg = SolidColorBrush.Parse("#334155").ToImmutable();

        // Tema Claro (Light)
        public static readonly IBrush LightWorkspaceBg = SolidColorBrush.Parse("#F1F5F9").ToImmutable();
        public static readonly IBrush LightBarBg = SolidColorBrush.Parse("#FFFFFF").ToImmutable();
        public static readonly IBrush LightWindowBg = SolidColorBrush.Parse("#FFFFFF").ToImmutable();
        public static readonly IBrush LightWindowHeaderBg = SolidColorBrush.Parse("#E2E8F0").ToImmutable();
        public static readonly IBrush LightWindowHeaderActiveBg = SolidColorBrush.Parse("#2563EB").ToImmutable();
        public static readonly IBrush LightContentBg = SolidColorBrush.Parse("#F8FAFC").ToImmutable();
        public static readonly IBrush LightBorder = SolidColorBrush.Parse("#CBD5E1").ToImmutable();
        public static readonly IBrush LightBorderActive = SolidColorBrush.Parse("#2563EB").ToImmutable();
        public static readonly IBrush LightTextPrimary = SolidColorBrush.Parse("#0F172A").ToImmutable();
        public static readonly IBrush LightTextSecondary = SolidColorBrush.Parse("#475569").ToImmutable();
        public static readonly IBrush LightButtonBg = SolidColorBrush.Parse("#E2E8F0").ToImmutable();
    }
}
