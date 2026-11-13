using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;

namespace AvaloniaMdiApp
{
    public partial class DraggableWindow : Border
    {
        public event Action? OnClosed;
        public event Action<DraggableWindow, ChecklistRowState>? OnRequestOpenRowNotepad;
        public event Action<DraggableWindow, ChecklistRowState>? OnRequestLinkRowWindow;
        public event Action<DraggableWindow>? OnSaveRequested;
        public event Action<DraggableWindow>? OnBoundsChanged;
        public event Action<DraggableWindow>? OnConnectRequested;
        public event Action<DraggableWindow>? OnWindowClicked;
        public event Action<DraggableWindow, ChecklistRowState>? OnChecklistRowClicked;
        public event Action<DraggableWindow>? OnInteractionCompleted;

        public string WindowId { get; }
        public string WindowTitle { get; private set; }
        public string WindowType { get; private set; }
        public string CustomColor { get; private set; } = string.Empty;
        public string CriticalityLevel { get; private set; } = "none";
        public int AccessCount { get; set; }
        public bool IsMinimized => _isMinimized;
        public bool IsMaximized => _isMaximized;

        private readonly Action<string, string, TextFormatData>? _onLinkedRowSave;
        public DraggableWindow? ParentChecklistWindow { get; }

        private enum ResizeMode { None, Top, Bottom, Left, Right, TopLeft, TopRight, BottomLeft, BottomRight }

        private bool _isDragging;
        private bool _pressedOnTitleText;
        private ResizeMode _resizeMode = ResizeMode.None;

        private Point _pointerStart;
        private Point _windowStart;
        private Rect _rectStart;

        private bool _isMaximized;
        private bool _isMinimized;
        private bool _isActive;
        private bool _isDarkMode = true;
        private Rect _normalRect;

        // Estado de formatação de texto do bloco
        private bool _isBold;
        private bool _isItalic;
        private double _fontSize = 13;
        private string _alignment = "left";
        private string _fontFamilyName = "default";

        private readonly Border _titleBar;
        private readonly Border _statusDot;
        private readonly Border _criticalityHeaderBadge;
        private readonly TextBlock _criticalityHeaderText;
        private readonly TextBlock _lblTitle;
        private readonly TextBox _txtTitleEditor;
        private readonly Button _btnEditTitle;
        private readonly Border? _formatToolbar;
        private readonly Button? _btnBold;
        private readonly Button? _btnItalic;
        private readonly Button? _btnFontDec;
        private readonly Button? _btnFontInc;
        private readonly Button? _btnFormatMenu;
        private readonly TextBlock? _lblFontSize;
        private readonly TextBlock? _lblContentTitle;
        private readonly TextBlock? _lblContentInfo;
        private TextBox? _noteTextBox;
        private readonly StackPanel? _checklistContainer;
        private readonly Button? _btnAddChecklistItem;
        private readonly List<ChecklistRowState> _checklistRows = new();
        private readonly Border _contentCard;
        private readonly Panel _contentPanel;
        private readonly Button _btnConfig;
        private readonly Button? _btnShapeConfig;
        private readonly Border? _shapeMoveHandle;
        private readonly TextBlock _btnMinText;
        private readonly TextBlock _btnMaxText;
        private readonly TextBlock _btnCloseText;
        private readonly TaskBar _taskBar;
        private bool _isHoveringWindow;
        private bool _isConfigMenuOpen;

        public Canvas Workspace { get; }

        public static readonly (string Hex, string Nome)[] PaletaCoresJanela =
        {
            ("", "Padrão do Tema"),
            ("#2563EB", "Azul Real"),
            ("#7C3AED", "Roxo / Violeta"),
            ("#059669", "Verde Esmeralda"),
            ("#D97706", "Âmbar / Laranja"),
            ("#DC2626", "Vermelho Rubi"),
            ("#DB2777", "Rosa / Magenta"),
            ("#0891B2", "Ciano / Turquesa"),
            ("#475569", "Grafite / Neutro")
        };

        public DraggableWindow(
            Canvas workspace,
            TaskBar taskBar,
            string title,
            double width,
            double height,
            double x,
            double y,
            bool isDarkMode = true,
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
            Workspace = workspace;
            _taskBar = taskBar;
            WindowId = string.IsNullOrWhiteSpace(windowId) ? Guid.NewGuid().ToString("N") : windowId;
            WindowTitle = title;
            WindowType = string.IsNullOrWhiteSpace(windowType) ? "default" : windowType;
            CustomColor = customColor ?? string.Empty;
            CriticalityLevel = string.IsNullOrWhiteSpace(criticalityLevel) ? "none" : criticalityLevel;
            AccessCount = Math.Max(1, accessCount);
            ParentChecklistWindow = parentChecklistWindow;
            _onLinkedRowSave = onLinkedRowSave;
            _isDarkMode = isDarkMode;

            if (IsFlowchartShape && initialFormat == null)
            {
                _alignment = "center";
                _isBold = true;
            }

            if (initialFormat != null)
            {
                _isBold = initialFormat.IsBold;
                _isItalic = initialFormat.IsItalic;
                _fontSize = Math.Clamp(initialFormat.FontSize > 0 ? initialFormat.FontSize : 13, 9, 32);
                _alignment = string.IsNullOrWhiteSpace(initialFormat.Alignment) ? "left" : initialFormat.Alignment;
                _fontFamilyName = string.IsNullOrWhiteSpace(initialFormat.FontFamilyName) ? "default" : initialFormat.FontFamilyName;
            }

            double minW = IsFlowchartShape ? 140 : 220;
            double minH = IsFlowchartShape ? 95 : 140;
            Width = Math.Max(minW, width);
            Height = Math.Max(minH, height);
            _normalRect = new Rect(x, y, Width, Height);

            Canvas.SetLeft(this, x);
            Canvas.SetTop(this, y);

            BorderThickness = IsFlowchartShape ? new Thickness(0) : new Thickness(1);
            CornerRadius = new CornerRadius(8);
            ClipToBounds = !IsFlowchartShape;
            Focusable = true;

            var rootLayout = new DockPanel();

            _titleBar = new Border
            {
                Name = "TitleBar",
                Height = 34,
                CornerRadius = new CornerRadius(7, 7, 0, 0),
                IsVisible = !IsFlowchartShape,
                Child = new Grid()
            };

            var grid = (Grid)_titleBar.Child!;
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            _statusDot = new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            Grid.SetColumn(_statusDot, 0);
            grid.Children.Add(_statusDot);

            _lblTitle = new TextBlock
            {
                Text = title,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(8, 0, 4, 0),
                FontWeight = FontWeight.SemiBold,
                FontSize = 12.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Cursor = new Cursor(StandardCursorType.Ibeam)
            };
            ToolTip.SetTip(_lblTitle, "Clique para alterar o nome da janela");

            _btnEditTitle = new Button
            {
                Content = AppIcons.Create(AppIcons.Edit, 10.5, ThemePalette.IconLightGray),
                Width = 22,
                Height = 22,
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                CornerRadius = new CornerRadius(4),
                Opacity = 0.80
            };
            ToolTip.SetTip(_btnEditTitle, "Renomear janela");
            _btnEditTitle.Click += (s, e) => IniciarEdicaoTitulo();

            _criticalityHeaderText = new TextBlock
            {
                FontSize = 9.5,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            _criticalityHeaderBadge = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5, 1),
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsVisible = false,
                IsHitTestVisible = false,
                Child = _criticalityHeaderText
            };

