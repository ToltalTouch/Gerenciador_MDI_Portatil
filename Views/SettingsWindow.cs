using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AvaloniaMdiApp
{
    public class SettingsWindow : Border
    {
        public event Action<bool, bool, double, string, string>? OnSettingsChanged;
        public event Action<string>? OnThemePresetChanged;
        public event Action<string>? OnWorkspaceBackgroundStyleChanged;
        public event Action<bool>? OnAutoUpdateChanged;
        public event Action? OnCheckUpdatesRequested;
        public event Action<bool, string>? OnGoogleDriveSettingsChanged;
        public event Action? OnTestGoogleDriveSyncRequested;
        public event Action? OnOpenGoogleDriveFolderRequested;
        public event Action<string, string>? OnGoogleLoginRequested;
        public event Action? OnGoogleLogoutRequested;
        public event Action? OnUploadCloudRequested;
        public event Action? OnDownloadCloudRequested;
        public event Action<bool>? OnCloudAutoSyncChanged;
        public event Action? OnClosed;

        private readonly AppSettings _settings;
        private readonly Border _modalCard;
        private readonly Border _sidebarBorder;
        private readonly Border _contentContainer;
        private readonly TextBlock _lblCategoryTitle;
        private readonly TextBlock _lblCategorySubtitle;
        private readonly Button _btnClose;
        private readonly Button _btnConcluido;

        private readonly ComboBox _cmbThemePreset;
        private readonly ComboBox _cmbBackgroundStyle;
        private readonly ToggleSwitch _notifToggle;
        private readonly ToggleSwitch _autoUpdateToggle;
        private readonly Button _btnCheckUpdateNow;
        private readonly Button _btnCreateShortcut;
        private readonly ComboBox _cmbLayoutScale;
        private readonly ComboBox _cmbMainWindowSize;
        private readonly ComboBox _cmbWidgetSize;

        private readonly ToggleSwitch _gdriveToggle;
        private readonly TextBox _txtGdriveFolder;
        private readonly Button _btnBrowseGdrive;
        private readonly Button _btnTestGdrive;
        private readonly Button _btnOpenGdrive;

        private readonly TextBox _txtClientId;
        private readonly TextBox _txtClientSecret;
        private readonly Button _btnLoginGoogle;
        private readonly Button _btnLogoutGoogle;
        private readonly Button _btnUploadCloud;
        private readonly Button _btnDownloadCloud;
        private readonly ToggleSwitch _cloudAutoSyncToggle;
        private readonly Border _advancedApiCard;

        private readonly Dictionary<string, (Button Button, Border Indicator, TextBlock Label)> _navItems = new();
        private readonly Dictionary<string, Control> _categoryPanels = new();
        private readonly ContentControl _activeContentHost = new();

        private readonly List<Border> _sectionCards = new();
        private readonly List<Border> _dividers = new();
        private readonly List<TextBlock> _sectionHeaders = new();
        private readonly List<TextBlock> _primaryLabels = new();
        private readonly List<TextBlock> _secondaryLabels = new();

        private string _activeCategory = "geral";
        private bool _suppressEvents;
        private bool _isDarkMode;

        public SettingsWindow(AppSettings settings, Canvas? workspace = null)
        {
            _settings = settings;
            _isDarkMode = settings.IsDarkMode;
            _suppressEvents = true;

            // Overlay em tela cheia (como a confirmação de encerramento)
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;
            Background = new SolidColorBrush(Color.FromArgb(185, 9, 13, 22));
            ZIndex = 9998;

            // Fechar ao clicar no backdrop (fora do card)
            PointerPressed += (s, e) =>
            {
                if (e.Source == this)
                {
                    Fechar();
                }
            };

            // Card Modal Centralizado estilo Windows Terminal Settings
            _modalCard = new Border
            {
                Width = 840,
                Height = 560,
                MaxHeight = 620,
                MaxWidth = 920,
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1),
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                BoxShadow = new BoxShadows(new BoxShadow
                {
                    Blur = 36,
                    Color = Color.FromArgb(160, 0, 0, 0),
                    OffsetY = 10
                })
            };

            var rootGrid = new Grid();
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition(215, GridUnitType.Pixel));
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            // ==================== COLUNA ESQUERDA: SIDEBAR ====================
            _sidebarBorder = new Border
            {
                BorderThickness = new Thickness(0, 0, 1, 0)
            };

            var sidebarDock = new DockPanel();

            // Cabeçalho da Sidebar
            var sidebarHeader = new StackPanel
            {
                Margin = new Thickness(16, 18, 16, 14),
                Spacing = 4
            };
            var headerTitlePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerTitlePanel.Children.Add(new Avalonia.Controls.Shapes.Path
            {
                Data = AppIcons.Settings,
                Fill = ThemePalette.Accent,
                Width = 16,
                Height = 16,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center
            });
            headerTitlePanel.Children.Add(new TextBlock
            {
                Text = "Preferências",
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center
            });
            sidebarHeader.Children.Add(headerTitlePanel);

            var lblSidebarSub = new TextBlock
            {
                Text = "Configurações do Sistema",
                FontSize = 10.5,
                Margin = new Thickness(24, 0, 0, 0)
            };
            _secondaryLabels.Add(lblSidebarSub);
            sidebarHeader.Children.Add(lblSidebarSub);

            DockPanel.SetDock(sidebarHeader, Dock.Top);
            sidebarDock.Children.Add(sidebarHeader);

            // Rodapé da Sidebar
            var sidebarFooter = new Border
            {
                Padding = new Thickness(16, 10),
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            _dividers.Add(sidebarFooter);
            var lblFooter = new TextBlock
            {
                Text = "Gerenciador MDI Portátil",
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _secondaryLabels.Add(lblFooter);
            sidebarFooter.Child = lblFooter;

            DockPanel.SetDock(sidebarFooter, Dock.Bottom);
            sidebarDock.Children.Add(sidebarFooter);

            // Lista de Categorias
            var navStack = new StackPanel
            {
                Spacing = 4,
                Margin = new Thickness(10, 6, 10, 10)
            };

            navStack.Children.Add(CriarBotaoNavegacao("geral", AppIcons.Settings, "Geral", "Comportamento e atualizações"));
            navStack.Children.Add(CriarBotaoNavegacao("aparencia", AppIcons.Theme, "Aparência", "Tema, fontes e escalas"));
            navStack.Children.Add(CriarBotaoNavegacao("cloud", AppIcons.Globe, "Google Drive (Nuvem)", "Sincronização direta na nuvem"));
            navStack.Children.Add(CriarBotaoNavegacao("backup", AppIcons.Folder, "Backup Local", "Espelhamento em pastas"));

            sidebarDock.Children.Add(navStack);
            _sidebarBorder.Child = sidebarDock;
            Grid.SetColumn(_sidebarBorder, 0);
            rootGrid.Children.Add(_sidebarBorder);

            // ==================== COLUNA DIREITA: CONTEÚDO ====================
            _contentContainer = new Border();
            var rightGrid = new Grid();
            rightGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Header da Categoria
            rightGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star)); // Conteúdo rolante
            rightGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Rodapé com botão Concluído

            // Header da Categoria Selecionada
            var categoryHeaderGrid = new Grid
            {
                Margin = new Thickness(24, 18, 20, 14)
            };
            categoryHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            categoryHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var titleStack = new StackPanel { Spacing = 3 };
            _lblCategoryTitle = new TextBlock
            {
                FontSize = 18,
                FontWeight = FontWeight.Bold
            };
            _lblCategorySubtitle = new TextBlock
            {
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap
            };
            titleStack.Children.Add(_lblCategoryTitle);
            titleStack.Children.Add(_lblCategorySubtitle);
            Grid.SetColumn(titleStack, 0);
            categoryHeaderGrid.Children.Add(titleStack);

            _btnClose = new Button
            {
                Content = new Avalonia.Controls.Shapes.Path
                {
                    Data = AppIcons.Close,
                    Fill = ThemePalette.IconLightGray,
                    Width = 11,
                    Height = 11,
                    Stretch = Stretch.Uniform
                },
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(6),
                Background = Brushes.Transparent,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(_btnClose, "Fechar Configurações (Esc)");
            _btnClose.Click += (s, e) => Fechar();
            Grid.SetColumn(_btnClose, 1);
            categoryHeaderGrid.Children.Add(_btnClose);

            var headerWrapper = new StackPanel();
            headerWrapper.Children.Add(categoryHeaderGrid);
            headerWrapper.Children.Add(CriarDivisor());

            Grid.SetRow(headerWrapper, 0);
            rightGrid.Children.Add(headerWrapper);

            // ==================== CONTROLES DE CADA CATEGORIA ====================

            // 1. GERAL
            _notifToggle = new ToggleSwitch
            {
                IsChecked = settings.EnableNotifications,
                OnContent = "Ativo",
                OffContent = "Desativado",
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            _notifToggle.IsCheckedChanged += (s, e) => SalvarConfiguracoesGerais();

            _autoUpdateToggle = new ToggleSwitch
            {
                IsChecked = settings.AutoCheckUpdatesOnStartup,
                OnContent = "Ativo",
                OffContent = "Desativado",
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            _autoUpdateToggle.IsCheckedChanged += (s, e) =>
            {
                if (!_suppressEvents) OnAutoUpdateChanged?.Invoke(_autoUpdateToggle.IsChecked == true);
            };

            _btnCheckUpdateNow = new Button
            {
                Content = AppIcons.WithText(AppIcons.Refresh, "Verificar Agora", 12, 6, ThemePalette.DarkTextPrimary),
                Height = 30,
                Padding = new Thickness(14, 0),
                CornerRadius = new CornerRadius(6),
                Background = ThemePalette.DarkButtonBg,
                Foreground = ThemePalette.DarkTextPrimary,
                FontSize = 11.5,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _btnCheckUpdateNow.Click += (s, e) => OnCheckUpdatesRequested?.Invoke();

            _btnCreateShortcut = new Button
            {
                Content = AppIcons.WithText(AppIcons.Pin, "Criar Atalho", 12, 6, ThemePalette.DarkTextPrimary),
                Height = 30,
                Padding = new Thickness(14, 0),
                CornerRadius = new CornerRadius(6),
                Background = ThemePalette.DarkButtonBg,
                Foreground = ThemePalette.DarkTextPrimary,
                FontSize = 11.5,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _btnCreateShortcut.Click += (s, e) => CriarAtalhoAreaDeTrabalho();

            var panelGeral = new StackPanel { Spacing = 14, Margin = new Thickness(24, 16, 24, 20) };
            panelGeral.Children.Add(CriarCabecalhoSecao("NOTIFICAÇÕES E SISTEMA"));
            panelGeral.Children.Add(CriarCardAgrupado(
                CriarLinhaItem("Notificações em Tela", "Exibir mensagens suspensas de salvamento e alertas", _notifToggle),
                CriarDivisor(),
                CriarLinhaItem("Atalho do Aplicativo", "Criar atalho oficial com ícone na Área de Trabalho", _btnCreateShortcut)
            ));
            panelGeral.Children.Add(CriarCabecalhoSecao("ATUALIZAÇÕES DO PROGRAMA"));
            panelGeral.Children.Add(CriarCardAgrupado(
                CriarLinhaItem("Checar ao Iniciar", "Verificar se há novas versões no GitHub Releases ao abrir", _autoUpdateToggle),
                CriarDivisor(),
                CriarLinhaItem("Verificação Manual", "Checar agora se existe uma versão mais recente", _btnCheckUpdateNow)
            ));
            _categoryPanels["geral"] = panelGeral;

            // 2. APARÊNCIA E LAYOUT
            _cmbThemePreset = new ComboBox
            {
                ItemsSource = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(ThemePalette.TemasDisponiveis, t => t.Nome)),
                SelectedItem = ThemePalette.ObterNomePorKey(settings.ThemePreset),
                Width = 220,
                FontSize = 12,
                CornerRadius = new CornerRadius(6)
            };
            _cmbThemePreset.SelectionChanged += (s, e) =>
            {
                if (_suppressEvents) return;
                string nome = _cmbThemePreset.SelectedItem as string ?? "";
                string key = ThemePalette.ObterKeyPorNome(nome);
                _settings.ThemePreset = key;
                _settings.IsDarkMode = ThemePalette.EhTemaEscuro(key);
                ThemePalette.AplicarEsquema(key);
                AplicarTema(_settings.IsDarkMode);
                OnThemePresetChanged?.Invoke(key);
                SalvarConfiguracoesGerais();
            };

            _cmbBackgroundStyle = new ComboBox
            {
                ItemsSource = new[]
                {
                    "Quadriculado (Grade de Planilha)",
                    "Pontilhado",
                    "Liso (Cor Sólida)"
                },
                SelectedItem = settings.WorkspaceBackgroundStyle switch
                {
                    "dots" => "Pontilhado",
                    "solid" => "Liso (Cor Sólida)",
                    _ => "Quadriculado (Grade de Planilha)"
                },
                Width = 220,
                FontSize = 12,
                CornerRadius = new CornerRadius(6)
            };
            _cmbBackgroundStyle.SelectionChanged += (s, e) =>
            {
                if (_suppressEvents) return;
                string sel = _cmbBackgroundStyle.SelectedItem as string ?? "";
                string style = sel switch
                {
                    "Pontilhado" => "dots",
                    "Liso (Cor Sólida)" => "solid",
                    _ => "grid"
                };
                _settings.WorkspaceBackgroundStyle = style;
                OnWorkspaceBackgroundStyleChanged?.Invoke(style);
            };

            _cmbLayoutScale = new ComboBox
            {
                ItemsSource = new[] { "80%", "90%", "100%", "110%", "125%", "150%" },
                SelectedItem = settings.LayoutScale switch
                {
                    0.80 => "80%",
                    0.90 => "90%",
                    1.10 => "110%",
                    1.25 => "125%",
                    1.50 => "150%",
                    _ => "100%"
                },
                Width = 120,
                FontSize = 12,
                CornerRadius = new CornerRadius(6)
            };
            _cmbLayoutScale.SelectionChanged += (s, e) => SalvarConfiguracoesGerais();

            _cmbMainWindowSize = new ComboBox
            {
                ItemsSource = new[] { "Padrão (1366x768)", "HD (1280x720)", "Full HD (1920x1080)", "Maximizado" },
                SelectedItem = settings.MainWindowSizePreset switch
                {
                    "hd" => "HD (1280x720)",
                    "fhd" => "Full HD (1920x1080)",
                    "maximized" => "Maximizado",
                    _ => "Padrão (1366x768)"
                },
                Width = 190,
                FontSize = 12,
                CornerRadius = new CornerRadius(6)
            };
            _cmbMainWindowSize.SelectionChanged += (s, e) => SalvarConfiguracoesGerais();

            _cmbWidgetSize = new ComboBox
            {
                ItemsSource = new[] { "Pequeno", "Normal", "Grande", "Compacto" },
                SelectedItem = settings.WidgetSizePreset switch
                {
                    "small" => "Pequeno",
                    "large" => "Grande",
                    "compact" => "Compacto",
                    _ => "Normal"
                },
                Width = 140,
                FontSize = 12,
                CornerRadius = new CornerRadius(6)
            };
            _cmbWidgetSize.SelectionChanged += (s, e) => SalvarConfiguracoesGerais();

            var panelAparencia = new StackPanel { Spacing = 14, Margin = new Thickness(24, 16, 24, 20) };
            panelAparencia.Children.Add(CriarCabecalhoSecao("TEMA E CORES"));
            panelAparencia.Children.Add(CriarCardAgrupado(
                CriarLinhaItem("Tema do Aplicativo", "Esquema visual completo de janelas, fundos e botões", _cmbThemePreset),
                CriarDivisor(),
                CriarLinhaItem("Fundo do Workspace", "Grade estilo planilha, pontilhado ou liso", _cmbBackgroundStyle)
            ));
            panelAparencia.Children.Add(CriarCabecalhoSecao("ESCALA E DIMENSÕES"));
            panelAparencia.Children.Add(CriarCardAgrupado(
                CriarLinhaItem("Escala da Interface", "Ajuste de zoom global (fontes, botões e barras)", _cmbLayoutScale),
                CriarDivisor(),
                CriarLinhaItem("Tamanho Padrão das Janelas", "Dimensão inicial ao criar notas, listas e planilhas", _cmbWidgetSize),
                CriarDivisor(),
                CriarLinhaItem("Tamanho da Janela Principal", "Resolução aplicada ao iniciar o programa", _cmbMainWindowSize)
            ));
            _categoryPanels["aparencia"] = panelAparencia;

            // 3. GOOGLE DRIVE (NUVEM OAUTH 2.0)
            bool isConnected = !string.IsNullOrWhiteSpace(settings.GoogleOAuthRefreshToken);

            var lblAccountBadge = new TextBlock
            {
                Text = isConnected ? $"✓ Conectado: {settings.GoogleUserEmail}" : "○ Não conectado",
                Foreground = isConnected ? Brushes.LimeGreen : ThemePalette.IconLightGray,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };

            _btnLogoutGoogle = new Button
            {
                Content = AppIcons.WithText(AppIcons.Trash, "Desconectar", 11, 4, ThemePalette.Danger),
                Height = 28,
                Padding = new Thickness(10, 0),
                CornerRadius = new CornerRadius(6),
                Background = ThemePalette.DarkButtonBg,
                Foreground = ThemePalette.Danger,
                FontSize = 11,
                VerticalContentAlignment = VerticalAlignment.Center,
                IsVisible = isConnected
            };
            _btnLogoutGoogle.Click += (s, e) => OnGoogleLogoutRequested?.Invoke();

            var accountStatusRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            accountStatusRow.Children.Add(lblAccountBadge);
            accountStatusRow.Children.Add(_btnLogoutGoogle);

            // Campos de API personalizados (opcional para quem quiser trocar o Client ID padrão)
            _txtClientId = new TextBox
            {
                Text = settings.GoogleOAuthClientId,
                Watermark = "Padrão embutido ativo (ou cole seu Client ID)",
                Width = 260,
                FontSize = 11,
                CornerRadius = new CornerRadius(6)
            };
            _txtClientSecret = new TextBox
            {
                Text = settings.GoogleOAuthClientSecret,
                Watermark = "Padrão embutido ativo (ou cole seu Secret)",
                PasswordChar = '•',
                Width = 260,
                FontSize = 11,
                CornerRadius = new CornerRadius(6)
            };

            _btnLoginGoogle = new Button
            {
                Content = AppIcons.WithText(AppIcons.Globe, "Fazer Login com o Google", 13, 8, Brushes.White),
                Height = 36,
                Padding = new Thickness(22, 0),
                CornerRadius = new CornerRadius(8),
                Background = ThemePalette.Accent,
                Foreground = Brushes.White,
                FontSize = 12.5,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(_btnLoginGoogle, "Abre o navegador oficial do Google para autorizar o acesso com apenas 1 clique.");
            _btnLoginGoogle.Click += (s, e) =>
            {
                string cId = (_txtClientId.Text ?? "").Trim();
                string cSec = (_txtClientSecret.Text ?? "").Trim();
                OnGoogleLoginRequested?.Invoke(cId, cSec);
            };

            _cloudAutoSyncToggle = new ToggleSwitch
            {
                IsChecked = settings.GoogleDriveCloudSyncEnabled,
                OnContent = "Ativo",
                OffContent = "Desativado",
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                IsEnabled = isConnected
            };
            _cloudAutoSyncToggle.IsCheckedChanged += (s, e) =>
            {
                if (!_suppressEvents) OnCloudAutoSyncChanged?.Invoke(_cloudAutoSyncToggle.IsChecked == true);
            };

            _btnUploadCloud = new Button
            {
                Content = AppIcons.WithText(AppIcons.Upload, "Enviar para Nuvem", 11.5, 6, Brushes.White),
                Height = 30,
                Padding = new Thickness(14, 0),
                CornerRadius = new CornerRadius(6),
                Background = ThemePalette.Accent,
                Foreground = Brushes.White,
                FontSize = 11.5,
                FontWeight = FontWeight.SemiBold,
                VerticalContentAlignment = VerticalAlignment.Center,
                IsEnabled = isConnected
            };
            _btnUploadCloud.Click += (s, e) => OnUploadCloudRequested?.Invoke();

            _btnDownloadCloud = new Button
            {
                Content = AppIcons.WithText(AppIcons.Download, "Baixar da Nuvem", 11.5, 6, ThemePalette.DarkTextPrimary),
                Height = 30,
                Padding = new Thickness(14, 0),
                CornerRadius = new CornerRadius(6),
                Background = ThemePalette.DarkButtonBg,
                Foreground = ThemePalette.DarkTextPrimary,
                FontSize = 11.5,
                VerticalContentAlignment = VerticalAlignment.Center,
                IsEnabled = isConnected
            };
            _btnDownloadCloud.Click += (s, e) => OnDownloadCloudRequested?.Invoke();

            var cloudManualActions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            cloudManualActions.Children.Add(_btnDownloadCloud);
            cloudManualActions.Children.Add(_btnUploadCloud);

            var btnToggleAdvancedApi = new Button
            {
                Content = "⚙ Configurar Chaves de API Personalizadas (Opcional)",
                Background = Brushes.Transparent,
                Foreground = ThemePalette.Accent,
                FontSize = 11,
                Padding = new Thickness(0, 4),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            _advancedApiCard = new Border
            {
                IsVisible = false,
                Margin = new Thickness(0, 6, 0, 0)
            };
            _advancedApiCard.Child = CriarCardAgrupado(
                CriarLinhaItem("Client ID Personalizado", "Deixe em branco para usar a chave oficial embutida", _txtClientId),
                CriarDivisor(),
                CriarLinhaItem("Client Secret Personalizado", "Chave secreta do projeto do Google Cloud", _txtClientSecret)
            );

            btnToggleAdvancedApi.Click += (s, e) =>
            {
                _advancedApiCard.IsVisible = !_advancedApiCard.IsVisible;
            };

            var panelCloud = new StackPanel { Spacing = 14, Margin = new Thickness(24, 16, 24, 20) };
            panelCloud.Children.Add(CriarCabecalhoSecao("STATUS DA CONEXÃO EM NUVEM"));

            if (isConnected)
            {
                panelCloud.Children.Add(CriarCardAgrupado(
                    CriarLinhaItem("Conta Google Conectada", "Autenticado via Google OAuth 2.0 seguro", accountStatusRow),
                    CriarDivisor(),
                    CriarLinhaItem("Sincronização Automática", "Envia à nuvem no 'Salvar Tudo' e atualiza na inicialização se outro PC salvou", _cloudAutoSyncToggle),
                    CriarDivisor(),
                    CriarLinhaItem("Sincronização Manual", "Forçar envio do workspace atual ou baixar a versão do Google Drive", cloudManualActions)
                ));
            }
            else
            {
                var loginCardStack = new StackPanel
                {
                    Spacing = 14,
                    Margin = new Thickness(16)
                };
                loginCardStack.Children.Add(new TextBlock
                {
                    Text = "Conecte sua conta do Google com apenas 1 clique para sincronizar seus workspaces entre computadores automaticamente via internet (sem necessidade de instalar o Google Drive para Desktop).",
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 18
                });
                loginCardStack.Children.Add(_btnLoginGoogle);
                loginCardStack.Children.Add(btnToggleAdvancedApi);
                loginCardStack.Children.Add(_advancedApiCard);

                var loginCard = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    BorderThickness = new Thickness(1),
                    Child = loginCardStack
                };
                _sectionCards.Add(loginCard);
                panelCloud.Children.Add(loginCard);
            }
            _categoryPanels["cloud"] = panelCloud;

            // 4. BACKUP LOCAL EM PASTA
            _gdriveToggle = new ToggleSwitch
            {
                IsChecked = settings.GoogleDriveSyncEnabled,
                OnContent = "Ativo",
                OffContent = "Desativado",
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            _gdriveToggle.IsCheckedChanged += (s, e) => SalvarGoogleDriveConfig();

            string pastaGdrive = string.IsNullOrWhiteSpace(settings.GoogleDriveFolderPath)
                ? GoogleDriveSyncService.DetectarPastaPadraoGoogleDrive()
                : settings.GoogleDriveFolderPath;

            _txtGdriveFolder = new TextBox
            {
                Text = pastaGdrive,
                FontSize = 11.5,
                CornerRadius = new CornerRadius(6),
                VerticalAlignment = VerticalAlignment.Center,
                Width = 240
            };
            _txtGdriveFolder.LostFocus += (s, e) => SalvarGoogleDriveConfig();

            _btnBrowseGdrive = new Button
            {
                Content = AppIcons.WithText(AppIcons.Folder, "Procurar...", 11, 4, ThemePalette.DarkTextPrimary),
                Height = 28,
                Padding = new Thickness(10, 0),
                CornerRadius = new CornerRadius(6),
                Background = ThemePalette.DarkButtonBg,
                Foreground = ThemePalette.DarkTextPrimary,
                FontSize = 11,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _btnBrowseGdrive.Click += (s, e) => SelecionarPastaGoogleDrive();

            var folderPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            folderPanel.Children.Add(_txtGdriveFolder);
            folderPanel.Children.Add(_btnBrowseGdrive);

            _btnTestGdrive = new Button
            {
                Content = AppIcons.WithText(AppIcons.Save, "Fazer Backup Imediato", 11, 4, Brushes.White),
                Height = 28,
                Padding = new Thickness(12, 0),
                CornerRadius = new CornerRadius(6),
                Background = ThemePalette.Accent,
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _btnTestGdrive.Click += (s, e) => OnTestGoogleDriveSyncRequested?.Invoke();

            _btnOpenGdrive = new Button
            {
                Content = AppIcons.WithText(AppIcons.ExternalLink, "Abrir Pasta", 11, 4, ThemePalette.DarkTextPrimary),
                Height = 28,
                Padding = new Thickness(10, 0),
                CornerRadius = new CornerRadius(6),
                Background = ThemePalette.DarkButtonBg,
                Foreground = ThemePalette.DarkTextPrimary,
                FontSize = 11,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _btnOpenGdrive.Click += (s, e) => OnOpenGoogleDriveFolderRequested?.Invoke();

            var actionsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            actionsPanel.Children.Add(_btnOpenGdrive);
            actionsPanel.Children.Add(_btnTestGdrive);

            var panelBackup = new StackPanel { Spacing = 14, Margin = new Thickness(24, 16, 24, 20) };
            panelBackup.Children.Add(CriarCabecalhoSecao("ESPELHAMENTO EM PASTA DO SISTEMA"));
            panelBackup.Children.Add(CriarCardAgrupado(
                CriarLinhaItem("Espelhamento em Pasta", "Salvar cópia .mdiws na pasta ao Salvar Tudo", _gdriveToggle),
                CriarDivisor(),
                CriarLinhaItem("Diretório de Destino", "Pasta local do Google Drive, OneDrive ou pendrive", folderPanel),
                CriarDivisor(),
                CriarLinhaItem("Ações de Backup", "Criar cópia agora ou inspecionar diretório", actionsPanel)
            ));
            _categoryPanels["backup"] = panelBackup;

            // Host de Conteúdo Rolante
            var contentScroller = new ScrollViewer
            {
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = _activeContentHost
            };
            Grid.SetRow(contentScroller, 1);
            rightGrid.Children.Add(contentScroller);

            // Rodapé do Conteúdo (com Botão Concluído)
            var footerGrid = new Grid
            {
                Margin = new Thickness(24, 10, 24, 14)
            };
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            _btnConcluido = new Button
            {
                Content = "Concluído",
                Height = 32,
                Padding = new Thickness(24, 0),
                CornerRadius = new CornerRadius(6),
                Background = ThemePalette.Accent,
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _btnConcluido.Click += (s, e) => Fechar();
            Grid.SetColumn(_btnConcluido, 1);
            footerGrid.Children.Add(_btnConcluido);

            var footerWrapper = new StackPanel();
            footerWrapper.Children.Add(CriarDivisor());
            footerWrapper.Children.Add(footerGrid);

            Grid.SetRow(footerWrapper, 2);
            rightGrid.Children.Add(footerWrapper);

            _contentContainer.Child = rightGrid;
            Grid.SetColumn(_contentContainer, 1);
            rootGrid.Children.Add(_contentContainer);

            _modalCard.Child = rootGrid;
            Child = _modalCard;

            // Ativa categoria inicial
            SelecionarCategoria("geral");

            AplicarTema(settings.IsDarkMode);
            _suppressEvents = false;
        }

        private Button CriarBotaoNavegacao(string id, StreamGeometry icon, string titulo, string subtitulo)
        {
            var btn = new Button
            {
                Height = 44,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(6),
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };

            var itemGrid = new Grid();
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition(3, GridUnitType.Pixel));
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition(34, GridUnitType.Pixel));
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            var indicator = new Border
            {
                Width = 3,
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 6, 0, 6),
                Background = Brushes.Transparent
            };
            Grid.SetColumn(indicator, 0);
            itemGrid.Children.Add(indicator);

            var iconPath = new Avalonia.Controls.Shapes.Path
            {
                Data = icon,
                Fill = ThemePalette.IconLightGray,
                Width = 14,
                Height = 14,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(iconPath, 1);
            itemGrid.Children.Add(iconPath);

            var textStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            var lblTitulo = new TextBlock
            {
                Text = titulo,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold
            };
            textStack.Children.Add(lblTitulo);

            Grid.SetColumn(textStack, 2);
            itemGrid.Children.Add(textStack);

            btn.Content = itemGrid;
            btn.Click += (s, e) => SelecionarCategoria(id);

            _navItems[id] = (btn, indicator, lblTitulo);
            return btn;
        }

        private void SelecionarCategoria(string id)
        {
            _activeCategory = id;

            foreach (var kvp in _navItems)
            {
                bool isSelected = kvp.Key == id;
                kvp.Value.Indicator.Background = isSelected ? ThemePalette.Accent : Brushes.Transparent;
                kvp.Value.Button.Background = isSelected
                    ? (_isDarkMode ? Brush("#1E293B") : Brush("#E2E8F0"))
                    : Brushes.Transparent;
                kvp.Value.Label.Foreground = isSelected
                    ? (_isDarkMode ? Brushes.White : Brush("#0F172A"))
                    : (_isDarkMode ? ThemePalette.DarkTextSecondary : ThemePalette.LightTextSecondary);
            }

            switch (id)
            {
                case "geral":
                    _lblCategoryTitle.Text = "Configurações Gerais";
                    _lblCategorySubtitle.Text = "Gerencie notificações, atalhos do sistema e atualizações do programa.";
                    break;
                case "aparencia":
                    _lblCategoryTitle.Text = "Aparência e Layout";
                    _lblCategorySubtitle.Text = "Personalize o tema visual, a escala da interface e o tamanho padrão das janelas.";
                    break;
                case "cloud":
                    _lblCategoryTitle.Text = "Google Drive (Nuvem)";
                    _lblCategorySubtitle.Text = "Sincronização em nuvem segura para acessar e atualizar seus workspaces em qualquer computador.";
                    break;
                case "backup":
                    _lblCategoryTitle.Text = "Backup em Pasta";
                    _lblCategorySubtitle.Text = "Espelhe seus arquivos locais em diretórios sincronizados do Google Drive, OneDrive ou pendrives.";
                    break;
            }

            if (_categoryPanels.TryGetValue(id, out var panel))
            {
                _activeContentHost.Content = panel;
            }
        }

        private TextBlock CriarCabecalhoSecao(string titulo)
        {
            var tb = new TextBlock
            {
                Text = titulo,
                FontSize = 10.5,
                FontWeight = FontWeight.Bold,
                LetterSpacing = 0.8,
                Margin = new Thickness(4, 4, 0, -6)
            };
            _sectionHeaders.Add(tb);
            return tb;
        }

        private Control CriarLinhaItem(string titulo, string subtitulo, Control control)
        {
            var rowGrid = new Grid { Margin = new Thickness(14, 10) };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var lblTitulo = new TextBlock
            {
                Text = titulo,
                FontSize = 12.5,
                FontWeight = FontWeight.SemiBold
            };
            var lblSub = new TextBlock
            {
                Text = subtitulo,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            };
            _primaryLabels.Add(lblTitulo);
            _secondaryLabels.Add(lblSub);

            var textStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 2,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
                Children = { lblTitulo, lblSub }
            };

            Grid.SetColumn(textStack, 0);
            Grid.SetColumn(control, 1);
            rowGrid.Children.Add(textStack);
            rowGrid.Children.Add(control);
            return rowGrid;
        }

        private Border CriarDivisor()
        {
            var div = new Border { Height = 1, Margin = new Thickness(0) };
            _dividers.Add(div);
            return div;
        }

        private Border CriarCardAgrupado(params Control[] items)
        {
            var stack = new StackPanel { Orientation = Orientation.Vertical };
            foreach (var item in items)
            {
                stack.Children.Add(item);
            }
            var card = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Child = stack
            };
            _sectionCards.Add(card);
            return card;
        }

        public void AplicarTema(bool isDark)
        {
            _isDarkMode = isDark;

            // Backdrop
            Background = new SolidColorBrush(Color.FromArgb(185, 9, 13, 22));

            // Card principal
            _modalCard.Background = isDark ? Brush("#161F30") : Brush("#FFFFFF");
            _modalCard.BorderBrush = isDark ? Brush("#2C3B52") : Brush("#CBD5E1");

            // Sidebar
            _sidebarBorder.Background = isDark ? Brush("#0F172A") : Brush("#F8FAFC");
            _sidebarBorder.BorderBrush = isDark ? Brush("#1E293B") : Brush("#E2E8F0");

            // Conteúdo
            _contentContainer.Background = isDark ? Brush("#161F30") : Brush("#FFFFFF");

            // Textos principais
            _lblCategoryTitle.Foreground = isDark ? Brushes.White : Brush("#0F172A");
            _lblCategorySubtitle.Foreground = isDark ? ThemePalette.DarkTextSecondary : ThemePalette.LightTextSecondary;

            foreach (var lbl in _primaryLabels)
            {
                lbl.Foreground = isDark ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;
            }
            foreach (var lbl in _secondaryLabels)
            {
                lbl.Foreground = isDark ? ThemePalette.DarkTextSecondary : ThemePalette.LightTextSecondary;
            }
            foreach (var header in _sectionHeaders)
            {
                header.Foreground = isDark ? ThemePalette.DarkTextSecondary : ThemePalette.LightTextSecondary;
            }
            foreach (var card in _sectionCards)
            {
                card.Background = isDark ? Brush("#1C2638") : Brush("#F8FAFC");
                card.BorderBrush = isDark ? Brush("#2D3A4E") : Brush("#E2E8F0");
            }
            foreach (var div in _dividers)
            {
                div.Background = isDark ? Brush("#233044") : Brush("#E2E8F0");
            }

            SelecionarCategoria(_activeCategory);
        }

        private void SalvarConfiguracoesGerais()
        {
            if (_suppressEvents) return;

            string preset = _cmbThemePreset.SelectedItem as string ?? "Escuro Moderno";
            bool isDark = ThemePalette.EhTemaEscuro(preset);
            bool notif = _notifToggle.IsChecked == true;

            double scale = (_cmbLayoutScale.SelectedItem as string) switch
            {
                "80%" => 0.80,
                "90%" => 0.90,
                "110%" => 1.10,
                "125%" => 1.25,
                "150%" => 1.50,
                _ => 1.0
            };

            string mainWinPreset = (_cmbMainWindowSize.SelectedItem as string) switch
            {
                "HD (1280x720)" => "hd",
                "Full HD (1920x1080)" => "fhd",
                "Maximizado" => "maximized",
                _ => "default"
            };

            string widgetPreset = (_cmbWidgetSize.SelectedItem as string) switch
            {
                "Pequeno" => "small",
                "Grande" => "large",
                "Compacto" => "compact",
                _ => "normal"
            };

            AplicarTema(isDark);
            OnSettingsChanged?.Invoke(isDark, notif, scale, mainWinPreset, widgetPreset);
        }

        private void SalvarGoogleDriveConfig()
        {
            if (_suppressEvents) return;
            bool enabled = _gdriveToggle.IsChecked == true;
            string folder = (_txtGdriveFolder.Text ?? "").Trim();
            OnGoogleDriveSettingsChanged?.Invoke(enabled, folder);
        }

        private async void SelecionarPastaGoogleDrive()
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return;

                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
                {
                    Title = "Selecionar Pasta de Sincronização do Google Drive",
                    AllowMultiple = false
                });

                if (folders != null && folders.Count > 0)
                {
                    string path = folders[0].Path.LocalPath;
                    _txtGdriveFolder.Text = path;
                    SalvarGoogleDriveConfig();
                }
            }
            catch { }
        }

        private void CriarAtalhoAreaDeTrabalho()
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (string.IsNullOrWhiteSpace(exePath) || !System.IO.File.Exists(exePath)) return;

                string shortcutPath = System.IO.Path.Combine(desktop, "Gerenciador MDI Portátil.url");
                System.IO.File.WriteAllText(shortcutPath, $"[InternetShortcut]\nURL=file:///{exePath.Replace('\\', '/')}\nIconIndex=0\nIconFile={exePath}\n");
            }
            catch { }
        }

        public void Fechar()
        {
            OnClosed?.Invoke();
        }

        private static SolidColorBrush Brush(string hex) => new(Color.Parse(hex));
    }
}
