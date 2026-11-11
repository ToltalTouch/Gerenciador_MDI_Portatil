using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System;

namespace AvaloniaMdiApp
{
    public class SettingsWindow : Border
    {
        public event Action<bool, bool>? OnSettingsChanged;
        public event Action? OnClosed;

        private readonly ToggleSwitch _themeToggle;
        private readonly ToggleSwitch _notifToggle;
        private readonly Canvas _workspace;
        private readonly Border _titleBar;
        private readonly TextBlock _lblTitle;
        private readonly Button _btnClose;
        private readonly Border _rowTheme;
        private readonly Border _rowNotif;
        private readonly TextBlock _lblTheme;
        private readonly TextBlock _lblNotif;

        private bool _isDragging;
        private Point _pointerStart;
        private Point _windowStart;

        public SettingsWindow(Canvas workspace, AppSettings settings)
        {
            _workspace = workspace;
            Width = 340;
            Height = 230;
            double initialX = (workspace.Bounds.Width - Width) / 2 > 0 ? (workspace.Bounds.Width - Width) / 2 : 120;
            Canvas.SetLeft(this, initialX);
            Canvas.SetTop(this, 70);

            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(8);
            ClipToBounds = true;

            var rootLayout = new DockPanel();

            _titleBar = new Border
            {
                Name = "TitleBar",
                Height = 36,
                CornerRadius = new CornerRadius(7, 7, 0, 0),
                Child = new Grid()
            };
            var grid = (Grid)_titleBar.Child!;
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            _lblTitle = new TextBlock
            {
                Text = "⚙ Preferências do Sistema",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
                FontWeight = FontWeight.SemiBold,
                FontSize = 13,
                IsHitTestVisible = false
            };
            Grid.SetColumn(_lblTitle, 0);
            grid.Children.Add(_lblTitle);

            _btnClose = new Button
            {
                Content = "✕",
                Width = 28,
                Height = 26,
                Margin = new Thickness(0, 0, 4, 0),
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(4)
            };
            _btnClose.Click += (s, e) => Fechar();
            Grid.SetColumn(_btnClose, 1);
            grid.Children.Add(_btnClose);

            DockPanel.SetDock(_titleBar, Dock.Top);
            rootLayout.Children.Add(_titleBar);

            var contentPanel = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 12
            };

            _lblTheme = new TextBlock
            {
                Text = "Modo Escuro (Dark)",
                FontSize = 13,
                FontWeight = FontWeight.Medium,
                VerticalAlignment = VerticalAlignment.Center
            };
            _themeToggle = new ToggleSwitch
            {
                IsChecked = settings.IsDarkMode,
                OnContent = "Ativo",
                OffContent = "Inativo",
                HorizontalAlignment = HorizontalAlignment.Right
            };
            _themeToggle.IsCheckedChanged += (s, e) => SalvarEAtualizar();

            _rowTheme = CriarLinhaOpcao(_lblTheme, _themeToggle);

            _lblNotif = new TextBlock
            {
                Text = "Notificações Toast",
                FontSize = 13,
                FontWeight = FontWeight.Medium,
                VerticalAlignment = VerticalAlignment.Center
            };
            _notifToggle = new ToggleSwitch
            {
                IsChecked = settings.EnableNotifications,
                OnContent = "Ativo",
                OffContent = "Inativo",
                HorizontalAlignment = HorizontalAlignment.Right
            };
            _notifToggle.IsCheckedChanged += (s, e) => SalvarEAtualizar();

            _rowNotif = CriarLinhaOpcao(_lblNotif, _notifToggle);

            contentPanel.Children.Add(_rowTheme);
            contentPanel.Children.Add(_rowNotif);

            rootLayout.Children.Add(contentPanel);
            Child = rootLayout;

            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;

            AplicarTema(settings.IsDarkMode);
            ZIndexManager.BringToFront(this);
        }

        private Border CriarLinhaOpcao(TextBlock label, ToggleSwitch toggle)
        {
            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            Grid.SetColumn(label, 0);
            Grid.SetColumn(toggle, 1);
            rowGrid.Children.Add(label);
            rowGrid.Children.Add(toggle);

            return new Border
            {
                Padding = new Thickness(12, 8),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Child = rowGrid
            };
        }

        public void AplicarTema(bool isDark)
        {
            Background = isDark ? ThemePalette.DarkWindowBg : ThemePalette.LightWindowBg;
            BorderBrush = isDark ? ThemePalette.DarkBorderActive : ThemePalette.LightBorderActive;
            _titleBar.Background = isDark ? ThemePalette.DarkWindowHeaderActiveBg : ThemePalette.LightWindowHeaderActiveBg;
            _lblTitle.Foreground = Brushes.White;
            _btnClose.Foreground = Brushes.White;

            var rowBg = isDark ? ThemePalette.DarkContentBg : ThemePalette.LightContentBg;
            var rowBorder = isDark ? ThemePalette.DarkBorder : ThemePalette.LightBorder;
            var textBrush = isDark ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;

            _rowTheme.Background = rowBg;
            _rowTheme.BorderBrush = rowBorder;
            _rowNotif.Background = rowBg;
            _rowNotif.BorderBrush = rowBorder;
            _lblTheme.Foreground = textBrush;
            _lblNotif.Foreground = textBrush;
        }

        public void Fechar()
        {
            _workspace.Children.Remove(this);
            OnClosed?.Invoke();
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            ZIndexManager.BringToFront(this);
            var source = e.Source as Control;
            if (source != null && (source.Name == "TitleBar" || source.Parent == _titleBar))
            {
                var props = e.GetCurrentPoint(this);
                if (props.Properties.IsLeftButtonPressed)
                {
                    _isDragging = true;
                    _pointerStart = e.GetPosition(_workspace);
                    _windowStart = new Point(Canvas.GetLeft(this), Canvas.GetTop(this));
                    e.Pointer.Capture(this);
                }
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isDragging) return;
            var diff = e.GetPosition(_workspace) - _pointerStart;
            Canvas.SetLeft(this, Math.Max(0, _windowStart.X + diff.X));
            Canvas.SetTop(this, Math.Max(0, _windowStart.Y + diff.Y));
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                e.Pointer.Capture(null);
            }
        }

        private void SalvarEAtualizar()
        {
            bool isDark = _themeToggle.IsChecked ?? true;
            bool notif = _notifToggle.IsChecked ?? true;
            AplicarTema(isDark);
            OnSettingsChanged?.Invoke(isDark, notif);

            if (notif)
            {
                ToastNotification.Mostrar(_workspace, "Preferências salvas com sucesso!", "success");
            }
        }
    }
}
