using Avalonia.Media;
using System;
using System.Linq;

namespace AvaloniaMdiApp
{
    public static class ThemePalette
    {
        public static readonly (string Key, string Nome, bool IsDark)[] TemasDisponiveis =
        {
            ("dark_blue", "Escuro Clássico (Azul)", true),
            ("dark_green", "Escuro Esmeralda (Verde)", true),
            ("dark_rose", "Escuro Ameixa (Rosa)", true),
            ("dark_purple", "Escuro Drácula (Roxo)", true),
            ("dark_cyber", "Escuro Cyberpunk (Neon)", true),
            ("dark_mono", "Escuro Grafite (Monocromático)", true),
            ("light_blue", "Claro Clássico (Azul)", false),
            ("light_green", "Claro Esmeralda (Verde)", false),
            ("light_rose", "Claro Rosa (Suave)", false),
            ("light_mono", "Claro Minimalista (Cinza)", false)
        };

        public static string ObterNomePorKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return TemasDisponiveis[0].Nome;
            foreach (var t in TemasDisponiveis)
            {
                if (string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase))
                    return t.Nome;
            }
            return TemasDisponiveis[0].Nome;
        }

        public static string ObterKeyPorNome(string? nome)
        {
            if (string.IsNullOrWhiteSpace(nome)) return "dark_blue";
            foreach (var t in TemasDisponiveis)
            {
                if (string.Equals(t.Nome, nome, StringComparison.OrdinalIgnoreCase))
                    return t.Key;
            }
            return "dark_blue";
        }

        // Cores de destaque (Accent)
        public static IBrush Accent { get; private set; } = SolidColorBrush.Parse("#2563EB").ToImmutable();
        public static IBrush AccentLight { get; private set; } = SolidColorBrush.Parse("#3B82F6").ToImmutable();
        public static IBrush AccentSubtle { get; private set; } = SolidColorBrush.Parse("#1E3A8A").ToImmutable();
        public static readonly IBrush Danger = SolidColorBrush.Parse("#DC2626").ToImmutable();

        // Tema Escuro (Dark)
        public static IBrush DarkWorkspaceBg { get; private set; } = SolidColorBrush.Parse("#0F172A").ToImmutable();
        public static IBrush DarkBarBg { get; private set; } = SolidColorBrush.Parse("#1E293B").ToImmutable();
        public static IBrush DarkWindowBg { get; private set; } = SolidColorBrush.Parse("#1E293B").ToImmutable();
        public static IBrush DarkWindowHeaderBg { get; private set; } = SolidColorBrush.Parse("#26354D").ToImmutable();
        public static IBrush DarkWindowHeaderActiveBg { get; private set; } = SolidColorBrush.Parse("#1D4ED8").ToImmutable();
        public static IBrush DarkContentBg { get; private set; } = SolidColorBrush.Parse("#0F172A").ToImmutable();
        public static IBrush DarkBorder { get; private set; } = SolidColorBrush.Parse("#334155").ToImmutable();
        public static IBrush DarkBorderActive { get; private set; } = SolidColorBrush.Parse("#3B82F6").ToImmutable();
        public static IBrush DarkTextPrimary { get; private set; } = SolidColorBrush.Parse("#F8FAFC").ToImmutable();
        public static IBrush DarkTextSecondary { get; private set; } = SolidColorBrush.Parse("#94A3B8").ToImmutable();
        public static IBrush DarkButtonBg { get; private set; } = SolidColorBrush.Parse("#334155").ToImmutable();

        // Ícones minimalistas em tons de cinza claro
        public static readonly IBrush IconLightGray = SolidColorBrush.Parse("#CBD5E1").ToImmutable();
        public static readonly IBrush IconMutedGray = SolidColorBrush.Parse("#94A3B8").ToImmutable();

        // Tema Claro (Light)
        public static IBrush LightWorkspaceBg { get; private set; } = SolidColorBrush.Parse("#F1F5F9").ToImmutable();
        public static IBrush LightBarBg { get; private set; } = SolidColorBrush.Parse("#FFFFFF").ToImmutable();
        public static IBrush LightWindowBg { get; private set; } = SolidColorBrush.Parse("#FFFFFF").ToImmutable();
        public static IBrush LightWindowHeaderBg { get; private set; } = SolidColorBrush.Parse("#E2E8F0").ToImmutable();
        public static IBrush LightWindowHeaderActiveBg { get; private set; } = SolidColorBrush.Parse("#2563EB").ToImmutable();
        public static IBrush LightContentBg { get; private set; } = SolidColorBrush.Parse("#F8FAFC").ToImmutable();
        public static IBrush LightBorder { get; private set; } = SolidColorBrush.Parse("#CBD5E1").ToImmutable();
        public static IBrush LightBorderActive { get; private set; } = SolidColorBrush.Parse("#2563EB").ToImmutable();
        public static IBrush LightTextPrimary { get; private set; } = SolidColorBrush.Parse("#0F172A").ToImmutable();
        public static IBrush LightTextSecondary { get; private set; } = SolidColorBrush.Parse("#475569").ToImmutable();
        public static IBrush LightButtonBg { get; private set; } = SolidColorBrush.Parse("#E2E8F0").ToImmutable();

        public static bool EhTemaEscuro(string? preset)
        {
            string key = preset ?? "";
            // Se for passado o nome amigável, converte para key
            if (key.Contains("Claro", StringComparison.OrdinalIgnoreCase)) return false;
            if (key.StartsWith("light_", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        public static void AplicarEsquema(string? preset)
        {
            string key = preset ?? "dark_blue";
            // Normaliza se for passado o nome de exibição em vez da chave
            if (key.Contains("Esmeralda") && key.Contains("Escuro")) key = "dark_green";
            else if (key.Contains("Ameixa") || (key.Contains("Rosa") && key.Contains("Escuro"))) key = "dark_rose";
            else if (key.Contains("Drácula") || key.Contains("Roxo")) key = "dark_purple";
            else if (key.Contains("Cyberpunk")) key = "dark_cyber";
            else if (key.Contains("Grafite") || (key.Contains("Monocromático") && key.Contains("Escuro"))) key = "dark_mono";
            else if (key.Contains("Claro") && key.Contains("Azul")) key = "light_blue";
            else if (key.Contains("Claro") && key.Contains("Verde")) key = "light_green";
            else if (key.Contains("Claro") && key.Contains("Rosa")) key = "light_rose";
            else if (key.Contains("Claro") && (key.Contains("Cinza") || key.Contains("Clean") || key.Contains("Minimalista"))) key = "light_mono";
            else if (key.Contains("Escuro")) key = "dark_blue";

            switch (key)
            {
                case "dark_green":
                    Accent = SolidColorBrush.Parse("#059669").ToImmutable();
                    AccentLight = SolidColorBrush.Parse("#10B981").ToImmutable();
                    AccentSubtle = SolidColorBrush.Parse("#064E3B").ToImmutable();
                    DarkWorkspaceBg = SolidColorBrush.Parse("#061A14").ToImmutable();
                    DarkBarBg = SolidColorBrush.Parse("#0A261D").ToImmutable();
                    DarkWindowBg = SolidColorBrush.Parse("#0E3125").ToImmutable();
                    DarkWindowHeaderBg = SolidColorBrush.Parse("#133E30").ToImmutable();
                    DarkWindowHeaderActiveBg = SolidColorBrush.Parse("#059669").ToImmutable();
                    DarkContentBg = SolidColorBrush.Parse("#081E17").ToImmutable();
                    DarkBorder = SolidColorBrush.Parse("#1A4B3A").ToImmutable();
                    DarkBorderActive = SolidColorBrush.Parse("#10B981").ToImmutable();
                    DarkTextPrimary = SolidColorBrush.Parse("#ECFDF5").ToImmutable();
                    DarkTextSecondary = SolidColorBrush.Parse("#86EFAC").ToImmutable();
                    DarkButtonBg = SolidColorBrush.Parse("#154233").ToImmutable();
                    break;

                case "dark_rose":
                    Accent = SolidColorBrush.Parse("#DB2777").ToImmutable();
                    AccentLight = SolidColorBrush.Parse("#EC4899").ToImmutable();
                    AccentSubtle = SolidColorBrush.Parse("#831843").ToImmutable();
                    DarkWorkspaceBg = SolidColorBrush.Parse("#1A0914").ToImmutable();
                    DarkBarBg = SolidColorBrush.Parse("#280F20").ToImmutable();
                    DarkWindowBg = SolidColorBrush.Parse("#311428").ToImmutable();
                    DarkWindowHeaderBg = SolidColorBrush.Parse("#3E1A33").ToImmutable();
                    DarkWindowHeaderActiveBg = SolidColorBrush.Parse("#BE185D").ToImmutable();
                    DarkContentBg = SolidColorBrush.Parse("#1E0B18").ToImmutable();
                    DarkBorder = SolidColorBrush.Parse("#532244").ToImmutable();
                    DarkBorderActive = SolidColorBrush.Parse("#EC4899").ToImmutable();
                    DarkTextPrimary = SolidColorBrush.Parse("#FDF2F8").ToImmutable();
                    DarkTextSecondary = SolidColorBrush.Parse("#F9A8D4").ToImmutable();
                    DarkButtonBg = SolidColorBrush.Parse("#441938").ToImmutable();
                    break;

                case "dark_purple":
                    Accent = SolidColorBrush.Parse("#8B5CF6").ToImmutable();
                    AccentLight = SolidColorBrush.Parse("#A78BFA").ToImmutable();
                    AccentSubtle = SolidColorBrush.Parse("#5B21B6").ToImmutable();
                    DarkWorkspaceBg = SolidColorBrush.Parse("#120E24").ToImmutable();
                    DarkBarBg = SolidColorBrush.Parse("#1B1536").ToImmutable();
                    DarkWindowBg = SolidColorBrush.Parse("#231B45").ToImmutable();
                    DarkWindowHeaderBg = SolidColorBrush.Parse("#2F245C").ToImmutable();
                    DarkWindowHeaderActiveBg = SolidColorBrush.Parse("#7C3AED").ToImmutable();
                    DarkContentBg = SolidColorBrush.Parse("#15102A").ToImmutable();
                    DarkBorder = SolidColorBrush.Parse("#3D2E78").ToImmutable();
                    DarkBorderActive = SolidColorBrush.Parse("#A78BFA").ToImmutable();
                    DarkTextPrimary = SolidColorBrush.Parse("#F5F3FF").ToImmutable();
                    DarkTextSecondary = SolidColorBrush.Parse("#C4B5FD").ToImmutable();
                    DarkButtonBg = SolidColorBrush.Parse("#312660").ToImmutable();
                    break;

                case "dark_cyber":
                    Accent = SolidColorBrush.Parse("#06B6D4").ToImmutable();
                    AccentLight = SolidColorBrush.Parse("#22D3EE").ToImmutable();
                    AccentSubtle = SolidColorBrush.Parse("#164E63").ToImmutable();
                    DarkWorkspaceBg = SolidColorBrush.Parse("#09101D").ToImmutable();
                    DarkBarBg = SolidColorBrush.Parse("#0D192D").ToImmutable();
                    DarkWindowBg = SolidColorBrush.Parse("#13233E").ToImmutable();
                    DarkWindowHeaderBg = SolidColorBrush.Parse("#192F53").ToImmutable();
                    DarkWindowHeaderActiveBg = SolidColorBrush.Parse("#0891B2").ToImmutable();
                    DarkContentBg = SolidColorBrush.Parse("#0B1527").ToImmutable();
                    DarkBorder = SolidColorBrush.Parse("#224273").ToImmutable();
                    DarkBorderActive = SolidColorBrush.Parse("#22D3EE").ToImmutable();
                    DarkTextPrimary = SolidColorBrush.Parse("#F0FDFA").ToImmutable();
                    DarkTextSecondary = SolidColorBrush.Parse("#67E8F9").ToImmutable();
                    DarkButtonBg = SolidColorBrush.Parse("#1C3660").ToImmutable();
                    break;

                case "dark_mono":
                    Accent = SolidColorBrush.Parse("#64748B").ToImmutable();
                    AccentLight = SolidColorBrush.Parse("#94A3B8").ToImmutable();
                    AccentSubtle = SolidColorBrush.Parse("#334155").ToImmutable();
                    DarkWorkspaceBg = SolidColorBrush.Parse("#121212").ToImmutable();
                    DarkBarBg = SolidColorBrush.Parse("#1E1E1E").ToImmutable();
                    DarkWindowBg = SolidColorBrush.Parse("#262626").ToImmutable();
                    DarkWindowHeaderBg = SolidColorBrush.Parse("#333333").ToImmutable();
                    DarkWindowHeaderActiveBg = SolidColorBrush.Parse("#475569").ToImmutable();
                    DarkContentBg = SolidColorBrush.Parse("#181818").ToImmutable();
                    DarkBorder = SolidColorBrush.Parse("#404040").ToImmutable();
                    DarkBorderActive = SolidColorBrush.Parse("#94A3B8").ToImmutable();
                    DarkTextPrimary = SolidColorBrush.Parse("#FFFFFF").ToImmutable();
                    DarkTextSecondary = SolidColorBrush.Parse("#A3A3A3").ToImmutable();
                    DarkButtonBg = SolidColorBrush.Parse("#383838").ToImmutable();
                    break;

                case "light_green":
                    Accent = SolidColorBrush.Parse("#059669").ToImmutable();
                    AccentLight = SolidColorBrush.Parse("#10B981").ToImmutable();
                    AccentSubtle = SolidColorBrush.Parse("#D1FAE5").ToImmutable();
                    LightWorkspaceBg = SolidColorBrush.Parse("#ECFDF5").ToImmutable();
                    LightBarBg = SolidColorBrush.Parse("#FFFFFF").ToImmutable();
                    LightWindowBg = SolidColorBrush.Parse("#FFFFFF").ToImmutable();
                    LightWindowHeaderBg = SolidColorBrush.Parse("#D1FAE5").ToImmutable();
                    LightWindowHeaderActiveBg = SolidColorBrush.Parse("#059669").ToImmutable();
                    LightContentBg = SolidColorBrush.Parse("#F6FEFA").ToImmutable();
                    LightBorder = SolidColorBrush.Parse("#A7F3D0").ToImmutable();
                    LightBorderActive = SolidColorBrush.Parse("#059669").ToImmutable();
                    LightTextPrimary = SolidColorBrush.Parse("#022C22").ToImmutable();
                    LightTextSecondary = SolidColorBrush.Parse("#065F46").ToImmutable();
                    LightButtonBg = SolidColorBrush.Parse("#D1FAE5").ToImmutable();
                    break;

                case "light_rose":
                    Accent = SolidColorBrush.Parse("#DB2777").ToImmutable();
                    AccentLight = SolidColorBrush.Parse("#EC4899").ToImmutable();
                    AccentSubtle = SolidColorBrush.Parse("#FCE7F3").ToImmutable();
                    LightWorkspaceBg = SolidColorBrush.Parse("#FDF2F8").ToImmutable();
                    LightBarBg = SolidColorBrush.Parse("#FFFFFF").ToImmutable();
                    LightWindowBg = SolidColorBrush.Parse("#FFFFFF").ToImmutable();
                    LightWindowHeaderBg = SolidColorBrush.Parse("#FCE7F3").ToImmutable();
                    LightWindowHeaderActiveBg = SolidColorBrush.Parse("#DB2777").ToImmutable();
                    LightContentBg = SolidColorBrush.Parse("#FFF9FC").ToImmutable();
                    LightBorder = SolidColorBrush.Parse("#FBCFE8").ToImmutable();
                    LightBorderActive = SolidColorBrush.Parse("#DB2777").ToImmutable();
                    LightTextPrimary = SolidColorBrush.Parse("#500724").ToImmutable();
                    LightTextSecondary = SolidColorBrush.Parse("#9D174D").ToImmutable();
                    LightButtonBg = SolidColorBrush.Parse("#FCE7F3").ToImmutable();
                    break;

                case "light_mono":
                    Accent = SolidColorBrush.Parse("#475569").ToImmutable();
                    AccentLight = SolidColorBrush.Parse("#64748B").ToImmutable();
                    AccentSubtle = SolidColorBrush.Parse("#F1F5F9").ToImmutable();
                    LightWorkspaceBg = SolidColorBrush.Parse("#F8FAFC").ToImmutable();
                    LightBarBg = SolidColorBrush.Parse("#FFFFFF").ToImmutable();
                    LightWindowBg = SolidColorBrush.Parse("#FFFFFF").ToImmutable();
                    LightWindowHeaderBg = SolidColorBrush.Parse("#F1F5F9").ToImmutable();
                    LightWindowHeaderActiveBg = SolidColorBrush.Parse("#475569").ToImmutable();
                    LightContentBg = SolidColorBrush.Parse("#FFFFFF").ToImmutable();
                    LightBorder = SolidColorBrush.Parse("#E2E8F0").ToImmutable();
                    LightBorderActive = SolidColorBrush.Parse("#475569").ToImmutable();
                    LightTextPrimary = SolidColorBrush.Parse("#0F172A").ToImmutable();
                    LightTextSecondary = SolidColorBrush.Parse("#64748B").ToImmutable();
                    LightButtonBg = SolidColorBrush.Parse("#F1F5F9").ToImmutable();
                    break;

                case "light_blue":
                    Accent = SolidColorBrush.Parse("#2563EB").ToImmutable();
                    AccentLight = SolidColorBrush.Parse("#3B82F6").ToImmutable();
                    AccentSubtle = SolidColorBrush.Parse("#DBEAFE").ToImmutable();
                    LightWorkspaceBg = SolidColorBrush.Parse("#F1F5F9").ToImmutable();
                    LightBarBg = SolidColorBrush.Parse("#FFFFFF").ToImmutable();
                    LightWindowBg = SolidColorBrush.Parse("#FFFFFF").ToImmutable();
                    LightWindowHeaderBg = SolidColorBrush.Parse("#E2E8F0").ToImmutable();
                    LightWindowHeaderActiveBg = SolidColorBrush.Parse("#2563EB").ToImmutable();
                    LightContentBg = SolidColorBrush.Parse("#F8FAFC").ToImmutable();
                    LightBorder = SolidColorBrush.Parse("#CBD5E1").ToImmutable();
                    LightBorderActive = SolidColorBrush.Parse("#2563EB").ToImmutable();
                    LightTextPrimary = SolidColorBrush.Parse("#0F172A").ToImmutable();
                    LightTextSecondary = SolidColorBrush.Parse("#475569").ToImmutable();
                    LightButtonBg = SolidColorBrush.Parse("#E2E8F0").ToImmutable();
                    break;

                default: // "dark_blue"
                    Accent = SolidColorBrush.Parse("#2563EB").ToImmutable();
                    AccentLight = SolidColorBrush.Parse("#3B82F6").ToImmutable();
                    AccentSubtle = SolidColorBrush.Parse("#1E3A8A").ToImmutable();
                    DarkWorkspaceBg = SolidColorBrush.Parse("#0F172A").ToImmutable();
                    DarkBarBg = SolidColorBrush.Parse("#1E293B").ToImmutable();
                    DarkWindowBg = SolidColorBrush.Parse("#1E293B").ToImmutable();
                    DarkWindowHeaderBg = SolidColorBrush.Parse("#26354D").ToImmutable();
                    DarkWindowHeaderActiveBg = SolidColorBrush.Parse("#1D4ED8").ToImmutable();
                    DarkContentBg = SolidColorBrush.Parse("#0F172A").ToImmutable();
                    DarkBorder = SolidColorBrush.Parse("#334155").ToImmutable();
                    DarkBorderActive = SolidColorBrush.Parse("#3B82F6").ToImmutable();
                    DarkTextPrimary = SolidColorBrush.Parse("#F8FAFC").ToImmutable();
                    DarkTextSecondary = SolidColorBrush.Parse("#94A3B8").ToImmutable();
                    DarkButtonBg = SolidColorBrush.Parse("#334155").ToImmutable();
                    break;
            }
        }
    }
}
