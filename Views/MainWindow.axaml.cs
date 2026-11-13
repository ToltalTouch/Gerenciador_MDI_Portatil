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

        // Estado de Panning (arrastar o fundo do Workspace)
        private bool _isPanningWorkspace = false;
        private Point _panPointerStart;
        private Vector _panOffsetStart;

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

            ModalOverlay.PropertyChanged += (s, e) =>
            {
                if (e.Property == Visual.IsVisibleProperty)
                {
                    bool modalAtivo = ModalOverlay.IsVisible;
                    foreach (var win in _ativasJanelas)
                    {
                        win.SetModalOverlaySuspended(modalAtivo);
                    }
                }
            };

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

        private void ToggleSidebar_Click(object? sender, RoutedEventArgs e)
        {
            _isSidebarExpanded = !_isSidebarExpanded;
            LeftSidebar.Width = _isSidebarExpanded ? 215 : 60;

            foreach (var lbl in new[]
            {
                LblSidebarTitle, LblSideNotas, LblSideListas,
                LblSidePlanilhas, LblSideGaleria, LblSideNavegador, LblSideFluxograma,
                LblSideSalvarTudo, LblSideExtrair, LblSidePreferencias
            })
            {
                if (lbl != null)
                {
                    lbl.IsVisible = _isSidebarExpanded;
                }
            }
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
            if (_undoStack.Count == 0) return;
            _redoStack.Push(CapturarSnapshotWorkspaceJson());
            string prev = _undoStack.Pop();
            AplicarSnapshotWorkspaceJson(prev);
            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, "Alteração desfeita (Ctrl+Z)", "info");
            }
        }

        private void RefazerWorkspace()
        {
            if (_redoStack.Count == 0) return;
            _undoStack.Push(CapturarSnapshotWorkspaceJson());
            string next = _redoStack.Pop();
            AplicarSnapshotWorkspaceJson(next);
            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, "Alteração refeita (Ctrl+Y)", "info");
            }
        }

        private void ConfigurarEventosWorkspaceZoomEPan()
        {
            WorkspaceViewport.SizeChanged += (s, e) => AtualizarDimensoesCanvasWorkspace();

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

            // Movimento do mouse no Workspace (linha de pré-visualização de conexão e Pan do fundo)
            Workspace.PointerPressed += (s, e) =>
            {
                if (e.Source == Workspace)
                {
                    if (_modoConexaoAtivo)
                    {
                        CancelarModoConexao();
                        e.Handled = true;
                        return;
                    }

                    var props = e.GetCurrentPoint(WorkspaceViewport);
                    if (props.Properties.IsLeftButtonPressed || props.Properties.IsMiddleButtonPressed)
                    {
                        _isPanningWorkspace = true;
                        _panPointerStart = props.Position;
                        _panOffsetStart = WorkspaceScroller.Offset;
                        Workspace.Cursor = new Cursor(StandardCursorType.Hand);
                        e.Pointer.Capture(Workspace);
                    }
                }
            };

            Workspace.PointerMoved += (s, e) =>
            {
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
            };

            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape && _isPickingCalendarTarget)
                {
                    CancelarModoVinculoCalendario();
                    e.Handled = true;
                    return;
                }

                if (e.Key == Key.Escape && _modoConexaoAtivo)
                {
                    CancelarModoConexao();
                    e.Handled = true;
                    return;
                }

                if ((e.KeyModifiers & KeyModifiers.Control) != 0 && !ModalOverlay.IsVisible)
                {
                    var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
                    if (focused is not TextBox)
                    {
                        if (e.Key == Key.Z)
                        {
                            DesfazerWorkspace();
                            e.Handled = true;
                            return;
                        }
                        if (e.Key == Key.Y)
                        {
                            RefazerWorkspace();
                            e.Handled = true;
                            return;
                        }
                    }
                }

                if (e.Key == Key.Delete && !ModalOverlay.IsVisible)
                {
                    var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
                    if (focused is TextBox)
                    {
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
            };
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
            CancelarModoConexao();
            CriarOuFocarConexaoFluxograma(origem, destino, mostrarToast: true);
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

        // ==================== CONFIRMAÇÃO DE SAÍDA E MODAIS ====================

        private WorkspaceData ObterWorkspaceAtual()
        {
            SettingsManager.GarantirWorkspaces(_settings);
            var ws = _settings.Workspaces.FirstOrDefault(w => w.Id == _settings.ActiveWorkspaceId)
                     ?? _settings.Workspaces[0];
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
            btnSalvarESair.Click += (s, e) =>
            {
                ModalOverlay.IsVisible = false;
                SalvarTodosWorkspacesNoDisco();
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

            foreach (var sideBtn in new[] { BtnSideNotas, BtnSideListas, BtnSidePlanilhas, BtnSideGaleria, BtnSideNavegador, BtnSideFluxograma, BtnSideSalvarTudo, BtnSideExtrair, BtnSidePreferencias })
            {
                if (sideBtn != null)
                {
                    sideBtn.Foreground = textPrimary;
                }
            }
            AtualizarTemaCalendario();

            BtnAddWorkspace.Background = btnBg;
            BtnAddWorkspace.Foreground = textPrimary;

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

            foreach (var ws in _settings.Workspaces)
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
                    ? $"Workspace ativo: {ws.Name}"
                    : $"{ws.Name} (Hibernado — 0 recursos visuais na RAM. Clique para ativar)");

                string targetId = ws.Id;
                btn.Click += (s, e) => AlternarParaWorkspace(targetId);
                WorkspaceTabsPanel.Children.Add(btn);
            }
        }

        private void AlternarParaWorkspace(string workspaceId)
        {
            var atual = ObterWorkspaceAtual();
            if (atual.Id == workspaceId) return;

            var destino = _settings.Workspaces.FirstOrDefault(w => w.Id == workspaceId);
            if (destino == null) return;

            string nomeAnterior = atual.Name;

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
            Workspace.Children.Clear();

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
                        winData.TextFormat,
                        winData.GalleryItems,
                        winData.CustomColor,
                        winData.SpreadsheetRows,
                        winData.SpreadsheetCols,
                        winData.SpreadsheetCells,
                        winData.ConditionalRules,
                        winData.CriticalityLevel,
                        winData.RichLines);
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

        private void RenomearWorkspaceAtual_Click(object? sender, RoutedEventArgs e)
        {
            var wsAtual = ObterWorkspaceAtual();
            ModalTitle.Text = "Renomear Workspace";
            ModalMessage.Text = $"Digite o novo nome para \"{wsAtual.Name}\":";
            ModalInputBox.Text = wsAtual.Name;
            ModalInputBox.IsVisible = true;
            ModalButtonsPanel.Children.Clear();

            var btnCancelar = CriarBotaoModal("Cancelar", isPrimary: false, isDanger: false);
            btnCancelar.Click += (s, ev) => ModalOverlay.IsVisible = false;

            var btnSalvar = CriarBotaoModal("Salvar Nome", isPrimary: true, isDanger: false);
            btnSalvar.Click += (s, ev) =>
            {
                string novoNome = (ModalInputBox.Text ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(novoNome))
                {
                    wsAtual.Name = novoNome;
                    AtualizarAbasWorkspaces();
                    SalvarTodosWorkspacesNoDisco();
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

        private void ExcluirWorkspaceAtual_Click(object? sender, RoutedEventArgs e)
        {
            if (_settings.Workspaces.Count <= 1)
            {
                ToastNotification.Mostrar(Workspace, "É necessário manter pelo menos um Workspace ativo.", "warning");
                return;
            }

            var wsAtual = ObterWorkspaceAtual();
            ModalTitle.Text = "Excluir Workspace";
            ModalMessage.Text = $"Tem certeza de que deseja excluir o workspace \"{wsAtual.Name}\" e todas as janelas dele?";
            ModalInputBox.IsVisible = false;
            ModalButtonsPanel.Children.Clear();

            var btnCancelar = CriarBotaoModal("Cancelar", isPrimary: false, isDanger: false);
            btnCancelar.Click += (s, ev) => ModalOverlay.IsVisible = false;

            var btnExcluir = CriarBotaoModal("Excluir Workspace", isPrimary: false, isDanger: true);
            btnExcluir.Click += (s, ev) =>
            {
                ModalOverlay.IsVisible = false;
                string nomeRemovido = wsAtual.Name;

                LimparJanelasVisuaisDoCanvas();
                _settings.Workspaces.Remove(wsAtual);

                var proximo = _settings.Workspaces[0];
                _settings.ActiveWorkspaceId = proximo.Id;
                _workspaceBgImagePath = proximo.BackgroundImagePath ?? string.Empty;
                _workspaceZoom = proximo.WorkspaceZoom > 0 ? proximo.WorkspaceZoom : 1.0;

                AplicarFundoWorkspace(_isDarkMode ? ThemePalette.DarkWorkspaceBg : ThemePalette.LightWorkspaceBg);
                CarregarJanelasDoWorkspace(proximo);
                AplicarZoomWorkspace(_workspaceZoom);
                AtualizarAbasWorkspaces();
                SalvarTodosWorkspacesNoDisco();

                if (_settings.EnableNotifications)
                {
                    ToastNotification.Mostrar(Workspace, $"Workspace \"{nomeRemovido}\" excluído.", "info");
                }
            };

            ModalButtonsPanel.Children.Add(btnCancelar);
            ModalButtonsPanel.Children.Add(btnExcluir);
            AplicarEstiloModal();
            ModalOverlay.IsVisible = true;
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
            if (!_settings.GoogleDriveCloudSyncEnabled || string.IsNullOrWhiteSpace(_settings.GoogleOAuthRefreshToken))
                return;

            _ = Task.Run(async () =>
            {
                try
                {
                    var ws = ObterWorkspaceAtual();
                    var (ok, fileId, msg) = await GoogleDriveCloudClient.UploadWorkspaceAsync(_settings, ws);
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (ok)
                        {
                            _settings.LastCloudSyncUtc = DateTime.UtcNow.ToString("o");
                            SalvarTodosWorkspacesNoDisco();
                            if (_settings.EnableNotifications)
                            {
                                ToastNotification.Mostrar(Workspace, $"☁ Workspace '{ws.Name}' sincronizado diretamente no Google Drive!", "success");
                            }
                        }
                    });
                }
                catch { }
            });
        }

        private async Task VerificarEAtualizarNuvemNaInicializacaoAsync()
        {
            if (!_settings.GoogleDriveCloudSyncEnabled || string.IsNullOrWhiteSpace(_settings.GoogleOAuthRefreshToken))
                return;

            try
            {
                var wsAtual = ObterWorkspaceAtual();
                var cloudInfo = await GoogleDriveCloudClient.VerificarVersaoNuvemAsync(_settings, wsAtual);

                if (cloudInfo != null && cloudInfo.ExistsInCloud && cloudInfo.IsCloudNewer)
                {
                    var cloudWs = await GoogleDriveCloudClient.BaixarWorkspaceDaNuvemAsync(_settings, wsAtual.Id);
                    if (cloudWs != null)
                    {
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
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
                            SalvarTodosWorkspacesNoDisco();

                            if (_settings.ActiveWorkspaceId == cloudWs.Id)
                            {
                                CarregarJanelasDoWorkspace(cloudWs);
                            }

                            ToastNotification.Mostrar(Workspace, $"☁ Versão mais recente do Google Drive carregada com sucesso!", "success");
                        });
                    }
                }
            }
            catch { }
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

        private void MenuModulo_SubmenuOpened(object? sender, RoutedEventArgs e)
        {
            var ws = ObterWorkspaceAtual();

            PreencherSubmenuSalvos(ws, new[] { "gallery", "image", "video" }, MenuGaleriaSalvas);
            PreencherSubmenuSalvos(ws, new[] { "browser" }, MenuNavegadorSalvos);
        }

        private void PreencherSubmenuSalvos(
            WorkspaceData ws,
            string[]? filtroTipos,
            MenuItem menuSalvas)
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

            if (row.OpenNoteWindow != null && _ativasJanelas.Contains(row.OpenNoteWindow))
            {
                row.OpenNoteWindow.AccessCount++;
                if (row.OpenNoteWindow.IsMinimized)
                {
                    row.OpenNoteWindow.ToggleMinimize();
                }
                CriarOuFocarConexaoFluxograma(checklistWin, row.OpenNoteWindow);
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
                CriarOuFocarConexaoFluxograma(checklistWin, existente);
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
                row.LinkedNoteFormat);

            row.OpenNoteWindow = noteWin;
            CriarOuFocarConexaoFluxograma(checklistWin, noteWin);

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

        private void VincularOuFocarJanelaDaLinha(DraggableWindow checklistWin, ChecklistRowState row)
        {
            var menu = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight };

            if (!string.IsNullOrWhiteSpace(row.LinkedWindowId))
            {
                var target = _ativasJanelas.FirstOrDefault(w => w.WindowId == row.LinkedWindowId);
                if (target != null)
                {
                    var miGo = new MenuItem { Header = $"Focar: {target.WindowTitle}" };
                    miGo.Click += (_, _) =>
                    {
                        if (target.IsMinimized) target.ToggleMinimize();
                        ZIndexManager.BringToFront(target);
                        CriarOuFocarConexaoFluxograma(checklistWin, target);
                    };
                    menu.Items.Add(miGo);
                }

                var miUnlink = new MenuItem { Header = "Remover vínculo" };
                miUnlink.Click += (_, _) =>
                {
                    row.LinkedWindowId = string.Empty;
                    checklistWin.AtualizarIndicadorVinculoLinha(row);
                };
                menu.Items.Add(miUnlink);
                menu.Items.Add(new Separator());
            }

            var outrasJanelas = _ativasJanelas.Where(w => w != checklistWin).ToList();
            if (outrasJanelas.Count == 0)
            {
                menu.Items.Add(new MenuItem { Header = "Nenhuma outra janela aberta", IsEnabled = false });
            }
            else
            {
                foreach (var win in outrasJanelas)
                {
                    var targetWin = win;
                    var miWin = new MenuItem { Header = $"Vincular a: {targetWin.WindowTitle}" };
                    miWin.Click += (_, _) =>
                    {
                        row.LinkedWindowId = targetWin.WindowId;
                        checklistWin.AtualizarIndicadorVinculoLinha(row);
                        CriarOuFocarConexaoFluxograma(checklistWin, targetWin);
                    };
                    menu.Items.Add(miWin);
                }
            }

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
            if (card.OpenImageWindow != null && _ativasJanelas.Contains(card.OpenImageWindow))
            {
                card.OpenImageWindow.AccessCount++;
                if (card.OpenImageWindow.IsMinimized)
                {
                    card.OpenImageWindow.ToggleMinimize();
                }
                CriarOuFocarConexaoFluxograma(galleryWin, card.OpenImageWindow);
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
                CriarOuFocarConexaoFluxograma(galleryWin, existente);
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
                card.FilePath);

            card.OpenImageWindow = mediaWin;
            CriarOuFocarConexaoFluxograma(galleryWin, mediaWin);

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
                    ToastNotification.Mostrar(Workspace, "Verificando versão no Google Drive...", "info");
                    var ws = ObterWorkspaceAtual();
                    var cloudInfo = await GoogleDriveCloudClient.VerificarVersaoNuvemAsync(_settings, ws);
                    if (cloudInfo == null || !cloudInfo.ExistsInCloud)
                    {
                        ToastNotification.Mostrar(Workspace, "Nenhum arquivo deste workspace encontrado na nuvem ainda.", "info");
                        return;
                    }

                    var cloudWs = await GoogleDriveCloudClient.BaixarWorkspaceDaNuvemAsync(_settings, ws.Id);
                    if (cloudWs != null)
                    {
                        int idx = _settings.Workspaces.FindIndex(w => w.Id == cloudWs.Id);
                        if (idx >= 0) _settings.Workspaces[idx] = cloudWs;
                        SalvarTodosWorkspacesNoDisco();
                        CarregarJanelasDoWorkspace(cloudWs);
                        ToastNotification.Mostrar(Workspace, $"✓ Workspace '{cloudWs.Name}' atualizado com a versão da nuvem!", "success");
                    }
                    else
                    {
                        ToastNotification.Mostrar(Workspace, "Não foi possível baixar o workspace da nuvem.", "danger");
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
            };

            MainRootGrid.Children.Add(_settingsWindow);
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

            _ativasJanelas.Add(win);
            win.OnClosed += () =>
            {
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
            win.OnChecklistRowClicked += (checklistWin, row) =>
            {
                if (_isPickingCalendarTarget)
                {
                    AoSelecionarAlvoParaCalendario(checklistWin, row.RowId);
                }
            };
            win.OnInteractionCompleted += (_) => RegistrarEstadoUndo();
            win.OnRequestOpenRowNotepad += (checklistWin, row) => AbrirOuFocarNotaDaLinha(checklistWin, row);
            win.OnRequestLinkRowWindow += (checklistWin, row) => VincularOuFocarJanelaDaLinha(checklistWin, row);
            win.OnRequestOpenGalleryImage += (galleryWin, card) => AbrirOuFocarImagemDaGaleria(galleryWin, card);
            win.OnRequestSetWorkspaceBackground += (filePath) => AlternarFundoWorkspace(filePath);
            win.OnSaveRequested += (janela) => SalvarJanelaNoCatalogo(janela, mostrarToast: true);

            AppTaskBar.RegistrarJanela(win, title);
            Workspace.Children.Add(win);
            ZIndexManager.BringToFront(win);
            AtualizarDimensoesCanvasWorkspace();
            return win;
        }
    }
}
