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
        private int _contadorPlanilhas = 0;

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

            Loaded += (s, e) =>
            {
                _settings = SettingsManager.Carregar();
                _widgetSizePreset = string.IsNullOrWhiteSpace(_settings.WidgetSizePreset) ? "normal" : _settings.WidgetSizePreset;

                var wsAtual = ObterWorkspaceAtual();
                _workspaceBgImagePath = wsAtual.BackgroundImagePath ?? string.Empty;
                _workspaceZoom = wsAtual.WorkspaceZoom > 0 ? wsAtual.WorkspaceZoom : 1.0;

                AplicarTema(_settings.IsDarkMode);
                AplicarEscalaLayout(_settings.LayoutScale);
                AplicarTamanhoJanelaPrincipal(_settings.MainWindowSizePreset);
                AtualizarAbasWorkspaces();
                CarregarJanelasDoWorkspace(wsAtual);
                AplicarZoomWorkspace(_workspaceZoom);
            };

            Closing += OnMainWindowClosing;
        }

        // ==================== ZOOM OUT / IN E PAN DO WORKSPACE ====================

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
                if (e.Key == Key.Escape && _modoConexaoAtivo)
                {
                    CancelarModoConexao();
                    e.Handled = true;
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
                ToastNotification.Mostrar(Workspace, $"🔗 \"{origem.WindowTitle}\" conectado a \"{destino.WindowTitle}\"!", "success");
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
            if (_modoConexaoAtivo)
            {
                BtnHudConnect.Content = "✕ Cancelar Ligação";
                BtnHudConnect.Background = ThemePalette.Accent;
                BtnHudConnect.Foreground = Brushes.White;
                BtnHudConnect.BorderBrush = ThemePalette.AccentLight;
            }
            else
            {
                BtnHudConnect.Content = "🔗 Conectar";
                BtnHudConnect.Background = _isDarkMode ? ThemePalette.DarkButtonBg : ThemePalette.LightButtonBg;
                BtnHudConnect.Foreground = _isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;
                BtnHudConnect.BorderBrush = _isDarkMode ? ThemePalette.DarkBorder : ThemePalette.LightBorder;
            }
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

        private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
        {
            if (_saidaConfirmada)
            {
                return;
            }

            e.Cancel = true;
            MostrarModalConfirmacaoSaida();
        }

        private void MostrarModalConfirmacaoSaida()
        {
            ModalTitle.Text = "💾 Salvar antes de fechar?";
            ModalMessage.Text = "Deseja salvar as janelas abertas, fluxogramas e informações dos seus Workspaces antes de encerrar o aplicativo?";
            ModalInputBox.IsVisible = false;
            ModalButtonsPanel.Children.Clear();

            var btnCancelar = CriarBotaoModal("Cancelar", isPrimary: false, isDanger: false);
            btnCancelar.Click += (s, e) => ModalOverlay.IsVisible = false;

            var btnSairSemSalvar = CriarBotaoModal("🚪 Sair sem Salvar", isPrimary: false, isDanger: true);
            btnSairSemSalvar.Click += (s, e) =>
            {
                ModalOverlay.IsVisible = false;
                _saidaConfirmada = true;
                Close();
            };

            var btnSalvarESair = CriarBotaoModal("💾 Salvar e Sair", isPrimary: true, isDanger: false);
            btnSalvarESair.Click += (s, e) =>
            {
                ModalOverlay.IsVisible = false;
                SalvarTodosWorkspacesNoDisco();
                _saidaConfirmada = true;
                Close();
            };

            ModalButtonsPanel.Children.Add(btnCancelar);
            ModalButtonsPanel.Children.Add(btnSairSemSalvar);
            ModalButtonsPanel.Children.Add(btnSalvarESair);

            AplicarEstiloModal();
            ModalOverlay.IsVisible = true;
        }

        private Button CriarBotaoModal(string texto, bool isPrimary, bool isDanger)
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
                Content = texto,
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
            WorkspaceViewport.Background = workspaceBg;
            AplicarFundoWorkspace(workspaceBg);
            TopBar.Background = barBg;
            TopBar.BorderBrush = borderBrush;

            foreach (var menuItem in new[] { MenuTopWorkspaces, MenuTopNotas, MenuTopListas, MenuTopPlanilhas, MenuTopGaleria, MenuTopNavegador, MenuTopFluxograma, MenuTopJanelas, MenuTopPreferencias })
            {
                if (menuItem != null)
                {
                    menuItem.Foreground = textPrimary;
                }
            }

            BtnAddWorkspace.Background = btnBg;
            BtnAddWorkspace.Foreground = textPrimary;

            WorkspaceHud.Background = barBg;
            WorkspaceHud.BorderBrush = borderBrush;
            HudDivider.Background = borderBrush;

            foreach (var btn in new[] { BtnHudZoomOut, BtnHudZoomReset, BtnHudZoomIn })
            {
                btn.Background = btnBg;
                btn.Foreground = textPrimary;
                btn.BorderBrush = borderBrush;
            }
            AtualizarVisualBotaoConexaoHud();

            AppTaskBar.AplicarTema(isDarkMode);
            _settingsWindow?.AplicarTema(isDarkMode);
            AplicarEstiloModal();
            AtualizarAbasWorkspaces();

            foreach (var win in _ativasJanelas)
            {
                win.AplicarTema(isDarkMode);
            }

            foreach (var conn in _conexoesAtivas)
            {
                conn.AplicarTema(isDarkMode);
            }
        }

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
                    Content = ativo ? $"🗂 {ws.Name}" : $"💤 {ws.Name}",
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
                    : $"💤 {ws.Name} (Hibernado — 0 recursos visuais na RAM. Clique para ativar)");

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
                    $"Ativado \"{destino.Name}\" • \"{nomeAnterior}\" entrou em hibernação profunda (💤)",
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
            _contadorPlanilhas = 0;

            if (ws.SavedWindows != null && ws.SavedWindows.Count > 0)
            {
                foreach (var winData in ws.SavedWindows)
                {
                    if (winData.WindowType == "notepad") _contadorNotas++;
                    else if (winData.WindowType == "checklist") _contadorListas++;
                    else if (winData.WindowType == "browser") _contadorNavegadores++;
                    else if (winData.WindowType == "gallery") _contadorGalerias++;
                    else if (winData.WindowType == "image") _contadorImagens++;
                    else if (winData.WindowType == "spreadsheet") _contadorPlanilhas++;
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
                        winData.ConditionalRules);
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
            ModalTitle.Text = "➕ Criar Novo Workspace";
            ModalMessage.Text = "Cada Workspace possui suas próprias janelas, fluxograma, galerias e plano de fundo. Digite o nome:";
            ModalInputBox.Text = nomeSugerido;
            ModalInputBox.IsVisible = true;
            ModalButtonsPanel.Children.Clear();

            var btnCancelar = CriarBotaoModal("Cancelar", isPrimary: false, isDanger: false);
            btnCancelar.Click += (s, ev) => ModalOverlay.IsVisible = false;

            var btnCriar = CriarBotaoModal("Criar Workspace", isPrimary: true, isDanger: false);
            void ConfirmarCriacao()
            {
                string nome = (ModalInputBox.Text ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(nome))
                {
                    nome = nomeSugerido;
                }

                ModalOverlay.IsVisible = false;
                SincronizarWorkspaceAtualEmMemoria();

                var novoWs = new WorkspaceData
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = nome
                };
                _settings.Workspaces.Add(novoWs);
                AlternarParaWorkspace(novoWs.Id);
                SalvarTodosWorkspacesNoDisco();
            }

            btnCriar.Click += (s, ev) => ConfirmarCriacao();
            ModalInputBox.KeyDown += OnModalInputKeyDown;

            void OnModalInputKeyDown(object? s, KeyEventArgs ev)
            {
                if (ev.Key == Key.Enter)
                {
                    ev.Handled = true;
                    ModalInputBox.KeyDown -= OnModalInputKeyDown;
                    ConfirmarCriacao();
                }
                else if (ev.Key == Key.Escape)
                {
                    ev.Handled = true;
                    ModalInputBox.KeyDown -= OnModalInputKeyDown;
                    ModalOverlay.IsVisible = false;
                }
            }

            ModalButtonsPanel.Children.Add(btnCancelar);
            ModalButtonsPanel.Children.Add(btnCriar);
            AplicarEstiloModal();
            ModalOverlay.IsVisible = true;
            ModalInputBox.Focus();
            ModalInputBox.SelectAll();
        }

        private void RenomearWorkspaceAtual_Click(object? sender, RoutedEventArgs e)
        {
            var wsAtual = ObterWorkspaceAtual();
            ModalTitle.Text = "✏ Renomear Workspace";
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
            ModalTitle.Text = "🗑 Excluir Workspace";
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

        private void MenuWorkspaces_SubmenuOpened(object? sender, RoutedEventArgs e)
        {
            var wsAtual = ObterWorkspaceAtual();
            MenuExcluirWorkspace.IsEnabled = _settings.Workspaces.Count > 1;
            MenuListaWorkspaces.Items.Clear();

            foreach (var ws in _settings.Workspaces)
            {
                bool isCurrent = ws.Id == wsAtual.Id;
                int qtdJanelas = isCurrent ? _ativasJanelas.Count : (ws.SavedWindows?.Count ?? 0);
                var item = new MenuItem
                {
                    Header = isCurrent
                        ? $"✓ 🗂 {ws.Name} (Ativo — {qtdJanelas} janela(s))"
                        : $"   💤 {ws.Name} (Hibernado — {qtdJanelas} janela(s) salvas)"
                };
                string id = ws.Id;
                item.Click += (s, ev) => AlternarParaWorkspace(id);
                MenuListaWorkspaces.Items.Add(item);
            }
        }

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
                SalvarTodosWorkspacesNoDisco();
                ToastNotification.Mostrar(Workspace, "Estado do Workspace salvo!", "success");
                return;
            }

            foreach (var win in _ativasJanelas)
            {
                SalvarJanelaNoCatalogo(win, mostrarToast: false);
            }

            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, $"Todas as {_ativasJanelas.Count} janelas foram salvas em {ObterWorkspaceAtual().Name}!", "success");
            }
        }

        private void MenuFluxograma_SubmenuOpened(object? sender, RoutedEventArgs e)
        {
            MenuCorLinhasFluxograma.Items.Clear();

            foreach (var cor in FlowchartConnectionVisual.PaletaCoresLinha)
            {
                bool isDefault = string.Equals(_settings.DefaultFlowchartLineColor, cor.Hex, StringComparison.OrdinalIgnoreCase);
                var item = new MenuItem
                {
                    Header = isDefault ? $"✓ {cor.Nome} (Padrão)" : $"   {cor.Nome}"
                };
                string hex = cor.Hex;
                item.Click += (s, ev) =>
                {
                    _settings.DefaultFlowchartLineColor = hex;
                    foreach (var conn in _conexoesAtivas)
                    {
                        conn.DefinirCor(hex);
                    }
                    SalvarTodosWorkspacesNoDisco();
                };
                MenuCorLinhasFluxograma.Items.Add(item);
            }
        }

        private void MenuModulo_SubmenuOpened(object? sender, RoutedEventArgs e)
        {
            var ws = ObterWorkspaceAtual();

            PreencherSubmenusModulo(ws, new[] { "notepad" }, MenuNotasMaisUsadas, MenuNotasSalvas);
            PreencherSubmenusModulo(ws, new[] { "checklist" }, MenuListasMaisUsadas, MenuListasSalvas);
            PreencherSubmenusModulo(ws, new[] { "spreadsheet" }, MenuPlanilhasMaisUsadas, MenuPlanilhasSalvas);
            PreencherSubmenusModulo(ws, new[] { "gallery", "image" }, MenuGaleriaMaisUsadas, MenuGaleriaSalvas);
            PreencherSubmenusModulo(ws, new[] { "browser" }, MenuNavegadorMaisUsadas, MenuNavegadorSalvos);
            PreencherSubmenusModulo(ws, null, MenuMaisUsadas, MenuJanelasSalvas);

            MenuCorJanelaAtiva.Items.Clear();
            var ativa = ZIndexManager.ActiveWindow ?? (_ativasJanelas.Count > 0 ? _ativasJanelas[^1] : null);
            MenuCorJanelaAtiva.IsEnabled = ativa != null;
            if (ativa != null)
            {
                foreach (var cor in DraggableWindow.PaletaCoresJanela)
                {
                    var item = new MenuItem { Header = cor.Nome };
                    string hex = cor.Hex;
                    item.Click += (s, ev) =>
                    {
                        ativa.DefinirCorJanela(hex);
                        SalvarTodosWorkspacesNoDisco();
                    };
                    MenuCorJanelaAtiva.Items.Add(item);
                }
            }
        }

        private void PreencherSubmenusModulo(
            WorkspaceData ws,
            string[]? filtroTipos,
            MenuItem menuMaisUsadas,
            MenuItem menuSalvas)
        {
            IEnumerable<WindowStateData> query = ws.SavedCatalog;
            if (filtroTipos != null && filtroTipos.Length > 0)
            {
                query = query.Where(x => filtroTipos.Contains(x.WindowType));
            }

            var listaFiltrada = query.ToList();

            menuMaisUsadas.Items.Clear();
            var maisUsadas = listaFiltrada
                .OrderByDescending(x => x.AccessCount)
                .ThenByDescending(x => x.LastUsedUtc)
                .Take(6)
                .ToList();

            if (maisUsadas.Count == 0)
            {
                menuMaisUsadas.Items.Add(new MenuItem { Header = "(Nenhum item salvo)", IsEnabled = false });
            }
            else
            {
                foreach (var item in maisUsadas)
                {
                    string icone = ObterIconeTipo(item.WindowType);
                    var mi = new MenuItem { Header = $"{icone} {item.Title} ({item.AccessCount}x)" };
                    string id = item.Id;
                    mi.Click += (s, ev) => AbrirJanelaDoCatalogo(id);
                    menuMaisUsadas.Items.Add(mi);
                }
            }

            menuSalvas.Items.Clear();
            var salvas = listaFiltrada
                .OrderByDescending(x => x.LastUsedUtc)
                .ToList();

            if (salvas.Count == 0)
            {
                menuSalvas.Items.Add(new MenuItem { Header = "(Nenhum item salvo)", IsEnabled = false });
            }
            else
            {
                foreach (var item in salvas)
                {
                    string icone = ObterIconeTipo(item.WindowType);
                    var mi = new MenuItem { Header = $"{icone} {item.Title}" };
                    string id = item.Id;
                    mi.Click += (s, ev) => AbrirJanelaDoCatalogo(id);
                    menuSalvas.Items.Add(mi);
                }

                menuSalvas.Items.Add(new Separator());
                var menuRemover = new MenuItem { Header = "🗑 Excluir dos Salvos" };
                foreach (var item in salvas)
                {
                    string icone = ObterIconeTipo(item.WindowType);
                    var delItem = new MenuItem { Header = $"✕ {icone} {item.Title}" };
                    string id = item.Id;
                    delItem.Click += (s, ev) => RemoverJanelaDoCatalogo(id);
                    menuRemover.Items.Add(delItem);
                }
                menuSalvas.Items.Add(menuRemover);
            }
        }

        private static string ObterIconeTipo(string windowType) => windowType switch
        {
            "checklist" => "☑",
            "spreadsheet" => "📊",
            "browser" => "🌐",
            "gallery" => "🖼",
            "image" => "🌄",
            _ => "📝"
        };

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
                saved.ConditionalRules);
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

        private void AbrirPlanilhaExemplo_Click(object? sender, RoutedEventArgs e)
        {
            _contadorPlanilhas++;
            double f = ObterFatorTamanhoWidget();
            double offsetX = 65 + ((_contadorPlanilhas - 1) * 32) % 280;
            double offsetY = 48 + ((_contadorPlanilhas - 1) * 32) % 180;

            var celulasExemplo = new List<SpreadsheetCellData>
            {
                new() { CellKey = "A1", RawValue = "Item" },
                new() { CellKey = "B1", RawValue = "Valor" },
                new() { CellKey = "C1", RawValue = "Status (SE)" },
                new() { CellKey = "A2", RawValue = "Vendas Jan" },
                new() { CellKey = "B2", RawValue = "150" },
                new() { CellKey = "C2", RawValue = "=SE(B2>=100; \"Aprovado\"; \"Reprovado\")" },
                new() { CellKey = "A3", RawValue = "Vendas Fev" },
                new() { CellKey = "B3", RawValue = "85" },
                new() { CellKey = "C3", RawValue = "=SE(B3>=100; \"Aprovado\"; \"Reprovado\")" },
                new() { CellKey = "A4", RawValue = "Vendas Mar" },
                new() { CellKey = "B4", RawValue = "210" },
                new() { CellKey = "C4", RawValue = "=SE(B4>=100; \"Aprovado\"; \"Reprovado\")" },
                new() { CellKey = "A5", RawValue = "Despesa Extra" },
                new() { CellKey = "B5", RawValue = "-45" },
                new() { CellKey = "C5", RawValue = "=SE(B5>=0; \"Positivo\"; \"Negativo\")" },
                new() { CellKey = "A7", RawValue = "SOMA Total" },
                new() { CellKey = "B7", RawValue = "=SOMA(B2:B5)" },
                new() { CellKey = "A8", RawValue = "MÉDIA" },
                new() { CellKey = "B8", RawValue = "=MEDIA(B2:B5)" },
                new() { CellKey = "A9", RawValue = "MÁXIMO" },
                new() { CellKey = "B9", RawValue = "=MAXIMO(B2:B5)" }
            };

            var regrasExemplo = new List<ConditionalFormatRuleData>
            {
                new() { Range = "B2:B9", Operator = ">=", Value1 = "100", StyleKey = "green" },
                new() { Range = "B2:B9", Operator = "<", Value1 = "0", StyleKey = "red" },
                new() { Range = "C2:C5", Operator = "contains", Value1 = "Aprovado", StyleKey = "green" },
                new() { Range = "C2:C5", Operator = "contains", Value1 = "Reprovado", StyleKey = "red" }
            };

            string titulo = $"Planilha Exemplo #{_contadorPlanilhas}";
            CriarJanelaInterna(
                titulo,
                offsetX,
                offsetY,
                Math.Round(640 * f),
                Math.Round(420 * f),
                "spreadsheet",
                "",
                null,
                null,
                1,
                null,
                null,
                null,
                null,
                "#059669",
                12,
                6,
                celulasExemplo,
                regrasExemplo);

            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, $"{titulo} (com SOMA, MEDIA, SE e Formatação Condicional) aberta!", "success");
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
                    CriarJanelaInterna($"🌄 {nome}", offsetX, offsetY, Math.Round(440 * f), Math.Round(340 * f), "image", localPath);
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

            string tituloImagem = string.IsNullOrWhiteSpace(card.Title)
                ? Path.GetFileNameWithoutExtension(card.FilePath)
                : card.Title;

            var existente = _ativasJanelas.FirstOrDefault(w =>
                w.WindowType == "image" &&
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

            _contadorImagens++;
            double f = ObterFatorTamanhoWidget();
            double offsetX = Math.Max(40, Canvas.GetLeft(galleryWin) + galleryWin.Width + 60);
            double offsetY = Math.Max(40, Canvas.GetTop(galleryWin));

            var imgWin = CriarJanelaInterna(
                $"🌄 {tituloImagem}",
                offsetX,
                offsetY,
                Math.Round(440 * f),
                Math.Round(340 * f),
                "image",
                card.FilePath);

            card.OpenImageWindow = imgWin;
            CriarOuFocarConexaoFluxograma(galleryWin, imgWin);

            imgWin.OnClosed += () =>
            {
                if (card.OpenImageWindow == imgWin)
                {
                    card.OpenImageWindow = null;
                }
            };

            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, $"Imagem \"{tituloImagem}\" aberta e conectada!", "info");
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

        private void AplicarFundoWorkspace(IBrush fallbackBg)
        {
            if (!string.IsNullOrWhiteSpace(_workspaceBgImagePath) && File.Exists(_workspaceBgImagePath))
            {
                try
                {
                    _workspaceBgBitmap?.Dispose();
                    _workspaceBgBitmap = new Bitmap(_workspaceBgImagePath);
                    Workspace.Background = new ImageBrush(_workspaceBgBitmap)
                    {
                        Stretch = Stretch.UniformToFill
                    };
                    return;
                }
                catch { }
            }

            _workspaceBgBitmap?.Dispose();
            _workspaceBgBitmap = null;
            Workspace.Background = fallbackBg;
        }

        private void AbrirNavegadorComUrl(string url, string sufixoTitulo = "")
        {
            _contadorNavegadores++;
            double f = ObterFatorTamanhoWidget();
            double offsetX = 50 + ((_contadorNavegadores - 1) * 32) % 260;
            double offsetY = 40 + ((_contadorNavegadores - 1) * 32) % 160;

            string titulo = string.IsNullOrWhiteSpace(sufixoTitulo)
                ? $"Navegador #{_contadorNavegadores}"
                : $"Navegador - {sufixoTitulo}";

            CriarJanelaInterna(titulo, offsetX, offsetY, Math.Round(660 * f), Math.Round(450 * f), "browser", url);

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

        private void OrganizarCascata_Click(object? sender, RoutedEventArgs e)
        {
            if (_ativasJanelas.Count == 0) return;

            double f = ObterFatorTamanhoWidget();
            double w = Math.Round(340 * f);
            double h = Math.Round(230 * f);

            for (int i = 0; i < _ativasJanelas.Count; i++)
            {
                var win = _ativasJanelas[i];
                double x = 36 + (i * 32) % Math.Max(100, Workspace.Bounds.Width - (w + 20));
                double y = 36 + (i * 32) % Math.Max(100, Workspace.Bounds.Height - (h + 20));
                win.SetPosicaoNormal(x, y, w, h);
                ZIndexManager.BringToFront(win);
            }
            AtualizarDimensoesCanvasWorkspace();
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
            AtualizarDimensoesCanvasWorkspace();
        }

        private void FecharTodasJanelas_Click(object? sender, RoutedEventArgs e)
        {
            if (_ativasJanelas.Count == 0) return;
            var copia = _ativasJanelas.ToList();
            foreach (var win in copia)
            {
                win.Fechar();
            }
            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, "Todas as janelas do Workspace foram fechadas.", "info");
            }
        }

        private void AlternarTemaRapido_Click(object? sender, RoutedEventArgs e)
        {
            _settings.IsDarkMode = !_isDarkMode;
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

        private void AbrirConfiguracoes_Click(object? sender, RoutedEventArgs e)
        {
            if (_settingsWindow != null)
            {
                ZIndexManager.BringToFront(_settingsWindow);
                return;
            }

            _settingsWindow = new SettingsWindow(Workspace, _settings);

            _settingsWindow.OnSettingsChanged += (isDark, notif, layoutScale, mainWinPreset, widgetPreset) =>
            {
                _settings.IsDarkMode = isDark;
                _settings.EnableNotifications = notif;
                _settings.LayoutScale = layoutScale;
                _settings.MainWindowSizePreset = mainWinPreset;
                _settings.WidgetSizePreset = widgetPreset;

                AplicarTema(isDark);
                AplicarEscalaLayout(layoutScale);
                AplicarTamanhoJanelaPrincipal(mainWinPreset);
                AplicarPresetJanelasInternas(widgetPreset);
                SalvarTodosWorkspacesNoDisco();
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
            TextFormatData? initialFormat = null,
            List<GalleryItemData>? initialGallery = null,
            string customColor = "",
            int spreadsheetRows = 12,
            int spreadsheetCols = 6,
            List<SpreadsheetCellData>? initialSpreadsheetCells = null,
            List<ConditionalFormatRuleData>? initialConditionalRules = null)
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
                initialConditionalRules);

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
            win.OnRequestOpenRowNotepad += (checklistWin, row) => AbrirOuFocarNotaDaLinha(checklistWin, row);
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
