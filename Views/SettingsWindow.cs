using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;

namespace AvaloniaMdiApp
{
    public class SettingsWindow : Border
    {
        public event Action<bool, bool, double, string, string>? OnSettingsChanged;
        public event Action<bool>? OnAutoUpdateChanged;
        public event Action? OnCheckUpdatesRequested;
        public event Action? OnClosed;

        private readonly ToggleSwitch _themeToggle;
        private readonly ToggleSwitch _notifToggle;
        private readonly ToggleSwitch _autoUpdateToggle;
        private readonly Button _btnCheckUpdateNow;
        private readonly ComboBox _cmbLayoutScale;
        private readonly ComboBox _cmbMainWindowSize;
        private readonly ComboBox _cmbWidgetSize;
        private readonly Canvas _workspace;
        private readonly Border _titleBar;
        private readonly TextBlock _lblTitle;
        private readonly Button _btnClose;
        private readonly Border _rowTheme;
        private readonly Border _rowNotif;
        private readonly Border _rowAutoUpdate;
        private readonly Border _rowLayoutScale;
        private readonly Border _rowMainWindowSize;
        private readonly Border _rowWidgetSize;
        private readonly TextBlock _lblTheme;
        private readonly TextBlock _lblNotif;
        private readonly TextBlock _lblAutoUpdate;
        private readonly TextBlock _lblSectionLayout;
        private readonly TextBlock _lblLayoutScale;
        private readonly TextBlock _lblMainWindowSize;
        private readonly TextBlock _lblWidgetSize;

        private bool _isDragging;
        private bool _suppressEvents;
        private Point _pointerStart;
        private Point _windowStart;

        private static readonly (double Value, string Label)[] ScaleOptions =
        {
            (0.85, "Compacto (85%)"),
            (1.00, "Padrão (100%)"),
            (1.15, "Confortável (115%)"),
            (1.30, "Grande (130%)")
        };

        private static readonly (string Key, string Label)[] MainWindowSizeOptions =
        {
            ("900x600", "Compacto (900 × 600)"),
            ("1024x680", "Padrão (1024 × 680)"),
            ("1280x720", "HD (1280 × 720)"),
            ("1440x860", "Amplo (1440 × 860)"),
            ("maximized", "Maximizado (Tela Cheia)")
        };

        private static readonly (string Key, string Label)[] WidgetSizeOptions =
        {
            ("compact", "Compacto (Menor)"),
            ("normal", "Padrão (Médio)"),
            ("large", "Expandido (Maior)")
        };

