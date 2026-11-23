using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Threading.Tasks;

namespace AvaloniaMdiApp
{
    public partial class MainWindow : Window
    {
        private int _contadorJanelas = 0;
        private int _contadorNotas = 0;
        private int _contadorListas = 0;
        private int _contadorNavegadores = 0;
        private int _contadorGalerias = 0;
        private int _contadorImagens = 0;
        private int _contadorVideos = 0;
        private int _contadorPlanilhas = 0;
        private int _contadorFormas = 0;

        private bool _isDarkMode = true;
        private string _widgetSizePreset = "normal";
        private double _workspaceZoom = 1.0;
        private string _workspaceBgImagePath = string.Empty;
        private Bitmap? _workspaceBgBitmap;
        private SettingsWindow? _settingsWindow;

        private AppSettings _settings = new();
        private bool _saidaConfirmada = false;
        private bool _trocandoWorkspace = false;
        private readonly List<DraggableWindow> _ativasJanelas = new();

        // Estado do Fluxograma (Conexões entre Janelas)
        private readonly List<FlowchartConnectionVisual> _conexoesAtivas = new();
        private bool _modoConexaoAtivo = false;
        private DraggableWindow? _janelaOrigemConexao;
        private Avalonia.Controls.Shapes.Path? _previewConnectionPath;

        // Estado de Vínculo de Linha de Checklist com Janela pelo Mouse
        private bool _modoVinculoLinhaAtivo = false;
        private DraggableWindow? _checklistWinOrigemVinculo;
        private ChecklistRowState? _linhaOrigemVinculo;

        // Estado de Panning (arrastar o fundo do Workspace)
        private bool _isPanningWorkspace = false;
        private Point _panPointerStart;
        private Vector _panOffsetStart;
        private string? _snapshotBeforeInteraction;

        // Seleção Múltipla de Janelas (Caixa Rubberband / Marquee e Ctrl+Clique)
        private readonly HashSet<DraggableWindow> _selectedWindows = new();
        private readonly Dictionary<DraggableWindow, Point> _dragGroupStartPositions = new();
        private Border? _selectionMarquee;
        private bool _isRubberbandSelecting = false;
        private Point _rubberbandStart;
        private bool _rubberbandCtrlHeld = false;
        private HashSet<DraggableWindow> _snapshotSelectedBeforeRubberband = new();
        private bool _isSpaceKeyDown = false;

        // Timers para expansão e retração dos menus laterais com delay
        private DispatcherTimer? _leftSidebarOpenTimer;
        private DispatcherTimer? _leftSidebarCloseTimer;
        private DispatcherTimer? _rightSidebarOpenTimer;
        private DispatcherTimer? _rightSidebarCloseTimer;

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                using var iconStream = AssetLoader.Open(new Uri("avares://AvaloniaMdiApp/Assets/app-icon.png"));
                Icon = new WindowIcon(iconStream);
            }
            catch { }

            LinuxDesktopIntegration.GarantirIntegracaoDesktopLinux();

            ConfigurarEventosWorkspaceZoomEPan();
            InicializarTimersHoverMenusLaterais();

            ModalOverlay.PropertyChanged += (s, e) =>
            {
                if (e.Property == Visual.IsVisibleProperty)
                {
                    AtualizarSuspensaoNavegadores();
                }
            };

            RadialMenuOverlay.PropertyChanged += (s, e) =>
            {
                if (e.Property == Visual.IsVisibleProperty)
                {
                    AtualizarSuspensaoNavegadores();
                }
            };

            RadialMenuOverlay.PointerPressed += (s, e) =>
            {
                FecharMenuCircular();
                e.Handled = true;
            };

            RootDock.PointerPressed += (s, e) =>
            {
                if (RadialMenuOverlay.IsVisible)
                {
                    FecharMenuCircular();
                }
            };

            AppTaskBar.OnNotificationHistoryClicked += () => AbrirHistoricoNotificacoes();

            Loaded += (s, e) =>
            {
                _settings = SettingsManager.Carregar();
                _widgetSizePreset = string.IsNullOrWhiteSpace(_settings.WidgetSizePreset) ? "normal" : _settings.WidgetSizePreset;
                if (string.IsNullOrWhiteSpace(_settings.ThemePreset))
                {
                    _settings.ThemePreset = _settings.IsDarkMode ? "dark_blue" : "light_blue";
                }
                _settings.IsDarkMode = ThemePalette.EhTemaEscuro(_settings.ThemePreset);
                ThemePalette.AplicarEsquema(_settings.ThemePreset);

                var wsAtual = ObterWorkspaceAtual();
                _workspaceZoom = wsAtual.WorkspaceZoom > 0 ? wsAtual.WorkspaceZoom : 1.0;

                AplicarTema(_settings.ThemePreset);
                AplicarEscalaLayout(_settings.LayoutScale);
                AplicarTamanhoJanelaPrincipal(_settings.MainWindowSizePreset);
                InicializarFiltroCores();
                AtualizarAbasWorkspaces();

                var menuAbrir = this.FindControl<MenuItem>("MenuAbrirWorkspace");
                if (menuAbrir != null)
                {
                    menuAbrir.SubmenuOpened += (s, e) => AtualizarMenuSuperiorWorkspaces();
                }
                var menuExcluir = this.FindControl<MenuItem>("MenuExcluirWorkspace");
                if (menuExcluir != null)
                {
                    menuExcluir.SubmenuOpened += (s, e) => AtualizarMenuSuperiorWorkspaces();
                }
                CarregarJanelasDoWorkspace(wsAtual);
                AplicarZoomWorkspace(_workspaceZoom);

                if (_settings.AutoCheckUpdatesOnStartup)
                {
                    _ = VerificarAtualizacaoGitHubAsync(silenciosoSeAtualizado: true);
                }

                if (_settings.GoogleDriveCloudSyncEnabled && !string.IsNullOrWhiteSpace(_settings.GoogleOAuthRefreshToken))
                {
                    _ = VerificarEAtualizarNuvemNaInicializacaoAsync();
                }

                if (!_settings.HasSeenWelcome)
                {
                    MostrarModalBoasVindas();
                }
            };

            Closing += OnMainWindowClosing;
            Closed += (s, e) =>
            {
                (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown();
            };
        }

        private bool _isSidebarExpanded;
        private bool _isSidebarPinned;
        private InteractiveTutorialModal? _tutorialModal;
        private string? _activeColorFilterHex;
        private readonly Dictionary<string, Button> _colorFilterButtons = new(StringComparer.OrdinalIgnoreCase);

        private void InicializarFiltroCores()
        {
            if (ColorFilterDotsPanel.Children.Count > 0) return;
            _colorFilterButtons.Clear();

            foreach (var cor in FlowchartConnectionVisual.PaletaCoresLinha)
            {
                string hex = cor.Hex;
                var dotBtn = new Button
                {
                    Width = 16,
                    Height = 16,
                    Padding = new Thickness(0),
                    CornerRadius = new CornerRadius(8),
                    Background = SolidColorBrush.Parse(hex),
                    BorderThickness = new Thickness(1.5),
                    BorderBrush = Brushes.Transparent
                };
                ToolTip.SetTip(dotBtn, $"Destacar cor: {cor.Nome}");
                dotBtn.Click += (_, _) =>
                {
                    _activeColorFilterHex = string.Equals(_activeColorFilterHex, hex, StringComparison.OrdinalIgnoreCase)
                        ? null
                        : hex;
                    AplicarFiltroCores();
                };
                _colorFilterButtons[hex] = dotBtn;
                ColorFilterDotsPanel.Children.Add(dotBtn);
            }

            AtualizarVisualFiltroCores();
        }

        private void ClearColorFilter_Click(object? sender, RoutedEventArgs e)
        {
            _activeColorFilterHex = null;
            AplicarFiltroCores();
        }

        private void AplicarFiltroCores()
        {
            AtualizarVisualFiltroCores();

            foreach (var win in _ativasJanelas)
            {
                win.AplicarFiltroCor(_activeColorFilterHex);
            }

            foreach (var conn in _conexoesAtivas)
            {
                bool match = DraggableWindow.CorCombinaComFiltro(conn.ColorHex, _activeColorFilterHex);
                conn.DefinirDestaqueFiltro(match);
            }

            AppTaskBar.AplicarFiltroCor(_activeColorFilterHex);
        }

        private void AtualizarVisualFiltroCores()
        {
            bool semFiltro = string.IsNullOrWhiteSpace(_activeColorFilterHex);
            BtnClearColorFilter.Background = semFiltro
                ? ThemePalette.Accent
                : (_isDarkMode ? ThemePalette.DarkButtonBg : ThemePalette.LightButtonBg);
            BtnClearColorFilter.Foreground = semFiltro
                ? Brushes.White
                : (_isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary);

            foreach (var kvp in _colorFilterButtons)
            {
                bool active = !semFiltro && string.Equals(kvp.Key, _activeColorFilterHex, StringComparison.OrdinalIgnoreCase);
                kvp.Value.BorderBrush = active
                    ? (_isDarkMode ? Brushes.White : ThemePalette.Accent)
                    : Brushes.Transparent;
                kvp.Value.Width = active ? 18 : 15;
                kvp.Value.Height = active ? 18 : 15;
            }
        }