            var titleDisplayStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleDisplayStack.Children.Add(_criticalityHeaderBadge);
            titleDisplayStack.Children.Add(_lblTitle);
            titleDisplayStack.Children.Add(_btnEditTitle);

            _txtTitleEditor = new TextBox
            {
                Text = title,
                IsVisible = false,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 2, 8, 2),
                Padding = new Thickness(6, 2),
                MinHeight = 24,
                Height = 24,
                FontSize = 12.5,
                FontWeight = FontWeight.SemiBold,
                CornerRadius = new CornerRadius(4)
            };
            _txtTitleEditor.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    ConfirmarEdicaoTitulo();
                }
                else if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    CancelarEdicaoTitulo();
                }
            };
            _txtTitleEditor.LostFocus += (s, e) => ConfirmarEdicaoTitulo();

            var titleContainer = new Panel
            {
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            titleContainer.Children.Add(titleDisplayStack);
            titleContainer.Children.Add(_txtTitleEditor);

            Grid.SetColumn(titleContainer, 1);
            grid.Children.Add(titleContainer);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 2,
                Margin = new Thickness(0, 0, 4, 0)
            };
            Grid.SetColumn(btnPanel, 2);

            var configFlyout = CriarMenuConfiguracaoJanela();
            configFlyout.Opened += (s, e) =>
            {
                _isConfigMenuOpen = true;
                AtualizarVisibilidadeControlesHover();
            };
            configFlyout.Closed += (s, e) =>
            {
                _isConfigMenuOpen = false;
                AtualizarVisibilidadeControlesHover();
            };

            _btnConfig = CreateControlButton("", out _, (s, e) => { });
            _btnConfig.Content = AppIcons.Create(AppIcons.Settings, 12, ThemePalette.IconLightGray);
            _btnConfig.IsVisible = false;
            _btnConfig.Flyout = configFlyout;
            ToolTip.SetTip(_btnConfig, "Configurações da janela (Cor, Criticidade, Conectar, Excluir)");

            var btnMin = CreateControlButton("—", out _btnMinText, (s, e) => ToggleMinimize());
            var btnMax = CreateControlButton("▢", out _btnMaxText, (s, e) => ToggleMaximize());
            var btnClose = CreateControlButton("✕", out _btnCloseText, (s, e) => Fechar());

            btnPanel.Children.Add(_btnConfig);
            btnPanel.Children.Add(btnMin);
            btnPanel.Children.Add(btnMax);
            btnPanel.Children.Add(btnClose);
            grid.Children.Add(btnPanel);

            DockPanel.SetDock(_titleBar, Dock.Top);
            rootLayout.Children.Add(_titleBar);

            _contentPanel = new Panel();

            _contentCard = new Border
            {
                Margin = IsFlowchartShape ? new Thickness(2, 4, 2, 2) : new Thickness(10),
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(6),
                BorderThickness = IsFlowchartShape ? new Thickness(0) : new Thickness(1),
                ClipToBounds = !IsFlowchartShape,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            if (IsFlowchartShape)
            {
                _contentCard.Child = CriarConteudoFormaFluxograma(initialNote);
            }
            else if (WindowType == "notepad")
            {
                var cardDock = new DockPanel();

                _formatToolbar = CriarBarraFormatacao(
                    out _btnBold,
                    out _btnItalic,
                    out _btnFontDec,
                    out _lblFontSize,
                    out _btnFontInc,
                    out _btnFormatMenu);

                DockPanel.SetDock(_formatToolbar, Dock.Top);
                cardDock.Children.Add(_formatToolbar);

                cardDock.Children.Add(CriarConteudoBlocoNotas(initialNote, initialRichLines, initialFormat));

                _contentCard.Child = cardDock;
            }
            else if (WindowType == "checklist")
            {
                _checklistContainer = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 6
                };

                _btnAddChecklistItem = new Button
                {
                    Content = "+",
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Padding = new Thickness(8, 4),
                    Margin = new Thickness(0, 2, 0, 0),
                    CornerRadius = new CornerRadius(6),
                    BorderThickness = new Thickness(1),
                    FontSize = 16,
                    FontWeight = FontWeight.Bold
                };
                ToolTip.SetTip(_btnAddChecklistItem, "Adicionar nova linha");
                _btnAddChecklistItem.Click += (s, e) => AdicionarLinhaChecklist("", false, "", null, focusInput: true);

                var outerListStack = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 6,
                    Margin = new Thickness(10)
                };
                outerListStack.Children.Add(_checklistContainer);
                outerListStack.Children.Add(_btnAddChecklistItem);

                var scrollViewer = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Content = outerListStack
                };

                if (initialChecklist != null && initialChecklist.Count > 0)
                {
                    foreach (var item in initialChecklist)
                    {
                        AdicionarLinhaChecklist(item.Text, item.IsChecked, item.LinkedNoteContent, item.LinkedNoteFormat, focusInput: false, item.ColorHex, item.LinkedWindowId, item.Id);
                    }
                }
                else
                {
                    AdicionarLinhaChecklist("", false, "", null, focusInput: false);
                }

                _contentCard.Child = scrollViewer;
            }
            else if (WindowType == "browser")
            {
                _contentCard.Child = CriarConteudoNavegador(initialNote);
            }
            else if (WindowType == "gallery")
            {
                _contentCard.Child = CriarConteudoGaleria(initialGallery);
            }
            else if (WindowType == "image")
            {
                _contentCard.Child = CriarConteudoImagem(initialNote);
            }
            else if (WindowType == "video")
            {
                _contentCard.Child = CriarConteudoVideo(initialNote);
            }
            else if (WindowType == "spreadsheet")
            {
                _contentCard.Child = CriarConteudoPlanilha(
                    spreadsheetRows,
                    spreadsheetCols,
                    initialSpreadsheetCells,
                    initialConditionalRules);
            }
            else
            {
                var contentStack = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Spacing = 6
                };

                _lblContentTitle = new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                };

                _lblContentInfo = new TextBlock
                {
                    Text = $"Arraste pelo topo • Duplo clique para maximizar\nDimensões: {(int)Width} × {(int)Height} px",
                    FontSize = 11.5,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    LineHeight = 18
                };

                contentStack.Children.Add(_lblContentTitle);
                contentStack.Children.Add(_lblContentInfo);
                _contentCard.Child = contentStack;
            }

            _contentPanel.Children.Add(_contentCard);
            rootLayout.Children.Add(_contentPanel);

            var mainGrid = new Grid();
            mainGrid.Children.Add(rootLayout);
            AdicionarBordasDeRedimensionamento(mainGrid);

            if (IsFlowchartShape)
            {
                var shapeFloatingControls = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 4,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(6),
                    ZIndex = 20
                };

                _shapeMoveHandle = new Border
                {
                    Width = 24,
                    Height = 24,
                    CornerRadius = new CornerRadius(12),
                    BorderThickness = new Thickness(1),
                    Background = SolidColorBrush.Parse("#CC1E293B"),
                    BorderBrush = SolidColorBrush.Parse("#475569"),
                    Cursor = new Cursor(StandardCursorType.SizeAll),
                    IsVisible = false,
                    Child = AppIcons.Create(AppIcons.Workspace, 11, ThemePalette.IconLightGray)
                };
                ToolTip.SetTip(_shapeMoveHandle, "Arrastar forma no fluxograma");

                _btnShapeConfig = new Button
                {
                    Width = 24,
                    Height = 24,
                    Padding = new Thickness(0),
                    CornerRadius = new CornerRadius(12),
                    BorderThickness = new Thickness(1),
                    Background = SolidColorBrush.Parse("#CC1E293B"),
                    BorderBrush = SolidColorBrush.Parse("#475569"),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    IsVisible = false,
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Content = AppIcons.Create(AppIcons.Settings, 11.5, ThemePalette.IconLightGray),
                    Flyout = configFlyout
                };
                ToolTip.SetTip(_btnShapeConfig, "Configurar forma (Cor, Formato, Criticidade, Conectar, Excluir)");

                shapeFloatingControls.Children.Add(_shapeMoveHandle);
                shapeFloatingControls.Children.Add(_btnShapeConfig);
                mainGrid.Children.Add(shapeFloatingControls);
            }

            Child = mainGrid;

            PointerEntered += (s, e) =>
            {
                _isHoveringWindow = true;
                AtualizarVisibilidadeControlesHover();
            };
            PointerExited += (s, e) =>
            {
                _isHoveringWindow = false;
                AtualizarVisibilidadeControlesHover();
            };

            PointerPressed += OnWindowPointerPressed;
            PointerMoved += OnWindowPointerMoved;
            PointerReleased += OnWindowPointerReleased;
            KeyDown += OnWindowKeyDown;
            Workspace.SizeChanged += OnWorkspaceSizeChanged;

            AplicarFormatacaoTexto();
            AtualizarCores();
        }

        public void SalvarJanela()
        {
            ConfirmarEdicaoTitulo();
            SincronizarNotaVinculada();
            OnSaveRequested?.Invoke(this);
        }

        public void SincronizarNotaVinculada()
        {
            if (_onLinkedRowSave != null && (WindowType == "notepad" || _noteTextBox != null))
            {
                _onLinkedRowSave(WindowTitle, ObterTextoNota(), ExportTextFormat());
            }
        }

        public void AtualizarTituloExterno(string novoTitulo)
        {
            if (string.IsNullOrWhiteSpace(novoTitulo)) return;
            WindowTitle = novoTitulo;
            _lblTitle.Text = novoTitulo;
            _txtTitleEditor.Text = novoTitulo;
            if (_lblContentTitle != null)
            {
                _lblContentTitle.Text = novoTitulo;
            }
            _taskBar.AtualizarTituloJanela(this, novoTitulo);
        }

        private void IniciarEdicaoTitulo()
        {
            _txtTitleEditor.Text = WindowTitle;
            _lblTitle.IsVisible = false;
            _btnEditTitle.IsVisible = false;
            _txtTitleEditor.IsVisible = true;
            _txtTitleEditor.Focus();
            _txtTitleEditor.SelectAll();
        }

        public IReadOnlyList<ChecklistRowState> ObterItensChecklist() => _checklistRows;

        public string ObterResumoParaCalendario(string? checklistRowId = null)
        {
            if (WindowType == "checklist" && !string.IsNullOrWhiteSpace(checklistRowId))
            {
                var row = _checklistRows.Find(r => r.RowId == checklistRowId);
                if (row != null)
                {
                    string itemTxt = (row.Input.Text ?? string.Empty).Trim();
                    string notaTxt = (row.OpenNoteWindow?.ObterTextoNota() ?? row.LinkedNoteContent ?? string.Empty).Trim();
                    string combinado = string.IsNullOrWhiteSpace(notaTxt) ? itemTxt : $"{itemTxt} — {notaTxt}";
                    return ResumirTexto(combinado);
                }
            }

            if (WindowType == "notepad")
            {
                return ResumirTexto(ObterTextoNota());
            }

            if (IsFlowchartShape)
            {
                return ResumirTexto(ObterTextoNota());
            }

            if (WindowType == "browser")
            {
                return ResumirTexto(_browserHost?.CurrentUrl ?? _txtBrowserUrl?.Text);
            }

            return ResumirTexto(WindowTitle);
        }

        private static string ResumirTexto(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return "(Sem texto)";
            string limpo = texto.Replace("\r", " ").Replace("\n", " ").Trim();
            while (limpo.Contains("  ")) limpo = limpo.Replace("  ", " ");
            return limpo.Length > 90 ? limpo.Substring(0, 87) + "..." : limpo;
        }

        public string ObterTextoNota() =>
            WindowType == "notepad" && _richLines.Count > 0
                ? string.Join(Environment.NewLine, _richLines.ConvertAll(l => l.GetPlainText()))
                : (_noteTextBox?.Text ?? string.Empty);

        private void ConfirmarEdicaoTitulo()
        {
            if (!_txtTitleEditor.IsVisible) return;

            string novoTitulo = (_txtTitleEditor.Text ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(novoTitulo))
            {
                WindowTitle = novoTitulo;
                _lblTitle.Text = novoTitulo;
                if (_lblContentTitle != null)
                {
                    _lblContentTitle.Text = novoTitulo;
                }
                _taskBar.AtualizarTituloJanela(this, novoTitulo);
                SincronizarNotaVinculada();
            }

            _txtTitleEditor.IsVisible = false;
            _lblTitle.IsVisible = true;
            _btnEditTitle.IsVisible = true;
        }

        private void CancelarEdicaoTitulo()
        {
            _txtTitleEditor.Text = WindowTitle;
            _txtTitleEditor.IsVisible = false;
            _lblTitle.IsVisible = true;
            _btnEditTitle.IsVisible = true;
        }

        public WindowStateData ExportState()
        {
            SincronizarNotaVinculada();
            Rect bounds = GetNormalBounds();
            var data = new WindowStateData
            {
                Id = WindowId,
                Title = WindowTitle,
                WindowType = WindowType,
                CustomColor = CustomColor,
                CriticalityLevel = CriticalityLevel,
                X = bounds.X,
                Y = bounds.Y,
                Width = bounds.Width,
                Height = bounds.Height,
                NoteContent = WindowType == "browser"
                    ? (_browserHost?.CurrentUrl ?? _txtBrowserUrl?.Text ?? "https://www.google.com")
                    : WindowType == "image"
                        ? _singleImagePath
                        : WindowType == "video"
                            ? _singleVideoPath
                            : ObterTextoNota(),
                RichLines = WindowType == "notepad" ? ExportRichLines() : new List<RichTextLineData>(),
                TextFormat = ExportTextFormat(),
                SpreadsheetRows = _sheetRows,
                SpreadsheetCols = _sheetCols,
                AccessCount = AccessCount,
                LastUsedUtc = DateTime.UtcNow
            };

            if (WindowType == "checklist")
            {
                foreach (var row in _checklistRows)
                {
                    if (row.OpenNoteWindow != null)
                    {
                        row.LinkedNoteContent = row.OpenNoteWindow.ObterTextoNota();
                        row.LinkedNoteFormat = row.OpenNoteWindow.ExportTextFormat();
                    }

                    data.ChecklistItems.Add(new ChecklistItemData
                    {
                        Id = row.RowId,
                        Text = row.Input.Text ?? string.Empty,
                        IsChecked = row.Check.IsChecked == true,
                        ColorHex = row.ColorHex,
                        LinkedWindowId = row.LinkedWindowId,
                        LinkedNoteContent = row.LinkedNoteContent,
                        LinkedNoteFormat = row.LinkedNoteFormat
                    });
                }
            }
            else if (WindowType == "gallery")
            {
                foreach (var card in _galleryCards)
                {
                    data.GalleryItems.Add(new GalleryItemData
                    {
                        Id = card.Id,
                        Title = card.Title,
                        FilePath = card.FilePath,
                        MediaType = card.MediaType
                    });
                }
            }
            else if (WindowType == "spreadsheet")
            {
                foreach (var kvp in _sheetRawValues)
                {
                    if (!string.IsNullOrEmpty(kvp.Value))
                    {
                        data.SpreadsheetCells.Add(new SpreadsheetCellData
                        {
                            CellKey = kvp.Key,
                            RawValue = kvp.Value
                        });
                    }
                }

                foreach (var rule in _conditionalRules)
                {
                    data.ConditionalRules.Add(rule);
                }
            }

            return data;
        }

        public Rect GetNormalBounds()
        {
            if (_isMinimized || _isMaximized)
                return _normalRect;

            return new Rect(Canvas.GetLeft(this), Canvas.GetTop(this), Width, Height);
        }

        public void SetPosicaoNormal(double x, double y, double width, double height)
        {
            if (_isMaximized)
                ToggleMaximize();
            if (_isMinimized)
                ToggleMinimize();

            Canvas.SetLeft(this, x);
            Canvas.SetTop(this, y);
            Width = width;
            Height = height;
            _normalRect = new Rect(x, y, width, height);
            AtualizarTextoDimensoes();
            OnBoundsChanged?.Invoke(this);
        }

        public void DefinirCorJanela(string hexColor)
        {
            CustomColor = hexColor ?? string.Empty;
            AtualizarCores();
            _taskBar.AtualizarCorJanela(this);
        }

        public static string NormalizarFamiliaCor(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return string.Empty;
            return hex.Trim().ToUpperInvariant() switch
            {
                "#3B82F6" or "#2563EB" => "blue",
                "#10B981" or "#059669" => "green",
                "#F59E0B" or "#D97706" => "amber",
                "#EF4444" or "#DC2626" => "red",
                "#8B5CF6" or "#7C3AED" => "purple",
                "#EC4899" or "#DB2777" => "pink",
                "#06B6D4" or "#0891B2" => "cyan",
                "#94A3B8" or "#475569" => "gray",
                var other => other
            };
        }

        public static bool CorCombinaComFiltro(string? itemHex, string? filterHex)
        {
            if (string.IsNullOrWhiteSpace(filterHex)) return true;
            if (string.IsNullOrWhiteSpace(itemHex)) return false;
            return string.Equals(NormalizarFamiliaCor(itemHex), NormalizarFamiliaCor(filterHex), StringComparison.OrdinalIgnoreCase);
        }

        public void AplicarFiltroCor(string? hexFilter)
        {
            bool semFiltro = string.IsNullOrWhiteSpace(hexFilter);
            bool janelaCombina = CorCombinaComFiltro(CustomColor, hexFilter);
            bool algumaLinhaCombina = !semFiltro && _checklistRows.Exists(r => CorCombinaComFiltro(r.ColorHex, hexFilter));
            Opacity = (janelaCombina || algumaLinhaCombina) ? 1.0 : 0.25;
            AtualizarFiltroCorChecklist(hexFilter);
        }

        public void DefinirCriticidade(string level)
        {
            CriticalityLevel = string.IsNullOrWhiteSpace(level) ? "none" : level.Trim().ToLowerInvariant();
            AtualizarCores();
        }

        public void AlterarFormatoGeometrico(string novoWindowType)
        {
            if (!EhTipoFormaFluxograma(novoWindowType)) return;
            WindowType = novoWindowType;
            if (_contentCard.Child is Control c && c.Bounds.Width > 0 && c.Bounds.Height > 0)
            {
                AtualizarGeometriaFormaFluxograma(c.Bounds.Width, c.Bounds.Height);
            }
            AtualizarCores();
        }

        public void AplicarTema(bool isDarkMode)
        {
            _isDarkMode = isDarkMode;
            AtualizarCores();
        }

        public void SetActive(bool isActive)
        {
            if (_isActive == isActive) return;
            _isActive = isActive;
            SincronizarEstadoHibernacaoRecursos();
            AtualizarCores();
            _taskBar.AtualizarEstadoAtivo(this, isActive);
        }

        private void SincronizarEstadoHibernacaoRecursos()
        {
            bool deveHibernar = !_isActive || _isMinimized;

            _browserHost?.SetHibernated(deveHibernar);

            if (WindowType == "spreadsheet")
            {
                AtualizarHibernacaoPlanilha(deveHibernar);
                _lblTitle.Text = deveHibernar ? $"◦ {WindowTitle}" : WindowTitle;
            }
        }

        private void AtualizarCores()
        {
            var (critBadgeText, critColorHex) = ObterInfoCriticidade(CriticalityLevel);

            if (!string.IsNullOrEmpty(critBadgeText) && !string.IsNullOrEmpty(critColorHex))
            {
                _criticalityHeaderText.Text = critBadgeText;
                try
                {
                    _criticalityHeaderBadge.Background = SolidColorBrush.Parse(critColorHex);
                }
                catch
                {
                    _criticalityHeaderBadge.Background = ThemePalette.Danger;
                }
                _criticalityHeaderBadge.IsVisible = !IsFlowchartShape;
            }
            else
            {
                _criticalityHeaderBadge.IsVisible = false;
            }

            IBrush? customBrush = null;
            string effectiveColorHex = !string.IsNullOrWhiteSpace(CustomColor) ? CustomColor : critColorHex;
            if (!string.IsNullOrWhiteSpace(effectiveColorHex))
            {
                try
                {
                    customBrush = SolidColorBrush.Parse(effectiveColorHex);
                }
                catch { }
            }

            if (_isDarkMode)
            {
                Background = IsFlowchartShape ? Brushes.Transparent : ThemePalette.DarkWindowBg;
                BorderBrush = IsFlowchartShape ? Brushes.Transparent : (customBrush ?? (_isActive ? ThemePalette.DarkBorderActive : ThemePalette.DarkBorder));
                _titleBar.Background = customBrush ?? (_isActive ? ThemePalette.DarkWindowHeaderActiveBg : ThemePalette.DarkWindowHeaderBg);
                _statusDot.Background = _isActive ? Brushes.White : (customBrush ?? ThemePalette.AccentLight);
                _lblTitle.Foreground = ThemePalette.DarkTextPrimary;
                _btnEditTitle.Foreground = ThemePalette.DarkTextPrimary;
                _txtTitleEditor.Background = ThemePalette.DarkContentBg;
                _txtTitleEditor.Foreground = ThemePalette.DarkTextPrimary;
                _txtTitleEditor.BorderBrush = customBrush ?? ThemePalette.DarkBorderActive;
                _btnConfig.Foreground = ThemePalette.DarkTextPrimary;
                _btnMinText.Foreground = ThemePalette.DarkTextPrimary;
                _btnMaxText.Foreground = ThemePalette.DarkTextPrimary;
                _btnCloseText.Foreground = ThemePalette.DarkTextPrimary;
                _contentPanel.Background = IsFlowchartShape ? Brushes.Transparent : ThemePalette.DarkContentBg;
                _contentCard.Background = IsFlowchartShape ? Brushes.Transparent : ThemePalette.DarkWindowBg;
                _contentCard.BorderBrush = IsFlowchartShape ? Brushes.Transparent : ThemePalette.DarkBorder;

                if (_lblContentTitle != null) _lblContentTitle.Foreground = ThemePalette.DarkTextPrimary;
                if (_lblContentInfo != null) _lblContentInfo.Foreground = ThemePalette.DarkTextSecondary;
                if (_noteTextBox != null) _noteTextBox.Foreground = ThemePalette.DarkTextPrimary;
                if (_btnAddChecklistItem != null)
                {
                    _btnAddChecklistItem.Background = ThemePalette.DarkButtonBg;
                    _btnAddChecklistItem.BorderBrush = ThemePalette.DarkBorder;
                    _btnAddChecklistItem.Foreground = ThemePalette.DarkTextPrimary;
                }
            }
            else
            {
                Background = IsFlowchartShape ? Brushes.Transparent : ThemePalette.LightWindowBg;
                BorderBrush = IsFlowchartShape ? Brushes.Transparent : (customBrush ?? (_isActive ? ThemePalette.LightBorderActive : ThemePalette.LightBorder));
                _titleBar.Background = customBrush ?? (_isActive ? ThemePalette.LightWindowHeaderActiveBg : ThemePalette.LightWindowHeaderBg);
                _statusDot.Background = (_isActive || customBrush != null) ? Brushes.White : ThemePalette.Accent;
                var headerTextBrush = (_isActive || customBrush != null) ? Brushes.White : ThemePalette.LightTextPrimary;
                _lblTitle.Foreground = headerTextBrush;
                _btnEditTitle.Foreground = headerTextBrush;
                _txtTitleEditor.Background = ThemePalette.LightWindowBg;
                _txtTitleEditor.Foreground = ThemePalette.LightTextPrimary;
                _txtTitleEditor.BorderBrush = customBrush ?? ThemePalette.LightBorderActive;
                _btnConfig.Foreground = headerTextBrush;
                _btnMinText.Foreground = headerTextBrush;
                _btnMaxText.Foreground = headerTextBrush;
                _btnCloseText.Foreground = headerTextBrush;
                _contentPanel.Background = IsFlowchartShape ? Brushes.Transparent : ThemePalette.LightContentBg;
                _contentCard.Background = IsFlowchartShape ? Brushes.Transparent : ThemePalette.LightWindowBg;
                _contentCard.BorderBrush = IsFlowchartShape ? Brushes.Transparent : ThemePalette.LightBorder;

                if (_lblContentTitle != null) _lblContentTitle.Foreground = ThemePalette.LightTextPrimary;
                if (_lblContentInfo != null) _lblContentInfo.Foreground = ThemePalette.LightTextSecondary;
                if (_noteTextBox != null) _noteTextBox.Foreground = ThemePalette.LightTextPrimary;
                if (_btnAddChecklistItem != null)
                {
                    _btnAddChecklistItem.Background = ThemePalette.LightButtonBg;
                    _btnAddChecklistItem.BorderBrush = ThemePalette.LightBorder;
                    _btnAddChecklistItem.Foreground = ThemePalette.LightTextPrimary;
                }
            }

            foreach (var row in _checklistRows)
            {
                AtualizarEstiloLinhaChecklist(row);
            }

            if (_richLines.Count > 0)
            {
                AtualizarTemaRichEditor();
            }

            if (_formatToolbar != null)
            {
                var barBg = _isDarkMode ? ThemePalette.DarkContentBg : ThemePalette.LightContentBg;
                var borderBrush = _isDarkMode ? ThemePalette.DarkBorder : ThemePalette.LightBorder;
                var btnBg = _isDarkMode ? ThemePalette.DarkButtonBg : ThemePalette.LightButtonBg;
                var textBrush = _isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;
                var subTextBrush = _isDarkMode ? ThemePalette.DarkTextSecondary : ThemePalette.LightTextSecondary;

                _formatToolbar.Background = barBg;
                _formatToolbar.BorderBrush = borderBrush;

                if (_btnBold != null)
                {
                    _btnBold.Background = _isBold ? ThemePalette.Accent : btnBg;
                    _btnBold.BorderBrush = _isBold ? ThemePalette.AccentLight : borderBrush;
                    _btnBold.Foreground = _isBold ? Brushes.White : textBrush;
                }

                if (_btnItalic != null)
                {
                    _btnItalic.Background = _isItalic ? ThemePalette.Accent : btnBg;
                    _btnItalic.BorderBrush = _isItalic ? ThemePalette.AccentLight : borderBrush;
                    _btnItalic.Foreground = _isItalic ? Brushes.White : textBrush;
                }

                if (_btnFontDec != null)
                {
                    _btnFontDec.Background = btnBg;
                    _btnFontDec.BorderBrush = borderBrush;
                    _btnFontDec.Foreground = textBrush;
                }

                if (_btnFontInc != null)
                {
                    _btnFontInc.Background = btnBg;
                    _btnFontInc.BorderBrush = borderBrush;
                    _btnFontInc.Foreground = textBrush;
                }

                if (_btnFormatMenu != null)
                {
                    _btnFormatMenu.Background = btnBg;
                    _btnFormatMenu.BorderBrush = borderBrush;
                    _btnFormatMenu.Foreground = textBrush;
                }

                if (_lblFontSize != null)
                {
                    _lblFontSize.Foreground = subTextBrush;
                }
            }

            AtualizarCoresFormaFluxograma(customBrush);
            AtualizarCoresNavegador();
            AtualizarCoresGaleriaEImagem();
            AtualizarCoresPlanilha();
        }

        public void Fechar()
        {
            SincronizarNotaVinculada();
            ZIndexManager.ClearActive(this);
            _browserHost?.DisposeBrowser();
            LiberarRecursosGaleriaEImagem();
            LiberarRecursosPlanilha();
            Workspace.SizeChanged -= OnWorkspaceSizeChanged;
            _taskBar.RemoverJanela(this);
            Workspace.Children.Remove(this);
            OnClosed?.Invoke();
        }

        private void OnWorkspaceSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (_isMaximized)
            {
                Width = Math.Max(220, e.NewSize.Width);
                Height = Math.Max(140, e.NewSize.Height);
                AtualizarTextoDimensoes();
            }
        }

        private Button CreateControlButton(string text, out TextBlock textBlockRef, EventHandler<RoutedEventArgs> onClick)
        {
            textBlockRef = new TextBlock
            {
                Text = text,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var btn = new Button
            {
                Width = 28,
                Height = 26,
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                Content = textBlockRef,
                CornerRadius = new CornerRadius(4),
                VerticalAlignment = VerticalAlignment.Center
            };
            btn.Click += onClick;
            return btn;
        }

        private void AdicionarBordasDeRedimensionamento(Grid parentGrid)
        {
            int thickness = 6;
            int corner = 11;
            parentGrid.Children.Add(CriarGripBorda(ResizeMode.Left, thickness, double.NaN, HorizontalAlignment.Left, VerticalAlignment.Stretch, StandardCursorType.SizeWestEast));
            parentGrid.Children.Add(CriarGripBorda(ResizeMode.Right, thickness, double.NaN, HorizontalAlignment.Right, VerticalAlignment.Stretch, StandardCursorType.SizeWestEast));
            parentGrid.Children.Add(CriarGripBorda(ResizeMode.Top, double.NaN, thickness, HorizontalAlignment.Stretch, VerticalAlignment.Top, StandardCursorType.SizeNorthSouth));
            parentGrid.Children.Add(CriarGripBorda(ResizeMode.Bottom, double.NaN, thickness, HorizontalAlignment.Stretch, VerticalAlignment.Bottom, StandardCursorType.SizeNorthSouth));

            parentGrid.Children.Add(CriarGripBorda(ResizeMode.TopLeft, corner, corner, HorizontalAlignment.Left, VerticalAlignment.Top, StandardCursorType.TopLeftCorner));
            parentGrid.Children.Add(CriarGripBorda(ResizeMode.TopRight, corner, corner, HorizontalAlignment.Right, VerticalAlignment.Top, StandardCursorType.TopRightCorner));
            parentGrid.Children.Add(CriarGripBorda(ResizeMode.BottomLeft, corner, corner, HorizontalAlignment.Left, VerticalAlignment.Bottom, StandardCursorType.BottomLeftCorner));
            parentGrid.Children.Add(CriarGripBorda(ResizeMode.BottomRight, corner, corner, HorizontalAlignment.Right, VerticalAlignment.Bottom, StandardCursorType.BottomRightCorner));
        }

        private Border CriarGripBorda(ResizeMode mode, double w, double h, HorizontalAlignment ha, VerticalAlignment va, StandardCursorType cursor)
        {
            var border = new Border
            {
                Width = w,
                Height = h,
                HorizontalAlignment = ha,
                VerticalAlignment = va,
                Background = Brushes.Transparent,
                Cursor = new Cursor(cursor)
            };
            border.PointerPressed += (s, e) => IniciarRedimensionamento(mode, e);
            return border;
        }

        private void IniciarRedimensionamento(ResizeMode mode, PointerPressedEventArgs e)
        {
            if (_isMaximized || _isMinimized) return;
            var props = e.GetCurrentPoint(this);
            if (props.Properties.IsLeftButtonPressed)
            {
                _resizeMode = mode;
                _pointerStart = e.GetPosition(Workspace);
                _rectStart = new Rect(Canvas.GetLeft(this), Canvas.GetTop(this), Width, Height);
                e.Pointer.Capture(this);
                ZIndexManager.BringToFront(this);
                e.Handled = true;
            }
        }

        private void AtualizarVisibilidadeControlesHover()
        {
            bool mostrar = _isHoveringWindow || _isConfigMenuOpen;
            if (IsFlowchartShape)
            {
                if (_btnShapeConfig != null) _btnShapeConfig.IsVisible = mostrar;
                if (_shapeMoveHandle != null) _shapeMoveHandle.IsVisible = mostrar;
            }
            else
            {
                _btnConfig.IsVisible = mostrar;
            }
        }

        private MenuFlyout CriarMenuConfiguracaoJanela()
        {
            var flyout = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight };

            if (IsFlowchartShape)
            {
                var menuFormato = new MenuItem
                {
                    Icon = AppIcons.Create(AppIcons.ShapeRect, 11, ThemePalette.IconLightGray),
                    Header = "Formato Geométrico"
                };
                var miRect = new MenuItem { Icon = AppIcons.Create(AppIcons.ShapeRect, 11), Header = "Retângulo (Etapa)" };
                miRect.Click += (s, e) => AlterarFormatoGeometrico("shape_rect");
                var miDiamond = new MenuItem { Icon = AppIcons.Create(AppIcons.ShapeDiamond, 11), Header = "Losango (Decisão)" };
                miDiamond.Click += (s, e) => AlterarFormatoGeometrico("shape_diamond");
                var miCircle = new MenuItem { Icon = AppIcons.Create(AppIcons.ShapeCircle, 11), Header = "Círculo (Início / Fim)" };
                miCircle.Click += (s, e) => AlterarFormatoGeometrico("shape_circle");
                menuFormato.Items.Add(miRect);
                menuFormato.Items.Add(miDiamond);
                menuFormato.Items.Add(miCircle);
                flyout.Items.Add(menuFormato);
            }

            var menuCores = new MenuItem
            {
                Icon = AppIcons.Create(AppIcons.Palette, 11, ThemePalette.IconLightGray),
                Header = "Mudar Cor"
            };
            foreach (var cor in PaletaCoresJanela)
            {
                IBrush dotBrush;
                if (string.IsNullOrWhiteSpace(cor.Hex))
                {
                    dotBrush = ThemePalette.IconMutedGray;
                }
                else
                {
                    try { dotBrush = SolidColorBrush.Parse(cor.Hex); }
                    catch { dotBrush = ThemePalette.Accent; }
                }

                var item = new MenuItem
                {
                    Icon = new Avalonia.Controls.Shapes.Ellipse
                    {
                        Width = 10,
                        Height = 10,
                        Fill = dotBrush,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    Header = cor.Nome
                };
                string hex = cor.Hex;
                item.Click += (s, e) => DefinirCorJanela(hex);
                menuCores.Items.Add(item);
            }
            flyout.Items.Add(menuCores);

            var menuCriticidade = new MenuItem
            {
                Icon = AppIcons.Create(AppIcons.Warning, 11, ThemePalette.IconLightGray),
                Header = "Criticidade"
            };
            foreach (var crit in NiveisCriticidade)
            {
                IBrush critBrush = ThemePalette.IconMutedGray;
                if (!string.IsNullOrWhiteSpace(crit.ColorHex))
                {
                    try { critBrush = SolidColorBrush.Parse(crit.ColorHex); }
                    catch { }
                }

                var item = new MenuItem
                {
                    Icon = AppIcons.Create(crit.Key == "none" ? AppIcons.Minus : AppIcons.Warning, 10.5, critBrush),
                    Header = crit.Label
                };
                string key = crit.Key;
                item.Click += (s, e) => DefinirCriticidade(key);
                menuCriticidade.Items.Add(item);
            }
            flyout.Items.Add(menuCriticidade);

            if (IsFlowchartShape)
            {
                var menuTexto = new MenuItem
                {
                    Icon = AppIcons.Create(AppIcons.Edit, 11, ThemePalette.IconLightGray),
                    Header = "Formatação de Texto"
                };
                var miBold = new MenuItem { Header = "Negrito (Ctrl+B)" };
                miBold.Click += (s, e) => AlternarNegrito();
                var miItalic = new MenuItem { Header = "Itálico (Ctrl+I)" };
                miItalic.Click += (s, e) => AlternarItalico();
                var miInc = new MenuItem { Icon = AppIcons.Create(AppIcons.Plus, 10), Header = "Aumentar Fonte" };
                miInc.Click += (s, e) => AlterarTamanhoFonte(1);
                var miDec = new MenuItem { Icon = AppIcons.Create(AppIcons.Minus, 10), Header = "Diminuir Fonte" };
                miDec.Click += (s, e) => AlterarTamanhoFonte(-1);
                menuTexto.Items.Add(miBold);
                menuTexto.Items.Add(miItalic);
                menuTexto.Items.Add(new Separator());
                menuTexto.Items.Add(miInc);
                menuTexto.Items.Add(miDec);
                flyout.Items.Add(menuTexto);
            }

            flyout.Items.Add(new Separator());

            var miConnect = new MenuItem
            {
                Icon = AppIcons.Create(AppIcons.Link, 11, ThemePalette.IconLightGray),
                Header = "Conectar (Fluxograma)"
            };
            miConnect.Click += (s, e) => OnConnectRequested?.Invoke(this);
            flyout.Items.Add(miConnect);

            var miSave = new MenuItem
            {
                Icon = AppIcons.Create(AppIcons.Save, 11, ThemePalette.IconLightGray),
                Header = _onLinkedRowSave != null ? "Salvar Nota na Lista (Ctrl+S)" : "Salvar (Ctrl+S)"
            };
            miSave.Click += (s, e) => SalvarJanela();
            flyout.Items.Add(miSave);

            if (!IsFlowchartShape)
            {
                var miRename = new MenuItem
                {
                    Icon = AppIcons.Create(AppIcons.Edit, 11, ThemePalette.IconLightGray),
                    Header = "Renomear Janela"
                };
                miRename.Click += (s, e) => IniciarEdicaoTitulo();
                flyout.Items.Add(miRename);
            }

            flyout.Items.Add(new Separator());

            var miDelete = new MenuItem
            {
                Icon = AppIcons.Create(AppIcons.Trash, 11, ThemePalette.IconLightGray),
                Header = "Excluir (Delete)"
            };
            miDelete.Click += (s, e) => Fechar();
            flyout.Items.Add(miDelete);

            return flyout;
        }

        private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            ZIndexManager.BringToFront(this);
            OnWindowClicked?.Invoke(this);

            var source = e.Source as Control;
            if (source is not TextBox)
            {
                Focus();
            }

            if (source == _txtTitleEditor || _txtTitleEditor.IsVisible)
            {
                return;
            }

            if (IsFlowchartShape)
            {
                bool clicouConfig = _btnShapeConfig != null && (source == _btnShapeConfig || source?.Parent == _btnShapeConfig);
                if (!clicouConfig && (source != _noteTextBox))
                {
                    var props = e.GetCurrentPoint(this);
                    if (props.Properties.IsLeftButtonPressed && !_isMaximized)
                    {
                        var detected = DetectarBordaFormaFluxograma(e.GetPosition(this));
                        if (detected.Mode != ResizeMode.None)
                        {
                            IniciarRedimensionamento(detected.Mode, e);
                            return;
                        }

                        _isDragging = true;
                        _pointerStart = e.GetPosition(Workspace);
                        _windowStart = new Point(Canvas.GetLeft(this), Canvas.GetTop(this));
                        e.Pointer.Capture(this);
                    }
                }
                return;
            }

            bool clicouTextoTitulo = source == _lblTitle;
            bool clicouBarraTitulo = source != null && (
                source.Name == "TitleBar" ||
                source.Parent == _titleBar ||
                clicouTextoTitulo ||
                source.Parent is StackPanel && source.Parent.Parent is Panel);

            if (clicouBarraTitulo)
            {
                if (e.ClickCount == 2 && !_isMinimized && !clicouTextoTitulo)
                {
                    ToggleMaximize();
                    e.Handled = true;
                    return;
                }

                var props = e.GetCurrentPoint(this);
                if (props.Properties.IsLeftButtonPressed)
                {
                    _pressedOnTitleText = clicouTextoTitulo;
                    if (!_isMaximized)
                    {
                        _isDragging = true;
                        _pointerStart = e.GetPosition(Workspace);
                        _windowStart = new Point(Canvas.GetLeft(this), Canvas.GetTop(this));
                        e.Pointer.Capture(this);
                    }
                    else if (clicouTextoTitulo)
                    {
                        IniciarEdicaoTitulo();
                    }
                }
            }
        }

        private static double DistanciaPontoSegmento(Point p, Point a, Point b)
        {
            double vx = b.X - a.X;
            double vy = b.Y - a.Y;
            double wx = p.X - a.X;
            double wy = p.Y - a.Y;
            double c1 = vx * wx + vy * wy;
            if (c1 <= 0) return Math.Sqrt(wx * wx + wy * wy);
            double c2 = vx * vx + vy * vy;
            if (c2 <= c1) return Math.Sqrt((p.X - b.X) * (p.X - b.X) + (p.Y - b.Y) * (p.Y - b.Y));
            double t = c1 / c2;
            double px = a.X + t * vx;
            double py = a.Y + t * vy;
            return Math.Sqrt((p.X - px) * (p.X - px) + (p.Y - py) * (p.Y - py));
        }

        private (ResizeMode Mode, StandardCursorType Cursor) DetectarBordaFormaFluxograma(Point pos)
        {
            double w = Bounds.Width > 1 ? Bounds.Width : Width;
            double h = Bounds.Height > 1 ? Bounds.Height : Height;
            if (w <= 10 || h <= 10 || _isMaximized || _isMinimized)
                return (ResizeMode.None, StandardCursorType.Arrow);

            double rx = w / 2.0;
            double ry = h / 2.0;
            double dx = (pos.X - rx) / rx;
            double dy = (pos.Y - ry) / ry;

            if (WindowType == "shape_circle")
            {
                double r = Math.Sqrt(dx * dx + dy * dy);
                if (r < 0.78 || r > 1.18)
                    return (ResizeMode.None, StandardCursorType.Arrow);
            }
            else if (WindowType == "shape_diamond")
            {
                double d = Math.Abs(dx) + Math.Abs(dy);
                if (d < 0.78 || d > 1.18)
                    return (ResizeMode.None, StandardCursorType.Arrow);
            }
            else if (WindowType == "shape_alert")
            {
                var top = new Point(w / 2.0, 4);
                var br = new Point(w - 4, h - 4);
                var bl = new Point(4, h - 4);
                double minDist = Math.Min(
                    DistanciaPontoSegmento(pos, top, br),
                    Math.Min(DistanciaPontoSegmento(pos, br, bl), DistanciaPontoSegmento(pos, bl, top)));
                if (minDist > 11)
                    return (ResizeMode.None, StandardCursorType.Arrow);
            }
            else
            {
                bool nearEdge = pos.X <= 10 || pos.X >= w - 10 || pos.Y <= 10 || pos.Y >= h - 10;
                if (!nearEdge)
                    return (ResizeMode.None, StandardCursorType.Arrow);
            }

            bool left = dx < -0.35;
            bool right = dx > 0.35;
            bool topEdge = dy < -0.35;
            bool bottomEdge = dy > 0.35;

            if (topEdge && left) return (ResizeMode.TopLeft, StandardCursorType.TopLeftCorner);
            if (topEdge && right) return (ResizeMode.TopRight, StandardCursorType.TopRightCorner);
            if (bottomEdge && left) return (ResizeMode.BottomLeft, StandardCursorType.BottomLeftCorner);
            if (bottomEdge && right) return (ResizeMode.BottomRight, StandardCursorType.BottomRightCorner);
            if (left) return (ResizeMode.Left, StandardCursorType.SizeWestEast);
            if (right) return (ResizeMode.Right, StandardCursorType.SizeWestEast);
            if (topEdge) return (ResizeMode.Top, StandardCursorType.SizeNorthSouth);
            if (bottomEdge) return (ResizeMode.Bottom, StandardCursorType.SizeNorthSouth);
            return (ResizeMode.BottomRight, StandardCursorType.BottomRightCorner);
        }

        private void OnWindowPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isDragging && _resizeMode == ResizeMode.None)
            {
                if (IsFlowchartShape)
                {
                    var detected = DetectarBordaFormaFluxograma(e.GetPosition(this));
                    Cursor = new Cursor(detected.Cursor);
                }
                return;
            }

            var currentPoint = e.GetPosition(Workspace);

            if (_isDragging)
            {
                var diff = currentPoint - _pointerStart;
                double newX = Math.Max(0, _windowStart.X + diff.X);
                double newY = Math.Max(0, _windowStart.Y + diff.Y);

                Canvas.SetLeft(this, newX);
                Canvas.SetTop(this, newY);
                _browserHost?.AtualizarLimitesNativos();

                if (_isMinimized)
                {
                    _normalRect = new Rect(newX, newY, _normalRect.Width, _normalRect.Height);
                }
                OnBoundsChanged?.Invoke(this);
            }
            else if (_resizeMode != ResizeMode.None)
            {
                var diff = currentPoint - _pointerStart;
                double newLeft = _rectStart.X;
                double newTop = _rectStart.Y;
                double newWidth = _rectStart.Width;
                double newHeight = _rectStart.Height;

                double minW = IsFlowchartShape ? 140 : 220;
                double minH = IsFlowchartShape ? 95 : 140;

                if (_resizeMode is ResizeMode.Right or ResizeMode.TopRight or ResizeMode.BottomRight)
                {
                    newWidth = Math.Max(minW, _rectStart.Width + diff.X);
                }
                else if (_resizeMode is ResizeMode.Left or ResizeMode.TopLeft or ResizeMode.BottomLeft)
                {
                    double rightEdge = _rectStart.X + _rectStart.Width;
                    newLeft = Math.Clamp(_rectStart.X + diff.X, 0, Math.Max(0, rightEdge - minW));
                    newWidth = Math.Max(minW, rightEdge - newLeft);
                }

                if (_resizeMode is ResizeMode.Bottom or ResizeMode.BottomLeft or ResizeMode.BottomRight)
                {
                    newHeight = Math.Max(minH, _rectStart.Height + diff.Y);
                }
                else if (_resizeMode is ResizeMode.Top or ResizeMode.TopLeft or ResizeMode.TopRight)
                {
                    double bottomEdge = _rectStart.Y + _rectStart.Height;
                    newTop = Math.Clamp(_rectStart.Y + diff.Y, 0, Math.Max(0, bottomEdge - minH));
                    newHeight = Math.Max(minH, bottomEdge - newTop);
                }

                Canvas.SetLeft(this, newLeft);
                Canvas.SetTop(this, newTop);
                Width = newWidth;
                Height = newHeight;
                _browserHost?.AtualizarLimitesNativos();
                AtualizarTextoDimensoes();
                OnBoundsChanged?.Invoke(this);
            }
        }

        private void OnWindowPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            bool abrirEdicaoTitulo = false;
            if (_pressedOnTitleText)
            {
                var diff = e.GetPosition(Workspace) - _pointerStart;
                if (Math.Abs(diff.X) < 3 && Math.Abs(diff.Y) < 3)
                {
                    abrirEdicaoTitulo = true;
                }
                _pressedOnTitleText = false;
            }

            if (_isDragging || _resizeMode != ResizeMode.None)
            {
                _isDragging = false;
                _resizeMode = ResizeMode.None;
                e.Pointer.Capture(null);
                OnInteractionCompleted?.Invoke(this);
            }

            if (abrirEdicaoTitulo)
            {
                IniciarEdicaoTitulo();
            }
        }

        private void AtualizarTextoDimensoes()
        {
            if (_lblContentInfo != null)
            {
                _lblContentInfo.Text = $"Arraste pelo topo • Duplo clique para maximizar\nDimensões: {(int)Width} × {(int)Height} px";
            }
        }

        public void ToggleMinimize()
        {
            if (!_isMinimized)
            {
                if (!_isMaximized)
                {
                    _normalRect = new Rect(Canvas.GetLeft(this), Canvas.GetTop(this), Width, Height);
                }
                _contentPanel.IsVisible = false;
                Height = 34;
                _isMinimized = true;
                _btnMinText.Text = "▫";
            }
            else
            {
                _contentPanel.IsVisible = true;
                Height = _isMaximized ? Math.Max(140, Workspace.Bounds.Height) : _normalRect.Height;
                _isMinimized = false;
                _btnMinText.Text = "—";
            }
            _browserHost?.SetBrowserVisible(!_isMinimized);
            AtualizarHibernacaoGaleriaEImagem(_isMinimized);
            SincronizarEstadoHibernacaoRecursos();
            _taskBar.AtualizarEstadoMinimizado(this, _isMinimized);
            OnBoundsChanged?.Invoke(this);
        }

        public void ToggleMaximize()
        {
            if (_isMinimized)
            {
                ToggleMinimize();
            }

            if (!_isMaximized)
            {
                _normalRect = new Rect(Canvas.GetLeft(this), Canvas.GetTop(this), Width, Height);

                Canvas.SetLeft(this, 0);
                Canvas.SetTop(this, 0);
                Width = Math.Max(220, Workspace.Bounds.Width);
                Height = Math.Max(140, Workspace.Bounds.Height);
                CornerRadius = new CornerRadius(0);
                _titleBar.CornerRadius = new CornerRadius(0);

                _isMaximized = true;
                _btnMaxText.Text = "❐";
            }
            else
            {
                Canvas.SetLeft(this, _normalRect.X);
                Canvas.SetTop(this, _normalRect.Y);
                Width = _normalRect.Width;
                Height = _normalRect.Height;
                CornerRadius = new CornerRadius(8);
                _titleBar.CornerRadius = new CornerRadius(7, 7, 0, 0);

                _isMaximized = false;
                _btnMaxText.Text = "▢";
            }
            AtualizarTextoDimensoes();
            OnBoundsChanged?.Invoke(this);
        }
    }
}
