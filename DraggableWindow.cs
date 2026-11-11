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
    public class DraggableWindow : Border
    {
        public event Action? OnClosed;
        public event Action<string>? OnRequestOpenNotepad;
        public string WindowTitle { get; private set; }
        public string WindowType { get; }
        public bool IsMinimized => _isMinimized;
        public bool IsMaximized => _isMaximized;

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

        private readonly Border _titleBar;
        private readonly Border _statusDot;
        private readonly TextBlock _lblTitle;
        private readonly TextBox _txtTitleEditor;
        private readonly Button _btnEditTitle;
        private readonly TextBlock? _lblContentTitle;
        private readonly TextBlock? _lblContentInfo;
        private readonly TextBox? _noteTextBox;
        private readonly StackPanel? _checklistContainer;
        private readonly Button? _btnAddChecklistItem;
        private readonly List<(CheckBox Check, TextBox Input, Border StrikeLine, Border RowBorder, Button BtnNote)> _checklistRows = new();
        private readonly Border _contentCard;
        private readonly Panel _contentPanel;
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
            List<ChecklistItemData>? initialChecklist = null)
        {
            Workspace = workspace;
            _taskBar = taskBar;
            WindowTitle = title;
            WindowType = string.IsNullOrWhiteSpace(windowType) ? "default" : windowType;
            _isDarkMode = isDarkMode;

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

            var btnMin = CreateControlButton("—", out _btnMinText, (s, e) => ToggleMinimize());
            var btnMax = CreateControlButton("▢", out _btnMaxText, (s, e) => ToggleMaximize());
            var btnClose = CreateControlButton("✕", out _btnCloseText, (s, e) => Fechar());

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
                Padding = new Thickness(WindowType == "notepad" ? 0 : 12),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

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
                    FontSize = 13,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                _contentCard.Child = _noteTextBox;
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
                _btnAddChecklistItem.Click += (s, e) => AdicionarLinhaChecklist("", false, focusInput: true);

                var outerListStack = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 6
                };
                outerListStack.Children.Add(_checklistContainer);
                outerListStack.Children.Add(_btnAddChecklistItem);

                var scrollViewer = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Content = outerListStack
                };

                _contentCard.Child = scrollViewer;

                if (initialChecklist != null && initialChecklist.Count > 0)
                {
                    foreach (var item in initialChecklist)
                    {
                        AdicionarLinhaChecklist(item.Text, item.IsChecked, focusInput: false);
                    }
                }
                else
                {
                    AdicionarLinhaChecklist("", false, focusInput: false);
                }
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
            Workspace.SizeChanged += OnWorkspaceSizeChanged;

            AtualizarCores();
        }

        private void AdicionarLinhaChecklist(string texto, bool marcado, bool focusInput)
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
                FontSize = 12.5
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
                Opacity = 0.85
            };
            ToolTip.SetTip(btnNote, "Abrir Bloco de Notas para esta linha");
            btnNote.Click += (s, e) =>
            {
                string nomeLinha = string.IsNullOrWhiteSpace(inputBox.Text)
                    ? "Nova Nota"
                    : inputBox.Text.Trim();
                OnRequestOpenNotepad?.Invoke(nomeLinha);
            };

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

            inputBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    AdicionarLinhaChecklist("", false, focusInput: true);
                }
            };

            var entry = (checkBox, inputBox, strikeLine, rowBorder, btnNote);
            _checklistRows.Add(entry);

            btnRemove.Click += (s, e) =>
            {
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

        private void AtualizarEstiloLinhaChecklist((CheckBox Check, TextBox Input, Border StrikeLine, Border RowBorder, Button BtnNote) row)
        {
            row.RowBorder.Background = _isDarkMode ? ThemePalette.DarkContentBg : ThemePalette.LightContentBg;
            row.RowBorder.BorderBrush = _isDarkMode ? ThemePalette.DarkBorder : ThemePalette.LightBorder;
            row.Input.Foreground = _isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;
            row.StrikeLine.Background = _isDarkMode ? ThemePalette.DarkTextSecondary : ThemePalette.LightTextSecondary;
            row.BtnNote.Foreground = _isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;
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
            Rect bounds = GetNormalBounds();
            var data = new WindowStateData
            {
                Title = WindowTitle,
                WindowType = WindowType,
                X = bounds.X,
                Y = bounds.Y,
                Width = bounds.Width,
                Height = bounds.Height,
                NoteContent = _noteTextBox?.Text ?? string.Empty
            };

            if (WindowType == "checklist")
            {
                foreach (var row in _checklistRows)
                {
                    data.ChecklistItems.Add(new ChecklistItemData
                    {
                        Text = row.Input.Text ?? string.Empty,
                        IsChecked = row.Check.IsChecked == true
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
        }

        public void Fechar()
        {
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

    public static class ZIndexManager
    {
        private static int _maxZIndex = 1;
        private static DraggableWindow? _activeWindow;

        public static void BringToFront(Control window)
        {
            _maxZIndex++;
            window.SetValue(Panel.ZIndexProperty, _maxZIndex);

            if (window is DraggableWindow dw && _activeWindow != dw)
            {
                _activeWindow?.SetActive(false);
                _activeWindow = dw;
                _activeWindow.SetActive(true);
            }
        }
    }
}
