using Avalonia.Interactivity;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Layout;
using System;

namespace AvaloniaMdiApp
{
    public class DraggableWindow : Border
    {
        public event Action? OnClosed;
        public string WindowTitle { get; }
        public bool IsMinimized => _isMinimized;

        private enum ResizeMode { None, Bottom, Right, BottomRight }

        private bool _isDragging;
        private ResizeMode _resizeMode = ResizeMode.None;
        
        private Point _pointerStart;
        private Point _windowStart;
        private Rect _rectStart;

        private bool _isMaximized;
        private bool _isMinimized;
        private Rect _normalRect;

        private Panel _contentPanel;
        private TextBlock _btnMinText;
        private TextBlock _btnMaxText;
        private readonly TaskBar _taskBar;

        public Canvas Workspace { get; }

        public DraggableWindow(Canvas workspace, TaskBar taskBar, string title, double width, double height, double x, double y)
        {
            Workspace = workspace;
            _taskBar = taskBar;
            WindowTitle = title;
            Width = width;
            Height = height;
            _normalRect = new Rect(x, y, width, height);

            Canvas.SetLeft(this, x);
            Canvas.SetTop(this, y);

            Background = SolidColorBrush.Parse("#252526");
            BorderBrush = SolidColorBrush.Parse("#007acc");
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(4);

            var rootLayout = new DockPanel();

            var titleBar = new Border
            {
                Name = "TitleBar",
                Background = SolidColorBrush.Parse("#007acc"),
                Height = 32,
                Child = new Grid()
            };

            var grid = (Grid)titleBar.Child;
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var lblTitle = new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                FontWeight = FontWeight.Bold
            };
            Grid.SetColumn(lblTitle, 0);
            grid.Children.Add(lblTitle);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal };
            Grid.SetColumn(btnPanel, 1);

            var btnMin = CreateControlButton("🕱", out _btnMinText, (s, e) => ToggleMinimize());
            var btnMax = CreateControlButton("🗖", out _btnMaxText, (s, e) => ToggleMaximize());
            var btnClose = CreateControlButton("✕", out _, (s, e) => {
                _taskBar.RemoverJanela(this);
                Workspace.Children.Remove(this);
                OnClosed?.Invoke();
            });

            btnPanel.Children.Add(btnMin);
            btnPanel.Children.Add(btnMax);
            btnPanel.Children.Add(btnClose);
            grid.Children.Add(btnPanel);

            DockPanel.SetDock(titleBar, Dock.Top);
            rootLayout.Children.Add(titleBar);

            _contentPanel = new Panel
            {
                Background = SolidColorBrush.Parse("#1e1e1e")
            };
            
            var lblContent = new TextBlock
            {
                Text = $"Conteúdo da {title}\nArraste, redimensione e minimize!",
                Foreground = Brushes.LightGray,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            _contentPanel.Children.Add(lblContent);
            rootLayout.Children.Add(_contentPanel);

            var mainGrid = new Grid();
            mainGrid.Children.Add(rootLayout);
            AdicionarBordasDeRedimensionamento(mainGrid);
            Child = mainGrid;

            PointerPressed += OnWindowPointerPressed;
            PointerMoved += OnWindowPointerMoved;
            PointerReleased += OnWindowPointerReleased;
        }

