using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace AvaloniaMdiApp
{
    public partial class MainWindow : Window
    {
        private int _contadorJanelas = 0;
        private int _contadorNotas = 0;
        private int _contadorListas = 0;
        private int _contadorNavegadores = 0;
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
                else if (winData.WindowType == "browser") _contadorNavegadores++;
                else _contadorJanelas++;

                CriarJanelaInterna(
                    winData.Title,
                    Math.Max(0, winData.X),
                    Math.Max(0, winData.Y),
                    winData.Width,
                    winData.Height,
                    winData.WindowType,
                    winData.NoteContent,
                    winData.ChecklistItems,
                    winData.Id,
                    winData.AccessCount,
                    null,
                    null,
                    winData.TextFormat);
            }
        }

        private void SalvarJanelasAtuais()
        {
            var settings = SettingsManager.Carregar();
            AtualizarListaJanelasAbertas(settings);
            SettingsManager.Salvar(settings);
        }

        private void AtualizarListaJanelasAbertas(AppSettings settings)
        {
            settings.SavedWindows.Clear();

            foreach (var win in _ativasJanelas)
            {
                var state = win.ExportState();
                settings.SavedWindows.Add(state);

                // Se já estiver no catálogo de salvos, mantém sincronizado ao fechar/salvar
                int idx = settings.SavedCatalog.FindIndex(x =>
                    x.Id == state.Id ||
                    (x.WindowType == state.WindowType && string.Equals(x.Title, state.Title, StringComparison.OrdinalIgnoreCase)));

                if (idx >= 0)
                {
                    state.AccessCount = Math.Max(settings.SavedCatalog[idx].AccessCount, state.AccessCount);
                    settings.SavedCatalog[idx] = state;
                }
            }
        }

        private void SalvarJanelaNoCatalogo(DraggableWindow win, bool mostrarToast = true)
        {
            var settings = SettingsManager.Carregar();

            void UpsertNoCatalogo(WindowStateData state)
            {
                int idx = settings.SavedCatalog.FindIndex(x =>
                    x.Id == state.Id ||
                    (x.WindowType == state.WindowType && string.Equals(x.Title, state.Title, StringComparison.OrdinalIgnoreCase)));

                if (idx >= 0)
                {
                    state.AccessCount = Math.Max(settings.SavedCatalog[idx].AccessCount, state.AccessCount);
                    state.LastUsedUtc = DateTime.UtcNow;
                    settings.SavedCatalog[idx] = state;
                }
                else
                {
                    state.LastUsedUtc = DateTime.UtcNow;
                    settings.SavedCatalog.Add(state);
                }
            }

            win.SincronizarNotaVinculada();
            var winState = win.ExportState();
            UpsertNoCatalogo(winState);

            if (win.ParentChecklistWindow != null)
            {
                var parentState = win.ParentChecklistWindow.ExportState();
                UpsertNoCatalogo(parentState);
            }

            AtualizarListaJanelasAbertas(settings);
            SettingsManager.Salvar(settings);

            if (mostrarToast && settings.EnableNotifications)
            {
                string msg = win.ParentChecklistWindow != null
                    ? $"Nota \"{win.WindowTitle}\" e lista \"{win.ParentChecklistWindow.WindowTitle}\" salvas!"
                    : $"\"{win.WindowTitle}\" salvo em Janelas Salvas!";
                ToastNotification.Mostrar(Workspace, msg, "success");
            }
        }

        private void SalvarJanelaAtiva_Click(object? sender, RoutedEventArgs e)
        {
            var ativa = ZIndexManager.ActiveWindow;
            if (ativa != null && _ativasJanelas.Contains(ativa))
            {
                SalvarJanelaNoCatalogo(ativa, mostrarToast: true);
            }
            else if (_ativasJanelas.Count > 0)
            {
                SalvarJanelaNoCatalogo(_ativasJanelas[^1], mostrarToast: true);
            }
            else
            {
                ToastNotification.Mostrar(Workspace, "Nenhuma janela aberta para salvar.", "warning");
            }
        }

        private void SalvarTodasJanelas_Click(object? sender, RoutedEventArgs e)
        {
            if (_ativasJanelas.Count == 0)
            {
                ToastNotification.Mostrar(Workspace, "Nenhuma janela aberta para salvar.", "warning");
                return;
            }

            foreach (var win in _ativasJanelas)
            {
                SalvarJanelaNoCatalogo(win, mostrarToast: false);
            }

            var settings = SettingsManager.Carregar();
            if (settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, $"Todas as {_ativasJanelas.Count} janelas foram salvas!", "success");
            }
        }

        private void MenuWidgets_Opening(object? sender, EventArgs e)
        {
            var settings = SettingsManager.Carregar();

            MenuMaisUsadas.Items.Clear();
            var maisUsadas = settings.SavedCatalog
                .OrderByDescending(x => x.AccessCount)
                .ThenByDescending(x => x.LastUsedUtc)
                .Take(6)
                .ToList();

            if (maisUsadas.Count == 0)
            {
                MenuMaisUsadas.Items.Add(new MenuItem { Header = "(Nenhuma janela salva ainda)", IsEnabled = false });
            }
            else
            {
                foreach (var item in maisUsadas)
                {
                    string icone = ObterIconeTipo(item.WindowType);
                    var menuItem = new MenuItem
                    {
                        Header = $"{icone} {item.Title} ({item.AccessCount}x)"
                    };
                    menuItem.Click += (s, ev) => AbrirJanelaDoCatalogo(item.Id);
                    MenuMaisUsadas.Items.Add(menuItem);
                }
            }

            MenuJanelasSalvas.Items.Clear();
            var salvas = settings.SavedCatalog
                .OrderByDescending(x => x.LastUsedUtc)
                .ToList();

            if (salvas.Count == 0)
            {
                MenuJanelasSalvas.Items.Add(new MenuItem { Header = "(Nenhuma janela salva ainda)", IsEnabled = false });
            }
            else
            {
                foreach (var item in salvas)
                {
                    string icone = ObterIconeTipo(item.WindowType);
                    var menuItem = new MenuItem
                    {
                        Header = $"{icone} {item.Title}"
                    };
                    menuItem.Click += (s, ev) => AbrirJanelaDoCatalogo(item.Id);
                    MenuJanelasSalvas.Items.Add(menuItem);
                }

                MenuJanelasSalvas.Items.Add(new Separator());
                var menuRemover = new MenuItem { Header = "🗑 Excluir dos Salvos" };
                foreach (var item in salvas)
                {
                    string icone = ObterIconeTipo(item.WindowType);
                    var delItem = new MenuItem
                    {
                        Header = $"✕ {icone} {item.Title}"
                    };
                    delItem.Click += (s, ev) => RemoverJanelaDoCatalogo(item.Id);
                    menuRemover.Items.Add(delItem);
                }
                MenuJanelasSalvas.Items.Add(menuRemover);
            }
        }

        private static string ObterIconeTipo(string windowType) => windowType switch
        {
            "checklist" => "☑",
            "browser" => "🌐",
            _ => "📝"
        };

        private void AbrirJanelaDoCatalogo(string id)
        {
            var settings = SettingsManager.Carregar();
            int idx = settings.SavedCatalog.FindIndex(x => x.Id == id);
            if (idx < 0) return;

            var saved = settings.SavedCatalog[idx];
            saved.AccessCount++;
            saved.LastUsedUtc = DateTime.UtcNow;
            SettingsManager.Salvar(settings);

            var existente = _ativasJanelas.FirstOrDefault(w =>
                w.WindowId == saved.Id ||
                (w.WindowType == saved.WindowType && string.Equals(w.WindowTitle, saved.Title, StringComparison.OrdinalIgnoreCase)));

            if (existente != null)
            {
                existente.AccessCount = saved.AccessCount;
                if (existente.IsMinimized)
                {
                    existente.ToggleMinimize();
                }
                ZIndexManager.BringToFront(existente);
                return;
            }

            CriarJanelaInterna(
                saved.Title,
                Math.Max(20, saved.X),
                Math.Max(20, saved.Y),
                saved.Width > 0 ? saved.Width : 360,
                saved.Height > 0 ? saved.Height : 260,
                saved.WindowType,
                saved.NoteContent,
                saved.ChecklistItems,
                saved.Id,
                saved.AccessCount,
                null,
                null,
                saved.TextFormat);
        }

        private void RemoverJanelaDoCatalogo(string id)
        {
            var settings = SettingsManager.Carregar();
            var alvo = settings.SavedCatalog.FirstOrDefault(x => x.Id == id);
            if (alvo == null) return;

            settings.SavedCatalog.Remove(alvo);
            SettingsManager.Salvar(settings);

            if (settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, $"\"{alvo.Title}\" removido dos salvos.", "info");
            }
        }

        private void AbrirBlocoDeNotas_Click(object? sender, RoutedEventArgs e)
        {
            _contadorNotas++;
            double offsetX = 60 + ((_contadorNotas - 1) * 30) % 300;
            double offsetY = 50 + ((_contadorNotas - 1) * 30) % 200;

            string titulo = $"Bloco de Notas #{_contadorNotas}";
            CriarJanelaInterna(titulo, offsetX, offsetY, 360, 260, "notepad");

            var settings = SettingsManager.Carregar();
            if (settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, $"{titulo} criado!", "info");
            }
        }

        private void AbrirOuFocarNotaDaLinha(DraggableWindow checklistWin, ChecklistRowState row)
        {
            string tituloLinha = string.IsNullOrWhiteSpace(row.Input.Text)
                ? "Nova Nota"
                : row.Input.Text.Trim();

            if (row.OpenNoteWindow != null && _ativasJanelas.Contains(row.OpenNoteWindow))
            {
                row.OpenNoteWindow.AccessCount++;
                if (row.OpenNoteWindow.IsMinimized)
                {
                    row.OpenNoteWindow.ToggleMinimize();
                }
                ZIndexManager.BringToFront(row.OpenNoteWindow);
                return;
            }

            var existente = _ativasJanelas.FirstOrDefault(w =>
                w.WindowType == "notepad" &&
                string.Equals(w.WindowTitle, tituloLinha, StringComparison.OrdinalIgnoreCase));

            if (existente != null)
            {
                row.OpenNoteWindow = existente;
                existente.AccessCount++;
                if (existente.IsMinimized)
                {
                    existente.ToggleMinimize();
                }
                ZIndexManager.BringToFront(existente);
                return;
            }

            _contadorNotas++;
            double offsetX = Math.Min(Math.Max(40, Canvas.GetLeft(checklistWin) + 40), Math.Max(60, Workspace.Bounds.Width - 380));
            double offsetY = Math.Min(Math.Max(40, Canvas.GetTop(checklistWin) + 40), Math.Max(60, Workspace.Bounds.Height - 280));

            var noteWin = CriarJanelaInterna(
                tituloLinha,
                offsetX,
                offsetY,
                360,
                260,
                "notepad",
                row.LinkedNoteContent,
                null,
                null,
                1,
                checklistWin,
                (novoTitulo, textoNota, formatoNota) =>
                {
                    if (!string.IsNullOrWhiteSpace(novoTitulo) && row.Input.Text != novoTitulo)
                    {
                        row.Input.Text = novoTitulo;
                    }
                    row.LinkedNoteContent = textoNota;
                    row.LinkedNoteFormat = formatoNota;
                    checklistWin.AtualizarIndicadorNotaLinha(row);
                },
                row.LinkedNoteFormat);

            row.OpenNoteWindow = noteWin;
            noteWin.OnClosed += () =>
            {
                if (row.OpenNoteWindow == noteWin)
                {
                    row.OpenNoteWindow = null;
                }
            };

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

            string titulo = $"Lista #{_contadorListas}";
            CriarJanelaInterna(titulo, offsetX, offsetY, 380, 290, "checklist");

            var settings = SettingsManager.Carregar();
            if (settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, $"{titulo} criada!", "info");
            }
        }

        private void AbrirNavegador_Click(object? sender, RoutedEventArgs e)
        {
            _contadorNavegadores++;
            double offsetX = 50 + ((_contadorNavegadores - 1) * 32) % 260;
            double offsetY = 40 + ((_contadorNavegadores - 1) * 32) % 160;

            string titulo = $"Navegador #{_contadorNavegadores}";
            CriarJanelaInterna(titulo, offsetX, offsetY, 640, 440, "browser", "https://www.google.com");

            var settings = SettingsManager.Carregar();
            if (settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, $"{titulo} (Chromium) aberto!", "info");
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

        private DraggableWindow CriarJanelaInterna(
            string title,
            double x,
            double y,
            double width,
            double height,
            string windowType = "default",
            string initialNote = "",
            List<ChecklistItemData>? initialChecklist = null,
            string? windowId = null,
            int accessCount = 1,
            DraggableWindow? parentChecklistWindow = null,
            Action<string, string, TextFormatData>? onLinkedRowSave = null,
            TextFormatData? initialFormat = null)
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
                initialChecklist,
                windowId,
                accessCount,
                parentChecklistWindow,
                onLinkedRowSave,
                initialFormat);

            _ativasJanelas.Add(win);
            win.OnClosed += () => _ativasJanelas.Remove(win);
            win.OnRequestOpenRowNotepad += (checklistWin, row) => AbrirOuFocarNotaDaLinha(checklistWin, row);
            win.OnSaveRequested += (janela) => SalvarJanelaNoCatalogo(janela, mostrarToast: true);

            AppTaskBar.RegistrarJanela(win, title);
            Workspace.Children.Add(win);
            ZIndexManager.BringToFront(win);
            return win;
        }
    }
}
