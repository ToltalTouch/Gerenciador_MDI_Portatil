using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;

namespace AvaloniaMdiApp
{
    /// <summary>
    /// Modal de Ajuda e Tutorial Interativo com dois modos:
    /// 1. Modo Completo: Guia explicativo com abas compactas e explicações detalhadas.
    /// 2. Modo Guiado na Tela: "Baixa a janela" para a parte inferior e exibe indicadores visuais
    ///    apontando exatamente onde clicar (barra lateral, engrenagem das janelas, menu circular,
    ///    vínculo de tarefas com o mouse e conexões de fluxograma).
    /// </summary>
    public class InteractiveTutorialModal : Border
    {
        public event Action? OnClosed;

        private class TutorialStep
        {
            public string TabLabel { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Subtitle { get; set; } = string.Empty;
            public StreamGeometry Icon { get; set; } = AppIcons.Info;
            public string IconColorHex { get; set; } = "#38BDF8";
            public string OndeClicar { get; set; } = string.Empty;
            public string OQueFazer { get; set; } = string.Empty;
            public string Dica { get; set; } = string.Empty;
            public int GuidedStepTarget { get; set; } = 0;
            public string GuidedButtonLabel { get; set; } = string.Empty;
        }

        private readonly MainWindow _mainWindow;
        private readonly bool _isDarkMode;
        private readonly List<TutorialStep> _steps = new();
        private int _currentStepIndex = 0;
        private int _currentGuidedStep = 0;
        private bool _isGuidedMode = false;

        private readonly Grid _rootGrid;
        private readonly Canvas _spotlightCanvas;
        private readonly Border _card;
        private readonly Border _bottomGuideCard;
        private readonly Border _pointerBadge;
        private TextBlock _lblPointerTitle = null!;
        private TextBlock _lblPointerText = null!;
        private TextBlock _lblPointerArrow = null!;

        // Controles do Card Completo
        private StackPanel _tabsPanel = null!;
        private TextBlock _lblStepIndicator = null!;
        private ProgressBar _progressBar = null!;
        private PathIcon _stepIcon = null!;
        private TextBlock _lblStepTitle = null!;
        private TextBlock _lblStepSubtitle = null!;
        private TextBlock _txtOndeClicar = null!;
        private TextBlock _txtOQueFazer = null!;
        private TextBlock _txtDica = null!;
        private Button _btnStartGuidedMode = null!;
        private Button _btnPrev = null!;
        private Button _btnNext = null!;
        private readonly List<Button> _tabButtons = new();

        // Controles do Card Inferior (Modo Guiado na Tela)
        private TextBlock _lblGuidedStepBadge = null!;
        private TextBlock _lblGuidedInstruction = null!;
        private Button _btnGuidedDoAction = null!;
        private Button _btnGuidedPrev = null!;
        private Button _btnGuidedNext = null!;
        private Button _btnGuidedExpandFull = null!;
        private Button _btnGuidedClose = null!;

        public InteractiveTutorialModal(bool isDarkMode, MainWindow mainWindow)
        {
            _isDarkMode = isDarkMode;
            _mainWindow = mainWindow;

            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;
            Background = new SolidColorBrush(Color.FromArgb(175, 9, 13, 22));
            ZIndex = 9996;

            _rootGrid = new Grid();

            // Canvas para o balão com seta apontando onde clicar
            _spotlightCanvas = new Canvas
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                IsHitTestVisible = false,
                ZIndex = 50
            };

            // Balão indicador visual (Ponteiro com Seta)
            _pointerBadge = CriarBalaoIndicador();
            _pointerBadge.IsVisible = false;
            _pointerBadge.IsHitTestVisible = false;
            _spotlightCanvas.Children.Add(_pointerBadge);
            _rootGrid.Children.Add(_spotlightCanvas);

            // Card Principal Completo (Modo A)
            _card = CriarCardCompleto();
            _rootGrid.Children.Add(_card);

            // Card Inferior Guiado (Modo B - "Baixa a Janela")
            _bottomGuideCard = CriarCardInferiorGuiado();
            _bottomGuideCard.IsVisible = false;
            _rootGrid.Children.Add(_bottomGuideCard);

            Child = _rootGrid;

            PointerPressed += (s, e) =>
            {
                if (e.Source == this && !_isGuidedMode)
                {
                    Fechar();
                }
            };

            IrParaPasso(0);
        }

