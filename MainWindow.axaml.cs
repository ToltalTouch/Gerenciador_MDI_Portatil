using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using System;
using System.Collections.Generic;

namespace AvaloniaMdiApp
{
    public partial class MainWindow : Window
    {
        private int _contadorJanelas = 0;
        private int _contadorNotas = 0;
        private int _contadorListas = 0;
        private bool _isDarkMode = true;
        private SettingsWindow? _settingsWindow;
        private readonly List<DraggableWindow> _ativasJanelas = new();

        public MainWindow()
        {
            InitializeComponent();

            Loaded += (s, e) =>
            {
                var settings = SettingsManager.Carregar();
                AplicarTema(settings.IsDarkMode);
                CarregarJanelasSalvas(settings);
            };

            Closing += (s, e) => SalvarJanelasAtuais();
        }

        private void AplicarTema(bool isDarkMode)
        {
            _isDarkMode = isDarkMode;

            if (Application.Current != null)
            {
                Application.Current.RequestedThemeVariant = isDarkMode ? ThemeVariant.Dark : ThemeVariant.Light;
            }

            var workspaceBg = isDarkMode ? ThemePalette.DarkWorkspaceBg : ThemePalette.LightWorkspaceBg;
            var barBg = isDarkMode ? ThemePalette.DarkBarBg : ThemePalette.LightBarBg;
            var borderBrush = isDarkMode ? ThemePalette.DarkBorder : ThemePalette.LightBorder;
            var textPrimary = isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;
            var btnBg = isDarkMode ? ThemePalette.DarkButtonBg : ThemePalette.LightButtonBg;

            RootDock.Background = workspaceBg;
            Workspace.Background = workspaceBg;
            TopBar.Background = barBg;
            TopBar.BorderBrush = borderBrush;
            AppTitleText.Foreground = textPrimary;
            TitleDivider.Background = borderBrush;

            BtnWidgets.Background = btnBg;
            BtnWidgets.Foreground = textPrimary;
            BtnCascata.Background = btnBg;
            BtnCascata.Foreground = textPrimary;
            BtnLadoALado.Background = btnBg;
            BtnLadoALado.Foreground = textPrimary;
            BtnConfiguracoes.Background = btnBg;
            BtnConfiguracoes.Foreground = textPrimary;

            AppTaskBar.AplicarTema(isDarkMode);
            _settingsWindow?.AplicarTema(isDarkMode);

            foreach (var win in _ativasJanelas)
            {
                win.AplicarTema(isDarkMode);
            }
        }

        private void CarregarJanelasSalvas(AppSettings settings)
        {
            if (settings.SavedWindows.Count == 0)
            {
                return;
            }

            foreach (var winData in settings.SavedWindows)
            {
                if (winData.WindowType == "notepad") _contadorNotas++;
                else if (winData.WindowType == "checklist") _contadorListas++;
                else _contadorJanelas++;

                CriarJanelaInterna(
                    winData.Title,
                    Math.Max(0, winData.X),
                    Math.Max(0, winData.Y),
                    winData.Width,
                    winData.Height,
                    winData.WindowType,
                    winData.NoteContent,
                    winData.ChecklistItems);
            }
        }

        private void SalvarJanelasAtuais()
        {
            var settings = SettingsManager.Carregar();
            settings.SavedWindows.Clear();

            foreach (var win in _ativasJanelas)
            {
                settings.SavedWindows.Add(win.ExportState());
            }

            SettingsManager.Salvar(settings);
        }

        private void AbrirBlocoDeNotas_Click(object? sender, RoutedEventArgs e)
        {
            _contadorNotas++;
            double offsetX = 60 + ((_contadorNotas - 1) * 30) % 300;
            double offsetY = 50 + ((_contadorNotas - 1) * 30) % 200;

            string titulo = $"📝 Bloco de Notas #{_contadorNotas}";
            CriarJanelaInterna(titulo, offsetX, offsetY, 360, 260, "notepad");

            var settings = SettingsManager.Carregar();
            if (settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, $"{titulo} criado!", "info");
            }
        }