        private Button CreateControlButton(string text, out TextBlock textBlockRef, EventHandler<RoutedEventArgs> onClick)
        {
            textBlockRef = new TextBlock { Text = text, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var btn = new Button
            {
                Width = 32, Height = 32,
                Background = Brushes.Transparent,
                Content = textBlockRef,
                CornerRadius = new CornerRadius(0)
            };
            btn.Click += onClick;
            return btn;
        }

        private void AdicionarBordasDeRedimensionamento(Grid parentGrid)
        {
            int thickness = 6;
            parentGrid.Children.Add(CriarGripBorda(ResizeMode.Right, thickness, Double.NaN, HorizontalAlignment.Right, VerticalAlignment.Stretch, StandardCursorType.SizeWestEast));
            parentGrid.Children.Add(CriarGripBorda(ResizeMode.Bottom, Double.NaN, thickness, HorizontalAlignment.Stretch, VerticalAlignment.Bottom, StandardCursorType.SizeNorthSouth));
            parentGrid.Children.Add(CriarGripBorda(ResizeMode.BottomRight, thickness, thickness, HorizontalAlignment.Right, VerticalAlignment.Bottom, StandardCursorType.BottomRightCorner));
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
            if (_isMaximized || _isMinimized) return;

            var source = e.Source as Control;
            if (source != null && (source.Name == "TitleBar" || source is TextBlock && source.Parent is Grid))
            {
                var props = e.GetCurrentPoint(this);
                if (props.Properties.IsLeftButtonPressed)
                {
                    _isDragging = true;
                    _pointerStart = e.GetPosition(Workspace);
                    _windowStart = new Point(Canvas.GetLeft(this), Canvas.GetTop(this));
                    e.Pointer.Capture(this);
                }
            }
        }

        private void OnWindowPointerMoved(object? sender, PointerEventArgs e)
        {
            var currentPoint = e.GetPosition(Workspace);

            if (_isDragging)
            {
                var diff = currentPoint - _pointerStart;
                Canvas.SetLeft(this, _windowStart.X + diff.X);
                Canvas.SetTop(this, _windowStart.Y + diff.Y);
            }
            else if (_resizeMode != ResizeMode.None)
            {
                var diff = currentPoint - _pointerStart;
                double newWidth = _rectStart.Width;
                double newHeight = _rectStart.Height;

                if (_resizeMode == ResizeMode.Right || _resizeMode == ResizeMode.BottomRight)
                {
                    newWidth = Math.Max(200, _rectStart.Width + diff.X);
                }
                if (_resizeMode == ResizeMode.Bottom || _resizeMode == ResizeMode.BottomRight)
                {
                    newHeight = Math.Max(120, _rectStart.Height + diff.Y);
                }

                Width = newWidth;
                Height = newHeight;
            }
        }

        private void OnWindowPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isDragging || _resizeMode != ResizeMode.None)
            {
                _isDragging = false;
                _resizeMode = ResizeMode.None;
                e.Pointer.Capture(null);
            }
        }

        public void ToggleMinimize()
        {
            if (!_isMinimized)
            {
                _normalRect = new Rect(Canvas.GetLeft(this), Canvas.GetTop(this), Width, Height);
                _contentPanel.IsVisible = false;
                Height = 32;
                _isMinimized = true;
                _btnMinText.Text = "🗗";
            }
            else
            {
                _contentPanel.IsVisible = true;
                Height = _normalRect.Height;
                _isMinimized = false;
                _btnMinText.Text = "🕱";
            }
            _taskBar.AtualizarEstadoMinimizado(this, _isMinimized);
        }

        private void ToggleMaximize()
        {
            if (_isMinimized) return;

            if (!_isMaximized)
            {
                _normalRect = new Rect(Canvas.GetLeft(this), Canvas.GetTop(this), Width, Height);

                Canvas.SetLeft(this, 0);
                Canvas.SetTop(this, 0);
                Width = Workspace.Bounds.Width;
                Height = Workspace.Bounds.Height;

                _isMaximized = true;
                _btnMaxText.Text = "🗗";
            }
            else
            {
                Canvas.SetLeft(this, _normalRect.X);
                Canvas.SetTop(this, _normalRect.Y);
                Width = _normalRect.Width;
                Height = _normalRect.Height;

                _isMaximized = false;
                _btnMaxText.Text = "🗖";
            }
        }
    }

    public static class ZIndexManager
    {
        private static int _maxZIndex = 1;
        public static void BringToFront(Control window)
        {
            _maxZIndex++;
            window.SetValue(Panel.ZIndexProperty, _maxZIndex);
        }
    }
}