        private void ExpandirLeftSidebar(bool expand)
        {
            _isSidebarExpanded = expand;
            LeftSidebar.Width = expand ? 230 : 60;

            var labels = new Control?[]
            {
                LblSidebarTitle, LblSideNotas, LblSideListas,
                LblSidePlanilhas, LblSideGaleria, LblSideNavegador, LblSideFluxograma,
                LblSideSalvarTudo, LblSideExtrair, LblSideAjuda, LblSidePreferencias,
                ChevronSideGaleria, ChevronSideNavegador, ChevronSideFluxograma
            };

            if (expand)
            {
                foreach (var lbl in labels)
                {
                    if (lbl != null)
                    {
                        lbl.IsVisible = true;
                        lbl.Opacity = 0.0;
                    }
                }

                Dispatcher.UIThread.Post(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(35);
                    if (_isSidebarExpanded)
                    {
                        foreach (var lbl in labels)
                        {
                            if (lbl != null) lbl.Opacity = 1.0;
                        }
                    }
                });
            }
            else
            {
                foreach (var lbl in labels)
                {
                    if (lbl != null) lbl.Opacity = 0.0;
                }

                // Fecha submenus ao retrair para modo compacto de 60px
                FechamentoRapidoDropdown(PanelSubmenuGaleria, ChevronSideGaleria);
                FechamentoRapidoDropdown(PanelSubmenuNavegador, ChevronSideNavegador);
                FechamentoRapidoDropdown(PanelSubmenuFluxograma, ChevronSideFluxograma);

                Dispatcher.UIThread.Post(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(130);
                    if (!_isSidebarExpanded)
                    {
                        foreach (var lbl in labels)
                        {
                            if (lbl != null) lbl.IsVisible = false;
                        }
                    }
                });
            }
        }

        private void AnimarToggleDropdown(Border? panel, PathIcon? chevron, double targetMaxHeight)
        {
            if (panel == null) return;

            bool estaAberto = panel.IsVisible && panel.MaxHeight > 0 && panel.Opacity > 0.4;
            if (estaAberto)
            {
                panel.MaxHeight = 0;
                panel.Opacity = 0.0;
                if (chevron != null) chevron.Data = AppIcons.ChevronDown;
                Dispatcher.UIThread.Post(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(250);
                    if (panel.MaxHeight == 0)
                    {
                        panel.IsVisible = false;
                    }
                });
            }
            else
            {
                panel.IsVisible = true;
                panel.MaxHeight = 0;
                panel.Opacity = 0.0;
                if (chevron != null) chevron.Data = AppIcons.ChevronUp;

                Dispatcher.UIThread.Post(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(20);
                    panel.MaxHeight = targetMaxHeight;
                    panel.Opacity = 1.0;
                });
            }
        }

        private void FechamentoRapidoDropdown(Border? panel, PathIcon? chevron)
        {
            if (panel == null) return;
            panel.MaxHeight = 0;
            panel.Opacity = 0.0;
            panel.IsVisible = false;
            if (chevron != null) chevron.Data = AppIcons.ChevronDown;
        }

        private void ToggleDropdownGaleria_Click(object? sender, RoutedEventArgs e)
        {
            _leftSidebarCloseTimer?.Stop();
            if (!_isSidebarExpanded)
            {
                ExpandirLeftSidebar(true);
                AnimarToggleDropdown(PanelSubmenuGaleria, ChevronSideGaleria, 150);
                return;
            }

            AnimarToggleDropdown(PanelSubmenuGaleria, ChevronSideGaleria, 150);
        }

        private void ToggleDropdownNavegador_Click(object? sender, RoutedEventArgs e)
        {
            _leftSidebarCloseTimer?.Stop();
            if (!_isSidebarExpanded)
            {
                ExpandirLeftSidebar(true);
                AnimarToggleDropdown(PanelSubmenuNavegador, ChevronSideNavegador, 110);
                return;
            }

            AnimarToggleDropdown(PanelSubmenuNavegador, ChevronSideNavegador, 110);
        }

        private void ToggleDropdownFluxograma_Click(object? sender, RoutedEventArgs e)
        {
            _leftSidebarCloseTimer?.Stop();
            if (!_isSidebarExpanded)
            {
                ExpandirLeftSidebar(true);
                AnimarToggleDropdown(PanelSubmenuFluxograma, ChevronSideFluxograma, 150);
                return;
            }

            AnimarToggleDropdown(PanelSubmenuFluxograma, ChevronSideFluxograma, 150);
        }

        private void ExpandirRightCalendarBar(bool expand)
        {
            RightCalendarBar.Width = expand ? 145 : 46;
            if (LblRightCalendario == null) return;

            if (expand)
            {
                LblRightCalendario.IsVisible = true;
                LblRightCalendario.Opacity = 0.0;
                Dispatcher.UIThread.Post(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(35);
                    if (RightCalendarBar.Width > 50)
                    {
                        LblRightCalendario.Opacity = 1.0;
                    }
                });
            }
            else
            {
                LblRightCalendario.Opacity = 0.0;
                Dispatcher.UIThread.Post(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(130);
                    if (RightCalendarBar.Width <= 50)
                    {
                        LblRightCalendario.IsVisible = false;
                    }
                });
            }
        }

        private void ToggleSidebar_Click(object? sender, RoutedEventArgs e)
        {
            _leftSidebarOpenTimer?.Stop();
            _leftSidebarCloseTimer?.Stop();
            _isSidebarPinned = !_isSidebarPinned;
            ExpandirLeftSidebar(_isSidebarPinned);
        }

        private void MostrarModalBoasVindas()
        {
            var cardBg = _isDarkMode ? ThemePalette.DarkWindowBg : ThemePalette.LightWindowBg;
            var borderBrush = _isDarkMode ? ThemePalette.DarkBorder : ThemePalette.LightBorder;
            var textPrimary = _isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;
            var textSecondary = _isDarkMode ? ThemePalette.DarkTextSecondary : ThemePalette.LightTextSecondary;

            var title = new TextBlock
            {
                Text = "Bem-vindo ao Gerenciador MDI Portátil!",
                FontSize = 18,
                FontWeight = FontWeight.Bold,
                Foreground = textPrimary
            };

            var subtitle = new TextBlock
            {
                Text = "Organize suas ideias, tarefas, planilhas, mídias, fluxogramas e páginas web em um único espaço de trabalho portátil.",
                FontSize = 12.5,
                TextWrapping = TextWrapping.Wrap,
                Foreground = textSecondary,
                LineHeight = 19
            };

            var tipsPanel = new StackPanel { Spacing = 8, Margin = new Thickness(0, 4, 0, 4) };
            string[] tips =
            {
                "• Menu Lateral: Use os ícones à esquerda (ou clique no botão de menu) para abrir Bloco de Notas, Listas, Planilhas, Galeria, Navegador e Fluxograma.",
                "• Fluxograma e Ligações: Conecte janelas e formas ou vincule itens individuais da Lista de Marcação a qualquer janela.",
                "• Workspaces Portáteis: Crie vários espaços na barra superior e exporte em PDF, PNG, HTML ou ZIP."
            };
            foreach (var tip in tips)
            {
                tipsPanel.Children.Add(new TextBlock
                {
                    Text = tip,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = textPrimary,
                    LineHeight = 18
                });
            }

            var btnStart = new Button
            {
                Content = "Começar a usar",
                HorizontalAlignment = HorizontalAlignment.Right,
                Padding = new Thickness(16, 8),
                CornerRadius = new CornerRadius(6),
                Background = ThemePalette.Accent,
                Foreground = Brushes.White,
                FontWeight = FontWeight.SemiBold,
                FontSize = 12.5
            };

            btnStart.Click += (_, _) =>
            {
                _settings.HasSeenWelcome = true;
                SettingsManager.Salvar(_settings);
                ModalOverlay.IsVisible = false;
                ModalOverlay.Child = null;
            };

            var card = new Border
            {
                Width = 480,
                Padding = new Thickness(24),
                CornerRadius = new CornerRadius(12),
                Background = cardBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new StackPanel
                {
                    Spacing = 12,
                    Children = { title, subtitle, tipsPanel, btnStart }
                }
            };

            ModalOverlay.Child = card;
            ModalOverlay.IsVisible = true;
        }

        // ==================== ZOOM OUT / IN E PAN DO WORKSPACE ====================

        private readonly Stack<string> _undoStack = new();
        private readonly Stack<string> _redoStack = new();
        private bool _isRestoringUndoRedo;

        private string CapturarSnapshotWorkspaceJson()
        {
            var ws = ObterWorkspaceAtual();
            var snapshot = new WorkspaceData
            {
                Id = ws.Id,
                Name = ws.Name,
                BackgroundImagePath = _workspaceBgImagePath,
                WorkspaceZoom = _workspaceZoom,
                SavedWindows = _ativasJanelas.Select(w => w.ExportState()).ToList(),
                Connections = _conexoesAtivas.Select(c => new WindowConnectionData
                {
                    Id = c.ConnectionId,
                    FromWindowId = c.FromWindow.WindowId,
                    ToWindowId = c.ToWindow.WindowId,
                    Label = c.Label,
                    ColorHex = c.ColorHex
                }).ToList(),
                CalendarLinks = ws.CalendarLinks.Select(l => new CalendarStageLinkData
                {
                    Id = l.Id,
                    StartDate = l.StartDate,
                    EndDate = l.EndDate,
                    TimeRange = l.TimeRange,
                    WindowId = l.WindowId,
                    ChecklistRowId = l.ChecklistRowId
                }).ToList()
            };
            return System.Text.Json.JsonSerializer.Serialize(snapshot);
        }

        private void RegistrarEstadoUndo()
        {
            if (_isRestoringUndoRedo || _trocandoWorkspace) return;
            string json = CapturarSnapshotWorkspaceJson();
            if (_undoStack.Count > 0 && _undoStack.Peek() == json) return;
            _undoStack.Push(json);
            if (_undoStack.Count > 35)
            {
                var trimmed = _undoStack.Take(35).Reverse().ToList();
                _undoStack.Clear();
                foreach (var s in trimmed) _undoStack.Push(s);
            }
            _redoStack.Clear();
        }

        private void AplicarSnapshotWorkspaceJson(string json)
        {
            var snapshot = System.Text.Json.JsonSerializer.Deserialize<WorkspaceData>(json);
            if (snapshot == null) return;

            var ws = ObterWorkspaceAtual();
            ws.SavedWindows = snapshot.SavedWindows;
            ws.Connections = snapshot.Connections;
            ws.CalendarLinks = snapshot.CalendarLinks;
            ws.BackgroundImagePath = snapshot.BackgroundImagePath;
            ws.WorkspaceZoom = snapshot.WorkspaceZoom;

            _isRestoringUndoRedo = true;
            _trocandoWorkspace = true;
            try
            {
                foreach (var w in _ativasJanelas.ToList())
                {
                    w.Fechar();
                }
                _ativasJanelas.Clear();
                foreach (var conn in _conexoesAtivas.ToList())
                {
                    conn.RemoverDoCanvas();
                }
                _conexoesAtivas.Clear();
                CarregarJanelasDoWorkspace(ws);
                if (_isCalendarOpen)
                {
                    RenderizarCalendarioMes();
                    RenderizarEtapasDoPeriodo();
                }
                AplicarFiltroCores();
            }
            finally
            {
                _trocandoWorkspace = false;
                _isRestoringUndoRedo = false;
            }
        }

        private void DesfazerWorkspace()
        {
            if (_undoStack.Count == 0)
            {
                if (_settings.EnableNotifications)
                {
                    ToastNotification.Mostrar(Workspace, "Nada para desfazer", "info");
                }
                return;
            }

            _redoStack.Push(CapturarSnapshotWorkspaceJson());
            string prev = _undoStack.Pop();
            AplicarSnapshotWorkspaceJson(prev);
            SalvarTodosWorkspacesNoDisco();
            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, "Alteração desfeita (Ctrl+Z)", "info");
            }
        }

        private void RefazerWorkspace()
        {
            if (_redoStack.Count == 0)
            {
                if (_settings.EnableNotifications)
                {
                    ToastNotification.Mostrar(Workspace, "Nada para refazer", "info");
                }
                return;
            }

            _undoStack.Push(CapturarSnapshotWorkspaceJson());
            string next = _redoStack.Pop();
            AplicarSnapshotWorkspaceJson(next);
            SalvarTodosWorkspacesNoDisco();
            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, "Alteração refeita (Ctrl+Y)", "info");
            }
        }

        private void ConfigurarEventosWorkspaceZoomEPan()
        {
            WorkspaceViewport.SizeChanged += (s, e) =>
            {
                AtualizarDimensoesCanvasWorkspace();
                foreach (var win in _ativasJanelas)
                {
                    win.AtualizarTamanhoSeMaximizado();
                }
            };

            // Atalho Ctrl + Roda do Mouse para Zoom In / Zoom Out no Workspace
            WorkspaceViewport.AddHandler(PointerWheelChangedEvent, (s, e) =>
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                    double delta = e.Delta.Y > 0 ? 0.12 : -0.12;
                    AplicarZoomWorkspace(_workspaceZoom + delta);
                    e.Handled = true;
                }
            }, RoutingStrategies.Tunnel);

            // Inicializa a caixa visual de seleção retangular (marquee / rubberband)
            _selectionMarquee = new Border
            {
                Background = SolidColorBrush.Parse("#2638BDF8"),
                BorderBrush = SolidColorBrush.Parse("#38BDF8"),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(2),
                IsHitTestVisible = false,
                IsVisible = false,
                ZIndex = 999999
            };
            Workspace.Children.Add(_selectionMarquee);

            // Interação no Workspace: Seleção Múltipla Retangular, Pan e Pré-visualização de conexões
            Workspace.PointerPressed += (s, e) =>
            {
                var sourceCtrl = e.Source as Control;
                bool isDirectWorkspace = e.Source == Workspace || e.Source == _selectionMarquee;
                bool isInsideWindow = sourceCtrl != null && sourceCtrl.FindAncestorOfType<DraggableWindow>() != null;

                if (isDirectWorkspace || !isInsideWindow)
                {
                    Workspace.Focus();
                    if (_modoVinculoLinhaAtivo)
                    {
                        CancelarModoVinculoLinha(notificar: true);
                        e.Handled = true;
                        return;
                    }

                    if (_modoConexaoAtivo)
                    {
                        CancelarModoVinculoLinha();
                        CancelarModoConexao();
                        e.Handled = true;
                        return;
                    }

                    var props = e.GetCurrentPoint(Workspace);
                    bool isMiddle = props.Properties.IsMiddleButtonPressed;
                    bool isLeft = props.Properties.IsLeftButtonPressed;

                    if (isMiddle || (isLeft && _isSpaceKeyDown))
                    {
                        _isPanningWorkspace = true;
                        _panPointerStart = e.GetCurrentPoint(WorkspaceViewport).Position;
                        _panOffsetStart = WorkspaceScroller.Offset;
                        Workspace.Cursor = new Cursor(StandardCursorType.Hand);
                        e.Pointer.Capture(Workspace);
                        e.Handled = true;
                        return;
                    }

                    if (isLeft)
                    {
                        _isRubberbandSelecting = true;
                        _rubberbandStart = e.GetPosition(Workspace);
                        _rubberbandCtrlHeld = e.KeyModifiers.HasFlag(KeyModifiers.Control);

                        if (!_rubberbandCtrlHeld)
                        {
                            LimparSelecaoJanelas();
                        }

                        _snapshotSelectedBeforeRubberband = new HashSet<DraggableWindow>(_selectedWindows);

                        GarantirCaixaSelecaoNoCanvas();
                        AtualizarVisualCaixaSelecao(_rubberbandStart, _rubberbandStart);
                        _selectionMarquee.IsVisible = true;
                        e.Pointer.Capture(Workspace);
                        e.Handled = true;
                        return;
                    }
                }
            };

            Workspace.PointerMoved += (s, e) =>
            {
                if (_modoVinculoLinhaAtivo && _checklistWinOrigemVinculo != null && _linhaOrigemVinculo != null)
                {
                    AtualizarLinhaPreviewVinculo(e.GetPosition(Workspace));
                }

                if (_modoConexaoAtivo && _janelaOrigemConexao != null)
                {
                    AtualizarLinhaPreviewConexao(e.GetPosition(Workspace));
                }

                if (_isPanningWorkspace)
                {
                    var currentPos = e.GetPosition(WorkspaceViewport);
                    var diff = currentPos - _panPointerStart;
                    WorkspaceScroller.Offset = new Vector(
                        Math.Max(0, _panOffsetStart.X - diff.X),
                        Math.Max(0, _panOffsetStart.Y - diff.Y));
                    return;
                }

                if (_isRubberbandSelecting && _selectionMarquee != null)
                {
                    var currentPos = e.GetPosition(Workspace);
                    AtualizarVisualCaixaSelecao(_rubberbandStart, currentPos);

                    double left = Math.Min(_rubberbandStart.X, currentPos.X);
                    double top = Math.Min(_rubberbandStart.Y, currentPos.Y);
                    double width = Math.Abs(currentPos.X - _rubberbandStart.X);
                    double height = Math.Abs(currentPos.Y - _rubberbandStart.Y);
                    var selRect = new Rect(left, top, width, height);

                    foreach (var win in _ativasJanelas)
                    {
                        if (win.IsMinimized) continue;

                        double wx = Canvas.GetLeft(win);
                        double wy = Canvas.GetTop(win);
                        if (double.IsNaN(wx)) wx = 0;
                        if (double.IsNaN(wy)) wy = 0;
                        double ww = win.Bounds.Width > 0 ? win.Bounds.Width : win.Width;
                        double wh = win.Bounds.Height > 0 ? win.Bounds.Height : win.Height;
                        var winRect = new Rect(wx, wy, ww, wh);

                        bool intersects = selRect.Intersects(winRect) || selRect.Contains(winRect);

                        if (_rubberbandCtrlHeld)
                        {
                            bool wasSelectedBefore = _snapshotSelectedBeforeRubberband.Contains(win);
                            DefinirSelecaoJanela(win, wasSelectedBefore || intersects);
                        }
                        else
                        {
                            DefinirSelecaoJanela(win, intersects);
                        }
                    }
                    e.Handled = true;
                }
            };

            Workspace.PointerReleased += (s, e) =>
            {
                if (_isPanningWorkspace)
                {
                    _isPanningWorkspace = false;
                    Workspace.Cursor = Cursor.Default;
                    e.Pointer.Capture(null);
                }

                if (_isRubberbandSelecting)
                {
                    _isRubberbandSelecting = false;
                    if (_selectionMarquee != null)
                    {
                        _selectionMarquee.IsVisible = false;
                    }
                    e.Pointer.Capture(null);

                    var endPos = e.GetPosition(Workspace);
                    if (Math.Abs(endPos.X - _rubberbandStart.X) < 4 && Math.Abs(endPos.Y - _rubberbandStart.Y) < 4 && !_rubberbandCtrlHeld)
                    {
                        LimparSelecaoJanelas();
                    }
                    e.Handled = true;
                }
            };

            AddHandler(InputElement.KeyDownEvent, MainWindow_KeyDown, RoutingStrategies.Tunnel);
            AddHandler(InputElement.KeyUpEvent, MainWindow_KeyUp, RoutingStrategies.Tunnel);

            // Expansão suave dos menus laterais ao passar o mouse com delays (1,0s abrir, 1,7s fechar)
            LeftSidebar.PointerEntered += (s, e) =>
            {
                _leftSidebarCloseTimer?.Stop();
                if (_settings.AutoExpandSidebarsOnHover && !_isSidebarPinned && !_isSidebarExpanded)
                {
                    _leftSidebarOpenTimer?.Stop();
                    _leftSidebarOpenTimer?.Start();
                }
            };
            LeftSidebar.PointerExited += (s, e) =>
            {
                _leftSidebarOpenTimer?.Stop();
                if (_settings.AutoExpandSidebarsOnHover && !_isSidebarPinned && _isSidebarExpanded)
                {
                    _leftSidebarCloseTimer?.Stop();
                    _leftSidebarCloseTimer?.Start();
                }
            };

            RightCalendarBar.PointerEntered += (s, e) =>
            {
                _rightSidebarCloseTimer?.Stop();
                if (_settings.AutoExpandSidebarsOnHover && RightCalendarBar.Width <= 50)
                {
                    _rightSidebarOpenTimer?.Stop();
                    _rightSidebarOpenTimer?.Start();
                }
            };
            RightCalendarBar.PointerExited += (s, e) =>
            {
                _rightSidebarOpenTimer?.Stop();
                if (_settings.AutoExpandSidebarsOnHover && RightCalendarBar.Width > 50)
                {
                    _rightSidebarCloseTimer?.Stop();
                    _rightSidebarCloseTimer?.Start();
                }
            };
        }

        private void InicializarTimersHoverMenusLaterais()
        {
            // Delay de 1,0s para expandir no hover
            _leftSidebarOpenTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.0) };
            _leftSidebarOpenTimer.Tick += (s, e) =>
            {
                _leftSidebarOpenTimer.Stop();
                if (_settings.AutoExpandSidebarsOnHover && !_isSidebarPinned && !_isSidebarExpanded)
                {
                    ExpandirLeftSidebar(true);
                }
            };

            // Delay de 1,7s para fechar após o mouse sair
            _leftSidebarCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.7) };
            _leftSidebarCloseTimer.Tick += (s, e) =>
            {
                _leftSidebarCloseTimer.Stop();
                if (_settings.AutoExpandSidebarsOnHover && !_isSidebarPinned && _isSidebarExpanded)
                {
                    ExpandirLeftSidebar(false);
                }
            };

            _rightSidebarOpenTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.0) };
            _rightSidebarOpenTimer.Tick += (s, e) =>
            {
                _rightSidebarOpenTimer.Stop();
                if (_settings.AutoExpandSidebarsOnHover)
                {
                    ExpandirRightCalendarBar(true);
                }
            };

            _rightSidebarCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.7) };
            _rightSidebarCloseTimer.Tick += (s, e) =>
            {
                _rightSidebarCloseTimer.Stop();
                if (_settings.AutoExpandSidebarsOnHover)
                {
                    ExpandirRightCalendarBar(false);
                }
            };
        }

        private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (RadialMenuOverlay.IsVisible)
                {
                    FecharMenuCircular();
                    e.Handled = true;
                    return;
                }

                if (_modoVinculoLinhaAtivo)
                {
                    CancelarModoVinculoLinha(notificar: true);
                    e.Handled = true;
                    return;
                }

                if (_isPickingCalendarTarget)
                {
                    CancelarModoVinculoCalendario();
                    e.Handled = true;
                    return;
                }

                if (_modoConexaoAtivo)
                {
                    CancelarModoVinculoLinha();
                    CancelarModoConexao();
                    e.Handled = true;
                    return;
                }
            }

            if (e.Key == Key.Space && !ModalOverlay.IsVisible)
            {
                var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
                if (focused is not TextBox)
                {
                    _isSpaceKeyDown = true;
                    if (!_isRubberbandSelecting)
                    {
                        Workspace.Cursor = new Cursor(StandardCursorType.Hand);
                    }
                }
            }

            if (e.Key == Key.Escape)
            {
                if (_selectedWindows.Count > 0)
                {
                    LimparSelecaoJanelas();
                    e.Handled = true;
                    return;
                }
            }

            bool isCtrl = (e.KeyModifiers & KeyModifiers.Control) != 0;
            bool isShift = (e.KeyModifiers & KeyModifiers.Shift) != 0;

            if (isCtrl && !ModalOverlay.IsVisible)
            {
                var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();

                if (e.Key == Key.A && focused is not TextBox)
                {
                    SelecionarTodasJanelas();
                    e.Handled = true;
                    return;
                }

                if (e.Key == Key.S)
                {
                    SalvarTodasJanelas_Click(null, new RoutedEventArgs());
                    e.Handled = true;
                    return;
                }

                if (e.Key == Key.Z && !isShift)
                {
                    if (focused is TextBox tb && tb.CanUndo)
                    {
                        // Deixa o TextBox nativo processar o Ctrl+Z de texto
                        return;
                    }

                    DesfazerWorkspace();
                    e.Handled = true;
                    return;
                }

                if (e.Key == Key.Y || (e.Key == Key.Z && isShift))
                {
                    if (focused is TextBox tb && tb.CanRedo)
                    {
                        tb.Redo();
                        e.Handled = true;
                        return;
                    }

                    RefazerWorkspace();
                    e.Handled = true;
                    return;
                }
            }

            if (e.Key == Key.Delete && !ModalOverlay.IsVisible)
            {
                var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
                if (focused is TextBox)
                {
                    return;
                }

                if (_selectedWindows.Count > 0)
                {
                    RegistrarEstadoUndo();
                    var toClose = _selectedWindows.ToList();
                    LimparSelecaoJanelas();
                    foreach (var win in toClose)
                    {
                        win.Fechar();
                    }
                    e.Handled = true;
                    return;
                }

                var ativa = ZIndexManager.ActiveWindow;
                if (ativa != null && _ativasJanelas.Contains(ativa))
                {
                    RegistrarEstadoUndo();
                    ativa.Fechar();
                    e.Handled = true;
                }
            }
        }

        private void MainWindow_KeyUp(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                _isSpaceKeyDown = false;
                if (!_isPanningWorkspace && !_isRubberbandSelecting)
                {
                    Workspace.Cursor = Cursor.Default;
                }
            }
        }

        private void GarantirCaixaSelecaoNoCanvas()
        {
            if (_selectionMarquee != null && !Workspace.Children.Contains(_selectionMarquee))
            {
                Workspace.Children.Add(_selectionMarquee);
            }
        }

        private void AtualizarVisualCaixaSelecao(Point p1, Point p2)
        {
            if (_selectionMarquee == null) return;
            double left = Math.Min(p1.X, p2.X);
            double top = Math.Min(p1.Y, p2.Y);
            double width = Math.Abs(p2.X - p1.X);
            double height = Math.Abs(p2.Y - p1.Y);

            Canvas.SetLeft(_selectionMarquee, left);
            Canvas.SetTop(_selectionMarquee, top);
            _selectionMarquee.Width = width;
            _selectionMarquee.Height = height;
        }

        private void DefinirSelecaoJanela(DraggableWindow win, bool select)
        {
            if (select)
            {
                if (_selectedWindows.Add(win))
                {
                    win.SetSelected(true);
                }
            }
            else
            {
                if (_selectedWindows.Remove(win))
                {
                    win.SetSelected(false);
                }
            }
        }

        private void SelecionarJanela(DraggableWindow win) => DefinirSelecaoJanela(win, true);

        private void DeselecionarJanela(DraggableWindow win) => DefinirSelecaoJanela(win, false);

        private void SelecionarApenasJanela(DraggableWindow win)
        {
            var toDeselect = _selectedWindows.Where(w => w != win).ToList();
            foreach (var w in toDeselect)
            {
                DeselecionarJanela(w);
            }
            SelecionarJanela(win);
        }

        private void LimparSelecaoJanelas()
        {
            foreach (var win in _selectedWindows.ToList())
            {
                win.SetSelected(false);
            }
            _selectedWindows.Clear();
        }

        private void SelecionarTodasJanelas()
        {
            foreach (var win in _ativasJanelas)
            {
                if (!win.IsMinimized)
                {
                    SelecionarJanela(win);
                }
            }
        }

        private void WorkspaceZoomIn_Click(object? sender, RoutedEventArgs e)
        {
            AplicarZoomWorkspace(_workspaceZoom + 0.15);
        }

        private void WorkspaceZoomOut_Click(object? sender, RoutedEventArgs e)
        {
            AplicarZoomWorkspace(_workspaceZoom - 0.15);
        }

        private void WorkspaceZoomReset_Click(object? sender, RoutedEventArgs e)
        {
            AplicarZoomWorkspace(1.0);
        }

        private void AplicarZoomWorkspace(double novoZoom)
        {
            _workspaceZoom = Math.Clamp(Math.Round(novoZoom, 2), 0.25, 2.00);
            var ws = ObterWorkspaceAtual();
            ws.WorkspaceZoom = _workspaceZoom;

            WorkspaceZoomHost.LayoutTransform = Math.Abs(_workspaceZoom - 1.0) < 0.01
                ? null
                : new ScaleTransform(_workspaceZoom, _workspaceZoom);

            BtnHudZoomReset.Content = $"{(int)Math.Round(_workspaceZoom * 100)}%";
            AtualizarDimensoesCanvasWorkspace();
        }

        private void AtualizarDimensoesCanvasWorkspace()
        {
            double viewW = Math.Max(640, WorkspaceViewport.Bounds.Width);
            double viewH = Math.Max(400, WorkspaceViewport.Bounds.Height);

            double minCanvasW = viewW / Math.Max(0.25, _workspaceZoom);
            double minCanvasH = viewH / Math.Max(0.25, _workspaceZoom);

            double maxWinRight = 0;
            double maxWinBottom = 0;

            foreach (var win in _ativasJanelas)
            {
                var b = win.GetNormalBounds();
                if (b.Right > maxWinRight) maxWinRight = b.Right;
                if (b.Bottom > maxWinBottom) maxWinBottom = b.Bottom;
            }

            // Garante folga extra para posicionar novas janelas ao dar Zoom Out
            Workspace.Width = Math.Max(minCanvasW, maxWinRight + 450);
            Workspace.Height = Math.Max(minCanvasH, maxWinBottom + 320);
        }

        // ==================== FLUXOGRAMA (CONEXÕES ENTRE JANELAS) ====================

        private void AlternarModoConexao_Click(object? sender, RoutedEventArgs e)
        {
            if (_modoConexaoAtivo)
            {
                CancelarModoVinculoLinha();
            CancelarModoConexao();
                return;
            }

            if (_ativasJanelas.Count < 2)
            {
                ToastNotification.Mostrar(Workspace, "Abra pelo menos 2 janelas no Workspace para conectá-las.", "warning");
                return;
            }

            _modoConexaoAtivo = true;
            _janelaOrigemConexao = null;
            AtualizarVisualBotaoConexaoHud();
            ToastNotification.Mostrar(Workspace, "Modo Fluxograma: clique na 1ª janela (origem) e depois na 2ª janela (destino).", "info");
        }

        private void IniciarConexaoAPartirDeJanela(DraggableWindow origem)
        {
            if (_modoConexaoAtivo && _janelaOrigemConexao != null && _janelaOrigemConexao != origem)
            {
                ConcluirConexaoEntreJanelas(_janelaOrigemConexao, origem);
                return;
            }

            _modoConexaoAtivo = true;
            _janelaOrigemConexao = origem;
            AtualizarVisualBotaoConexaoHud();
            GarantirLinhaPreviewNoCanvas();

            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, $"Origem: \"{origem.WindowTitle}\". Agora clique na janela de destino para ligar!", "info");
            }
        }

        private void AoClicarEmJanela(DraggableWindow janelaClicada)
        {
            if (_modoVinculoLinhaAtivo)
            {
                ConcluirVinculoLinhaComJanela(janelaClicada);
                return;
            }

            if (_isPickingCalendarTarget)
            {
                if (janelaClicada.WindowType != "checklist")
                {
                    AoSelecionarAlvoParaCalendario(janelaClicada, string.Empty);
                }
                return;
            }

            if (!_modoConexaoAtivo) return;

            if (_janelaOrigemConexao == null)
            {
                _janelaOrigemConexao = janelaClicada;
                GarantirLinhaPreviewNoCanvas();
                ToastNotification.Mostrar(Workspace, $"Origem: \"{janelaClicada.WindowTitle}\". Clique agora na janela de destino!", "info");
                return;
            }

            if (_janelaOrigemConexao == janelaClicada)
            {
                return;
            }

            ConcluirConexaoEntreJanelas(_janelaOrigemConexao, janelaClicada);
        }

        private void ConcluirConexaoEntreJanelas(DraggableWindow origem, DraggableWindow destino)
        {
            CancelarModoVinculoLinha();
            CancelarModoConexao();

            RegistrarEstadoUndo();

            // Se a janela de origem possui uma cor definida, herda para a linha e para a janela de destino
            string corOrigem = origem.ObterCorEfetiva();
            string? corLinha = !string.IsNullOrWhiteSpace(corOrigem) ? corOrigem : null;

            if (!string.IsNullOrWhiteSpace(corOrigem) && string.IsNullOrWhiteSpace(destino.ObterCorEfetiva()))
            {
                destino.DefinirCorJanela(corOrigem);
            }

            CriarOuFocarConexaoFluxograma(origem, destino, mostrarToast: true, colorHex: corLinha);
            SalvarTodosWorkspacesNoDisco();
        }

        private FlowchartConnectionVisual? CriarOuFocarConexaoFluxograma(
            DraggableWindow origem,
            DraggableWindow destino,
            string? connectionId = null,
            string label = "",
            bool mostrarToast = false,
            string? colorHex = null)
        {
            if (origem == destino) return null;

            var existente = _conexoesAtivas.FirstOrDefault(c =>
                (c.FromWindow == origem && c.ToWindow == destino) ||
                (c.FromWindow == destino && c.ToWindow == origem));

            if (existente != null)
            {
                existente.AtualizarGeometria();
                if (mostrarToast && _settings.EnableNotifications)
                {
                    ToastNotification.Mostrar(Workspace, "Essas duas janelas já estão conectadas!", "info");
                }
                return existente;
            }

            string corLinha = string.IsNullOrWhiteSpace(colorHex)
                ? (string.IsNullOrWhiteSpace(_settings.DefaultFlowchartLineColor) ? "#3B82F6" : _settings.DefaultFlowchartLineColor)
                : colorHex;

            var visual = new FlowchartConnectionVisual(
                Workspace,
                origem,
                destino,
                _isDarkMode,
                connectionId,
                label,
                corLinha);

            visual.OnColorChanged += (_) => SalvarTodosWorkspacesNoDisco();
            visual.OnRemoveRequested += (conn) =>
            {
                RegistrarEstadoUndo();
                conn.RemoverDoCanvas();
                _conexoesAtivas.Remove(conn);
                SalvarTodosWorkspacesNoDisco();
                if (_settings.EnableNotifications)
                {
                    ToastNotification.Mostrar(Workspace, "Ligação removida do fluxograma.", "info");
                }
            };

            _conexoesAtivas.Add(visual);

            if (mostrarToast && _settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, $"\"{origem.WindowTitle}\" conectado a \"{destino.WindowTitle}\"!", "success");
            }

            return visual;
        }

        private void GarantirLinhaPreviewNoCanvas()
        {
            if (_previewConnectionPath == null)
            {
                _previewConnectionPath = new Avalonia.Controls.Shapes.Path
                {
                    Stroke = ThemePalette.AccentLight,
                    StrokeThickness = 2.4,
                    StrokeDashArray = new AvaloniaList<double> { 5, 4 },
                    IsHitTestVisible = false,
                    ZIndex = 900
                };
            }

            if (!Workspace.Children.Contains(_previewConnectionPath))
            {
                Workspace.Children.Add(_previewConnectionPath);
            }
        }

        private void AtualizarLinhaPreviewConexao(Point mousePos)
        {
            if (_janelaOrigemConexao == null || _previewConnectionPath == null) return;

            double x = Canvas.GetLeft(_janelaOrigemConexao);
            double y = Canvas.GetTop(_janelaOrigemConexao);
            Point start = new Point(
                (double.IsNaN(x) ? 0 : x) + _janelaOrigemConexao.Width / 2,
                (double.IsNaN(y) ? 0 : y) + _janelaOrigemConexao.Height / 2);

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(start, false);
                ctx.LineTo(mousePos);
            }
            _previewConnectionPath.Data = geo;
        }

        private void CancelarModoConexao()
        {
            _modoConexaoAtivo = false;
            _janelaOrigemConexao = null;
            if (_previewConnectionPath != null)
            {
                Workspace.Children.Remove(_previewConnectionPath);
                _previewConnectionPath.Data = null;
            }
            AtualizarVisualBotaoConexaoHud();
        }

        private void AtualizarVisualBotaoConexaoHud()
        {
        }

        private void LimparConexoesWorkspace_Click(object? sender, RoutedEventArgs e)
        {
            if (_conexoesAtivas.Count == 0)
            {
                ToastNotification.Mostrar(Workspace, "Não há ligações ativas neste Workspace.", "info");
                return;
            }

            RegistrarEstadoUndo();

            foreach (var conn in _conexoesAtivas.ToList())
            {
                conn.RemoverDoCanvas();
            }
            _conexoesAtivas.Clear();
            SalvarTodosWorkspacesNoDisco();

            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, "Todas as ligações do fluxograma foram removidas.", "info");
            }
        }

        private void RemoverConexoesDaJanela(DraggableWindow win)
        {
            var ligadas = _conexoesAtivas
                .Where(c => c.FromWindow == win || c.ToWindow == win)
                .ToList();

            foreach (var conn in ligadas)
            {
                conn.RemoverDoCanvas();
                _conexoesAtivas.Remove(conn);
            }
        }

        // ==================== MENU CIRCULAR / RADIAL (THE SIMS 4) ====================

        private void AtualizarSuspensaoNavegadores()
        {
            bool suspenso = ModalOverlay.IsVisible 
                || RadialMenuOverlay.IsVisible
                || (_settingsWindow != null && _settingsWindow.IsVisible)
                || (_notificationHistoryModal != null && _notificationHistoryModal.IsVisible)
                || (_tutorialModal != null && _tutorialModal.IsVisible);

            foreach (var win in _ativasJanelas)
            {
                win.SetModalOverlaySuspended(suspenso);
            }
        }

        private void AbrirMenuCircularJanela(DraggableWindow win, Control anchor)
        {
            FecharMenuCircular();

            Point anchorCenterLocal = new Point(anchor.Bounds.Width / 2.0, anchor.Bounds.Height / 2.0);
            Point? ptNoCanvas = anchor.TranslatePoint(anchorCenterLocal, RadialMenuCanvas);

            double overlayW = RadialMenuCanvas.Bounds.Width > 0 ? RadialMenuCanvas.Bounds.Width : WorkspaceViewport.Bounds.Width;
            double overlayH = RadialMenuCanvas.Bounds.Height > 0 ? RadialMenuCanvas.Bounds.Height : WorkspaceViewport.Bounds.Height;
            if (overlayW <= 0) overlayW = Bounds.Width > 0 ? Bounds.Width - 120 : 800;
            if (overlayH <= 0) overlayH = Bounds.Height > 0 ? Bounds.Height - 120 : 600;

            Point center = ptNoCanvas ?? new Point(overlayW / 2.0, overlayH / 2.0);

            // Raio máximo do submenu: rx = 185px, ry = 125px.
            // Com a largura média do botão (~130px -> metade ~65px) e respiro (~15px):
            // Margem horizontal segura = 265px
            // Margem vertical segura = 155px
            double marginX = 265;
            double marginY = 155;

            double clampedX = overlayW >= marginX * 2
                ? Math.Clamp(center.X, marginX, overlayW - marginX)
                : overlayW / 2.0;

            double clampedY = overlayH >= marginY * 2
                ? Math.Clamp(center.Y, marginY, overlayH - marginY)
                : overlayH / 2.0;

            Point clampedCenter = new Point(clampedX, clampedY);

            bool hasConnections = _conexoesAtivas.Any(c => c.FromWindow == win || c.ToWindow == win);
            var radialMenu = new RadialWindowMenu(win, clampedCenter, hasConnections);
            radialMenu.OnClosed += () => FecharMenuCircular();

            RadialMenuCanvas.Children.Add(radialMenu);
            RadialMenuOverlay.IsVisible = true;
            AtualizarSuspensaoNavegadores();
        }

        private void FecharMenuCircular()
        {
            if (RadialMenuOverlay.IsVisible || RadialMenuCanvas.Children.Count > 0)
            {
                RadialMenuOverlay.IsVisible = false;
                RadialMenuCanvas.Children.Clear();
                AtualizarSuspensaoNavegadores();
            }
        }

        private void PropagarCorParaConectadas(DraggableWindow janelaOrigem, string novaCorHex)
        {
            RegistrarEstadoUndo();

            var visitadas = new HashSet<DraggableWindow>();
            var fila = new Queue<DraggableWindow>();
            var conexoesAfetadas = new HashSet<FlowchartConnectionVisual>();

            visitadas.Add(janelaOrigem);
            fila.Enqueue(janelaOrigem);

            while (fila.Count > 0)
            {
                var atual = fila.Dequeue();

                foreach (var conn in _conexoesAtivas)
                {
                    DraggableWindow? vizinha = null;
                    if (conn.FromWindow == atual) vizinha = conn.ToWindow;
                    else if (conn.ToWindow == atual) vizinha = conn.FromWindow;

                    if (vizinha != null)
                    {
                        conexoesAfetadas.Add(conn);
                        if (!visitadas.Contains(vizinha))
                        {
                            visitadas.Add(vizinha);
                            fila.Enqueue(vizinha);
                        }
                    }
                }
            }

            if (visitadas.Count <= 1 && conexoesAfetadas.Count == 0)
                return;

            string corLinha = !string.IsNullOrWhiteSpace(novaCorHex)
                ? novaCorHex
                : (string.IsNullOrWhiteSpace(_settings.DefaultFlowchartLineColor) ? "#3B82F6" : _settings.DefaultFlowchartLineColor);

            foreach (var conn in conexoesAfetadas)
            {
                conn.DefinirCor(corLinha);
            }

            foreach (var win in visitadas)
            {
                if (win != janelaOrigem)
                {
                    win.DefinirCorJanela(novaCorHex ?? string.Empty, notificarMudanca: false);
                }
            }

            SalvarTodosWorkspacesNoDisco();
            if (_settings.EnableNotifications && visitadas.Count > 1)
            {
                ToastNotification.Mostrar(Workspace, $"Cor propagada para {visitadas.Count} janelas conectadas!", "info");
            }
        }

        // ==================== CONFIRMAÇÃO DE SAÍDA E MODAIS ====================

        private WorkspaceData ObterWorkspaceAtual()
        {
            SettingsManager.GarantirWorkspaces(_settings);
            var ws = _settings.Workspaces.FirstOrDefault(w => w.Id == _settings.ActiveWorkspaceId && !w.IsClosed)
                     ?? _settings.Workspaces.FirstOrDefault(w => !w.IsClosed)
                     ?? _settings.Workspaces[0];
            ws.IsClosed = false;
            _settings.ActiveWorkspaceId = ws.Id;
            return ws;
        }

        private bool TemCoisasParaSalvar()
        {
            return _ativasJanelas.Count > 0 || _conexoesAtivas.Count > 0;
        }

        private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
        {
            if (_saidaConfirmada)
            {
                return;
            }

            // Se não há nada aberto no workspace para salvar, fecha diretamente sem modal nem travamento
            if (!TemCoisasParaSalvar())
            {
                _saidaConfirmada = true;
                LiberarRecursosAntesDeFechar();
                return;
            }

            e.Cancel = true;
            MostrarModalConfirmacaoSaida();
        }

        private void MostrarModalConfirmacaoSaida()
        {
            ModalTitle.Text = "Salvar antes de fechar?";
            ModalMessage.Text = "Deseja salvar as janelas abertas, fluxogramas e informações dos seus Workspaces antes de encerrar o aplicativo?";
            ModalInputBox.IsVisible = false;
            if (ModalCheckBox != null) ModalCheckBox.IsVisible = false;
            ModalButtonsPanel.Children.Clear();

            var btnCancelar = CriarBotaoModal("Cancelar", isPrimary: false, isDanger: false);
            btnCancelar.Click += (s, e) => ModalOverlay.IsVisible = false;

            var btnSairSemSalvar = CriarBotaoModal("Sair sem Salvar", isPrimary: false, isDanger: true, AppIcons.Exit);
            btnSairSemSalvar.Click += (s, e) =>
            {
                ModalOverlay.IsVisible = false;
                _saidaConfirmada = true;
                LiberarRecursosAntesDeFechar();
                Close();
            };

            var btnSalvarESair = CriarBotaoModal("Salvar e Sair", isPrimary: true, isDanger: false, AppIcons.Save);
            btnSalvarESair.Click += async (s, e) =>
            {
                SalvarTodosWorkspacesNoDisco();

                if (!string.IsNullOrWhiteSpace(_settings.GoogleOAuthRefreshToken))
                {
                    _settings.GoogleDriveCloudSyncEnabled = true;
                    ModalTitle.Text = "Sincronizando Nuvem...";
                    ModalMessage.Text = "Salvando seus workspaces diretamente no Google Drive antes de fechar...";
                    ModalButtonsPanel.Children.Clear();
                    ModalInputBox.IsVisible = false;
                    if (ModalCheckBox != null) ModalCheckBox.IsVisible = false;
                    AplicarEstiloModal();

                    try
                    {
                        var uploadTask = GoogleDriveCloudClient.UploadTodosWorkspacesAsync(_settings);
                        await Task.WhenAny(uploadTask, Task.Delay(4000));
                    }
                    catch { }
                }

                ModalOverlay.IsVisible = false;
                _saidaConfirmada = true;
                LiberarRecursosAntesDeFechar();
                Close();
            };

            ModalButtonsPanel.Children.Add(btnCancelar);
            ModalButtonsPanel.Children.Add(btnSairSemSalvar);
            ModalButtonsPanel.Children.Add(btnSalvarESair);

            AplicarEstiloModal();
            ModalOverlay.IsVisible = true;
        }

        private void LiberarRecursosAntesDeFechar()
        {
            foreach (var win in _ativasJanelas.ToList())
            {
                try
                {
                    win.Fechar();
                }
                catch { }
            }
        }

        private Button CriarBotaoModal(string texto, bool isPrimary, bool isDanger, StreamGeometry? icon = null)
        {
            IBrush bg = isPrimary
                ? ThemePalette.Accent
                : isDanger
                    ? ThemePalette.Danger
                    : (_isDarkMode ? ThemePalette.DarkButtonBg : ThemePalette.LightButtonBg);

            IBrush fg = (isPrimary || isDanger)
                ? Brushes.White
                : (_isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary);

            return new Button
            {
                Content = icon != null ? AppIcons.WithText(icon, texto, 12, 6, ThemePalette.IconLightGray) : texto,
                Padding = new Thickness(14, 7),
                CornerRadius = new CornerRadius(6),
                Background = bg,
                Foreground = fg,
                FontSize = 12.5,
                FontWeight = FontWeight.SemiBold
            };
        }

        private void AplicarEstiloModal()
        {
            ModalCard.Background = _isDarkMode ? ThemePalette.DarkWindowBg : ThemePalette.LightWindowBg;
            ModalCard.BorderBrush = _isDarkMode ? ThemePalette.DarkBorderActive : ThemePalette.LightBorderActive;
            ModalTitle.Foreground = _isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;
            ModalMessage.Foreground = _isDarkMode ? ThemePalette.DarkTextSecondary : ThemePalette.LightTextSecondary;
            ModalInputBox.Background = _isDarkMode ? ThemePalette.DarkContentBg : ThemePalette.LightContentBg;
            ModalInputBox.Foreground = _isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;
            ModalInputBox.BorderBrush = _isDarkMode ? ThemePalette.DarkBorder : ThemePalette.LightBorder;
            if (ModalCheckBox != null)
            {
                ModalCheckBox.Foreground = _isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;
            }
        }

        // ==================== TEMA E LAYOUT ====================

        private double ObterFatorTamanhoWidget(string? preset = null)
        {
            string p = (preset ?? _widgetSizePreset).ToLowerInvariant();
            return p switch
            {
                "compact" => 0.85,
                "large" => 1.20,
                _ => 1.00
            };
        }

        private void AplicarEscalaLayout(double scale)
        {
            double s = Math.Clamp(scale > 0 ? scale : 1.0, 0.75, 1.50);
            RootScaleHost.LayoutTransform = Math.Abs(s - 1.0) < 0.01
                ? null
                : new ScaleTransform(s, s);
        }

        private void AplicarTamanhoJanelaPrincipal(string? preset)
        {
            string key = (preset ?? "1024x680").Trim().ToLowerInvariant();
            if (key == "maximized")
            {
                WindowState = WindowState.Maximized;
                return;
            }

            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }

            (double w, double h) = key switch
            {
                "900x600" => (900, 600),
                "1280x720" => (1280, 720),
                "1440x860" => (1440, 860),
                _ => (1024, 680)
            };

            Width = w;
            Height = h;
        }

        private void AplicarPresetJanelasInternas(string novoPreset)
        {
            double fatorAntigo = ObterFatorTamanhoWidget(_widgetSizePreset);
            double fatorNovo = ObterFatorTamanhoWidget(novoPreset);
            _widgetSizePreset = novoPreset;

            if (Math.Abs(fatorNovo - fatorAntigo) < 0.01) return;

            double razao = fatorNovo / fatorAntigo;
            foreach (var win in _ativasJanelas)
            {
                if (!win.IsMaximized && !win.IsMinimized)
                {
                    var bounds = win.GetNormalBounds();
                    win.SetPosicaoNormal(
                        bounds.X,
                        bounds.Y,
                        Math.Max(220, Math.Round(bounds.Width * razao)),
                        Math.Max(140, Math.Round(bounds.Height * razao)));
                }
            }
            AtualizarDimensoesCanvasWorkspace();
        }

        private void AplicarTema(string? preset = null)
        {
            string p = preset ?? _settings.ThemePreset;
            ThemePalette.AplicarEsquema(p);
            _isDarkMode = ThemePalette.EhTemaEscuro(p);
            _settings.IsDarkMode = _isDarkMode;
            _settings.ThemePreset = p;

            if (Application.Current != null)
            {
                Application.Current.RequestedThemeVariant = _isDarkMode ? ThemeVariant.Dark : ThemeVariant.Light;
            }

            var workspaceBg = _isDarkMode ? ThemePalette.DarkWorkspaceBg : ThemePalette.LightWorkspaceBg;
            var barBg = _isDarkMode ? ThemePalette.DarkBarBg : ThemePalette.LightBarBg;
            var borderBrush = _isDarkMode ? ThemePalette.DarkBorder : ThemePalette.LightBorder;
            var textPrimary = _isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;
            var btnBg = _isDarkMode ? ThemePalette.DarkButtonBg : ThemePalette.LightButtonBg;

            RootDock.Background = workspaceBg;
            WorkspaceViewport.Background = workspaceBg;
            AplicarFundoWorkspace();
            TopBar.Background = barBg;
            TopBar.BorderBrush = borderBrush;
            LblColorFilter.Foreground = _isDarkMode ? ThemePalette.DarkTextSecondary : ThemePalette.LightTextSecondary;
            AtualizarVisualFiltroCores();
            LeftSidebar.Background = barBg;
            LeftSidebar.BorderBrush = borderBrush;
            BtnToggleSidebar.Foreground = textPrimary;

            foreach (var sideBtn in new[] { BtnSideNotas, BtnSideListas, BtnSidePlanilhas, BtnSideGaleria, BtnSideNavegador, BtnSideFluxograma, BtnSideSalvarTudo, BtnSideExtrair, BtnSidePreferencias, BtnSideAjuda })
            {
                if (sideBtn != null)
                {
                    sideBtn.Foreground = textPrimary;
                }
            }
            AtualizarTemaCalendario();

            if (BtnRightCalendario != null)
            {
                BtnRightCalendario.Foreground = textPrimary;
            }
            if (RightCalendarBar != null)
            {
                RightCalendarBar.Background = barBg;
                RightCalendarBar.BorderBrush = borderBrush;
            }

            if (BtnWorkspacesMenu != null)
            {
                BtnWorkspacesMenu.Background = btnBg;
                BtnWorkspacesMenu.Foreground = textPrimary;
            }

            BottomBar.Background = barBg;
            BottomBar.BorderBrush = borderBrush;
            WorkspaceHud.Background = workspaceBg;
            WorkspaceHud.BorderBrush = borderBrush;

            foreach (var btn in new[] { BtnHudZoomOut, BtnHudZoomReset, BtnHudZoomIn })
            {
                btn.Background = btnBg;
                btn.Foreground = textPrimary;
                btn.BorderBrush = borderBrush;
            }

            AppTaskBar.AplicarTema(_isDarkMode);
            _settingsWindow?.AplicarTema(_isDarkMode);
            AplicarEstiloModal();
            AtualizarAbasWorkspaces();

            foreach (var win in _ativasJanelas)
            {
                win.AplicarTema(_isDarkMode);
            }

            foreach (var conn in _conexoesAtivas)
            {
                conn.AplicarTema(_isDarkMode);
            }
        }

        private void AplicarTema(bool isDarkMode) => AplicarTema(isDarkMode ? "dark_blue" : "light_blue");

        // ==================== GERENCIAMENTO DE WORKSPACES ====================

        private void AtualizarAbasWorkspaces()
        {
            WorkspaceTabsPanel.Children.Clear();
            var wsAtual = ObterWorkspaceAtual();

            var abertos = _settings.Workspaces.Where(w => !w.IsClosed).ToList();
            if (abertos.Count == 0)
            {
                wsAtual.IsClosed = false;
                abertos.Add(wsAtual);
            }

            foreach (var ws in abertos)
            {
                bool ativo = ws.Id == wsAtual.Id;
                var btn = new Button
                {
                    Content = AppIcons.WithText(
                        ativo ? AppIcons.Workspace : AppIcons.Sleep,
                        ws.Name,
                        11,
                        5,
                        ativo ? ThemePalette.IconLightGray : ThemePalette.IconMutedGray),
                    Height = 26,
                    Padding = new Thickness(10, 0),
                    CornerRadius = new CornerRadius(5),
                    BorderThickness = new Thickness(1),
                    FontSize = 11.5,
                    FontWeight = ativo ? FontWeight.SemiBold : FontWeight.Normal,
                    Opacity = ativo ? 1.0 : 0.80,
                    VerticalAlignment = VerticalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Background = ativo
                        ? ThemePalette.Accent
                        : (_isDarkMode ? ThemePalette.DarkButtonBg : ThemePalette.LightButtonBg),
                    Foreground = ativo
                        ? Brushes.White
                        : (_isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary),
                    BorderBrush = ativo
                        ? ThemePalette.AccentLight
                        : (_isDarkMode ? ThemePalette.DarkBorder : ThemePalette.LightBorder)
                };
                ToolTip.SetTip(btn, ativo
                    ? $"Workspace ativo: {ws.Name} (Clique c/ botão direito para mudar nome, fechar ou excluir)"
                    : $"{ws.Name} (Hibernado. Clique para ativar ou botão direito para opções)");

                btn.ContextMenu = CriarMenuContextoWorkspace(ws);

                string targetId = ws.Id;
                btn.Click += (s, e) => AlternarParaWorkspace(targetId);
                WorkspaceTabsPanel.Children.Add(btn);
            }

            AtualizarMenuContextoCanvas();
            AtualizarMenuSuperiorWorkspaces();
        }

        private void AlternarParaWorkspace(string workspaceId)
        {
            var atual = ObterWorkspaceAtual();
            if (atual.Id == workspaceId) return;

            var destino = _settings.Workspaces.FirstOrDefault(w => w.Id == workspaceId);
            if (destino == null) return;

            string nomeAnterior = atual.Name;

            CancelarModoVinculoLinha();
            CancelarModoConexao();
            SincronizarWorkspaceAtualEmMemoria();
            LimparJanelasVisuaisDoCanvas();
            ForcarLimpezaMemoriaHibernacao();

            _settings.ActiveWorkspaceId = destino.Id;
            _workspaceBgImagePath = destino.BackgroundImagePath ?? string.Empty;
            _workspaceZoom = destino.WorkspaceZoom > 0 ? destino.WorkspaceZoom : 1.0;

            var fallbackBg = _isDarkMode ? ThemePalette.DarkWorkspaceBg : ThemePalette.LightWorkspaceBg;
            AplicarFundoWorkspace(fallbackBg);

            CarregarJanelasDoWorkspace(destino);
            AplicarZoomWorkspace(_workspaceZoom);
            AtualizarAbasWorkspaces();

            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(
                    Workspace,
                    $"Ativado \"{destino.Name}\" • \"{nomeAnterior}\" entrou em hibernação profunda",
                    "info");
            }
        }

        private void LimparJanelasVisuaisDoCanvas()
        {
            FecharMenuCircular();
            _trocandoWorkspace = true;
            ZIndexManager.ClearActive();

            foreach (var conn in _conexoesAtivas.ToList())
            {
                conn.RemoverDoCanvas();
            }
            _conexoesAtivas.Clear();

            var copia = _ativasJanelas.ToList();
            _ativasJanelas.Clear();
            foreach (var win in copia)
            {
                win.Fechar();
            }

            _settingsWindow = null;
            _workspaceBgBitmap?.Dispose();
            _workspaceBgBitmap = null;
            LimparSelecaoJanelas();
            _dragGroupStartPositions.Clear();
            Workspace.Children.Clear();
            GarantirCaixaSelecaoNoCanvas();

            _trocandoWorkspace = false;
        }

        private static void ForcarLimpezaMemoriaHibernacao()
        {
            try
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(2, GCCollectionMode.Forced, blocking: false, compacting: true);
                GC.WaitForPendingFinalizers();
            }
            catch { }
        }

        private void CarregarJanelasDoWorkspace(WorkspaceData ws)
        {
            _contadorJanelas = 0;
            _contadorNotas = 0;
            _contadorListas = 0;
            _contadorNavegadores = 0;
            _contadorGalerias = 0;
            _contadorImagens = 0;
            _contadorVideos = 0;
            _contadorPlanilhas = 0;
            _contadorFormas = 0;

            if (ws.SavedWindows != null && ws.SavedWindows.Count > 0)
            {
                foreach (var winData in ws.SavedWindows)
                {
                    if (winData.WindowType == "notepad") _contadorNotas++;
                    else if (winData.WindowType == "checklist") _contadorListas++;
                    else if (winData.WindowType == "browser") _contadorNavegadores++;
                    else if (winData.WindowType == "gallery") _contadorGalerias++;
                    else if (winData.WindowType == "image") _contadorImagens++;
                    else if (winData.WindowType == "video") _contadorVideos++;
                    else if (winData.WindowType == "spreadsheet") _contadorPlanilhas++;
                    else if (DraggableWindow.EhTipoFormaFluxograma(winData.WindowType)) _contadorFormas++;
                    else _contadorJanelas++;

                    var createdWin = CriarJanelaInterna(
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
                        winData.TextFormat,
                        winData.GalleryItems,
                        winData.CustomColor,
                        winData.SpreadsheetRows,
                        winData.SpreadsheetCols,
                        winData.SpreadsheetCells,
                        winData.ConditionalRules,
                        winData.CriticalityLevel,
                        winData.RichLines);

                    if (winData.IsMaximized)
                    {
                        createdWin.AplicarEstadoMaximizadoInicial();
                        if (winData.IsMinimized)
                        {
                            createdWin.AplicarEstadoMinimizadoInicial();
                        }
                    }
                    else if (winData.IsMinimized)
                    {
                        createdWin.AplicarEstadoMinimizadoInicial();
                    }
                }
            }

            if (ws.Connections != null && ws.Connections.Count > 0)
            {
                foreach (var connData in ws.Connections)
                {
                    var fromWin = _ativasJanelas.FirstOrDefault(w => w.WindowId == connData.FromWindowId);
                    var toWin = _ativasJanelas.FirstOrDefault(w => w.WindowId == connData.ToWindowId);
                    if (fromWin != null && toWin != null)
                    {
                        CriarOuFocarConexaoFluxograma(fromWin, toWin, connData.Id, connData.Label, mostrarToast: false, connData.ColorHex);
                    }
                }
            }

            var ultimaAberta = _ativasJanelas.LastOrDefault(w => !w.IsMinimized) ?? _ativasJanelas.LastOrDefault();
            if (ultimaAberta != null)
            {
                ZIndexManager.BringToFront(ultimaAberta);
            }

            AtualizarDimensoesCanvasWorkspace();
        }

        private void SincronizarWorkspaceAtualEmMemoria()
        {
            var ws = ObterWorkspaceAtual();
            ws.LastModifiedUtc = DateTime.UtcNow;
            ws.BackgroundImagePath = _workspaceBgImagePath ?? string.Empty;
            ws.WorkspaceZoom = _workspaceZoom;
            ws.SavedWindows.Clear();

            foreach (var win in _ativasJanelas)
            {
                var state = win.ExportState();
                ws.SavedWindows.Add(state);

                int idx = ws.SavedCatalog.FindIndex(x =>
                    x.Id == state.Id ||
                    (x.WindowType == state.WindowType && string.Equals(x.Title, state.Title, StringComparison.OrdinalIgnoreCase)));

                if (idx >= 0)
                {
                    state.AccessCount = Math.Max(ws.SavedCatalog[idx].AccessCount, state.AccessCount);
                    ws.SavedCatalog[idx] = state;
                }
            }

            ws.Connections.Clear();
            foreach (var conn in _conexoesAtivas)
            {
                ws.Connections.Add(new WindowConnectionData
                {
                    Id = conn.ConnectionId,
                    FromWindowId = conn.FromWindow.WindowId,
                    ToWindowId = conn.ToWindow.WindowId,
                    Label = conn.Label,
                    ColorHex = conn.ColorHex
                });
            }

            _settings.WorkspaceBackgroundImagePath = ws.BackgroundImagePath;
            _settings.SavedWindows = new(ws.SavedWindows);
            _settings.SavedCatalog = new(ws.SavedCatalog);
        }

        private void SalvarTodosWorkspacesNoDisco()
        {
            SincronizarWorkspaceAtualEmMemoria();
            SettingsManager.Salvar(_settings);
        }

        private void NovoWorkspace_Click(object? sender, RoutedEventArgs e)
        {
            string nomeSugerido = $"Workspace {_settings.Workspaces.Count + 1}";
            SincronizarWorkspaceAtualEmMemoria();

            var novoWs = new WorkspaceData
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = nomeSugerido,
                LastModifiedUtc = DateTime.UtcNow
            };
            _settings.Workspaces.Add(novoWs);
            AlternarParaWorkspace(novoWs.Id);
            SalvarTodosWorkspacesNoDisco();

            ToastNotification.Mostrar(Workspace, $"✓ {novoWs.Name} criado com sucesso!", "success");
        }

        private ContextMenu CriarMenuContextoWorkspace(WorkspaceData ws)
        {
            var menu = new ContextMenu();
            bool ativo = ws.Id == _settings.ActiveWorkspaceId;

            var itemRenomear = new MenuItem
            {
                Header = "Mudar Nome...",
                Icon = new PathIcon
                {
                    Data = AppIcons.Edit,
                    Width = 13,
                    Height = 13,
                    Foreground = ThemePalette.IconLightGray
                }
            };
            itemRenomear.Click += (s, e) => SolicitarRenomearWorkspace(ws);
            menu.Items.Add(itemRenomear);

            var itemFechar = new MenuItem
            {
                Header = "Fechar Workspace (Ocultar da Barra)",
                Icon = new PathIcon
                {
                    Data = AppIcons.Exit,
                    Width = 13,
                    Height = 13,
                    Foreground = ThemePalette.IconMutedGray
                }
            };
            itemFechar.Click += (s, e) => SolicitarFecharWorkspace(ws);
            menu.Items.Add(itemFechar);

            var itemExcluir = new MenuItem
            {
                Header = "Excluir Workspace Permanentemente...",
                Icon = new PathIcon
                {
                    Data = AppIcons.Trash,
                    Width = 13,
                    Height = 13,
                    Foreground = ThemePalette.Danger
                }
            };

            if (_settings.Workspaces.Count <= 1)
            {
                itemExcluir.IsEnabled = false;
                ToolTip.SetTip(itemExcluir, "É necessário manter pelo menos um Workspace ativo.");
            }
            else
            {
                itemExcluir.Click += (s, e) => SolicitarExcluirWorkspace(ws);
            }
            menu.Items.Add(itemExcluir);

            menu.Items.Add(new Separator());

            var itemNovo = new MenuItem
            {
                Header = "Novo Workspace",
                Icon = new PathIcon
                {
                    Data = AppIcons.Plus,
                    Width = 13,
                    Height = 13,
                    Foreground = ThemePalette.IconLightGray
                }
            };
            itemNovo.Click += (s, e) => NovoWorkspace_Click(null, null!);
            menu.Items.Add(itemNovo);

            return menu;
        }

        private void AtualizarMenuContextoCanvas()
        {
            if (Workspace == null) return;
            Workspace.ContextMenu = CriarMenuContextoCanvas();
        }

        private ContextMenu CriarMenuContextoCanvas()
        {
            var menu = new ContextMenu();
            var wsAtual = ObterWorkspaceAtual();

            var itemRenomear = new MenuItem
            {
                Header = $"Mudar Nome de \"{wsAtual.Name}\"...",
                Icon = new PathIcon
                {
                    Data = AppIcons.Edit,
                    Width = 13,
                    Height = 13,
                    Foreground = ThemePalette.IconLightGray
                }
            };
            itemRenomear.Click += (s, e) => SolicitarRenomearWorkspace(ObterWorkspaceAtual());
            menu.Items.Add(itemRenomear);

            var itemFechar = new MenuItem
            {
                Header = $"Fechar \"{wsAtual.Name}\" (Ocultar da Barra)",
                Icon = new PathIcon
                {
                    Data = AppIcons.Exit,
                    Width = 13,
                    Height = 13,
                    Foreground = ThemePalette.IconMutedGray
                }
            };
            itemFechar.Click += (s, e) => SolicitarFecharWorkspace(ObterWorkspaceAtual());
            menu.Items.Add(itemFechar);

            var itemExcluir = new MenuItem
            {
                Header = $"Excluir \"{wsAtual.Name}\" Permanentemente...",
                Icon = new PathIcon
                {
                    Data = AppIcons.Trash,
                    Width = 13,
                    Height = 13,
                    Foreground = ThemePalette.Danger
                }
            };

            if (_settings.Workspaces.Count <= 1)
            {
                itemExcluir.IsEnabled = false;
                ToolTip.SetTip(itemExcluir, "É necessário manter pelo menos um Workspace ativo.");
            }
            else
            {
                itemExcluir.Click += (s, e) => SolicitarExcluirWorkspace(ObterWorkspaceAtual());
            }
            menu.Items.Add(itemExcluir);

            menu.Items.Add(new Separator());

            var itemNovo = new MenuItem
            {
                Header = "Novo Workspace",
                Icon = new PathIcon
                {
                    Data = AppIcons.Plus,
                    Width = 13,
                    Height = 13,
                    Foreground = ThemePalette.IconLightGray
                }
            };
            itemNovo.Click += (s, e) => NovoWorkspace_Click(null, null!);
            menu.Items.Add(itemNovo);

            if (!string.IsNullOrWhiteSpace(_workspaceBgImagePath))
            {
                menu.Items.Add(new Separator());
                var itemResetFundo = new MenuItem
                {
                    Header = "Restaurar Fundo Padrão do Workspace",
                    Icon = new PathIcon
                    {
                        Data = AppIcons.Refresh,
                        Width = 13,
                        Height = 13,
                        Foreground = ThemePalette.IconMutedGray
                    }
                };
                itemResetFundo.Click += (s, e) => RestaurarFundoWorkspace_Click(null, null!);
                menu.Items.Add(itemResetFundo);
            }

            return menu;
        }

        private void AtualizarMenuSuperiorWorkspaces()
        {
            var menuAbrir = this.FindControl<MenuItem>("MenuAbrirWorkspace");
            var menuExcluir = this.FindControl<MenuItem>("MenuExcluirWorkspace");
            if (menuAbrir == null || menuExcluir == null) return;

            // 1. Menu Abrir Workspace...
            menuAbrir.Items.Clear();

            var fechados = _settings.Workspaces.Where(w => w.IsClosed).ToList();
            var abertos = _settings.Workspaces.Where(w => !w.IsClosed).ToList();

            if (fechados.Count > 0)
            {
                var lblFechados = new MenuItem
                {
                    Header = "Workspaces Fechados (Salvos):",
                    IsEnabled = false,
                    FontWeight = FontWeight.SemiBold
                };
                menuAbrir.Items.Add(lblFechados);

                foreach (var ws in fechados)
                {
                    var item = new MenuItem
                    {
                        Header = ws.Name,
                        Icon = new PathIcon
                        {
                            Data = AppIcons.Folder,
                            Width = 13,
                            Height = 13,
                            Foreground = ThemePalette.AccentLight
                        }
                    };
                    string idAlvo = ws.Id;
                    item.Click += (s, e) => ReabrirWorkspace(idAlvo);
                    menuAbrir.Items.Add(item);
                }

                if (abertos.Count > 0)
                {
                    menuAbrir.Items.Add(new Separator());
                }
            }

            if (abertos.Count > 0)
            {
                var lblAbertos = new MenuItem
                {
                    Header = "Workspaces Abertos na Barra:",
                    IsEnabled = false,
                    FontWeight = FontWeight.SemiBold
                };
                menuAbrir.Items.Add(lblAbertos);

                foreach (var ws in abertos)
                {
                    bool ativo = ws.Id == _settings.ActiveWorkspaceId;
                    var item = new MenuItem
                    {
                        Header = ativo ? $"{ws.Name}  (Ativo no momento)" : ws.Name,
                        Icon = new PathIcon
                        {
                            Data = AppIcons.Workspace,
                            Width = 13,
                            Height = 13,
                            Foreground = ativo ? ThemePalette.Accent : ThemePalette.IconMutedGray
                        }
                    };
                    string idAlvo = ws.Id;
                    item.Click += (s, e) => AlternarParaWorkspace(idAlvo);
                    menuAbrir.Items.Add(item);
                }
            }

            // 2. Menu Excluir Workspace Permanentemente...
            menuExcluir.Items.Clear();
            if (_settings.Workspaces.Count <= 1)
            {
                var itemUnico = new MenuItem
                {
                    Header = "Mínimo de 1 Workspace necessário (não é possível excluir)",
                    IsEnabled = false
                };
                menuExcluir.Items.Add(itemUnico);
            }
            else
            {
                foreach (var ws in _settings.Workspaces.ToList())
                {
                    var wsRef = ws;
                    var item = new MenuItem
                    {
                        Header = $"Excluir \"{ws.Name}\"",
                        Icon = new PathIcon
                        {
                            Data = AppIcons.Trash,
                            Width = 13,
                            Height = 13,
                            Foreground = ThemePalette.Danger
                        }
                    };
                    item.Click += (s, e) => SolicitarExcluirWorkspace(wsRef);
                    menuExcluir.Items.Add(item);
                }
            }
        }

        private void ReabrirWorkspace(string workspaceId)
        {
            var ws = _settings.Workspaces.FirstOrDefault(w => w.Id == workspaceId);
            if (ws == null) return;

            ws.IsClosed = false;
            SalvarTodosWorkspacesNoDisco();
            AlternarParaWorkspace(ws.Id);
            AtualizarAbasWorkspaces();

            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, $"Workspace \"{ws.Name}\" reaberto na barra!", "success");
            }
        }

        private void SolicitarRenomearWorkspace(WorkspaceData ws)
        {
            ModalTitle.Text = "Mudar Nome do Workspace";
            ModalMessage.Text = $"Digite o novo nome para \"{ws.Name}\":";
            ModalInputBox.Text = ws.Name;
            ModalInputBox.IsVisible = true;
            if (ModalCheckBox != null) ModalCheckBox.IsVisible = false;
            ModalButtonsPanel.Children.Clear();

            var btnCancelar = CriarBotaoModal("Cancelar", isPrimary: false, isDanger: false);
            btnCancelar.Click += (s, ev) => ModalOverlay.IsVisible = false;

            var btnSalvar = CriarBotaoModal("Salvar Nome", isPrimary: true, isDanger: false);
            btnSalvar.Click += (s, ev) =>
            {
                string novoNome = (ModalInputBox.Text ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(novoNome))
                {
                    ws.Name = novoNome;
                    AtualizarAbasWorkspaces();
                    SalvarTodosWorkspacesNoDisco();
                    if (_settings.EnableNotifications)
                    {
                        ToastNotification.Mostrar(Workspace, $"Workspace renomeado para \"{novoNome}\".", "success");
                    }
                }
                ModalOverlay.IsVisible = false;
            };

            ModalButtonsPanel.Children.Add(btnCancelar);
            ModalButtonsPanel.Children.Add(btnSalvar);
            AplicarEstiloModal();
            ModalOverlay.IsVisible = true;
            ModalInputBox.Focus();
            ModalInputBox.SelectAll();
        }

        private void SolicitarFecharWorkspace(WorkspaceData ws)
        {
            var abertos = _settings.Workspaces.Where(w => !w.IsClosed).ToList();
            if (abertos.Count <= 1 && abertos.Contains(ws))
            {
                ToastNotification.Mostrar(Workspace, "É necessário manter pelo menos um Workspace aberto na barra.", "warning");
                return;
            }

            bool eraAtivo = ws.Id == _settings.ActiveWorkspaceId;
            ws.IsClosed = true;

            if (eraAtivo)
            {
                CancelarModoVinculoLinha();
                CancelarModoConexao();
                SincronizarWorkspaceAtualEmMemoria();
                LimparJanelasVisuaisDoCanvas();
                ForcarLimpezaMemoriaHibernacao();

                var proximo = _settings.Workspaces.FirstOrDefault(w => !w.IsClosed) ?? _settings.Workspaces[0];
                proximo.IsClosed = false;
                _settings.ActiveWorkspaceId = proximo.Id;
                _workspaceBgImagePath = proximo.BackgroundImagePath ?? string.Empty;
                _workspaceZoom = proximo.WorkspaceZoom > 0 ? proximo.WorkspaceZoom : 1.0;

                AplicarFundoWorkspace(_isDarkMode ? ThemePalette.DarkWorkspaceBg : ThemePalette.LightWorkspaceBg);
                CarregarJanelasDoWorkspace(proximo);
                AplicarZoomWorkspace(_workspaceZoom);
            }

            AtualizarAbasWorkspaces();
            SalvarTodosWorkspacesNoDisco();

            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, $"Workspace \"{ws.Name}\" fechado da barra. Reabra-o quando quiser no menu Workspaces.", "info");
            }
        }

        private void SolicitarExcluirWorkspace(WorkspaceData ws)
        {
            if (_settings.Workspaces.Count <= 1)
            {
                ToastNotification.Mostrar(Workspace, "É necessário manter pelo menos um Workspace ativo.", "warning");
                return;
            }

            ModalTitle.Text = "Excluir Workspace Permanentemente";
            ModalMessage.Text = $"Tem certeza de que deseja EXCLUIR PERMANENTEMENTE o workspace \"{ws.Name}\"?\n\nTodas as janelas locais serão apagadas e o backup na nuvem (Google Drive) também será removido.";
            ModalInputBox.IsVisible = false;
            if (ModalCheckBox != null) ModalCheckBox.IsVisible = false;
            ModalButtonsPanel.Children.Clear();

            var btnCancelar = CriarBotaoModal("Cancelar", isPrimary: false, isDanger: false);
            btnCancelar.Click += (s, ev) => ModalOverlay.IsVisible = false;

            var btnExcluir = CriarBotaoModal("Excluir Permanentemente", isPrimary: false, isDanger: true);
            btnExcluir.Click += (s, ev) =>
            {
                ModalOverlay.IsVisible = false;
                string nomeRemovido = ws.Name;
                string idRemovido = ws.Id;

                bool eraAtivo = ws.Id == _settings.ActiveWorkspaceId;
                if (eraAtivo)
                {
                    CancelarModoVinculoLinha();
                    CancelarModoConexao();
                    LimparJanelasVisuaisDoCanvas();
                    ForcarLimpezaMemoriaHibernacao();
                }

                _settings.Workspaces.Remove(ws);

                if (eraAtivo)
                {
                    var proximo = _settings.Workspaces.FirstOrDefault(w => !w.IsClosed) ?? _settings.Workspaces[0];
                    proximo.IsClosed = false;
                    _settings.ActiveWorkspaceId = proximo.Id;
                    _workspaceBgImagePath = proximo.BackgroundImagePath ?? string.Empty;
                    _workspaceZoom = proximo.WorkspaceZoom > 0 ? proximo.WorkspaceZoom : 1.0;

                    AplicarFundoWorkspace(_isDarkMode ? ThemePalette.DarkWorkspaceBg : ThemePalette.LightWorkspaceBg);
                    CarregarJanelasDoWorkspace(proximo);
                    AplicarZoomWorkspace(_workspaceZoom);
                }

                AtualizarAbasWorkspaces();
                SalvarTodosWorkspacesNoDisco();

                // Exclui do Google Drive em segundo plano caso a sincronização na nuvem esteja configurada
                if (!string.IsNullOrWhiteSpace(_settings.GoogleOAuthRefreshToken))
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await GoogleDriveCloudClient.ExcluirWorkspaceDaNuvemAsync(_settings, idRemovido);
                        }
                        catch { }
                    });
                }

                if (_settings.EnableNotifications)
                {
                    ToastNotification.Mostrar(Workspace, $"Workspace \"{nomeRemovido}\" excluído do computador e do Google Drive.", "info");
                }
            };

            ModalButtonsPanel.Children.Add(btnCancelar);
            ModalButtonsPanel.Children.Add(btnExcluir);
            AplicarEstiloModal();
            ModalOverlay.IsVisible = true;
        }

        private void RenomearWorkspaceAtual_Click(object? sender, RoutedEventArgs e)
        {
            SolicitarRenomearWorkspace(ObterWorkspaceAtual());
        }

        private void ExcluirWorkspaceAtual_Click(object? sender, RoutedEventArgs e)
        {
            SolicitarExcluirWorkspace(ObterWorkspaceAtual());
        }

        private void MenuModulo_Opening(object? sender, EventArgs e) => MenuModulo_SubmenuOpened(sender, null!);

        // ==================== CATÁLOGO E MENUS DE MÓDULOS ====================

        private void SalvarJanelaNoCatalogo(DraggableWindow win, bool mostrarToast = true)
        {
            var ws = ObterWorkspaceAtual();

            void UpsertNoCatalogo(WindowStateData state)
            {
                int idx = ws.SavedCatalog.FindIndex(x =>
                    x.Id == state.Id ||
                    (x.WindowType == state.WindowType && string.Equals(x.Title, state.Title, StringComparison.OrdinalIgnoreCase)));

                if (idx >= 0)
                {
                    state.AccessCount = Math.Max(ws.SavedCatalog[idx].AccessCount, state.AccessCount);
                    state.LastUsedUtc = DateTime.UtcNow;
                    ws.SavedCatalog[idx] = state;
                }
                else
                {
                    state.LastUsedUtc = DateTime.UtcNow;
                    ws.SavedCatalog.Add(state);
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

            SalvarTodosWorkspacesNoDisco();

            if (mostrarToast && _settings.EnableNotifications)
            {
                string msg = win.ParentChecklistWindow != null
                    ? $"Nota \"{win.WindowTitle}\" e lista \"{win.ParentChecklistWindow.WindowTitle}\" salvas em {ws.Name}!"
                    : $"\"{win.WindowTitle}\" salvo no catálogo de {ws.Name}!";
                ToastNotification.Mostrar(Workspace, msg, "success");
            }
        }

        private void SalvarTodasJanelas_Click(object? sender, RoutedEventArgs e)
        {
            if (_ativasJanelas.Count == 0)
            {
                SalvarTodosWorkspacesNoDisco();
                TentarSincronizarGoogleDriveAutomatico();
                TentarSincronizarGoogleDriveNuvemAutomatico();
                ToastNotification.Mostrar(Workspace, "Estado do Workspace salvo!", "success");
                return;
            }

            foreach (var win in _ativasJanelas)
            {
                SalvarJanelaNoCatalogo(win, mostrarToast: false);
            }

            bool driveOk = TentarSincronizarGoogleDriveAutomatico();
            TentarSincronizarGoogleDriveNuvemAutomatico();

            if (_settings.EnableNotifications)
            {
                string msgBase = $"Todas as {_ativasJanelas.Count} janelas foram salvas em {ObterWorkspaceAtual().Name}!";
                if (_settings.GoogleDriveSyncEnabled)
                {
                    msgBase += driveOk
                        ? " • Backup sincronizado no Google Drive!"
                        : " • (Aviso: falha na sincronização do Google Drive)";
                }
                ToastNotification.Mostrar(Workspace, msgBase, driveOk ? "success" : "info");
            }
        }

        private void TentarSincronizarGoogleDriveNuvemAutomatico()
        {
            if (string.IsNullOrWhiteSpace(_settings.GoogleOAuthRefreshToken))
                return;

            _settings.GoogleDriveCloudSyncEnabled = true;

            _ = Task.Run(async () =>
            {
                try
                {
                    var (sucessos, falhas, ultimoErro) = await GoogleDriveCloudClient.UploadTodosWorkspacesAsync(_settings);
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (sucessos > 0)
                        {
                            _settings.LastCloudSyncUtc = DateTime.UtcNow.ToString("o");
                            SalvarTodosWorkspacesNoDisco();
                            if (_settings.EnableNotifications)
                            {
                                ToastNotification.Mostrar(Workspace, $"☁ {sucessos} workspace(s) sincronizado(s) no Google Drive!", "success");
                            }
                        }
                        else if (falhas > 0 && _settings.EnableNotifications)
                        {
                            ToastNotification.Mostrar(Workspace, $"Aviso Google Drive: {ultimoErro}", "danger");
                        }
                    });
                }
                catch { }
            });
        }

        private async Task VerificarEAtualizarNuvemNaInicializacaoAsync()
        {
            if (string.IsNullOrWhiteSpace(_settings.GoogleOAuthRefreshToken))
                return;

            try
            {
                var (cloudFiles, _) = await GoogleDriveCloudClient.ListarWorkspacesNaNuvemAsync(_settings);
                if (cloudFiles.Count == 0) return;

                bool temNovosOuAtualizados = cloudFiles.Any(cf =>
                    !_settings.Workspaces.Any(w => w.Id == cf.WorkspaceId) ||
                    _settings.Workspaces.Any(w => w.Id == cf.WorkspaceId && cf.ModifiedTimeUtc > w.LastModifiedUtc.AddSeconds(5))
                );

                if (temNovosOuAtualizados)
                {
                    if (_settings.AutoDownloadCloudWorkspaces)
                    {
                        var (baixados, _) = await GoogleDriveCloudClient.BaixarTodosWorkspacesDaNuvemAsync(_settings);
                        if (baixados.Count > 0)
                        {
                            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                MesclarWorkspacesDaNuvem(baixados);
                                ToastNotification.Mostrar(Workspace, $"☁ {baixados.Count} workspace(s) atualizados da nuvem!", "success");
                            });
                        }
                    }
                    else if (!_settings.RememberCloudDownloadChoice)
                    {
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            MostrarModalDownloadWorkspacesNuvem(cloudFiles);
                        });
                    }
                }
            }
            catch { }
        }

        private void MostrarModalDownloadWorkspacesNuvem(List<CloudFileInfo> cloudFiles)
        {
            if (cloudFiles.Count == 0) return;

            var nomes = string.Join(", ", cloudFiles.Select(c => $"\"{c.Name}\"").Take(3));
            if (cloudFiles.Count > 3) nomes += $", e mais {cloudFiles.Count - 3}";

            ModalTitle.Text = "☁ Workspaces Encontrados no Google Drive";
            ModalMessage.Text = $"Detectamos {cloudFiles.Count} workspace(s) salvo(s) na sua conta do Google Drive ({nomes}).\n\nDeseja fazer o download dos workspaces da nuvem agora neste computador?";
            ModalInputBox.IsVisible = false;
            ModalCheckBox.IsVisible = true;
            ModalCheckBox.Content = "Salvar resposta (baixar sempre automaticamente)";
            ModalCheckBox.IsChecked = _settings.AutoDownloadCloudWorkspaces;
            ModalButtonsPanel.Children.Clear();

            var btnManter = CriarBotaoModal("Manter Locais", isPrimary: false, isDanger: false);
            btnManter.Click += (_, _) =>
            {
                if (ModalCheckBox.IsChecked == true)
                {
                    _settings.AutoDownloadCloudWorkspaces = false;
                    _settings.RememberCloudDownloadChoice = true;
                    SalvarTodosWorkspacesNoDisco();
                }
                ModalOverlay.IsVisible = false;
                ModalCheckBox.IsVisible = false;
            };

            var btnBaixar = CriarBotaoModal("Baixar Workspaces da Nuvem", isPrimary: true, isDanger: false, AppIcons.Download);
            btnBaixar.Click += async (_, _) =>
            {
                bool salvarEscolha = ModalCheckBox.IsChecked == true;
                if (salvarEscolha)
                {
                    _settings.AutoDownloadCloudWorkspaces = true;
                    _settings.RememberCloudDownloadChoice = true;
                }

                ModalTitle.Text = "Baixando da Nuvem...";
                ModalMessage.Text = "Transferindo e configurando seus workspaces do Google Drive...";
                ModalButtonsPanel.Children.Clear();
                ModalCheckBox.IsVisible = false;

                try
                {
                    var (baixados, dlErr) = await GoogleDriveCloudClient.BaixarTodosWorkspacesDaNuvemAsync(_settings);
                    if (baixados.Count > 0)
                    {
                        MesclarWorkspacesDaNuvem(baixados);
                        ToastNotification.Mostrar(Workspace, $"☁ {baixados.Count} workspace(s) carregados do Google Drive!", "success");
                    }
                    else if (!string.IsNullOrEmpty(dlErr))
                    {
                        ToastNotification.Mostrar(Workspace, dlErr, "warning");
                    }
                }
                catch (Exception ex)
                {
                    ToastNotification.Mostrar(Workspace, $"Erro ao baixar workspaces: {ex.Message}", "danger");
                }

                ModalOverlay.IsVisible = false;
            };

            ModalButtonsPanel.Children.Add(btnManter);
            ModalButtonsPanel.Children.Add(btnBaixar);

            AplicarEstiloModal();
            ModalOverlay.IsVisible = true;
        }

        private void MesclarWorkspacesDaNuvem(List<WorkspaceData> baixados)
        {
            if (baixados.Count == 0) return;

            foreach (var cloudWs in baixados)
            {
                int idx = _settings.Workspaces.FindIndex(w => w.Id == cloudWs.Id);
                if (idx >= 0)
                {
                    _settings.Workspaces[idx] = cloudWs;
                }
                else
                {
                    _settings.Workspaces.Add(cloudWs);
                }
            }

            SalvarTodosWorkspacesNoDisco();

            var targetWs = _settings.Workspaces.FirstOrDefault(w => w.Id == _settings.ActiveWorkspaceId) ?? _settings.Workspaces.First();
            AlternarParaWorkspace(targetWs.Id);
        }

        private bool TentarSincronizarGoogleDriveAutomatico()
        {
            if (!_settings.GoogleDriveSyncEnabled) return false;
            try
            {
                var ws = ObterWorkspaceAtual();
                if (GoogleDriveSyncService.SincronizarWorkspaceNoGoogleDrive(ws, _settings.GoogleDriveFolderPath, out _, out _))
                {
                    _settings.LastGoogleDriveSyncDate = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                    SalvarTodosWorkspacesNoDisco();
                    return true;
                }
            }
            catch { }
            return false;
        }

        private void SincronizarGoogleDriveManual_Click(object? sender, RoutedEventArgs e)
        {
            var ws = ObterWorkspaceAtual();
            string pasta = string.IsNullOrWhiteSpace(_settings.GoogleDriveFolderPath)
                ? GoogleDriveSyncService.DetectarPastaPadraoGoogleDrive()
                : _settings.GoogleDriveFolderPath;

            if (GoogleDriveSyncService.SincronizarWorkspaceNoGoogleDrive(ws, pasta, out string salvoEm, out string erro))
            {
                _settings.LastGoogleDriveSyncDate = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                SalvarTodosWorkspacesNoDisco();
                ToastNotification.Mostrar(Workspace, $"Backup do Workspace sincronizado no Google Drive com sucesso!\nArquivo: {System.IO.Path.GetFileName(salvoEm)}", "success");
            }
            else
            {
                ToastNotification.Mostrar(Workspace, $"Falha ao sincronizar no Google Drive: {erro}", "danger");
            }
        }

        private void AbrirPastaGoogleDrive_Click(object? sender, RoutedEventArgs e)
        {
            string pasta = string.IsNullOrWhiteSpace(_settings.GoogleDriveFolderPath)
                ? GoogleDriveSyncService.DetectarPastaPadraoGoogleDrive()
                : _settings.GoogleDriveFolderPath;
            GoogleDriveSyncService.AbrirPastaNoExplorador(pasta);
        }

        private NotificationHistoryModal? _notificationHistoryModal;

        private void AbrirHistoricoNotificacoes_Click(object? sender, RoutedEventArgs e)
        {
            AbrirHistoricoNotificacoes();
        }

        private void AbrirHistoricoNotificacoes()
        {
            if (_notificationHistoryModal != null)
            {
                MainRootGrid.Children.Remove(_notificationHistoryModal);
                _notificationHistoryModal = null;
            }

            _notificationHistoryModal = new NotificationHistoryModal(_isDarkMode);
            _notificationHistoryModal.OnClosed += () =>
            {
                if (_notificationHistoryModal != null)
                {
                    MainRootGrid.Children.Remove(_notificationHistoryModal);
                    _notificationHistoryModal = null;
                }
                AtualizarSuspensaoNavegadores();
            };

            MainRootGrid.Children.Add(_notificationHistoryModal);
            AtualizarSuspensaoNavegadores();
        }

        private void AbrirAjudaTutorial_Click(object? sender, RoutedEventArgs e)
        {
            AbrirAjudaTutorial();
        }

        private void AbrirAjudaTutorial()
        {
            if (_tutorialModal != null)
            {
                MainRootGrid.Children.Remove(_tutorialModal);
                _tutorialModal = null;
            }

            _tutorialModal = new InteractiveTutorialModal(_isDarkMode, this);
            _tutorialModal.OnClosed += () =>
            {
                if (_tutorialModal != null)
                {
                    MainRootGrid.Children.Remove(_tutorialModal);
                    _tutorialModal = null;
                }
                AtualizarSuspensaoNavegadores();
            };

            MainRootGrid.Children.Add(_tutorialModal);
            AtualizarSuspensaoNavegadores();
        }

        public Control ObterBotaoNotas() => BtnSideNotas;
        public Control ObterBotaoListas() => BtnSideListas;
        public Control ObterBotaoFluxograma() => BtnSideFluxograma;

        public DraggableWindow GarantirJanelaParaDemonstracao(string tipo = "notepad")
        {
            var existente = _ativasJanelas.FirstOrDefault(w => w.WindowType == tipo) ?? _ativasJanelas.FirstOrDefault();
            if (existente != null)
            {
                ZIndexManager.BringToFront(existente);
                return existente;
            }

            double f = ObterFatorTamanhoWidget();
            if (tipo == "checklist")
            {
                var itens = new List<ChecklistItemData>
                {
                    new ChecklistItemData { Text = "Clique no ícone 🔗 ao lado para vincular a uma janela", ColorHex = "#38BDF8" }
                };
                return CriarJanelaInterna("Lista de Demonstração", 140, 90, Math.Round(380 * f), Math.Round(290 * f), "checklist", initialChecklist: itens);
            }
            else if (tipo == "shape_rect")
            {
                return CriarJanelaInterna("Etapa 1 (Início)", 140, 90, Math.Round(200 * f), Math.Round(120 * f), "shape_rect");
            }
            else
            {
                return CriarJanelaInterna("Bloco de Demonstração", 140, 90, Math.Round(360 * f), Math.Round(260 * f), "notepad", "Exemplo de anotação para demonstração!");
            }
        }

        public (DraggableWindow win1, DraggableWindow win2) GarantirDuasJanelasConectadasParaDemonstracao()
        {
            double f = ObterFatorTamanhoWidget();
            var win1 = _ativasJanelas.Count > 0 ? _ativasJanelas[0] : CriarJanelaInterna("Etapa A", 120, 90, Math.Round(200 * f), Math.Round(120 * f), "shape_rect");
            var win2 = _ativasJanelas.Count > 1 ? _ativasJanelas[1] : CriarJanelaInterna("Etapa B", 450, 90, Math.Round(200 * f), Math.Round(120 * f), "shape_diamond");

            CriarOuFocarConexaoFluxograma(win1, win2, mostrarToast: false, colorHex: "#38BDF8");
            return (win1, win2);
        }

        private void MenuModulo_SubmenuOpened(object? sender, RoutedEventArgs e)
        {
        }

        private void PreencherSubmenuSalvos(
            WorkspaceData ws,
            string[]? filtroTipos,
            MenuItem? menuSalvas)
        {
            if (menuSalvas == null) return;
            IEnumerable<WindowStateData> query = ws.SavedCatalog;
            if (filtroTipos != null && filtroTipos.Length > 0)
            {
                query = query.Where(x => filtroTipos.Contains(x.WindowType));
            }

            var salvas = query
                .OrderByDescending(x => x.LastUsedUtc)
                .ToList();

            menuSalvas.Items.Clear();

            if (salvas.Count == 0)
            {
                menuSalvas.Items.Add(new MenuItem { Header = "(Nenhum item salvo)", IsEnabled = false });
            }
            else
            {
                foreach (var item in salvas)
                {
                    var mi = new MenuItem
                    {
                        Icon = AppIcons.Create(AppIcons.FromWindowType(item.WindowType)),
                        Header = item.Title
                    };
                    string id = item.Id;
                    mi.Click += (s, ev) => AbrirJanelaDoCatalogo(id);
                    menuSalvas.Items.Add(mi);
                }

                menuSalvas.Items.Add(new Separator());
                var menuRemover = new MenuItem
                {
                    Icon = AppIcons.Create(AppIcons.Trash),
                    Header = "Excluir dos Salvos"
                };
                foreach (var item in salvas)
                {
                    var delItem = new MenuItem
                    {
                        Icon = AppIcons.Create(AppIcons.FromWindowType(item.WindowType)),
                        Header = $"✕ {item.Title}"
                    };
                    string id = item.Id;
                    delItem.Click += (s, ev) => RemoverJanelaDoCatalogo(id);
                    menuRemover.Items.Add(delItem);
                }
                menuSalvas.Items.Add(menuRemover);
            }
        }

        private void AbrirJanelaDoCatalogo(string id)
        {
            var ws = ObterWorkspaceAtual();
            int idx = ws.SavedCatalog.FindIndex(x => x.Id == id);
            if (idx < 0) return;

            var saved = ws.SavedCatalog[idx];
            saved.AccessCount++;
            saved.LastUsedUtc = DateTime.UtcNow;
            SalvarTodosWorkspacesNoDisco();

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
                saved.TextFormat,
                saved.GalleryItems,
                saved.CustomColor,
                saved.SpreadsheetRows,
                saved.SpreadsheetCols,
                saved.SpreadsheetCells,
                saved.ConditionalRules,
                saved.CriticalityLevel);
        }

        private void RemoverJanelaDoCatalogo(string id)
        {
            var ws = ObterWorkspaceAtual();
            var alvo = ws.SavedCatalog.FirstOrDefault(x => x.Id == id);
            if (alvo == null) return;

            ws.SavedCatalog.Remove(alvo);
            SalvarTodosWorkspacesNoDisco();

            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, $"\"{alvo.Title}\" removido dos salvos.", "info");
            }
        }

        // ==================== AÇÕES DOS MÓDULOS ====================

        private DraggableWindow CriarFormaFluxograma(
            string shapeType,
            string prefixoTitulo,
            double larguraBase,
            double alturaBase,
            string textoInicial = "",
            string criticalityLevel = "none")
        {
            _contadorFormas++;
            double f = ObterFatorTamanhoWidget();
            double offsetX = 90 + ((_contadorFormas - 1) * 34) % 320;
            double offsetY = 70 + ((_contadorFormas - 1) * 34) % 210;

            string titulo = $"{prefixoTitulo} #{_contadorFormas}";
            var win = CriarJanelaInterna(
                titulo,
                offsetX,
                offsetY,
                Math.Round(larguraBase * f),
                Math.Round(alturaBase * f),
                shapeType,
                textoInicial,
                null,
                null,
                1,
                null,
                null,
                null,
                null,
                "",
                12,
                6,
                null,
                null,
                criticalityLevel);

            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, $"{titulo} adicionado ao fluxograma!", "info");
            }
            return win;
        }

        private void AdicionarFormaRetangulo_Click(object? sender, RoutedEventArgs e)
            => CriarFormaFluxograma("shape_rect", "Etapa", 220, 130, "Descreva a etapa...");

        private void AdicionarFormaLosango_Click(object? sender, RoutedEventArgs e)
            => CriarFormaFluxograma("shape_diamond", "Decisão", 230, 155, "Condição / Decisão?");

        private void AdicionarFormaCirculo_Click(object? sender, RoutedEventArgs e)
            => CriarFormaFluxograma("shape_circle", "Início / Fim", 180, 145, "Início / Fim");

        private void AdicionarAvisoCritico_Click(object? sender, RoutedEventArgs e)
            => CriarFormaFluxograma("shape_rect", "Aviso Crítico", 235, 135, "Atenção: Ponto Crítico!", "critical");

        private void AdicionarAvisoAlto_Click(object? sender, RoutedEventArgs e)
            => CriarFormaFluxograma("shape_rect", "Aviso Alta Prioridade", 235, 135, "Alta Criticidade", "high");

        private void AdicionarAvisoMedio_Click(object? sender, RoutedEventArgs e)
            => CriarFormaFluxograma("shape_rect", "Aviso de Atenção", 235, 135, "Ponto de Atenção", "medium");

        private void AdicionarAvisoBaixo_Click(object? sender, RoutedEventArgs e)
            => CriarFormaFluxograma("shape_rect", "Aviso Informativo", 235, 135, "Observação / Baixa Criticidade", "low");

        private void AbrirBlocoDeNotas_Click(object? sender, RoutedEventArgs e)
        {
            _contadorNotas++;
            double f = ObterFatorTamanhoWidget();
            double offsetX = 60 + ((_contadorNotas - 1) * 30) % 300;
            double offsetY = 50 + ((_contadorNotas - 1) * 30) % 200;

            string titulo = $"Bloco de Notas #{_contadorNotas}";
            CriarJanelaInterna(titulo, offsetX, offsetY, Math.Round(360 * f), Math.Round(260 * f), "notepad");

            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, $"{titulo} criado!", "info");
            }
        }

        private void AbrirOuFocarNotaDaLinha(DraggableWindow checklistWin, ChecklistRowState row)
        {
            string tituloLinha = string.IsNullOrWhiteSpace(row.Input.Text)
                ? "Nova Nota"
                : row.Input.Text.Trim();

            // A cor base é priorizada pela cor do item da linha; se não houver, herda da janela da checklist
            string corBase = !string.IsNullOrWhiteSpace(row.ColorHex)
                ? row.ColorHex
                : checklistWin.ObterCorEfetiva();
            string? corLinha = !string.IsNullOrWhiteSpace(corBase) ? corBase : null;

            if (row.OpenNoteWindow != null && _ativasJanelas.Contains(row.OpenNoteWindow))
            {
                row.OpenNoteWindow.AccessCount++;
                if (row.OpenNoteWindow.IsMinimized)
                {
                    row.OpenNoteWindow.ToggleMinimize();
                }

                if (!string.IsNullOrWhiteSpace(corBase) && string.IsNullOrWhiteSpace(row.OpenNoteWindow.ObterCorEfetiva()))
                {
                    row.OpenNoteWindow.DefinirCorJanela(corBase);
                }

                CriarOuFocarConexaoFluxograma(checklistWin, row.OpenNoteWindow, colorHex: corLinha);
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

                if (!string.IsNullOrWhiteSpace(corBase) && string.IsNullOrWhiteSpace(existente.ObterCorEfetiva()))
                {
                    existente.DefinirCorJanela(corBase);
                }

                CriarOuFocarConexaoFluxograma(checklistWin, existente, colorHex: corLinha);
                ZIndexManager.BringToFront(existente);
                return;
            }

            _contadorNotas++;
            double f = ObterFatorTamanhoWidget();
            double offsetX = Math.Max(40, Canvas.GetLeft(checklistWin) + checklistWin.Width + 60);
            double offsetY = Math.Max(40, Canvas.GetTop(checklistWin));

            var noteWin = CriarJanelaInterna(
                tituloLinha,
                offsetX,
                offsetY,
                Math.Round(360 * f),
                Math.Round(260 * f),
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
                row.LinkedNoteFormat,
                customColor: corBase);

            row.OpenNoteWindow = noteWin;
            CriarOuFocarConexaoFluxograma(checklistWin, noteWin, colorHex: corLinha);

            noteWin.OnClosed += () =>
            {
                if (row.OpenNoteWindow == noteWin)
                {
                    row.OpenNoteWindow = null;
                }
            };

            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, $"Bloco de notas \"{tituloLinha}\" aberto e conectado!", "info");
            }
        }

        private void IniciarVinculoLinhaComMouse(DraggableWindow checklistWin, ChecklistRowState row)
        {
            CancelarModoVinculoLinha();
            CancelarModoConexao();
            CancelarModoVinculoCalendario();

            var outrasJanelas = _ativasJanelas.Where(w => w != checklistWin).ToList();
            if (outrasJanelas.Count == 0)
            {
                if (_settings.EnableNotifications)
                {
                    ToastNotification.Mostrar(Workspace, "Abra ou crie outra janela no Workspace para vinculá-la a este item.", "info");
                }
                return;
            }

            _modoVinculoLinhaAtivo = true;
            _checklistWinOrigemVinculo = checklistWin;
            _linhaOrigemVinculo = row;

            GarantirLinhaPreviewNoCanvas();
            if (_previewConnectionPath != null)
            {
                string corBase = !string.IsNullOrWhiteSpace(row.ColorHex)
                    ? row.ColorHex
                    : checklistWin.ObterCorEfetiva();
                _previewConnectionPath.Stroke = !string.IsNullOrWhiteSpace(corBase)
                    ? new SolidColorBrush(Color.Parse(corBase))
                    : ThemePalette.AccentLight;
            }

            Workspace.Cursor = new Cursor(StandardCursorType.Cross);

            string nomeItem = !string.IsNullOrWhiteSpace(row.Input?.Text) ? $" \"{row.Input.Text}\"" : "";
            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, $"🔗 Vincular item{nomeItem}: aponte e clique na janela desejada no Workspace (ESC para cancelar)", "info");
            }
        }

        private void AtualizarLinhaPreviewVinculo(Point mousePos)
        {
            if (_checklistWinOrigemVinculo == null || _linhaOrigemVinculo == null || _previewConnectionPath == null) return;

            Point start;
            if (_linhaOrigemVinculo.BtnLinkWindow != null)
            {
                var pt = _linhaOrigemVinculo.BtnLinkWindow.TranslatePoint(
                    new Point(_linhaOrigemVinculo.BtnLinkWindow.Bounds.Width / 2, _linhaOrigemVinculo.BtnLinkWindow.Bounds.Height / 2),
                    Workspace);
                start = pt ?? ObterCentroJanelaWorkspace(_checklistWinOrigemVinculo);
            }
            else
            {
                start = ObterCentroJanelaWorkspace(_checklistWinOrigemVinculo);
            }

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(start, false);
                ctx.LineTo(mousePos);
            }
            _previewConnectionPath.Data = geo;
        }

        private Point ObterCentroJanelaWorkspace(DraggableWindow win)
        {
            double x = Canvas.GetLeft(win);
            double y = Canvas.GetTop(win);
            return new Point(
                (double.IsNaN(x) ? 0 : x) + win.Width / 2,
                (double.IsNaN(y) ? 0 : y) + win.Height / 2);
        }

        private void ConcluirVinculoLinhaComJanela(DraggableWindow destino)
        {
            if (_checklistWinOrigemVinculo == null || _linhaOrigemVinculo == null)
            {
                CancelarModoVinculoLinha();
                return;
            }

            if (destino == _checklistWinOrigemVinculo)
            {
                if (_settings.EnableNotifications)
                {
                    ToastNotification.Mostrar(Workspace, "Selecione uma janela diferente para vincular este item.", "info");
                }
                return;
            }

            var checklistWin = _checklistWinOrigemVinculo;
            var row = _linhaOrigemVinculo;
            CancelarModoVinculoLinha();

            RegistrarEstadoUndo();

            row.LinkedWindowId = destino.WindowId;
            checklistWin.AtualizarIndicadorVinculoLinha(row);

            string corBase = !string.IsNullOrWhiteSpace(row.ColorHex)
                ? row.ColorHex
                : checklistWin.ObterCorEfetiva();
            string? corLinha = !string.IsNullOrWhiteSpace(corBase) ? corBase : null;

            if (!string.IsNullOrWhiteSpace(corBase) && string.IsNullOrWhiteSpace(destino.ObterCorEfetiva()))
            {
                destino.DefinirCorJanela(corBase);
            }

            CriarOuFocarConexaoFluxograma(checklistWin, destino, colorHex: corLinha);
            SalvarTodosWorkspacesNoDisco();

            if (_settings.EnableNotifications)
            {
                string nomeItem = !string.IsNullOrWhiteSpace(row.Input?.Text) ? $" \"{row.Input.Text}\"" : "Item";
                ToastNotification.Mostrar(Workspace, $"✓ {nomeItem} vinculado à janela \"{destino.WindowTitle}\" com sucesso!", "success");
            }
        }

        private void CancelarModoVinculoLinha(bool notificar = false)
        {
            _modoVinculoLinhaAtivo = false;
            _checklistWinOrigemVinculo = null;
            _linhaOrigemVinculo = null;
            Workspace.Cursor = Cursor.Default;

            if (_previewConnectionPath != null)
            {
                Workspace.Children.Remove(_previewConnectionPath);
                _previewConnectionPath.Data = null;
            }

            if (notificar && _settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, "Seleção de vínculo cancelada.", "info");
            }
        }

        private void VincularOuFocarJanelaDaLinha(DraggableWindow checklistWin, ChecklistRowState row)
        {
            // Se ainda não está vinculado a nenhuma janela, entra diretamente no modo de seleção com o mouse!
            if (string.IsNullOrWhiteSpace(row.LinkedWindowId))
            {
                IniciarVinculoLinhaComMouse(checklistWin, row);
                return;
            }

            // Se a janela vinculada não existe mais no workspace atual, permite selecionar nova com o mouse
            var target = _ativasJanelas.FirstOrDefault(w => w.WindowId == row.LinkedWindowId);
            if (target == null)
            {
                row.LinkedWindowId = string.Empty;
                checklistWin.AtualizarIndicadorVinculoLinha(row);
                IniciarVinculoLinhaComMouse(checklistWin, row);
                return;
            }

            // Se já tem vínculo ativo, exibe menu rápido e direto
            var menu = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight };

            string corBase = !string.IsNullOrWhiteSpace(row.ColorHex)
                ? row.ColorHex
                : checklistWin.ObterCorEfetiva();
            string? corLinha = !string.IsNullOrWhiteSpace(corBase) ? corBase : null;

            var miGo = new MenuItem { Header = $"Focar: {target.WindowTitle}" };
            miGo.Click += (_, _) =>
            {
                if (target.IsMinimized) target.ToggleMinimize();
                ZIndexManager.BringToFront(target);
                if (!string.IsNullOrWhiteSpace(corBase) && string.IsNullOrWhiteSpace(target.ObterCorEfetiva()))
                {
                    target.DefinirCorJanela(corBase);
                }
                CriarOuFocarConexaoFluxograma(checklistWin, target, colorHex: corLinha);
            };
            menu.Items.Add(miGo);

            var miChange = new MenuItem { Header = "Alterar vínculo (selecionar com o mouse)..." };
            miChange.Click += (_, _) =>
            {
                IniciarVinculoLinhaComMouse(checklistWin, row);
            };
            menu.Items.Add(miChange);

            var miUnlink = new MenuItem { Header = "Remover vínculo" };
            miUnlink.Click += (_, _) =>
            {
                row.LinkedWindowId = string.Empty;
                checklistWin.AtualizarIndicadorVinculoLinha(row);
                SalvarTodosWorkspacesNoDisco();
                if (_settings.EnableNotifications)
                {
                    ToastNotification.Mostrar(Workspace, "Vínculo removido.", "info");
                }
            };
            menu.Items.Add(miUnlink);

            if (row.BtnLinkWindow != null)
            {
                menu.ShowAt(row.BtnLinkWindow);
            }
        }

        private void AbrirChecklist_Click(object? sender, RoutedEventArgs e)
        {
            _contadorListas++;
            double f = ObterFatorTamanhoWidget();
            double offsetX = 80 + ((_contadorListas - 1) * 30) % 300;
            double offsetY = 60 + ((_contadorListas - 1) * 30) % 200;

            string titulo = $"Lista #{_contadorListas}";
            CriarJanelaInterna(titulo, offsetX, offsetY, Math.Round(380 * f), Math.Round(290 * f), "checklist");

            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, $"{titulo} criada!", "info");
            }
        }

        private void AbrirPlanilha_Click(object? sender, RoutedEventArgs e)
        {
            _contadorPlanilhas++;
            double f = ObterFatorTamanhoWidget();
            double offsetX = 65 + ((_contadorPlanilhas - 1) * 32) % 280;
            double offsetY = 48 + ((_contadorPlanilhas - 1) * 32) % 180;

            string titulo = $"Planilha #{_contadorPlanilhas}";
            CriarJanelaInterna(titulo, offsetX, offsetY, Math.Round(640 * f), Math.Round(420 * f), "spreadsheet");

            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, $"{titulo} criada!", "info");
            }
        }

        private void AbrirGaleria_Click(object? sender, RoutedEventArgs e)
        {
            _contadorGalerias++;
            double f = ObterFatorTamanhoWidget();
            double offsetX = 70 + ((_contadorGalerias - 1) * 32) % 280;
            double offsetY = 50 + ((_contadorGalerias - 1) * 32) % 180;

            string titulo = $"Galeria de Imagens #{_contadorGalerias}";
            CriarJanelaInterna(titulo, offsetX, offsetY, Math.Round(540 * f), Math.Round(390 * f), "gallery");

            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, $"{titulo} aberta!", "info");
            }
        }

        private async void AbrirImagemAvulsa_Click(object? sender, RoutedEventArgs e)
        {
            if (StorageProvider == null) return;

            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Abrir Imagem em Janela no Workspace",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Imagens")
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp", "*.gif" }
                    },
                    FilePickerFileTypes.All
                }
            });

            double f = ObterFatorTamanhoWidget();
            foreach (var file in files)
            {
                string? localPath = file.TryGetLocalPath();
                if (!string.IsNullOrWhiteSpace(localPath) && File.Exists(localPath))
                {
                    _contadorImagens++;
                    double offsetX = 75 + ((_contadorImagens - 1) * 30) % 260;
                    double offsetY = 55 + ((_contadorImagens - 1) * 30) % 180;
                    string nome = Path.GetFileNameWithoutExtension(localPath);
                    CriarJanelaInterna(nome, offsetX, offsetY, Math.Round(440 * f), Math.Round(340 * f), "image", localPath);
                }
            }
        }

        private async void AbrirVideoAvulso_Click(object? sender, RoutedEventArgs e)
        {
            if (StorageProvider == null) return;

            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Abrir Vídeo em Janela no Workspace",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Vídeos")
                    {
                        Patterns = new[] { "*.mp4", "*.webm", "*.mkv", "*.avi", "*.mov", "*.wmv", "*.m4v", "*.ogv" }
                    },
                    FilePickerFileTypes.All
                }
            });

            double f = ObterFatorTamanhoWidget();
            foreach (var file in files)
            {
                string? localPath = file.TryGetLocalPath();
                if (!string.IsNullOrWhiteSpace(localPath) && File.Exists(localPath))
                {
                    _contadorVideos++;
                    double offsetX = 80 + ((_contadorVideos - 1) * 30) % 260;
                    double offsetY = 60 + ((_contadorVideos - 1) * 30) % 180;
                    string nome = Path.GetFileNameWithoutExtension(localPath);
                    CriarJanelaInterna(nome, offsetX, offsetY, Math.Round(560 * f), Math.Round(380 * f), "video", localPath);
                }
            }
        }

        private void RestaurarFundoWorkspace_Click(object? sender, RoutedEventArgs e)
        {
            _workspaceBgImagePath = string.Empty;
            var ws = ObterWorkspaceAtual();
            ws.BackgroundImagePath = string.Empty;
            AplicarFundoWorkspace(_isDarkMode ? ThemePalette.DarkWorkspaceBg : ThemePalette.LightWorkspaceBg);
            SalvarTodosWorkspacesNoDisco();

            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, "Plano de fundo do Workspace restaurado.", "info");
            }
        }

        private void AbrirOuFocarImagemDaGaleria(DraggableWindow galleryWin, GalleryCardState card)
        {
            string? corOrigem = galleryWin.ObterCorEfetiva();

            if (card.OpenImageWindow != null && _ativasJanelas.Contains(card.OpenImageWindow))
            {
                card.OpenImageWindow.AccessCount++;
                if (card.OpenImageWindow.IsMinimized)
                {
                    card.OpenImageWindow.ToggleMinimize();
                }

                if (!string.IsNullOrEmpty(corOrigem) && string.IsNullOrEmpty(card.OpenImageWindow.ObterCorEfetiva()))
                {
                    card.OpenImageWindow.DefinirCorJanela(corOrigem);
                }

                CriarOuFocarConexaoFluxograma(galleryWin, card.OpenImageWindow, colorHex: corOrigem);
                ZIndexManager.BringToFront(card.OpenImageWindow);
                return;
            }

            string tituloMidia = string.IsNullOrWhiteSpace(card.Title)
                ? Path.GetFileNameWithoutExtension(card.FilePath)
                : card.Title;

            bool isVideo = card.MediaType == "video" || DraggableWindow.EhArquivoDeVideo(card.FilePath);
            string targetType = isVideo ? "video" : "image";

            var existente = _ativasJanelas.FirstOrDefault(w =>
                w.WindowType == targetType &&
                string.Equals(w.ExportState().NoteContent, card.FilePath, StringComparison.OrdinalIgnoreCase));

            if (existente != null)
            {
                card.OpenImageWindow = existente;
                existente.AccessCount++;
                if (existente.IsMinimized)
                {
                    existente.ToggleMinimize();
                }

                if (!string.IsNullOrEmpty(corOrigem) && string.IsNullOrEmpty(existente.ObterCorEfetiva()))
                {
                    existente.DefinirCorJanela(corOrigem);
                }

                CriarOuFocarConexaoFluxograma(galleryWin, existente, colorHex: corOrigem);
                ZIndexManager.BringToFront(existente);
                return;
            }

            double f = ObterFatorTamanhoWidget();
            double offsetX = Math.Max(40, Canvas.GetLeft(galleryWin) + galleryWin.Width + 60);
            double offsetY = Math.Max(40, Canvas.GetTop(galleryWin));

            double winW = isVideo ? Math.Round(560 * f) : Math.Round(440 * f);
            double winH = isVideo ? Math.Round(380 * f) : Math.Round(340 * f);
            if (isVideo) _contadorVideos++;
            else _contadorImagens++;

            var mediaWin = CriarJanelaInterna(
                tituloMidia,
                offsetX,
                offsetY,
                winW,
                winH,
                targetType,
                card.FilePath,
                customColor: corOrigem);

            card.OpenImageWindow = mediaWin;
            CriarOuFocarConexaoFluxograma(galleryWin, mediaWin, colorHex: corOrigem);

            mediaWin.OnClosed += () =>
            {
                if (card.OpenImageWindow == mediaWin)
                {
                    card.OpenImageWindow = null;
                }
            };

            if (_settings.EnableNotifications)
            {
                string tipoLabel = isVideo ? "Vídeo" : "Imagem";
                ToastNotification.Mostrar(Workspace, $"{tipoLabel} \"{tituloMidia}\" aberto(a) e conectado(a)!", "info");
            }
        }

        private void AlternarFundoWorkspace(string filePath)
        {
            var ws = ObterWorkspaceAtual();
            var fallbackBg = _isDarkMode ? ThemePalette.DarkWorkspaceBg : ThemePalette.LightWorkspaceBg;

            if (string.Equals(_workspaceBgImagePath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                _workspaceBgImagePath = string.Empty;
                ws.BackgroundImagePath = string.Empty;
                AplicarFundoWorkspace(fallbackBg);
                SalvarTodosWorkspacesNoDisco();

                if (_settings.EnableNotifications)
                {
                    ToastNotification.Mostrar(Workspace, "Plano de fundo do Workspace restaurado.", "info");
                }
                return;
            }

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                ToastNotification.Mostrar(Workspace, "Arquivo de imagem não encontrado.", "warning");
                return;
            }

            _workspaceBgImagePath = filePath;
            ws.BackgroundImagePath = filePath;
            AplicarFundoWorkspace(fallbackBg);
            SalvarTodosWorkspacesNoDisco();

            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, "Imagem aplicada no fundo do Workspace!", "success");
            }
        }

        private void AplicarFundoWorkspace(IBrush? fallbackBg = null)
        {
            var baseBg = fallbackBg ?? (_isDarkMode ? ThemePalette.DarkWorkspaceBg : ThemePalette.LightWorkspaceBg);
            var solidColor = (baseBg as ISolidColorBrush)?.Color ?? (_isDarkMode ? Color.Parse("#0F172A") : Color.Parse("#F1F5F9"));

            string style = _settings.WorkspaceBackgroundStyle?.ToLowerInvariant() ?? "grid";

            if (style == "dots")
            {
                // Fundo Pontilhado (Dots Pattern)
                var dotColor = _isDarkMode ? Color.FromArgb(70, 203, 213, 225) : Color.FromArgb(70, 71, 85, 105);
                var group = new DrawingGroup();
                group.Children.Add(new GeometryDrawing
                {
                    Brush = new SolidColorBrush(solidColor),
                    Geometry = new RectangleGeometry(new Rect(0, 0, 24, 24))
                });
                group.Children.Add(new GeometryDrawing
                {
                    Brush = new SolidColorBrush(dotColor),
                    Geometry = new EllipseGeometry(new Rect(11, 11, 2, 2))
                });

                Workspace.Background = new DrawingBrush
                {
                    TileMode = TileMode.Tile,
                    SourceRect = new RelativeRect(0, 0, 24, 24, RelativeUnit.Absolute),
                    DestinationRect = new RelativeRect(0, 0, 24, 24, RelativeUnit.Absolute),
                    Drawing = group
                };
            }
            else if (style == "grid")
            {
                // Fundo Quadriculado estilo planilha (Grid Pattern)
                var gridLineColor = _isDarkMode ? Color.FromArgb(40, 148, 163, 184) : Color.FromArgb(45, 100, 116, 139);
                var linePen = new Pen(new SolidColorBrush(gridLineColor), 1);

                var group = new DrawingGroup();
                group.Children.Add(new GeometryDrawing
                {
                    Brush = new SolidColorBrush(solidColor),
                    Geometry = new RectangleGeometry(new Rect(0, 0, 30, 30))
                });
                group.Children.Add(new GeometryDrawing
                {
                    Pen = linePen,
                    Geometry = StreamGeometry.Parse("M 0 30 L 30 30 M 30 0 L 30 30")
                });

                Workspace.Background = new DrawingBrush
                {
                    TileMode = TileMode.Tile,
                    SourceRect = new RelativeRect(0, 0, 30, 30, RelativeUnit.Absolute),
                    DestinationRect = new RelativeRect(0, 0, 30, 30, RelativeUnit.Absolute),
                    Drawing = group
                };
            }
            else
            {
                // Fundo Liso / Cor Sólida do Tema
                Workspace.Background = new SolidColorBrush(solidColor);
            }

            AtualizarMenuContextoCanvas();
        }

        private void AbrirNavegadorComUrl(string url, string sufixoTitulo = "")
        {
            _contadorNavegadores++;
            double f = ObterFatorTamanhoWidget();
            double w = Math.Round(800 * f);
            double h = Math.Round(450 * f);

            double vpW = WorkspaceViewport.Bounds.Width;
            double vpH = WorkspaceViewport.Bounds.Height;
            if (vpW > 100 && vpH > 100)
            {
                double maxW = Math.Max(420, vpW - 40);
                double maxH = Math.Max(260, vpH - 40);
                if (w > maxW)
                {
                    w = maxW;
                    h = Math.Round(w * 9.0 / 16.0);
                }
                if (h > maxH)
                {
                    h = maxH;
                    w = Math.Round(h * 16.0 / 9.0);
                }
            }

            double offsetX = 50 + ((_contadorNavegadores - 1) * 32) % 260;
            double offsetY = 40 + ((_contadorNavegadores - 1) * 32) % 160;
            if (vpW > w + 20) offsetX = Math.Min(offsetX, vpW - w - 20);
            if (vpH > h + 20) offsetY = Math.Min(offsetY, vpH - h - 20);

            string titulo = string.IsNullOrWhiteSpace(sufixoTitulo)
                ? $"Navegador #{_contadorNavegadores}"
                : $"Navegador - {sufixoTitulo}";

            CriarJanelaInterna(titulo, Math.Max(12, offsetX), Math.Max(12, offsetY), w, h, "browser", url);

            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, $"{titulo} aberto!", "info");
            }
        }

        private void AbrirNavegador_Click(object? sender, RoutedEventArgs e) => AbrirNavegadorComUrl("https://www.google.com");
        private void AbrirNavegadorGoogle_Click(object? sender, RoutedEventArgs e) => AbrirNavegadorComUrl("https://www.google.com", "Google");
        private void AbrirNavegadorYouTube_Click(object? sender, RoutedEventArgs e) => AbrirNavegadorComUrl("https://www.youtube.com", "YouTube");
        private void AbrirNavegadorGitHub_Click(object? sender, RoutedEventArgs e) => AbrirNavegadorComUrl("https://github.com", "GitHub");
        private void AbrirNavegadorWikipedia_Click(object? sender, RoutedEventArgs e) => AbrirNavegadorComUrl("https://pt.wikipedia.org", "Wikipédia");

        private void AlternarTemaRapido_Click(object? sender, RoutedEventArgs e)
        {
            _settings.IsDarkMode = !_isDarkMode;
            _settings.ThemePreset = _settings.IsDarkMode ? "dark_blue" : "light_blue";
            ThemePalette.AplicarEsquema(_settings.ThemePreset);
            AplicarTema(_settings.IsDarkMode);
            SalvarTodosWorkspacesNoDisco();
        }

        private void CriarAtalhoAreaDeTrabalho_Click(object? sender, RoutedEventArgs e)
        {
            if (OperatingSystem.IsLinux())
            {
                LinuxDesktopIntegration.GarantirIntegracaoDesktopLinux(criarAtalhoAreaDeTrabalho: true);
                string? caminho = LinuxDesktopIntegration.CriarAtalhoNaAreaDeTrabalho();
                if (!string.IsNullOrWhiteSpace(caminho))
                {
                    ToastNotification.Mostrar(Workspace, $"Atalho com ícone criado na Área de Trabalho e Barra de Tarefas!", "success");
                }
                else
                {
                    ToastNotification.Mostrar(Workspace, "Ícone registrado no Menu de Aplicativos e Barra de Tarefas do Linux!", "success");
                }
            }
            else
            {
                ToastNotification.Mostrar(Workspace, "O ícone já está embutido nativamente no executável (.exe).", "info");
            }
        }

        private async void VerificarAtualizacoes_Click(object? sender, RoutedEventArgs e)
        {
            await VerificarAtualizacaoGitHubAsync(silenciosoSeAtualizado: false);
        }

        private async System.Threading.Tasks.Task VerificarAtualizacaoGitHubAsync(bool silenciosoSeAtualizado)
        {
            try
            {
                if (!silenciosoSeAtualizado)
                {
                    ToastNotification.Mostrar(Workspace, "Consultando última tag de Release no GitHub...", "info");
                }

                string repo = GitHubUpdateService.NormalizarRepositorio(_settings.GitHubRepository);
                var release = await GitHubUpdateService.VerificarUltimaReleaseAsync(repo, _settings.LastInstalledTag);

                if (release == null)
                {
                    if (!silenciosoSeAtualizado)
                    {
                        MostrarModalConfigurarRepoUpdate(repo, "Nenhuma tag ou release encontrada neste repositório ainda.");
                    }
                    return;
                }

                if (release.IsNewer || !silenciosoSeAtualizado)
                {
                    MostrarModalAtualizacaoGitHub(release, repo);
                }
            }
            catch
            {
                if (!silenciosoSeAtualizado)
                {
                    ToastNotification.Mostrar(Workspace, "Não foi possível conectar ao GitHub Releases.", "warning");
                }
            }
        }

        private void MostrarModalConfigurarRepoUpdate(string repoAtual, string motivo)
        {
            ModalTitle.Text = "Atualização Automática via GitHub";
            ModalMessage.Text = $"{motivo}\nInforme o repositório GitHub (formato usuario/repositorio) para verificar as tags de Release:";
            ModalInputBox.Text = repoAtual;
            ModalInputBox.IsVisible = true;
            ModalButtonsPanel.Children.Clear();

            var btnFechar = CriarBotaoModal("Fechar", isPrimary: false, isDanger: false);
            btnFechar.Click += (s, e) => ModalOverlay.IsVisible = false;

            var btnVerificar = CriarBotaoModal("Salvar e Verificar", isPrimary: true, isDanger: false, AppIcons.Download);
            btnVerificar.Click += async (s, e) =>
            {
                string novoRepo = GitHubUpdateService.NormalizarRepositorio(ModalInputBox.Text);
                _settings.GitHubRepository = novoRepo;
                SalvarTodosWorkspacesNoDisco();
                ModalOverlay.IsVisible = false;
                await VerificarAtualizacaoGitHubAsync(silenciosoSeAtualizado: false);
            };

            ModalButtonsPanel.Children.Add(btnFechar);
            ModalButtonsPanel.Children.Add(btnVerificar);
            AplicarEstiloModal();
            ModalOverlay.IsVisible = true;
        }

        private void MostrarModalAtualizacaoGitHub(GitHubReleaseInfo release, string repo)
        {
            string statusVersao = release.IsNewer
                ? $"Nova versão disponível no GitHub! (Atual: {release.CurrentVersion} → Nova Tag: {release.TagName})"
                : $"Você já está na versão/tag mais recente ({release.TagName}).";

            string resumoNotas = string.IsNullOrWhiteSpace(release.Notes)
                ? string.Empty
                : $"\n\nNotas da versão:\n{(release.Notes.Length > 220 ? release.Notes[..220] + "..." : release.Notes)}";

            ModalTitle.Text = release.IsNewer
                ? $"Nova Release Encontrada: {release.TagName}"
                : $"GitHub Release: {release.TagName}";

            ModalMessage.Text =
                $"{statusVersao}\n" +
                $"Repositório: {repo}\n" +
                $"Pacote: {release.AssetName}" +
                resumoNotas;

            ModalInputBox.Text = repo;
            ModalInputBox.IsVisible = false;
            ModalButtonsPanel.Children.Clear();

            var btnFechar = CriarBotaoModal("Depois", isPrimary: false, isDanger: false);
            btnFechar.Click += (s, e) => ModalOverlay.IsVisible = false;

            string labelDownload = release.IsNewer ? "Baixar e Atualizar Agora" : "Baixar Novamente";
            var btnBaixar = CriarBotaoModal(labelDownload, isPrimary: true, isDanger: false, AppIcons.Download);
            btnBaixar.Click += async (s, e) =>
            {
                btnBaixar.IsEnabled = false;
                btnFechar.IsEnabled = false;

                var progress = new Progress<int>(pct =>
                {
                    ModalMessage.Text = $"Baixando pacote {release.AssetName} da tag {release.TagName}... {pct}%";
                });

                var resultado = await GitHubUpdateService.BaixarAtualizacaoAsync(release, progress);
                if (resultado.Success)
                {
                    _settings.LastInstalledTag = release.TagName;
                    SalvarTodosWorkspacesNoDisco();

                    ModalTitle.Text = $"Download Concluído ({release.TagName})";
                    ModalMessage.Text = $"{resultado.Message}\n\nDeseja aplicar e abrir a nova versão agora?";
                    ModalButtonsPanel.Children.Clear();

                    var btnAbrirPasta = CriarBotaoModal("Apenas Fechar", isPrimary: false, isDanger: false);
                    btnAbrirPasta.Click += (_, _) => ModalOverlay.IsVisible = false;

                    var btnInstalar = CriarBotaoModal("Aplicar e Reiniciar", isPrimary: true, isDanger: false, AppIcons.Download);
                    btnInstalar.Click += (_, _) =>
                    {
                        ModalOverlay.IsVisible = false;
                        bool iniciou = GitHubUpdateService.IniciarInstalacaoAutomatica(resultado);
                        if (iniciou)
                        {
                            _saidaConfirmada = true;
                            Close();
                        }
                    };

                    ModalButtonsPanel.Children.Add(btnAbrirPasta);
                    ModalButtonsPanel.Children.Add(btnInstalar);
                }
                else
                {
                    btnBaixar.IsEnabled = true;
                    btnFechar.IsEnabled = true;
                    ModalMessage.Text = resultado.Message;
                }
            };

            ModalButtonsPanel.Children.Add(btnFechar);
            ModalButtonsPanel.Children.Add(btnBaixar);
            AplicarEstiloModal();
            ModalOverlay.IsVisible = true;
        }

        // ==================== EXPORTAÇÃO E IMPORTAÇÃO (PDF / ARQUIVOS / WORKSPACE) ====================

        private static string SanitizarNomeArquivo(string nome)
        {
            string s = string.IsNullOrWhiteSpace(nome) ? "Workspace" : nome.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                s = s.Replace(c, '_');
            }
            return s;
        }

        private async void ExportarWorkspacePdf_Click(object? sender, RoutedEventArgs e)
        {
            if (StorageProvider == null) return;
            SincronizarWorkspaceAtualEmMemoria();
            var ws = ObterWorkspaceAtual();

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Exportar Demonstração do Workspace em PDF",
                SuggestedFileName = $"{SanitizarNomeArquivo(ws.Name)}_Demonstracao.pdf",
                DefaultExtension = "pdf",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Documento PDF") { Patterns = new[] { "*.pdf" } }
                }
            });

            string? path = file?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path)) return;

            bool ok = WorkspaceExportService.ExportarWorkspaceParaPdf(Workspace, ws, path);
            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(
                    Workspace,
                    ok ? $"PDF de demonstração salvo em: {Path.GetFileName(path)}" : "Não foi possível gerar o PDF.",
                    ok ? "success" : "warning");
            }
        }

        private async void ExportarWorkspacePng_Click(object? sender, RoutedEventArgs e)
        {
            if (StorageProvider == null) return;
            SincronizarWorkspaceAtualEmMemoria();
            var ws = ObterWorkspaceAtual();

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Exportar Imagem do Workspace",
                SuggestedFileName = $"{SanitizarNomeArquivo(ws.Name)}_Visual.png",
                DefaultExtension = "png",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Imagem PNG") { Patterns = new[] { "*.png" } }
                }
            });

            string? path = file?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path)) return;

            bool ok = WorkspaceExportService.ExportarWorkspaceParaPng(Workspace, path);
            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(
                    Workspace,
                    ok ? $"Imagem salva em: {Path.GetFileName(path)}" : "Não foi possível gerar a imagem.",
                    ok ? "success" : "warning");
            }
        }

        private async void ExportarWorkspaceHtml_Click(object? sender, RoutedEventArgs e)
        {
            if (StorageProvider == null) return;
            SincronizarWorkspaceAtualEmMemoria();
            var ws = ObterWorkspaceAtual();

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Exportar Relatório Interativo de Demonstração (HTML)",
                SuggestedFileName = $"{SanitizarNomeArquivo(ws.Name)}_Relatorio.html",
                DefaultExtension = "html",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Página HTML") { Patterns = new[] { "*.html" } }
                }
            });

            string? path = file?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path)) return;

            bool ok = WorkspaceExportService.ExportarWorkspaceParaHtml(Workspace, ws, path);
            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(
                    Workspace,
                    ok ? $"Relatório HTML salvo em: {Path.GetFileName(path)}" : "Não foi possível gerar o relatório HTML.",
                    ok ? "success" : "warning");
            }
        }

        private async void ExportarWorkspaceJson_Click(object? sender, RoutedEventArgs e)
        {
            if (StorageProvider == null) return;
            SincronizarWorkspaceAtualEmMemoria();
            var ws = ObterWorkspaceAtual();

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Exportar Workspace (.mdiws)",
                SuggestedFileName = $"{SanitizarNomeArquivo(ws.Name)}.mdiws",
                DefaultExtension = "mdiws",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Workspace MDI") { Patterns = new[] { "*.mdiws", "*.json" } }
                }
            });

            string? path = file?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path)) return;

            bool ok = WorkspaceExportService.ExportarWorkspaceJson(ws, path);
            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(
                    Workspace,
                    ok ? $"Workspace exportado: {Path.GetFileName(path)}" : "Erro ao exportar Workspace.",
                    ok ? "success" : "warning");
            }
        }

        private async void ExportarWorkspaceZip_Click(object? sender, RoutedEventArgs e)
        {
            if (StorageProvider == null) return;
            SincronizarWorkspaceAtualEmMemoria();
            var ws = ObterWorkspaceAtual();

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Exportar Pacote Portátil Completo com Mídias (.zip)",
                SuggestedFileName = $"{SanitizarNomeArquivo(ws.Name)}_Pacote.zip",
                DefaultExtension = "zip",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Pacote ZIP") { Patterns = new[] { "*.zip" } }
                }
            });

            string? path = file?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path)) return;

            bool ok = WorkspaceExportService.ExportarWorkspaceZip(ws, path);
            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(
                    Workspace,
                    ok ? $"Pacote completo exportado: {Path.GetFileName(path)}" : "Erro ao gerar pacote ZIP.",
                    ok ? "success" : "warning");
            }
        }

        private async void ExportarJanelaAtiva_Click(object? sender, RoutedEventArgs e)
        {
            if (StorageProvider == null) return;
            var ativa = ZIndexManager.ActiveWindow ?? (_ativasJanelas.Count > 0 ? _ativasJanelas[^1] : null);
            if (ativa == null)
            {
                ToastNotification.Mostrar(Workspace, "Nenhuma janela ativa selecionada para exportar.", "warning");
                return;
            }

            var state = ativa.ExportState();
            string ext = state.WindowType switch
            {
                "spreadsheet" => "csv",
                "image" or "video" => Path.GetExtension(state.NoteContent).TrimStart('.'),
                _ => "txt"
            };
            if (string.IsNullOrWhiteSpace(ext)) ext = "txt";

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = $"Exportar \"{state.Title}\"",
                SuggestedFileName = $"{SanitizarNomeArquivo(state.Title)}.{ext}",
                DefaultExtension = ext
            });

            string? path = file?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path)) return;

            bool ok = WorkspaceExportService.ExportarJanelaIndividual(state, path);
            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(
                    Workspace,
                    ok ? $"\"{state.Title}\" exportado para {Path.GetFileName(path)}!" : "Não foi possível exportar a janela ativa.",
                    ok ? "success" : "warning");
            }
        }

        private async void ImportarWorkspace_Click(object? sender, RoutedEventArgs e)
        {
            if (StorageProvider == null) return;

            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Importar Workspace ou Pacote (.mdiws, .json, .zip)",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Workspaces e Pacotes") { Patterns = new[] { "*.mdiws", "*.json", "*.zip" } },
                    FilePickerFileTypes.All
                }
            });

            if (files.Count == 0) return;
            string? path = files[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path)) return;

            var imported = WorkspaceExportService.ImportarWorkspace(path);
            if (imported == null)
            {
                ToastNotification.Mostrar(Workspace, "Arquivo de Workspace inválido ou corrompido.", "warning");
                return;
            }

            SincronizarWorkspaceAtualEmMemoria();
            _settings.Workspaces.Add(imported);
            AlternarParaWorkspace(imported.Id);
            SalvarTodosWorkspacesNoDisco();

            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, $"Workspace \"{imported.Name}\" importado e ativado!", "success");
            }
        }

        private async void ImportarArquivoParaWorkspace_Click(object? sender, RoutedEventArgs e)
        {
            if (StorageProvider == null) return;

            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Importar Arquivo para o Workspace (.txt, .md, .csv, Imagem, Vídeo)",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Arquivos Suportados")
                    {
                        Patterns = new[]
                        {
                            "*.txt", "*.md", "*.csv", "*.json", "*.log", "*.xml",
                            "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp", "*.gif",
                            "*.mp4", "*.webm", "*.mkv", "*.avi", "*.mov", "*.wmv", "*.m4v"
                        }
                    },
                    FilePickerFileTypes.All
                }
            });

            if (files.Count == 0) return;

            double f = ObterFatorTamanhoWidget();
            int importados = 0;

            foreach (var file in files)
            {
                string? localPath = file.TryGetLocalPath();
                if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath)) continue;

                string ext = Path.GetExtension(localPath).ToLowerInvariant();
                string nome = Path.GetFileNameWithoutExtension(localPath);

                if (DraggableWindow.EhArquivoDeVideo(localPath))
                {
                    _contadorVideos++;
                    CriarJanelaInterna(nome, 70 + (_contadorVideos * 26) % 240, 55 + (_contadorVideos * 26) % 160, Math.Round(560 * f), Math.Round(380 * f), "video", localPath);
                    importados++;
                }
                else if (ext is ".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp" or ".gif")
                {
                    _contadorImagens++;
                    CriarJanelaInterna(nome, 70 + (_contadorImagens * 26) % 240, 55 + (_contadorImagens * 26) % 160, Math.Round(440 * f), Math.Round(340 * f), "image", localPath);
                    importados++;
                }
                else if (ext == ".csv")
                {
                    try
                    {
                        string csv = File.ReadAllText(localPath);
                        var (rows, cols, cells) = WorkspaceExportService.ParseCsvParaPlanilha(csv);
                        _contadorPlanilhas++;
                        CriarJanelaInterna(
                            nome,
                            65 + (_contadorPlanilhas * 28) % 240,
                            50 + (_contadorPlanilhas * 28) % 160,
                            Math.Round(640 * f),
                            Math.Round(420 * f),
                            "spreadsheet",
                            "",
                            null, null, 1, null, null, null, null, "",
                            rows, cols, cells);
                        importados++;
                    }
                    catch { }
                }
                else
                {
                    try
                    {
                        string text = File.ReadAllText(localPath);
                        _contadorNotas++;
                        CriarJanelaInterna(
                            nome,
                            60 + (_contadorNotas * 28) % 240,
                            50 + (_contadorNotas * 28) % 160,
                            Math.Round(380 * f),
                            Math.Round(280 * f),
                            "notepad",
                            text);
                        importados++;
                    }
                    catch { }
                }
            }

            if (importados > 0)
            {
                SalvarTodosWorkspacesNoDisco();
                if (_settings.EnableNotifications)
                {
                    ToastNotification.Mostrar(Workspace, $"{importados} arquivo(s) importado(s) para o Workspace!", "success");
                }
            }
        }

        private void AbrirConfiguracoes_Click(object? sender, RoutedEventArgs e)
        {
            if (_settingsWindow != null)
            {
                _settingsWindow.IsVisible = true;
                AtualizarSuspensaoNavegadores();
                return;
            }

            _settingsWindow = new SettingsWindow(_settings, Workspace);

            _settingsWindow.OnThemePresetChanged += (preset) =>
            {
                _settings.ThemePreset = preset;
                _settings.IsDarkMode = ThemePalette.EhTemaEscuro(preset);
                AplicarTema(preset);
                SalvarTodosWorkspacesNoDisco();
            };

            _settingsWindow.OnWorkspaceBackgroundStyleChanged += (style) =>
            {
                _settings.WorkspaceBackgroundStyle = style;
                AplicarFundoWorkspace();
                SalvarTodosWorkspacesNoDisco();
            };

            _settingsWindow.OnSettingsChanged += (isDark, notif, layoutScale, mainWinPreset, widgetPreset) =>
            {
                _settings.IsDarkMode = isDark;
                _settings.EnableNotifications = notif;
                _settings.LayoutScale = layoutScale;
                _settings.MainWindowSizePreset = mainWinPreset;
                _settings.WidgetSizePreset = widgetPreset;

                AplicarTema(_settings.ThemePreset);
                AplicarEscalaLayout(layoutScale);
                AplicarTamanhoJanelaPrincipal(mainWinPreset);
                AplicarPresetJanelasInternas(widgetPreset);
                SalvarTodosWorkspacesNoDisco();
            };

            _settingsWindow.OnAutoUpdateChanged += (autoCheck) =>
            {
                _settings.AutoCheckUpdatesOnStartup = autoCheck;
                SalvarTodosWorkspacesNoDisco();
            };

            _settingsWindow.OnCheckUpdatesRequested += async () =>
            {
                await VerificarAtualizacaoGitHubAsync(silenciosoSeAtualizado: false);
            };

            _settingsWindow.OnGoogleDriveSettingsChanged += (enabled, folder) =>
            {
                _settings.GoogleDriveSyncEnabled = enabled;
                _settings.GoogleDriveFolderPath = folder;
                SalvarTodosWorkspacesNoDisco();
            };

            _settingsWindow.OnTestGoogleDriveSyncRequested += () =>
            {
                var ws = ObterWorkspaceAtual();
                string pasta = string.IsNullOrWhiteSpace(_settings.GoogleDriveFolderPath)
                    ? GoogleDriveSyncService.DetectarPastaPadraoGoogleDrive()
                    : _settings.GoogleDriveFolderPath;

                if (GoogleDriveSyncService.SincronizarWorkspaceNoGoogleDrive(ws, pasta, out string salvoEm, out string erro))
                {
                    _settings.LastGoogleDriveSyncDate = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                    SalvarTodosWorkspacesNoDisco();
                    ToastNotification.Mostrar(Workspace, $"Backup do Google Drive criado com sucesso!\nArquivo: {System.IO.Path.GetFileName(salvoEm)}", "success");
                }
                else
                {
                    ToastNotification.Mostrar(Workspace, $"Falha ao sincronizar no Google Drive: {erro}", "danger");
                }
            };

            _settingsWindow.OnOpenGoogleDriveFolderRequested += () =>
            {
                string pasta = string.IsNullOrWhiteSpace(_settings.GoogleDriveFolderPath)
                    ? GoogleDriveSyncService.DetectarPastaPadraoGoogleDrive()
                    : _settings.GoogleDriveFolderPath;
                GoogleDriveSyncService.AbrirPastaNoExplorador(pasta);
            };

            _settingsWindow.OnGoogleLoginRequested += async (clientId, clientSecret) =>
            {
                try
                {
                    ToastNotification.Mostrar(Workspace, "Abrindo navegador para autorizar acesso ao Google Drive...", "info");
                    var (sucesso, email, refreshToken, accessToken, expiryUtc, erro) =
                        await GoogleDriveCloudClient.IniciarFluxoLoginAsync(clientId, clientSecret);

                    if (sucesso)
                    {
                        _settings.GoogleOAuthClientId = clientId;
                        _settings.GoogleOAuthClientSecret = clientSecret;
                        _settings.GoogleOAuthAccessToken = accessToken;
                        _settings.GoogleOAuthRefreshToken = refreshToken;
                        _settings.GoogleOAuthTokenExpiryUtc = expiryUtc.ToString("o");
                        _settings.GoogleUserEmail = email;
                        _settings.GoogleDriveCloudSyncEnabled = true;
                        SalvarTodosWorkspacesNoDisco();

                        ToastNotification.Mostrar(Workspace, $"✓ Conta conectada: {email}", "success");

                        if (_settingsWindow != null)
                        {
                            MainRootGrid.Children.Remove(_settingsWindow);
                            _settingsWindow = null;
                        }
                        AbrirConfiguracoes_Click(null, new RoutedEventArgs());

                        // Verifica se há workspaces salvos no Google Drive em outro computador
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var (cloudFiles, _) = await GoogleDriveCloudClient.ListarWorkspacesNaNuvemAsync(_settings);
                                if (cloudFiles.Count > 0)
                                {
                                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                                    {
                                        if (_settings.AutoDownloadCloudWorkspaces)
                                        {
                                            _ = Task.Run(async () =>
                                            {
                                                var (baixados, _) = await GoogleDriveCloudClient.BaixarTodosWorkspacesDaNuvemAsync(_settings);
                                                if (baixados.Count > 0)
                                                {
                                                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                                                    {
                                                        MesclarWorkspacesDaNuvem(baixados);
                                                        ToastNotification.Mostrar(Workspace, $"☁ {baixados.Count} workspace(s) baixados do Google Drive!", "success");
                                                    });
                                                }
                                            });
                                        }
                                        else
                                        {
                                            MostrarModalDownloadWorkspacesNuvem(cloudFiles);
                                        }
                                    });
                                }
                            }
                            catch { }
                        });
                    }
                    else
                    {
                        ToastNotification.Mostrar(Workspace, $"Falha ao conectar com o Google: {erro}", "danger");
                    }
                }
                catch (Exception ex)
                {
                    ToastNotification.Mostrar(Workspace, $"Erro: {ex.Message}", "danger");
                }
            };

            _settingsWindow.OnGoogleLogoutRequested += () =>
            {
                _settings.GoogleOAuthAccessToken = "";
                _settings.GoogleOAuthRefreshToken = "";
                _settings.GoogleOAuthTokenExpiryUtc = "";
                _settings.GoogleUserEmail = "";
                _settings.GoogleDriveCloudSyncEnabled = false;
                SalvarTodosWorkspacesNoDisco();

                ToastNotification.Mostrar(Workspace, "Conta do Google Drive desconectada.", "info");

                if (_settingsWindow != null)
                {
                    MainRootGrid.Children.Remove(_settingsWindow);
                    _settingsWindow = null;
                }
                AbrirConfiguracoes_Click(null, new RoutedEventArgs());
            };

            _settingsWindow.OnCloudAutoSyncChanged += (enabled) =>
            {
                _settings.GoogleDriveCloudSyncEnabled = enabled;
                SalvarTodosWorkspacesNoDisco();
                ToastNotification.Mostrar(Workspace, enabled ? "Sincronização em nuvem ativada" : "Sincronização em nuvem desativada", "info");
            };

            _settingsWindow.OnAutoDownloadCloudChanged += (autoDownload) =>
            {
                _settings.AutoDownloadCloudWorkspaces = autoDownload;
                _settings.RememberCloudDownloadChoice = autoDownload;
                SalvarTodosWorkspacesNoDisco();
                ToastNotification.Mostrar(Workspace, autoDownload
                    ? "Download automático de workspaces da nuvem ativado"
                    : "Perguntar antes de baixar workspaces da nuvem ao logar", "info");
            };

            _settingsWindow.OnUploadCloudRequested += async () =>
            {
                try
                {
                    ToastNotification.Mostrar(Workspace, "Enviando workspace para o Google Drive...", "info");
                    var ws = ObterWorkspaceAtual();
                    var (ok, fileId, msg) = await GoogleDriveCloudClient.UploadWorkspaceAsync(_settings, ws);
                    if (ok)
                    {
                        _settings.LastCloudSyncUtc = DateTime.UtcNow.ToString("o");
                        SalvarTodosWorkspacesNoDisco();
                        ToastNotification.Mostrar(Workspace, $"☁ Workspace '{ws.Name}' enviado com sucesso para a nuvem!", "success");
                    }
                    else
                    {
                        ToastNotification.Mostrar(Workspace, $"Falha no upload para nuvem: {msg}", "danger");
                    }
                }
                catch (Exception ex)
                {
                    ToastNotification.Mostrar(Workspace, $"Erro no upload: {ex.Message}", "danger");
                }
            };

            _settingsWindow.OnDownloadCloudRequested += async () =>
            {
                try
                {
                    ToastNotification.Mostrar(Workspace, "Verificando arquivos no Google Drive...", "info");
                    var ws = ObterWorkspaceAtual();
                    var (cloudInfo, checkErr) = await GoogleDriveCloudClient.VerificarVersaoNuvemAsync(_settings, ws);

                    if (cloudInfo != null && cloudInfo.ExistsInCloud)
                    {
                        var (cloudWs, dlErr) = await GoogleDriveCloudClient.BaixarWorkspaceDaNuvemAsync(_settings, cloudInfo.FileId);
                        if (cloudWs != null)
                        {
                            int idx = _settings.Workspaces.FindIndex(w => w.Id == cloudWs.Id);
                            if (idx >= 0) _settings.Workspaces[idx] = cloudWs;
                            else _settings.Workspaces.Add(cloudWs);
                            SalvarTodosWorkspacesNoDisco();
                            CarregarJanelasDoWorkspace(cloudWs);
                            ToastNotification.Mostrar(Workspace, $"✓ Workspace '{cloudWs.Name}' atualizado com a versão da nuvem!", "success");
                            return;
                        }
                        else
                        {
                            ToastNotification.Mostrar(Workspace, $"Falha ao baixar da nuvem: {dlErr}", "danger");
                            return;
                        }
                    }

                    // Se não encontrou pelo ID específico do workspace atual, busca todos os workspaces salvos no Google Drive
                    var (baixados, listErr) = await GoogleDriveCloudClient.BaixarTodosWorkspacesDaNuvemAsync(_settings);
                    if (baixados.Count > 0)
                    {
                        MesclarWorkspacesDaNuvem(baixados);
                        ToastNotification.Mostrar(Workspace, $"✓ {baixados.Count} workspace(s) baixado(s) do Google Drive com sucesso!", "success");
                    }
                    else
                    {
                        string msg = !string.IsNullOrEmpty(listErr)
                            ? $"Aviso: {listErr}"
                            : "Nenhum arquivo de workspace (.mdiws) foi encontrado na sua pasta do Google Drive.";
                        ToastNotification.Mostrar(Workspace, msg, "warning");
                    }
                }
                catch (Exception ex)
                {
                    ToastNotification.Mostrar(Workspace, $"Erro no download: {ex.Message}", "danger");
                }
            };

            _settingsWindow.OnClosed += () =>
            {
                if (_settingsWindow != null)
                {
                    MainRootGrid.Children.Remove(_settingsWindow);
                    _settingsWindow = null;
                }
                AtualizarSuspensaoNavegadores();
            };

            MainRootGrid.Children.Add(_settingsWindow);
            AtualizarSuspensaoNavegadores();
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
            TextFormatData? initialFormat = null,
            List<GalleryItemData>? initialGallery = null,
            string customColor = "",
            int spreadsheetRows = 12,
            int spreadsheetCols = 6,
            List<SpreadsheetCellData>? initialSpreadsheetCells = null,
            List<ConditionalFormatRuleData>? initialConditionalRules = null,
            string criticalityLevel = "none",
            List<RichTextLineData>? initialRichLines = null)
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
                initialFormat,
                initialGallery,
                customColor,
                spreadsheetRows,
                spreadsheetCols,
                initialSpreadsheetCells,
                initialConditionalRules,
                criticalityLevel,
                initialRichLines);

            if (!_trocandoWorkspace && !_isRestoringUndoRedo)
            {
                RegistrarEstadoUndo();
            }

            _ativasJanelas.Add(win);
            win.OnClosed += () =>
            {
                if (!_trocandoWorkspace && !_isRestoringUndoRedo)
                {
                    RegistrarEstadoUndo();
                }
                _selectedWindows.Remove(win);
                _dragGroupStartPositions.Remove(win);
                RemoverConexoesDaJanela(win);
                if (!_trocandoWorkspace)
                {
                    _ativasJanelas.Remove(win);
                    AtualizarDimensoesCanvasWorkspace();
                }
            };
            win.OnBoundsChanged += (_) => AtualizarDimensoesCanvasWorkspace();
            win.OnConnectRequested += (janelaOrigem) => IniciarConexaoAPartirDeJanela(janelaOrigem);
            win.OnWindowClicked += (janelaClicada) => AoClicarEmJanela(janelaClicada);

            win.OnWindowSelectionRequested += (w, modifiers) =>
            {
                bool ctrl = modifiers.HasFlag(KeyModifiers.Control);
                if (ctrl)
                {
                    if (w.IsSelected) DeselecionarJanela(w);
                    else SelecionarJanela(w);
                }
                else
                {
                    if (!w.IsSelected)
                    {
                        SelecionarApenasJanela(w);
                    }
                }
            };

            win.OnWindowSingleClickedWithoutDrag += (w) =>
            {
                if (_selectedWindows.Count > 1)
                {
                    SelecionarApenasJanela(w);
                }
            };

            win.OnGroupDragStarted += (leadWin) =>
            {
                _dragGroupStartPositions.Clear();
                if (!leadWin.IsSelected)
                {
                    SelecionarApenasJanela(leadWin);
                }

                foreach (var w in _selectedWindows)
                {
                    double wx = Canvas.GetLeft(w);
                    double wy = Canvas.GetTop(w);
                    if (double.IsNaN(wx)) wx = 0;
                    if (double.IsNaN(wy)) wy = 0;
                    _dragGroupStartPositions[w] = new Point(wx, wy);
                }
            };

            win.OnGroupDragDelta += (leadWin, rawDiff) =>
            {
                if (_dragGroupStartPositions.Count == 0) return;

                double minX = double.MaxValue;
                double minY = double.MaxValue;
                foreach (var pos in _dragGroupStartPositions.Values)
                {
                    if (pos.X < minX) minX = pos.X;
                    if (pos.Y < minY) minY = pos.Y;
                }

                double clampedDiffX = Math.Max(-minX, rawDiff.X);
                double clampedDiffY = Math.Max(-minY, rawDiff.Y);

                foreach (var kvp in _dragGroupStartPositions)
                {
                    var w = kvp.Key;
                    var start = kvp.Value;
                    w.MoverPara(start.X + clampedDiffX, start.Y + clampedDiffY);
                }
            };

            win.OnGroupDragCompleted += (leadWin) =>
            {
                _dragGroupStartPositions.Clear();
                AtualizarDimensoesCanvasWorkspace();
            };

            win.OnChecklistRowClicked += (checklistWin, row) =>
            {
                if (_isPickingCalendarTarget)
                {
                    AoSelecionarAlvoParaCalendario(checklistWin, row.RowId);
                }
            };
            win.OnInteractionStarted += (_) =>
            {
                _snapshotBeforeInteraction = CapturarSnapshotWorkspaceJson();
            };
            win.OnInteractionCompleted += (_) =>
            {
                if (!string.IsNullOrEmpty(_snapshotBeforeInteraction))
                {
                    string current = CapturarSnapshotWorkspaceJson();
                    if (current != _snapshotBeforeInteraction)
                    {
                        if (_undoStack.Count == 0 || _undoStack.Peek() != _snapshotBeforeInteraction)
                        {
                            _undoStack.Push(_snapshotBeforeInteraction);
                            if (_undoStack.Count > 35)
                            {
                                var trimmed = _undoStack.Take(35).Reverse().ToList();
                                _undoStack.Clear();
                                foreach (var s in trimmed) _undoStack.Push(s);
                            }
                        }
                        _redoStack.Clear();
                    }
                    _snapshotBeforeInteraction = null;
                }
            };
            win.OnRequestOpenRowNotepad += (checklistWin, row) => AbrirOuFocarNotaDaLinha(checklistWin, row);
            win.OnRequestLinkRowWindow += (checklistWin, row) => VincularOuFocarJanelaDaLinha(checklistWin, row);
            win.OnRequestOpenGalleryImage += (galleryWin, card) => AbrirOuFocarImagemDaGaleria(galleryWin, card);
            win.OnRequestSetWorkspaceBackground += (filePath) => AlternarFundoWorkspace(filePath);
            win.OnSaveRequested += (janela) => SalvarJanelaNoCatalogo(janela, mostrarToast: true);
            win.OnRequestRadialMenu += (janelaAlvo, anchor) => AbrirMenuCircularJanela(janelaAlvo, anchor);
            win.OnColorChanged += (janelaAlvo, cor) =>
            {
                RegistrarEstadoUndo();
                SalvarTodosWorkspacesNoDisco();
            };
            win.OnWindowStateChanged += (janelaAlvo) =>
            {
                if (!_trocandoWorkspace && !_isRestoringUndoRedo)
                {
                    RegistrarEstadoUndo();
                    SalvarTodosWorkspacesNoDisco();
                }
            };
            win.OnRequestPropagateColor += (janelaAlvo, cor) => PropagarCorParaConectadas(janelaAlvo, cor);

            AppTaskBar.RegistrarJanela(win, title);
            Workspace.Children.Add(win);
            ZIndexManager.BringToFront(win);
            AtualizarDimensoesCanvasWorkspace();
            return win;
        }
    }
}