        private void AbrirOuFocarBlocoDeNotas(string tituloLinha)
        {
            foreach (var existente in _ativasJanelas)
            {
                if (existente.WindowType == "notepad" &&
                    string.Equals(existente.WindowTitle, tituloLinha, StringComparison.OrdinalIgnoreCase))
                {
                    if (existente.IsMinimized)
                    {
                        existente.ToggleMinimize();
                    }
                    ZIndexManager.BringToFront(existente);
                    return;
                }
            }

            _contadorNotas++;
            double offsetX = 90 + ((_contadorNotas - 1) * 30) % 320;
            double offsetY = 70 + ((_contadorNotas - 1) * 30) % 220;

            CriarJanelaInterna(tituloLinha, offsetX, offsetY, 360, 260, "notepad");

            var settings = SettingsManager.Carregar();
            if (settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, $"Bloco de notas \"{tituloLinha}\" aberto!", "info");
            }
        }

        private void AbrirChecklist_Click(object? sender, RoutedEventArgs e)
        {
            _contadorListas++;
            double offsetX = 80 + ((_contadorListas - 1) * 30) % 300;
            double offsetY = 60 + ((_contadorListas - 1) * 30) % 200;

            string titulo = $"☑ Lista #{_contadorListas}";
            CriarJanelaInterna(titulo, offsetX, offsetY, 360, 280, "checklist");

            var settings = SettingsManager.Carregar();
            if (settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, $"{titulo} criada!", "info");
            }
        }

        private void OrganizarCascata_Click(object? sender, RoutedEventArgs e)
        {
            if (_ativasJanelas.Count == 0) return;

            for (int i = 0; i < _ativasJanelas.Count; i++)
            {
                var win = _ativasJanelas[i];
                double x = 36 + (i * 32) % Math.Max(100, Workspace.Bounds.Width - 360);
                double y = 36 + (i * 32) % Math.Max(100, Workspace.Bounds.Height - 250);
                win.SetPosicaoNormal(x, y, 340, 230);
                ZIndexManager.BringToFront(win);
            }
        }

        private void OrganizarGrade_Click(object? sender, RoutedEventArgs e)
        {
            int count = _ativasJanelas.Count;
            if (count == 0) return;

            int cols = (int)Math.Ceiling(Math.Sqrt(count));
            int rows = (int)Math.Ceiling((double)count / cols);

            double padding = 16;
            double availW = Math.Max(300, Workspace.Bounds.Width - padding * (cols + 1));
            double availH = Math.Max(200, Workspace.Bounds.Height - padding * (rows + 1));

            double cellW = Math.Max(220, availW / cols);
            double cellH = Math.Max(140, availH / rows);

            for (int i = 0; i < count; i++)
            {
                int col = i % cols;
                int row = i / cols;
                double x = padding + col * (cellW + padding);
                double y = padding + row * (cellH + padding);

                _ativasJanelas[i].SetPosicaoNormal(x, y, cellW, cellH);
            }
        }

        private void AbrirConfiguracoes_Click(object? sender, RoutedEventArgs e)
        {
            if (_settingsWindow != null)
            {
                ZIndexManager.BringToFront(_settingsWindow);
                return;
            }

            var settings = SettingsManager.Carregar();
            _settingsWindow = new SettingsWindow(Workspace, settings);

            _settingsWindow.OnSettingsChanged += (isDark, notif) =>
            {
                settings.IsDarkMode = isDark;
                settings.EnableNotifications = notif;
                AplicarTema(isDark);
                SettingsManager.Salvar(settings);
            };

            _settingsWindow.OnClosed += () => _settingsWindow = null;

            Workspace.Children.Add(_settingsWindow);
        }

        private void CriarJanelaInterna(
            string title,
            double x,
            double y,
            double width,
            double height,
            string windowType = "default",
            string initialNote = "",
            List<ChecklistItemData>? initialChecklist = null)
        {
            var win = new DraggableWindow(
                Workspace,
                AppTaskBar,
                title,
                width,
                height,
                x,
                y,
                _isDarkMode,
                windowType,
                initialNote,
                initialChecklist);

            _ativasJanelas.Add(win);
            win.OnClosed += () => _ativasJanelas.Remove(win);
            win.OnRequestOpenNotepad += (tituloLinha) => AbrirOuFocarBlocoDeNotas(tituloLinha);

            AppTaskBar.RegistrarJanela(win, title);
            Workspace.Children.Add(win);
            ZIndexManager.BringToFront(win);
        }
    }
}
