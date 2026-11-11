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
    public class ChecklistRowState
    {
        public CheckBox Check { get; init; } = null!;
        public TextBox Input { get; init; } = null!;
        public Border StrikeLine { get; init; } = null!;
        public Border RowBorder { get; init; } = null!;
        public Button BtnNote { get; init; } = null!;
        public string LinkedNoteContent { get; set; } = string.Empty;
        public TextFormatData LinkedNoteFormat { get; set; } = new();
        public DraggableWindow? OpenNoteWindow { get; set; }
    }

    public class DraggableWindow : Border
    {
        public event Action? OnClosed;
        public event Action<DraggableWindow, ChecklistRowState>? OnRequestOpenRowNotepad;
        public event Action<DraggableWindow>? OnSaveRequested;

        public string WindowId { get; }
        public string WindowTitle { get; private set; }
        public string WindowType { get; }
        public int AccessCount { get; set; }
        public bool IsMinimized => _isMinimized;
        public bool IsMaximized => _isMaximized;

        private readonly Action<string, string, TextFormatData>? _onLinkedRowSave;
        public DraggableWindow? ParentChecklistWindow { get; }

        private enum ResizeMode { None, Bottom, Right, BottomRight }

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
        private readonly TextBox? _noteTextBox;
        private readonly StackPanel? _checklistContainer;
        private readonly Button? _btnAddChecklistItem;
        private readonly List<ChecklistRowState> _checklistRows = new();
        private readonly Border _contentCard;
        private readonly Panel _contentPanel;
        private readonly TextBlock _btnSaveText;
        private readonly TextBlock _btnMinText;
        private readonly TextBlock _btnMaxText;
        private readonly TextBlock _btnCloseText;
        private readonly TaskBar _taskBar;

        public Canvas Workspace { get; }

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
            TextFormatData? initialFormat = null)
        {
            Workspace = workspace;
            _taskBar = taskBar;
            WindowId = string.IsNullOrWhiteSpace(windowId) ? Guid.NewGuid().ToString("N") : windowId;
            WindowTitle = title;
            WindowType = string.IsNullOrWhiteSpace(windowType) ? "default" : windowType;
            AccessCount = Math.Max(1, accessCount);
            ParentChecklistWindow = parentChecklistWindow;
            _onLinkedRowSave = onLinkedRowSave;
            _isDarkMode = isDarkMode;

            if (initialFormat != null)
            {
                _isBold = initialFormat.IsBold;
                _isItalic = initialFormat.IsItalic;
                _fontSize = Math.Clamp(initialFormat.FontSize > 0 ? initialFormat.FontSize : 13, 9, 32);
                _alignment = string.IsNullOrWhiteSpace(initialFormat.Alignment) ? "left" : initialFormat.Alignment;
                _fontFamilyName = string.IsNullOrWhiteSpace(initialFormat.FontFamilyName) ? "default" : initialFormat.FontFamilyName;
            }

            Width = Math.Max(220, width);
            Height = Math.Max(140, height);
            _normalRect = new Rect(x, y, Width, Height);

            Canvas.SetLeft(this, x);
            Canvas.SetTop(this, y);

            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(8);
            ClipToBounds = true;

            var rootLayout = new DockPanel();

            _titleBar = new Border
            {
                Name = "TitleBar",
                Height = 34,
                CornerRadius = new CornerRadius(7, 7, 0, 0),
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
                Content = "✎",
                Width = 22,
                Height = 22,
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                CornerRadius = new CornerRadius(4),
                FontSize = 11,
                Opacity = 0.75
            };
            ToolTip.SetTip(_btnEditTitle, "Renomear janela");
            _btnEditTitle.Click += (s, e) => IniciarEdicaoTitulo();

            var titleDisplayStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
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

            var btnSave = CreateControlButton("💾", out _btnSaveText, (s, e) => SalvarJanela());
            ToolTip.SetTip(btnSave, _onLinkedRowSave != null ? "Salvar nota vinculada à linha da lista (Ctrl+S)" : "Salvar janela (Ctrl+S)");

            var btnMin = CreateControlButton("—", out _btnMinText, (s, e) => ToggleMinimize());
            var btnMax = CreateControlButton("▢", out _btnMaxText, (s, e) => ToggleMaximize());
            var btnClose = CreateControlButton("✕", out _btnCloseText, (s, e) => Fechar());

            btnPanel.Children.Add(btnSave);
            btnPanel.Children.Add(btnMin);
            btnPanel.Children.Add(btnMax);
            btnPanel.Children.Add(btnClose);
            grid.Children.Add(btnPanel);

            DockPanel.SetDock(_titleBar, Dock.Top);
            rootLayout.Children.Add(_titleBar);

            _contentPanel = new Panel();

            _contentCard = new Border
            {
                Margin = new Thickness(10),
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            if (WindowType == "notepad" || WindowType == "checklist")
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

                if (WindowType == "notepad")
                {
                    _noteTextBox = new TextBox
                    {
                        Text = initialNote,
                        Watermark = "Digite suas anotações aqui...",
                        AcceptsReturn = true,
                        TextWrapping = TextWrapping.Wrap,
                        BorderThickness = new Thickness(0),
                        Background = Brushes.Transparent,
                        Padding = new Thickness(12),
                        FontSize = _fontSize,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch
                    };
                    cardDock.Children.Add(_noteTextBox);
                }
                else
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

                    cardDock.Children.Add(scrollViewer);

                    if (initialChecklist != null && initialChecklist.Count > 0)
                    {
                        foreach (var item in initialChecklist)
                        {
                            AdicionarLinhaChecklist(item.Text, item.IsChecked, item.LinkedNoteContent, item.LinkedNoteFormat, focusInput: false);
                        }
                    }
                    else
                    {
                        AdicionarLinhaChecklist("", false, "", null, focusInput: false);
                    }
                }

                _contentCard.Child = cardDock;
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
            Child = mainGrid;

            PointerPressed += OnWindowPointerPressed;
            PointerMoved += OnWindowPointerMoved;
            PointerReleased += OnWindowPointerReleased;
            KeyDown += OnWindowKeyDown;
            Workspace.SizeChanged += OnWorkspaceSizeChanged;

            AplicarFormatacaoTexto();
            AtualizarCores();
        }

        private Border CriarBarraFormatacao(
            out Button btnBold,
            out Button btnItalic,
            out Button btnFontDec,
            out TextBlock lblFontSize,
            out Button btnFontInc,
            out Button btnFormatMenu)
        {
            var bar = new Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(8, 4),
                Height = 32
            };

            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center
            };

            btnBold = CriarBotaoFormatacao("B", "Negrito (Ctrl+B)", FontWeight.Bold, FontStyle.Normal);
            btnBold.Click += (s, e) => AlternarNegrito();

            btnItalic = CriarBotaoFormatacao("I", "Itálico (Ctrl+I)", FontWeight.SemiBold, FontStyle.Italic);
            btnItalic.Click += (s, e) => AlternarItalico();

            btnFontDec = CriarBotaoFormatacao("A-", "Diminuir fonte (Ctrl+-)", FontWeight.SemiBold, FontStyle.Normal, width: 28);
            btnFontDec.Click += (s, e) => AlterarTamanhoFonte(-1);

            lblFontSize = new TextBlock
            {
                Text = $"{(int)_fontSize}pt",
                FontSize = 11,
                FontWeight = FontWeight.Medium,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0)
            };

            btnFontInc = CriarBotaoFormatacao("A+", "Aumentar fonte (Ctrl++)", FontWeight.SemiBold, FontStyle.Normal, width: 28);
            btnFontInc.Click += (s, e) => AlterarTamanhoFonte(1);

            btnFormatMenu = new Button
            {
                Content = "Aa ▾",
                Height = 22,
                Padding = new Thickness(8, 0),
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(btnFormatMenu, "Mais opções de formatação de texto");

            var flyout = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedLeft };

            var miBold = new MenuItem { Header = "𝐁 Alternar Negrito (Ctrl+B)" };
            miBold.Click += (s, e) => AlternarNegrito();

            var miItalic = new MenuItem { Header = "𝐼 Alternar Itálico (Ctrl+I)" };
            miItalic.Click += (s, e) => AlternarItalico();

            var miFontInc = new MenuItem { Header = "A+ Aumentar Fonte" };
            miFontInc.Click += (s, e) => AlterarTamanhoFonte(2);

            var miFontDec = new MenuItem { Header = "A- Diminuir Fonte" };
            miFontDec.Click += (s, e) => AlterarTamanhoFonte(-2);

            var miAlignLeft = new MenuItem { Header = "⯇ Alinhar à Esquerda" };
            miAlignLeft.Click += (s, e) => DefinirAlinhamento("left");

            var miAlignCenter = new MenuItem { Header = "⯌ Centralizar Texto" };
            miAlignCenter.Click += (s, e) => DefinirAlinhamento("center");

            var miAlignRight = new MenuItem { Header = "⯈ Alinhar à Direita" };
            miAlignRight.Click += (s, e) => DefinirAlinhamento("right");

            var miFontDefault = new MenuItem { Header = "Fonte Padrão (Sans-Serif)" };
            miFontDefault.Click += (s, e) => DefinirFamiliaFonte("default");

            var miFontMono = new MenuItem { Header = "Fonte Monoespaçada (Código)" };
            miFontMono.Click += (s, e) => DefinirFamiliaFonte("mono");

            var miFontSerif = new MenuItem { Header = "Fonte Serifada (Leitura)" };
            miFontSerif.Click += (s, e) => DefinirFamiliaFonte("serif");

            var miReset = new MenuItem { Header = "↺ Restaurar Formatação Padrão" };
            miReset.Click += (s, e) => RestaurarFormatacaoPadrao();

            flyout.Items.Add(miBold);
            flyout.Items.Add(miItalic);
            flyout.Items.Add(new Separator());
            flyout.Items.Add(miFontInc);
            flyout.Items.Add(miFontDec);
            flyout.Items.Add(new Separator());
            flyout.Items.Add(miAlignLeft);
            flyout.Items.Add(miAlignCenter);
            flyout.Items.Add(miAlignRight);
            flyout.Items.Add(new Separator());
            flyout.Items.Add(miFontDefault);
            flyout.Items.Add(miFontMono);
            flyout.Items.Add(miFontSerif);
            flyout.Items.Add(new Separator());
            flyout.Items.Add(miReset);

            btnFormatMenu.Flyout = flyout;

            stack.Children.Add(btnBold);
            stack.Children.Add(btnItalic);
            stack.Children.Add(btnFontDec);
            stack.Children.Add(lblFontSize);
            stack.Children.Add(btnFontInc);
            stack.Children.Add(btnFormatMenu);

            bar.Child = stack;
            return bar;
        }

        private Button CriarBotaoFormatacao(string texto, string tooltip, FontWeight weight, FontStyle style, double width = 24)
        {
            var btn = new Button
            {
                Content = texto,
                Width = width,
                Height = 22,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                FontSize = 11,
                FontWeight = weight,
                FontStyle = style,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(btn, tooltip);
            return btn;
        }

        private void AlternarNegrito()
        {
            _isBold = !_isBold;
            AplicarFormatacaoTexto();
            AtualizarCores();
        }

        private void AlternarItalico()
        {
            _isItalic = !_isItalic;
            AplicarFormatacaoTexto();
            AtualizarCores();
        }

        private void AlterarTamanhoFonte(double delta)
        {
            _fontSize = Math.Clamp(_fontSize + delta, 9, 32);
            if (_lblFontSize != null)
            {
                _lblFontSize.Text = $"{(int)_fontSize}pt";
            }
            AplicarFormatacaoTexto();
        }

        private void DefinirAlinhamento(string alignment)
        {
            _alignment = alignment;
            AplicarFormatacaoTexto();
        }

        private void DefinirFamiliaFonte(string familyName)
        {
            _fontFamilyName = familyName;
            AplicarFormatacaoTexto();
        }

        private void RestaurarFormatacaoPadrao()
        {
            _isBold = false;
            _isItalic = false;
            _fontSize = 13;
            _alignment = "left";
            _fontFamilyName = "default";
            if (_lblFontSize != null)
            {
                _lblFontSize.Text = "13pt";
            }
            AplicarFormatacaoTexto();
            AtualizarCores();
        }

        private static FontFamily ObterFontFamily(string familyName) => familyName switch
        {
            "mono" => new FontFamily("Consolas, Courier New, monospace"),
            "serif" => new FontFamily("Georgia, Times New Roman, serif"),
            _ => FontFamily.Default
        };

        private void AplicarFormatacaoTexto()
        {
            var weight = _isBold ? FontWeight.Bold : FontWeight.Normal;
            var style = _isItalic ? FontStyle.Italic : FontStyle.Normal;
            var align = _alignment == "center"
                ? TextAlignment.Center
                : _alignment == "right"
                    ? TextAlignment.Right
                    : TextAlignment.Left;
            var family = ObterFontFamily(_fontFamilyName);

            if (_noteTextBox != null)
            {
                _noteTextBox.FontWeight = weight;
                _noteTextBox.FontStyle = style;
                _noteTextBox.FontSize = _fontSize;
                _noteTextBox.TextAlignment = align;
                _noteTextBox.FontFamily = family;
            }

            foreach (var row in _checklistRows)
            {
                row.Input.FontWeight = weight;
                row.Input.FontStyle = style;
                row.Input.FontSize = _fontSize;
                row.Input.TextAlignment = align;
                row.Input.FontFamily = family;
            }
        }

        public TextFormatData ExportTextFormat() => new()
        {
            IsBold = _isBold,
            IsItalic = _isItalic,
            FontSize = _fontSize,
            Alignment = _alignment,
            FontFamilyName = _fontFamilyName
        };

        private void OnWindowKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                if (e.Key == Key.S)
                {
                    e.Handled = true;
                    SalvarJanela();
                }
                else if (e.Key == Key.B)
                {
                    e.Handled = true;
                    AlternarNegrito();
                }
                else if (e.Key == Key.I)
                {
                    e.Handled = true;
                    AlternarItalico();
                }
                else if (e.Key == Key.OemPlus || e.Key == Key.Add)
                {
                    e.Handled = true;
                    AlterarTamanhoFonte(1);
                }
                else if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
                {
                    e.Handled = true;
                    AlterarTamanhoFonte(-1);
                }
            }
        }

        public void SalvarJanela()
        {
            ConfirmarEdicaoTitulo();
            SincronizarNotaVinculada();
            OnSaveRequested?.Invoke(this);
        }

        public void SincronizarNotaVinculada()
        {
            if (_onLinkedRowSave != null && _noteTextBox != null)
            {
                _onLinkedRowSave(WindowTitle, _noteTextBox.Text ?? string.Empty, ExportTextFormat());
            }
        }

        private void AdicionarLinhaChecklist(string texto, bool marcado, string notaVinculada, TextFormatData? formatoNotaVinculada, bool focusInput)
        {
            if (_checklistContainer == null) return;

            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var checkBox = new CheckBox
            {
                IsChecked = marcado,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 4, 0)
            };

            var inputBox = new TextBox
            {
                Text = texto,
                Watermark = "Digite um item...",
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(4, 6),
                FontSize = _fontSize,
                FontWeight = _isBold ? FontWeight.Bold : FontWeight.Normal,
                FontStyle = _isItalic ? FontStyle.Italic : FontStyle.Normal,
                FontFamily = ObterFontFamily(_fontFamilyName)
            };

            var btnNote = new Button
            {
                Content = "📝",
                Width = 26,
                Height = 24,
                Padding = new Thickness(0),
                Margin = new Thickness(2, 0, 2, 0),
                Background = Brushes.Transparent,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                CornerRadius = new CornerRadius(4),
                FontSize = 11.5,
                Opacity = string.IsNullOrWhiteSpace(notaVinculada) ? 0.65 : 1.0
            };
            ToolTip.SetTip(btnNote, "Abrir Bloco de Notas atrelado a esta linha");

            var btnRemove = new Button
            {
                Content = "✕",
                Width = 24,
                Height = 24,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 4, 0),
                Background = Brushes.Transparent,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                CornerRadius = new CornerRadius(4),
                FontSize = 10,
                Opacity = 0.6
            };
            ToolTip.SetTip(btnRemove, "Excluir linha");

            var strikeLine = new Border
            {
                Height = 1.5,
                Margin = new Thickness(6, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsHitTestVisible = false,
                IsVisible = marcado
            };

            var rowBorder = new Border
            {
                CornerRadius = new CornerRadius(5),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(2),
                Child = rowGrid
            };

            void AtualizarEstiloMarcacao()
            {
                bool isChecked = checkBox.IsChecked == true;
                strikeLine.IsVisible = isChecked;
                inputBox.Opacity = isChecked ? 0.5 : 1.0;
            }

            checkBox.IsCheckedChanged += (s, e) => AtualizarEstiloMarcacao();

            var entry = new ChecklistRowState
            {
                Check = checkBox,
                Input = inputBox,
                StrikeLine = strikeLine,
                RowBorder = rowBorder,
                BtnNote = btnNote,
                LinkedNoteContent = notaVinculada ?? string.Empty,
                LinkedNoteFormat = formatoNotaVinculada ?? new TextFormatData()
            };
            _checklistRows.Add(entry);

            inputBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    AdicionarLinhaChecklist("", false, "", null, focusInput: true);
                }
            };

            inputBox.TextChanged += (s, e) =>
            {
                if (entry.OpenNoteWindow != null && !string.IsNullOrWhiteSpace(inputBox.Text))
                {
                    entry.OpenNoteWindow.AtualizarTituloExterno(inputBox.Text.Trim());
                }
            };

            btnNote.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(inputBox.Text))
                {
                    inputBox.Text = $"Item {_checklistRows.IndexOf(entry) + 1}";
                }
                OnRequestOpenRowNotepad?.Invoke(this, entry);
            };

            btnRemove.Click += (s, e) =>
            {
                entry.OpenNoteWindow?.Fechar();
                _checklistRows.Remove(entry);
                _checklistContainer.Children.Remove(rowBorder);
            };

            Grid.SetColumn(checkBox, 0);
            Grid.SetColumn(inputBox, 1);
            Grid.SetColumn(strikeLine, 1);
            Grid.SetColumn(btnNote, 2);
            Grid.SetColumn(btnRemove, 3);
            rowGrid.Children.Add(checkBox);
            rowGrid.Children.Add(inputBox);
            rowGrid.Children.Add(strikeLine);
            rowGrid.Children.Add(btnNote);
            rowGrid.Children.Add(btnRemove);

            _checklistContainer.Children.Add(rowBorder);

            AtualizarEstiloLinhaChecklist(entry);
            AtualizarEstiloMarcacao();

            if (focusInput)
            {
                inputBox.Focus();
            }
        }

        public void AtualizarIndicadorNotaLinha(ChecklistRowState row)
        {
            bool temNota = !string.IsNullOrWhiteSpace(row.LinkedNoteContent);
            row.BtnNote.Opacity = temNota ? 1.0 : 0.65;
            row.BtnNote.BorderThickness = temNota ? new Thickness(1) : new Thickness(0);
            row.BtnNote.BorderBrush = temNota ? ThemePalette.AccentLight : Brushes.Transparent;
        }

        private void AtualizarEstiloLinhaChecklist(ChecklistRowState row)
        {
            row.RowBorder.Background = _isDarkMode ? ThemePalette.DarkContentBg : ThemePalette.LightContentBg;
            row.RowBorder.BorderBrush = _isDarkMode ? ThemePalette.DarkBorder : ThemePalette.LightBorder;
            row.Input.Foreground = _isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;
            row.StrikeLine.Background = _isDarkMode ? ThemePalette.DarkTextSecondary : ThemePalette.LightTextSecondary;
            row.BtnNote.Foreground = _isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;
            AtualizarIndicadorNotaLinha(row);
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

        public string ObterTextoNota() => _noteTextBox?.Text ?? string.Empty;

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
                X = bounds.X,
                Y = bounds.Y,
                Width = bounds.Width,
                Height = bounds.Height,
                NoteContent = _noteTextBox?.Text ?? string.Empty,
                TextFormat = ExportTextFormat(),
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
                        Text = row.Input.Text ?? string.Empty,
                        IsChecked = row.Check.IsChecked == true,
                        LinkedNoteContent = row.LinkedNoteContent,
                        LinkedNoteFormat = row.LinkedNoteFormat
                    });
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
            AtualizarCores();
            _taskBar.AtualizarEstadoAtivo(this, isActive);
        }

        private void AtualizarCores()
        {
            if (_isDarkMode)
            {
                Background = ThemePalette.DarkWindowBg;
                BorderBrush = _isActive ? ThemePalette.DarkBorderActive : ThemePalette.DarkBorder;
                _titleBar.Background = _isActive ? ThemePalette.DarkWindowHeaderActiveBg : ThemePalette.DarkWindowHeaderBg;
                _statusDot.Background = _isActive ? Brushes.White : ThemePalette.AccentLight;
                _lblTitle.Foreground = ThemePalette.DarkTextPrimary;
                _btnEditTitle.Foreground = ThemePalette.DarkTextPrimary;
                _txtTitleEditor.Background = ThemePalette.DarkContentBg;
                _txtTitleEditor.Foreground = ThemePalette.DarkTextPrimary;
                _txtTitleEditor.BorderBrush = ThemePalette.DarkBorderActive;
                _btnSaveText.Foreground = ThemePalette.DarkTextPrimary;
                _btnMinText.Foreground = ThemePalette.DarkTextPrimary;
                _btnMaxText.Foreground = ThemePalette.DarkTextPrimary;
                _btnCloseText.Foreground = ThemePalette.DarkTextPrimary;
                _contentPanel.Background = ThemePalette.DarkContentBg;
                _contentCard.Background = ThemePalette.DarkWindowBg;
                _contentCard.BorderBrush = ThemePalette.DarkBorder;

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
                Background = ThemePalette.LightWindowBg;
                BorderBrush = _isActive ? ThemePalette.LightBorderActive : ThemePalette.LightBorder;
                _titleBar.Background = _isActive ? ThemePalette.LightWindowHeaderActiveBg : ThemePalette.LightWindowHeaderBg;
                _statusDot.Background = _isActive ? Brushes.White : ThemePalette.Accent;
                var headerTextBrush = _isActive ? Brushes.White : ThemePalette.LightTextPrimary;
                _lblTitle.Foreground = headerTextBrush;
                _btnEditTitle.Foreground = headerTextBrush;
                _txtTitleEditor.Background = ThemePalette.LightWindowBg;
                _txtTitleEditor.Foreground = ThemePalette.LightTextPrimary;
                _txtTitleEditor.BorderBrush = ThemePalette.LightBorderActive;
                _btnSaveText.Foreground = headerTextBrush;
                _btnMinText.Foreground = headerTextBrush;
                _btnMaxText.Foreground = headerTextBrush;
                _btnCloseText.Foreground = headerTextBrush;
                _contentPanel.Background = ThemePalette.LightContentBg;
                _contentCard.Background = ThemePalette.LightWindowBg;
                _contentCard.BorderBrush = ThemePalette.LightBorder;

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
        }

        public void Fechar()
        {
            SincronizarNotaVinculada();
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
            parentGrid.Children.Add(CriarGripBorda(ResizeMode.Right, thickness, double.NaN, HorizontalAlignment.Right, VerticalAlignment.Stretch, StandardCursorType.SizeWestEast));
            parentGrid.Children.Add(CriarGripBorda(ResizeMode.Bottom, double.NaN, thickness, HorizontalAlignment.Stretch, VerticalAlignment.Bottom, StandardCursorType.SizeNorthSouth));
            parentGrid.Children.Add(CriarGripBorda(ResizeMode.BottomRight, thickness + 4, thickness + 4, HorizontalAlignment.Right, VerticalAlignment.Bottom, StandardCursorType.BottomRightCorner));
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

        private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            ZIndexManager.BringToFront(this);

            var source = e.Source as Control;
            if (source == _txtTitleEditor || _txtTitleEditor.IsVisible)
            {
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

        private void OnWindowPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isDragging && _resizeMode == ResizeMode.None) return;

            var currentPoint = e.GetPosition(Workspace);

            if (_isDragging)
            {
                var diff = currentPoint - _pointerStart;
                double newX = Math.Max(0, _windowStart.X + diff.X);
                double newY = Math.Max(0, _windowStart.Y + diff.Y);

                Canvas.SetLeft(this, newX);
                Canvas.SetTop(this, newY);

                if (_isMinimized)
                {
                    _normalRect = new Rect(newX, newY, _normalRect.Width, _normalRect.Height);
                }
            }
            else if (_resizeMode != ResizeMode.None)
            {
                var diff = currentPoint - _pointerStart;
                double newWidth = _rectStart.Width;
                double newHeight = _rectStart.Height;

                if (_resizeMode == ResizeMode.Right || _resizeMode == ResizeMode.BottomRight)
                {
                    newWidth = Math.Max(220, _rectStart.Width + diff.X);
                }
                if (_resizeMode == ResizeMode.Bottom || _resizeMode == ResizeMode.BottomRight)
                {
                    newHeight = Math.Max(140, _rectStart.Height + diff.Y);
                }

                Width = newWidth;
                Height = newHeight;
                AtualizarTextoDimensoes();
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
            _taskBar.AtualizarEstadoMinimizado(this, _isMinimized);
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
        }
    }
}