        public SettingsWindow(Canvas workspace, AppSettings settings)
        {
            _workspace = workspace;
            _suppressEvents = true;

            Width = 390;
            Height = 450;
            double initialX = (workspace.Bounds.Width - Width) / 2 > 0 ? (workspace.Bounds.Width - Width) / 2 : 120;
            Canvas.SetLeft(this, initialX);
            Canvas.SetTop(this, 50);

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
                Text = "Preferências do Sistema",
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeight.SemiBold,
                FontSize = 13,
                IsHitTestVisible = false
            };
            var titleStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 7,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
                IsHitTestVisible = false
            };
            titleStack.Children.Add(AppIcons.Create(AppIcons.Settings, 13, ThemePalette.IconLightGray));
            titleStack.Children.Add(_lblTitle);
            Grid.SetColumn(titleStack, 0);
            grid.Children.Add(titleStack);

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
                Margin = new Thickness(16, 14),
                Spacing = 10
            };

            _lblTheme = new TextBlock
            {
                Text = "Modo Escuro (Dark)",
                FontSize = 12.5,
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
            _rowTheme = CriarLinhaControle(_lblTheme, _themeToggle);

            _lblNotif = new TextBlock
            {
                Text = "Notificações Toast",
                FontSize = 12.5,
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
            _rowNotif = CriarLinhaControle(_lblNotif, _notifToggle);

            _lblAutoUpdate = new TextBlock
            {
                Text = "Atualização via GitHub Release",
                FontSize = 12.5,
                FontWeight = FontWeight.Medium,
                VerticalAlignment = VerticalAlignment.Center
            };
            _autoUpdateToggle = new ToggleSwitch
            {
                IsChecked = settings.AutoCheckUpdatesOnStartup,
                OnContent = "Auto",
                OffContent = "Manual",
                VerticalAlignment = VerticalAlignment.Center
            };
            _autoUpdateToggle.IsCheckedChanged += (s, e) =>
            {
                if (_suppressEvents) return;
                OnAutoUpdateChanged?.Invoke(_autoUpdateToggle.IsChecked ?? true);
            };
            _btnCheckUpdateNow = new Button
            {
                Content = AppIcons.WithText(AppIcons.Download, "Buscar", 10.5, 4, ThemePalette.IconLightGray),
                Height = 26,
                Padding = new Thickness(8, 0),
                CornerRadius = new CornerRadius(5),
                Background = ThemePalette.Accent,
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(_btnCheckUpdateNow, "Verificar nova tag de Release no GitHub agora");
            _btnCheckUpdateNow.Click += (s, e) => OnCheckUpdatesRequested?.Invoke();

            var updateRightStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            updateRightStack.Children.Add(_autoUpdateToggle);
            updateRightStack.Children.Add(_btnCheckUpdateNow);
            _rowAutoUpdate = CriarLinhaControle(_lblAutoUpdate, updateRightStack);

            _lblSectionLayout = new TextBlock
            {
                Text = "AJUSTE DO TAMANHO DO LAYOUT",
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(2, 6, 0, 2)
            };

            // 1. Escala da Interface (UI Scale)
            _lblLayoutScale = new TextBlock
            {
                Text = "Escala da Interface",
                FontSize = 12.5,
                FontWeight = FontWeight.Medium,
                VerticalAlignment = VerticalAlignment.Center
            };
            _cmbLayoutScale = new ComboBox
            {
                Width = 175,
                FontSize = 12,
                CornerRadius = new CornerRadius(5),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            int scaleIdx = 1;
            for (int i = 0; i < ScaleOptions.Length; i++)
            {
                _cmbLayoutScale.Items.Add(ScaleOptions[i].Label);
                if (Math.Abs(ScaleOptions[i].Value - settings.LayoutScale) < 0.05)
                {
                    scaleIdx = i;
                }
            }
            _cmbLayoutScale.SelectedIndex = scaleIdx;
            _cmbLayoutScale.SelectionChanged += (s, e) => SalvarEAtualizar();
            _rowLayoutScale = CriarLinhaControle(_lblLayoutScale, _cmbLayoutScale);

            // 2. Tamanho da Janela Principal
            _lblMainWindowSize = new TextBlock
            {
                Text = "Janela Principal",
                FontSize = 12.5,
                FontWeight = FontWeight.Medium,
                VerticalAlignment = VerticalAlignment.Center
            };
            _cmbMainWindowSize = new ComboBox
            {
                Width = 175,
                FontSize = 12,
                CornerRadius = new CornerRadius(5),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            int winSizeIdx = 1;
            for (int i = 0; i < MainWindowSizeOptions.Length; i++)
            {
                _cmbMainWindowSize.Items.Add(MainWindowSizeOptions[i].Label);
                if (string.Equals(MainWindowSizeOptions[i].Key, settings.MainWindowSizePreset, StringComparison.OrdinalIgnoreCase))
                {
                    winSizeIdx = i;
                }
            }
            _cmbMainWindowSize.SelectedIndex = winSizeIdx;
            _cmbMainWindowSize.SelectionChanged += (s, e) => SalvarEAtualizar();
            _rowMainWindowSize = CriarLinhaControle(_lblMainWindowSize, _cmbMainWindowSize);

            // 3. Tamanho das Janelas Internas (Widgets)
            _lblWidgetSize = new TextBlock
            {
                Text = "Janelas Internas",
                FontSize = 12.5,
                FontWeight = FontWeight.Medium,
                VerticalAlignment = VerticalAlignment.Center
            };
            _cmbWidgetSize = new ComboBox
            {
                Width = 175,
                FontSize = 12,
                CornerRadius = new CornerRadius(5),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            int widgetSizeIdx = 1;
            for (int i = 0; i < WidgetSizeOptions.Length; i++)
            {
                _cmbWidgetSize.Items.Add(WidgetSizeOptions[i].Label);
                if (string.Equals(WidgetSizeOptions[i].Key, settings.WidgetSizePreset, StringComparison.OrdinalIgnoreCase))
                {
                    widgetSizeIdx = i;
                }
            }
            _cmbWidgetSize.SelectedIndex = widgetSizeIdx;
            _cmbWidgetSize.SelectionChanged += (s, e) => SalvarEAtualizar();
            _rowWidgetSize = CriarLinhaControle(_lblWidgetSize, _cmbWidgetSize);

            contentPanel.Children.Add(_rowTheme);
            contentPanel.Children.Add(_rowNotif);
            contentPanel.Children.Add(_rowAutoUpdate);
            contentPanel.Children.Add(_lblSectionLayout);
            contentPanel.Children.Add(_rowLayoutScale);
            contentPanel.Children.Add(_rowMainWindowSize);
            contentPanel.Children.Add(_rowWidgetSize);

            rootLayout.Children.Add(contentPanel);
            Child = rootLayout;

            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;

            AplicarTema(settings.IsDarkMode);
            _suppressEvents = false;
            ZIndexManager.BringToFront(this);
        }

        private Border CriarLinhaControle(TextBlock label, Control control)
        {
            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            Grid.SetColumn(label, 0);
            Grid.SetColumn(control, 1);
            rowGrid.Children.Add(label);
            rowGrid.Children.Add(control);

            return new Border
            {
                Padding = new Thickness(12, 7),
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
            var subTextBrush = isDark ? ThemePalette.DarkTextSecondary : ThemePalette.LightTextSecondary;

            foreach (var row in new[] { _rowTheme, _rowNotif, _rowAutoUpdate, _rowLayoutScale, _rowMainWindowSize, _rowWidgetSize })
            {
                row.Background = rowBg;
                row.BorderBrush = rowBorder;
            }

            foreach (var lbl in new[] { _lblTheme, _lblNotif, _lblAutoUpdate, _lblLayoutScale, _lblMainWindowSize, _lblWidgetSize })
            {
                lbl.Foreground = textBrush;
            }

            _lblSectionLayout.Foreground = subTextBrush;
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
            if (_suppressEvents) return;

            bool isDark = _themeToggle.IsChecked ?? true;
            bool notif = _notifToggle.IsChecked ?? true;

            int scaleIdx = Math.Clamp(_cmbLayoutScale.SelectedIndex, 0, ScaleOptions.Length - 1);
            double scale = ScaleOptions[scaleIdx].Value;

            int winSizeIdx = Math.Clamp(_cmbMainWindowSize.SelectedIndex, 0, MainWindowSizeOptions.Length - 1);
            string mainWinPreset = MainWindowSizeOptions[winSizeIdx].Key;

            int widgetSizeIdx = Math.Clamp(_cmbWidgetSize.SelectedIndex, 0, WidgetSizeOptions.Length - 1);
            string widgetPreset = WidgetSizeOptions[widgetSizeIdx].Key;

            AplicarTema(isDark);
            OnSettingsChanged?.Invoke(isDark, notif, scale, mainWinPreset, widgetPreset);

            if (notif)
            {
                ToastNotification.Mostrar(_workspace, "Preferências e layout atualizados!", "success");
            }
        }
    }
}