        private Border CriarBalaoIndicador()
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.Parse("#F0090D16")),
                BorderBrush = new SolidColorBrush(Color.Parse("#38BDF8")),
                BorderThickness = new Thickness(1.8),
                Padding = new Thickness(14, 10, 14, 10),
                MaxWidth = 300,
                IsHitTestVisible = false,
                BoxShadow = new BoxShadows(new BoxShadow
                {
                    Blur = 24,
                    Spread = 1,
                    Color = Color.FromArgb(200, 56, 189, 248),
                    OffsetY = 4
                })
            };

            var stack = new StackPanel { Spacing = 3 };

            var topRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            _lblPointerArrow = new TextBlock
            {
                Text = "◀",
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.Parse("#38BDF8")),
                VerticalAlignment = VerticalAlignment.Center
            };
            _lblPointerTitle = new TextBlock
            {
                Text = "1. ONDE CLICAR",
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.Parse("#38BDF8")),
                VerticalAlignment = VerticalAlignment.Center
            };
            topRow.Children.Add(_lblPointerArrow);
            topRow.Children.Add(_lblPointerTitle);
            stack.Children.Add(topRow);

            _lblPointerText = new TextBlock
            {
                Text = "Clique aqui para abrir!",
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.White,
                LineHeight = 16
            };
            stack.Children.Add(_lblPointerText);

            border.Child = stack;
            return border;
        }

        private Border CriarCardCompleto()
        {
            var cardBg = _isDarkMode ? Color.Parse("#182234") : Color.Parse("#FFFFFF");
            var cardBorder = _isDarkMode ? Color.Parse("#2C3A50") : Color.Parse("#CBD5E1");

            var card = new Border
            {
                Width = 780,
                Height = 610,
                MaxWidth = 860,
                MaxHeight = 670,
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(cardBg),
                BorderBrush = new SolidColorBrush(cardBorder),
                BorderThickness = new Thickness(1),
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ZIndex = 20,
                BoxShadow = new BoxShadows(new BoxShadow
                {
                    Blur = 36,
                    Color = Color.FromArgb(180, 0, 0, 0),
                    OffsetY = 12
                })
            };

            InicializarPassosTutorial();

            var mainDock = new DockPanel();

            // HEADER
            var headerBorder = new Border
            {
                Background = new SolidColorBrush(_isDarkMode ? Color.Parse("#111827") : Color.Parse("#F8FAFC")),
                BorderBrush = new SolidColorBrush(cardBorder),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(20, 13, 20, 11)
            };
            DockPanel.SetDock(headerBorder, Dock.Top);

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var logoStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
            logoStack.Children.Add(new PathIcon
            {
                Data = AppIcons.Help,
                Width = 20,
                Height = 20,
                Foreground = new SolidColorBrush(Color.Parse("#38BDF8")),
                VerticalAlignment = VerticalAlignment.Center
            });
            var headerTitleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            headerTitleStack.Children.Add(new TextBlock
            {
                Text = "Guia Interativo de Uso",
                FontSize = 15,
                FontWeight = FontWeight.Bold,
                Foreground = _isDarkMode ? Brushes.White : new SolidColorBrush(Color.Parse("#0F172A"))
            });
            headerTitleStack.Children.Add(new TextBlock
            {
                Text = "Aprenda onde clicar e o que fazer em cada função do aplicativo",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse("#94A3B8"))
            });
            logoStack.Children.Add(headerTitleStack);
            Grid.SetColumn(logoStack, 0);
            headerGrid.Children.Add(logoStack);

            var btnClose = new Button
            {
                Content = "✕",
                Width = 28,
                Height = 28,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(14),
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.Parse("#94A3B8")),
                FontSize = 13,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            btnClose.Click += (s, e) => Fechar();
            Grid.SetColumn(btnClose, 2);
            headerGrid.Children.Add(btnClose);

            headerBorder.Child = headerGrid;
            mainDock.Children.Add(headerBorder);

            // TABS COMPACTAS COM SCROLL
            var tabsContainer = new Border
            {
                Background = new SolidColorBrush(_isDarkMode ? Color.Parse("#0E1626") : Color.Parse("#F1F5F9")),
                BorderBrush = new SolidColorBrush(cardBorder),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(10, 6)
            };
            DockPanel.SetDock(tabsContainer, Dock.Top);

            var tabsScroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            _tabsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            for (int i = 0; i < _steps.Count; i++)
            {
                int stepIdx = i;
                var step = _steps[i];
                var tabBtn = new Button
                {
                    Content = $"{stepIdx + 1}. {step.TabLabel}",
                    Height = 28,
                    Padding = new Thickness(12, 0),
                    CornerRadius = new CornerRadius(14),
                    FontSize = 11,
                    FontWeight = FontWeight.SemiBold,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(1),
                    BorderBrush = Brushes.Transparent,
                    Foreground = new SolidColorBrush(Color.Parse("#94A3B8")),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Cursor = new Cursor(StandardCursorType.Hand)
                };
                tabBtn.Click += (s, e) => IrParaPasso(stepIdx);
                _tabButtons.Add(tabBtn);
                _tabsPanel.Children.Add(tabBtn);
            }
            tabsScroll.Content = _tabsPanel;
            tabsContainer.Child = tabsScroll;
            mainDock.Children.Add(tabsContainer);

            // FOOTER
            var footerBorder = new Border
            {
                Background = new SolidColorBrush(_isDarkMode ? Color.Parse("#111827") : Color.Parse("#F8FAFC")),
                BorderBrush = new SolidColorBrush(cardBorder),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(20, 11)
            };
            DockPanel.SetDock(footerBorder, Dock.Bottom);

            var footerGrid = new Grid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var progressStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 3 };
            _lblStepIndicator = new TextBlock
            {
                Text = "Passo 1 de 6",
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse("#94A3B8"))
            };
            _progressBar = new ProgressBar
            {
                Minimum = 1,
                Maximum = _steps.Count,
                Value = 1,
                Width = 110,
                Height = 5,
                CornerRadius = new CornerRadius(2.5),
                Background = new SolidColorBrush(_isDarkMode ? Color.Parse("#233044") : Color.Parse("#E2E8F0")),
                Foreground = new SolidColorBrush(Color.Parse("#38BDF8"))
            };
            progressStack.Children.Add(_lblStepIndicator);
            progressStack.Children.Add(_progressBar);
            Grid.SetColumn(progressStack, 0);
            footerGrid.Children.Add(progressStack);

            var navStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            _btnPrev = new Button
            {
                Content = AppIcons.WithText(AppIcons.ArrowLeft, "Anterior", 10, 6, new SolidColorBrush(Color.Parse("#CBD5E1"))),
                Height = 30,
                Padding = new Thickness(14, 0),
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(_isDarkMode ? Color.Parse("#233044") : Color.Parse("#E2E8F0")),
                Foreground = _isDarkMode ? Brushes.White : new SolidColorBrush(Color.Parse("#1E293B")),
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            _btnPrev.Click += (s, e) => IrParaPasso(_currentStepIndex - 1);

            _btnNext = new Button
            {
                Content = AppIcons.WithText(AppIcons.ArrowRight, "Próximo", 10, 6, Brushes.White),
                Height = 30,
                Padding = new Thickness(14, 0),
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.Parse("#2563EB")),
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            _btnNext.Click += (s, e) =>
            {
                if (_currentStepIndex < _steps.Count - 1)
                {
                    IrParaPasso(_currentStepIndex + 1);
                }
                else
                {
                    Fechar();
                }
            };

            navStack.Children.Add(_btnPrev);
            navStack.Children.Add(_btnNext);
            Grid.SetColumn(navStack, 2);
            footerGrid.Children.Add(navStack);

            footerBorder.Child = footerGrid;
            mainDock.Children.Add(footerBorder);

            // CORPO
            var contentScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(22, 16, 22, 16)
            };

            var contentStack = new StackPanel { Spacing = 12 };

            var stepTitleGrid = new Grid();
            stepTitleGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            stepTitleGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            var iconCircle = new Border
            {
                Width = 42,
                Height = 42,
                CornerRadius = new CornerRadius(21),
                Background = new SolidColorBrush(_isDarkMode ? Color.Parse("#1E293B") : Color.Parse("#F1F5F9")),
                BorderBrush = new SolidColorBrush(_isDarkMode ? Color.Parse("#475569") : Color.Parse("#CBD5E1")),
                BorderThickness = new Thickness(1.5),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            _stepIcon = new PathIcon
            {
                Data = AppIcons.Note,
                Width = 18,
                Height = 18,
                Foreground = new SolidColorBrush(Color.Parse("#CBD5E1")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconCircle.Child = _stepIcon;
            Grid.SetColumn(iconCircle, 0);
            stepTitleGrid.Children.Add(iconCircle);

            var titleTextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _lblStepTitle = new TextBlock
            {
                Text = "Título do Passo",
                FontSize = 15,
                FontWeight = FontWeight.Bold,
                Foreground = _isDarkMode ? Brushes.White : new SolidColorBrush(Color.Parse("#0F172A"))
            };
            _lblStepSubtitle = new TextBlock
            {
                Text = "Subtítulo explicativo",
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.Parse("#94A3B8")),
                Margin = new Thickness(0, 2, 0, 0)
            };
            titleTextStack.Children.Add(_lblStepTitle);
            titleTextStack.Children.Add(_lblStepSubtitle);
            Grid.SetColumn(titleTextStack, 1);
            stepTitleGrid.Children.Add(titleTextStack);
            contentStack.Children.Add(stepTitleGrid);

            _txtOndeClicar = new TextBlock
            {
                FontSize = 11.5,
                LineHeight = 17,
                TextWrapping = TextWrapping.Wrap,
                Foreground = _isDarkMode ? new SolidColorBrush(Color.Parse("#E2E8F0")) : new SolidColorBrush(Color.Parse("#334155"))
            };
            contentStack.Children.Add(CriarCardSecao(AppIcons.Pin, "ONDE CLICAR", _txtOndeClicar));

            _txtOQueFazer = new TextBlock
            {
                FontSize = 11.5,
                LineHeight = 17,
                TextWrapping = TextWrapping.Wrap,
                Foreground = _isDarkMode ? new SolidColorBrush(Color.Parse("#E2E8F0")) : new SolidColorBrush(Color.Parse("#334155"))
            };
            contentStack.Children.Add(CriarCardSecao(AppIcons.Settings, "O QUE FAZER", _txtOQueFazer));

            _txtDica = new TextBlock
            {
                FontSize = 11,
                LineHeight = 16,
                TextWrapping = TextWrapping.Wrap,
                Foreground = _isDarkMode ? new SolidColorBrush(Color.Parse("#E2E8F0")) : new SolidColorBrush(Color.Parse("#334155"))
            };
            contentStack.Children.Add(CriarCardSecao(AppIcons.Sparkle, "DICA DE OURO", _txtDica));

            // BOTÃO: BAIXAR A JANELA E MOSTRAR ONDE CLICAR NA TELA (CENTRALIZADO)
            _btnStartGuidedMode = new Button
            {
                Height = 36,
                Padding = new Thickness(24, 0),
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.Parse("#0284C7")),
                Foreground = Brushes.White,
                FontSize = 11.5,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Hand),
                Margin = new Thickness(0, 8, 0, 4)
            };
            _btnStartGuidedMode.Click += (s, e) =>
            {
                var step = _steps[_currentStepIndex];
                IniciarModoGuiado(step.GuidedStepTarget);
            };
            contentStack.Children.Add(_btnStartGuidedMode);

            contentScroll.Content = contentStack;
            mainDock.Children.Add(contentScroll);

            card.Child = mainDock;
            return card;
        }

        private Border CriarCardInferiorGuiado()
        {
            var card = new Border
            {
                Width = 620,
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.Parse("#F4090D16")),
                BorderBrush = new SolidColorBrush(Color.Parse("#38BDF8")),
                BorderThickness = new Thickness(1.8),
                Padding = new Thickness(18, 12, 18, 12),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 24),
                ZIndex = 60,
                BoxShadow = new BoxShadows(new BoxShadow
                {
                    Blur = 28,
                    Spread = 1,
                    Color = Color.FromArgb(200, 0, 0, 0),
                    OffsetY = 6
                })
            };

            var stack = new StackPanel { Spacing = 8 };

            // Cabeçalho do Card Guiado
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var stepBadge = new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.Parse("#2563EB")),
                Padding = new Thickness(8, 2),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _lblGuidedStepBadge = new TextBlock
            {
                Text = "PASSO 1 DE 4: ONDE ABRIR UMA JANELA",
                FontSize = 10,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White
            };
            stepBadge.Child = _lblGuidedStepBadge;
            Grid.SetColumn(stepBadge, 0);
            headerGrid.Children.Add(stepBadge);

            var topActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            _btnGuidedExpandFull = new Button
            {
                Content = AppIcons.WithText(AppIcons.Book, "Guia Completo", 10, 5, new SolidColorBrush(Color.Parse("#CBD5E1"))),
                Height = 24,
                Padding = new Thickness(8, 0),
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.Parse("#1E293B")),
                BorderBrush = new SolidColorBrush(Color.Parse("#475569")),
                BorderThickness = new Thickness(1),
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            _btnGuidedExpandFull.Click += (s, e) => MostrarModoCompleto();

            _btnGuidedClose = new Button
            {
                Content = "✕",
                Width = 24,
                Height = 24,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(12),
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.Parse("#94A3B8")),
                FontSize = 11,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            _btnGuidedClose.Click += (s, e) => Fechar();

            topActions.Children.Add(_btnGuidedExpandFull);
            topActions.Children.Add(_btnGuidedClose);
            Grid.SetColumn(topActions, 1);
            headerGrid.Children.Add(topActions);

            stack.Children.Add(headerGrid);

            // Instrução
            _lblGuidedInstruction = new TextBlock
            {
                Text = "Instrução do que fazer na tela...",
                FontSize = 11.5,
                LineHeight = 16,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.White
            };
            stack.Children.Add(_lblGuidedInstruction);

            // Ações Guiadas
            var bottomActionsGrid = new Grid();
            bottomActionsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            bottomActionsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            bottomActionsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            _btnGuidedDoAction = new Button
            {
                Content = AppIcons.WithText(AppIcons.Sparkle, "Abrir Bloco de Notas Agora", 11, 6, Brushes.White),
                Height = 28,
                Padding = new Thickness(12, 0),
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.Parse("#059669")),
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            _btnGuidedDoAction.Click += (s, e) => ExecutarAcaoGuiadaAutomatica();
            Grid.SetColumn(_btnGuidedDoAction, 0);
            bottomActionsGrid.Children.Add(_btnGuidedDoAction);

            var navStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            _btnGuidedPrev = new Button
            {
                Content = AppIcons.WithText(AppIcons.ArrowLeft, "Anterior", 10, 6, new SolidColorBrush(Color.Parse("#CBD5E1"))),
                Height = 28,
                Padding = new Thickness(10, 0),
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.Parse("#1E293B")),
                Foreground = Brushes.White,
                FontSize = 11,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            _btnGuidedPrev.Click += (s, e) => IniciarModoGuiado(_currentGuidedStep - 1);

            _btnGuidedNext = new Button
            {
                Content = AppIcons.WithText(AppIcons.ArrowRight, "Próximo Passo", 10, 6, Brushes.White),
                Height = 28,
                Padding = new Thickness(12, 0),
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.Parse("#2563EB")),
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            _btnGuidedNext.Click += (s, e) =>
            {
                if (_currentGuidedStep < 3)
                {
                    IniciarModoGuiado(_currentGuidedStep + 1);
                }
                else
                {
                    Fechar();
                }
            };

            navStack.Children.Add(_btnGuidedPrev);
            navStack.Children.Add(_btnGuidedNext);
            Grid.SetColumn(navStack, 2);
            bottomActionsGrid.Children.Add(navStack);

            stack.Children.Add(bottomActionsGrid);

            card.Child = stack;
            return card;
        }

        private void InicializarPassosTutorial()
        {
            _steps.Clear();

            // 1. Janelas & Widgets
            _steps.Add(new TutorialStep
            {
                TabLabel = "Janelas",
                Title = "1. Janelas Multitarefa & Widgets",
                Subtitle = "A base do seu espaço de trabalho: notas, listas, planilhas e navegador.",
                Icon = AppIcons.Note,
                IconColorHex = "#94A3B8",
                OndeClicar = "Na barra lateral esquerda, passe o mouse e clique no ícone do Bloco de Notas, Lista de Tarefas, Planilha, Galeria ou Navegador.",
                OQueFazer = "1. Uma janela surge imediatamente no Workspace.\n" +
                            "2. Segure pela barra de título para arrastar a janela para onde quiser.\n" +
                            "3. Redimensione puxando as bordas externas.\n" +
                            "4. Use os botões da janela: [—] minimiza para a barra inferior, [▢] maximiza e [✕] fecha.",
                Dica = "Nos blocos de notas, formate o texto com Negrito, Itálico e tamanho de fonte. Tudo salva automaticamente ao fechar!",
                GuidedStepTarget = 0,
                GuidedButtonLabel = "Baixar Janela & Mostrar Onde Abrir Janelas na Tela"
            });

            // 2. Menu Circular & Cores
            _steps.Add(new TutorialStep
            {
                TabLabel = "Menu Circular",
                Title = "2. Menu Circular (The Sims 4) & Cores em Cascata",
                Subtitle = "Acesse opções rápidas em cápsulas radiais com propagação automática de cores.",
                Icon = AppIcons.Palette,
                IconColorHex = "#94A3B8",
                OndeClicar = "Na barra superior de qualquer janela, clique no botão de engrenagem.",
                OQueFazer = "1. O Menu Circular em cápsulas se abre ao redor da janela.\n" +
                            "2. Clique em 'Mudar Cor ▶': o anel concêntrico de cores se abre!\n" +
                            "3. SUPER EFEITO CASCATA: Ao trocar a cor de uma janela, todas as janelas e conexões ligadas a ela mudam de cor juntas automaticamente!\n" +
                            "4. Clique em 'Criticidade ▶' para definir o nível de urgência da tarefa.",
                Dica = "Clicar em qualquer ponto fora do menu ou apertar ESC fecha o menu circular imediatamente.",
                GuidedStepTarget = 1,
                GuidedButtonLabel = "Baixar Janela & Mostrar Menu Circular e Cores na Tela"
            });

            // 3. Conexões de Fluxograma
            _steps.Add(new TutorialStep
            {
                TabLabel = "Fluxograma",
                Title = "3. Criar Conexões no Fluxograma",
                Subtitle = "Ligue janelas e processos com curvas suaves Bézier e setas direcionais.",
                Icon = AppIcons.ShapeDiamond,
                IconColorHex = "#94A3B8",
                OndeClicar = "Na barra lateral esquerda em 'Fluxograma' (Retângulos, Losangos ou Círculos), ou clique na engrenagem de qualquer janela > 'Conectar'.",
                OQueFazer = "1. O modo de ligação é ativado.\n" +
                            "2. Clique na Janela de Origem e depois na Janela de Destino.\n" +
                            "3. Uma curva elegante com seta direcional é traçada ligando as duas janelas!\n" +
                            "4. Ao arrastar as janelas pelo Workspace, a linha de conexão segue os blocos dinamicamente.",
                Dica = "Passe o cursor sobre a bolinha no centro da linha para mudar a cor da ligação ou removê-la.",
                GuidedStepTarget = 2,
                GuidedButtonLabel = "Baixar Janela & Mostrar Como Fazer Ligação na Tela"
            });

            // 4. Vínculo com Mouse
            _steps.Add(new TutorialStep
            {
                TabLabel = "Vínculo (Mouse)",
                Title = "4. Vincular Tarefas a Janelas com o Mouse",
                Subtitle = "Conecte diretamente cada item do seu checklist a qualquer janela com apenas um clique.",
                Icon = AppIcons.Link,
                IconColorHex = "#94A3B8",
                OndeClicar = "Em qualquer 'Lista de Marcação' (Checklist), clique no ícone de corrente ao lado da tarefa.",
                OQueFazer = "1. Uma linha tracejada guia o seu mouse pela tela.\n" +
                            "2. Aponte para qualquer janela do Workspace (nota, planilha, etc.) e clique nela!\n" +
                            "3. Pronto! A tarefa fica vinculada com um pontinho colorido.",
                Dica = "Clicar no ícone de link a qualquer momento traz aquela janela imediatamente para a frente da tela!",
                GuidedStepTarget = 3,
                GuidedButtonLabel = "Baixar Janela & Mostrar Como Vincular Tarefas na Tela"
            });

            // 5. Calendário
            _steps.Add(new TutorialStep
            {
                TabLabel = "Calendário",
                Title = "5. Calendário de Etapas, Horários e Prazos",
                Subtitle = "Planeje sprints, marque horários de reuniões e consulte feriados nacionais.",
                Icon = AppIcons.Calendar,
                IconColorHex = "#94A3B8",
                OndeClicar = "Na barra lateral direita, passe o mouse e clique no ícone de Calendário.",
                OQueFazer = "1. O painel retrátil do calendário se abre suavemente.\n" +
                            "2. Clique em um dia para adicionar uma etapa com descrição e horário.\n" +
                            "3. Ou arraste o mouse por múltiplos dias para criar um período contínuo!\n" +
                            "4. Vincule tarefas de listas ou janelas inteiras a datas no calendário.",
                Dica = "Feriados nacionais são destacados automaticamente na grade mensal.",
                GuidedStepTarget = 0,
                GuidedButtonLabel = "Baixar Janela & Ver Barra de Acesso Rápido"
            });

            // 6. Atalhos & Nuvem
            _steps.Add(new TutorialStep
            {
                TabLabel = "Atalhos",
                Title = "6. Atalhos Rápidos e Sincronização em Nuvem",
                Subtitle = "Produtividade máxima com comandos de teclado e backup no Google Drive.",
                Icon = AppIcons.Upload,
                IconColorHex = "#94A3B8",
                OndeClicar = "Use os atalhos no teclado durante o uso do programa.",
                OQueFazer = "• Ctrl + Z: Desfaz a última ação (mover janela, criar, excluir, mudar cor, conexões).\n" +
                            "• Ctrl + Y (ou Ctrl + Shift + Z): Refaz a alteração.\n" +
                            "• Ctrl + Roda do Mouse: Zoom In e Zoom Out fluido em todo o Workspace.\n" +
                            "• Arrastar com Botão Direito ou Meio do mouse no fundo: Move a visão da tela (Pan).\n" +
                            "• Filtro de Cores (topo): Clique nas bolinhas de cores para filtrar instantaneamente apenas as janelas daquela categoria!\n" +
                            "• Sincronização Google Drive: Em Preferências (engrenagem) conecte sua conta Google para salvar backups na nuvem automaticamente.",
                Dica = "No menu 'Exportar / Extrair' você pode gerar relatórios em PDF, imagens PNG do Workspace ou pacotes .zip portáteis para levar em um pendrive!",
                GuidedStepTarget = 0,
                GuidedButtonLabel = "Baixar Janela & Testar no Workspace"
            });
        }

        private Border CriarCardSecao(StreamGeometry iconData, string tituloSecao, Control conteudo)
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(_isDarkMode ? Color.Parse("#111A29") : Color.Parse("#F8FAFC")),
                BorderBrush = new SolidColorBrush(_isDarkMode ? Color.Parse("#1F2D42") : Color.Parse("#E2E8F0")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 9, 14, 10)
            };

            var stack = new StackPanel { Spacing = 6 };

            var headerStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            var icon = new PathIcon
            {
                Data = iconData,
                Width = 12,
                Height = 12,
                Foreground = new SolidColorBrush(Color.Parse("#94A3B8")),
                VerticalAlignment = VerticalAlignment.Center
            };
            var headerText = new TextBlock
            {
                Text = tituloSecao,
                FontSize = 10.5,
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.Parse("#94A3B8")),
                VerticalAlignment = VerticalAlignment.Center,
                LetterSpacing = 0.5
            };
            headerStack.Children.Add(icon);
            headerStack.Children.Add(headerText);

            stack.Children.Add(headerStack);
            stack.Children.Add(conteudo);
            border.Child = stack;
            return border;
        }

        private void IrParaPasso(int index)
        {
            if (index < 0 || index >= _steps.Count) return;
            _currentStepIndex = index;
            var step = _steps[index];

            _lblStepIndicator.Text = $"Passo {_currentStepIndex + 1} de {_steps.Count}";
            _progressBar.Value = _currentStepIndex + 1;

            _stepIcon.Data = step.Icon;
            _stepIcon.Foreground = new SolidColorBrush(Color.Parse(step.IconColorHex));
            _lblStepTitle.Text = step.Title;
            _lblStepSubtitle.Text = step.Subtitle;

            _txtOndeClicar.Text = step.OndeClicar;
            _txtOQueFazer.Text = step.OQueFazer;
            _txtDica.Text = step.Dica;

            var btnLabel = !string.IsNullOrEmpty(step.GuidedButtonLabel)
                ? step.GuidedButtonLabel
                : "Baixar Janela & Mostrar Onde Clicar na Tela";
            _btnStartGuidedMode.Content = AppIcons.WithText(AppIcons.Target, btnLabel, 13, 8, new SolidColorBrush(Color.Parse("#CBD5E1")));

            _btnPrev.IsEnabled = _currentStepIndex > 0;
            _btnPrev.Opacity = _btnPrev.IsEnabled ? 1.0 : 0.4;

            bool isLast = _currentStepIndex == _steps.Count - 1;
            _btnNext.Content = isLast 
                ? AppIcons.WithText(AppIcons.Check, "Concluir", 11, 6, Brushes.White) 
                : AppIcons.WithText(AppIcons.ArrowRight, "Próximo", 10, 6, Brushes.White);
            _btnNext.Background = new SolidColorBrush(isLast ? Color.Parse("#10B981") : Color.Parse("#2563EB"));

            for (int i = 0; i < _tabButtons.Count; i++)
            {
                var btn = _tabButtons[i];
                bool active = i == index;
                btn.Background = active
                    ? new SolidColorBrush(_isDarkMode ? Color.Parse("#1E293B") : Color.Parse("#DBEAFE"))
                    : Brushes.Transparent;
                btn.Foreground = active
                    ? Brushes.White
                    : new SolidColorBrush(Color.Parse("#94A3B8"));
                btn.BorderBrush = active
                    ? new SolidColorBrush(Color.Parse("#38BDF8"))
                    : Brushes.Transparent;
            }
        }

        // ==================== MODO GUIADO ("BAIXA A JANELA E MOSTRA ONDE CLICAR") ====================

        public void IniciarModoGuiado(int stepTarget)
        {
            _isGuidedMode = true;
            _currentGuidedStep = Math.Clamp(stepTarget, 0, 3);

            // "Baixa a janela" (esconde o modal principal centralizado e exibe o card inferior para liberar a visão total do Workspace)
            _card.IsVisible = false;
            _bottomGuideCard.IsVisible = true;
            Background = null; // Permite que o usuário clique e interaja diretamente com as janelas e botões!

            ExecutarPassoGuiado(_currentGuidedStep);
        }

        public void MostrarModoCompleto()
        {
            _isGuidedMode = false;
            _card.IsVisible = true;
            _bottomGuideCard.IsVisible = false;
            _pointerBadge.IsVisible = false;
            Background = new SolidColorBrush(Color.FromArgb(175, 9, 13, 22));
        }

        private void ExecutarPassoGuiado(int step)
        {
            _currentGuidedStep = step;
            _btnGuidedPrev.IsEnabled = step > 0;
            _btnGuidedPrev.Opacity = _btnGuidedPrev.IsEnabled ? 1.0 : 0.4;

            bool isLast = step == 3;
            _btnGuidedNext.Content = isLast 
                ? AppIcons.WithText(AppIcons.Check, "Concluir", 11, 6, Brushes.White) 
                : AppIcons.WithText(AppIcons.ArrowRight, "Próximo Passo", 10, 6, Brushes.White);
            _btnGuidedNext.Background = new SolidColorBrush(isLast ? Color.Parse("#10B981") : Color.Parse("#2563EB"));

            switch (step)
            {
                case 0:
                    // 1. Onde abrir uma janela (aponta para o Bloco de Notas na barra lateral)
                    _lblGuidedStepBadge.Text = "PASSO 1 DE 4: ONDE ABRIR UMA JANELA";
                    _lblGuidedInstruction.Text = "Passe o mouse no menu lateral esquerdo para expandi-lo e clique no Bloco de Notas para criar uma nova janela no Workspace.";
                    _btnGuidedDoAction.Content = AppIcons.WithText(AppIcons.Sparkle, "Abrir Bloco de Notas Agora", 11, 6, Brushes.White);
                    _btnGuidedDoAction.IsVisible = true;
                    PosicionarPonteiroNoElemento(_mainWindow.ObterBotaoNotas(), "1. ABRIR JANELAS", "Passe o mouse e clique aqui para abrir uma janela!", "left");
                    break;

                case 1:
                    // 2. Menu Circular & Cores (aponta para a engrenagem da janela)
                    var win = _mainWindow.GarantirJanelaParaDemonstracao("notepad");
                    _lblGuidedStepBadge.Text = "PASSO 2 DE 4: MENU CIRCULAR & MUDAR COR";
                    _lblGuidedInstruction.Text = "Clique na engrenagem da janela para abrir o Menu Circular. Em 'Mudar Cor ▶' você escolhe uma cor e toda a cadeia conectada muda de cor em cascata!";
                    _btnGuidedDoAction.Content = AppIcons.WithText(AppIcons.Sparkle, "Abrir Menu Circular Agora", 11, 6, Brushes.White);
                    _btnGuidedDoAction.IsVisible = true;
                    win.ForcarExibirBotaoConfig(true);
                    PosicionarPonteiroNoElemento(win.ObterBotaoConfig(), "2. MENU CIRCULAR & CORES", "Clique na engrenagem para abrir o menu e mudar cores!", "top");
                    break;

                case 2:
                    // 3. Fazer conexão no fluxograma
                    var (winA, winB) = _mainWindow.GarantirDuasJanelasConectadasParaDemonstracao();
                    _lblGuidedStepBadge.Text = "PASSO 3 DE 4: LIGAÇÃO NO FLUXOGRAMA";
                    _lblGuidedInstruction.Text = "Para ligar duas janelas no fluxograma, abra o menu circular da primeira janela, clique em 'Conectar' e depois clique na janela de destino!";
                    _btnGuidedDoAction.Content = AppIcons.WithText(AppIcons.Sparkle, "Conectar Janelas de Exemplo", 11, 6, Brushes.White);
                    _btnGuidedDoAction.IsVisible = true;
                    PosicionarPonteiroNoElemento(winA, "3. FLUXOGRAMA & LIGAÇÕES", "Janelas conectadas formam um fluxo contínuo com cores sincronizadas!", "left");
                    break;

                case 3:
                    // 4. Vincular item de checklist com mouse
                    var checklistWin = _mainWindow.GarantirJanelaParaDemonstracao("checklist");
                    _lblGuidedStepBadge.Text = "PASSO 4 DE 4: VINCULAR TAREFA COM O MOUSE";
                    _lblGuidedInstruction.Text = "Nas Listas de Tarefas, clique no ícone de corrente do item e depois clique com o mouse em qualquer janela para vinculá-las!";
                    _btnGuidedDoAction.Content = AppIcons.WithText(AppIcons.Sparkle, "Criar Janela de Apoio", 11, 6, Brushes.White);
                    _btnGuidedDoAction.IsVisible = true;
                    PosicionarPonteiroNoElemento(checklistWin, "4. VÍNCULO COM O MOUSE", "Clique no ícone de corrente para ligar uma tarefa à janela desejada!", "left");
                    break;
            }
        }

        private void ExecutarAcaoGuiadaAutomatica()
        {
            switch (_currentGuidedStep)
            {
                case 0:
                    // Cria uma janela e avança para o passo 2
                    var win = _mainWindow.GarantirJanelaParaDemonstracao("notepad");
                    IniciarModoGuiado(1);
                    break;

                case 1:
                    // Abre o menu circular da janela
                    var winConfig = _mainWindow.GarantirJanelaParaDemonstracao("notepad");
                    winConfig.DispararMenuRadialManual();
                    break;

                case 2:
                    // Conecta duas janelas de demonstração
                    _mainWindow.GarantirDuasJanelasConectadasParaDemonstracao();
                    break;

                case 3:
                    // Garante uma checklist e uma janela de apoio
                    _mainWindow.GarantirJanelaParaDemonstracao("checklist");
                    _mainWindow.GarantirJanelaParaDemonstracao("notepad");
                    break;
            }
        }

        private void PosicionarPonteiroNoElemento(Control? target, string titulo, string instrucao, string direcao = "left")
        {
            if (target == null)
            {
                _pointerBadge.IsVisible = false;
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (target == null) return;
                Point? pos = target.TranslatePoint(new Point(target.Bounds.Width / 2.0, target.Bounds.Height / 2.0), _spotlightCanvas);
                if (pos == null)
                {
                    _pointerBadge.IsVisible = false;
                    return;
                }

                _lblPointerTitle.Text = titulo;
                _lblPointerText.Text = instrucao;
                _pointerBadge.IsVisible = true;

                _pointerBadge.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double badgeW = _pointerBadge.DesiredSize.Width > 0 ? _pointerBadge.DesiredSize.Width : 220;
                double badgeH = _pointerBadge.DesiredSize.Height > 0 ? _pointerBadge.DesiredSize.Height : 65;

                double x, y;
                if (direcao == "left")
                {
                    x = pos.Value.X + target.Bounds.Width / 2.0 + 16;
                    y = pos.Value.Y - badgeH / 2.0;
                    _lblPointerArrow.Text = "◀";
                }
                else if (direcao == "top")
                {
                    x = pos.Value.X - badgeW / 2.0;
                    y = pos.Value.Y + target.Bounds.Height / 2.0 + 14;
                    _lblPointerArrow.Text = "▲";
                }
                else
                {
                    x = pos.Value.X - badgeW / 2.0;
                    y = pos.Value.Y - target.Bounds.Height / 2.0 - badgeH - 14;
                    _lblPointerArrow.Text = "▼";
                }

                double maxX = Math.Max(20, Bounds.Width > 0 ? Bounds.Width - badgeW - 20 : 800);
                double maxY = Math.Max(20, Bounds.Height > 0 ? Bounds.Height - badgeH - 140 : 500);
                x = Math.Clamp(x, 10, maxX);
                y = Math.Clamp(y, 10, maxY);

                Canvas.SetLeft(_pointerBadge, x);
                Canvas.SetTop(_pointerBadge, y);
            }, DispatcherPriority.Loaded);
        }

        public void Fechar()
        {
            _pointerBadge.IsVisible = false;
            OnClosed?.Invoke();
        }
    }
}
