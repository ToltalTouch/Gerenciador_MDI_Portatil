using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;

namespace AvaloniaMdiApp
{
    public class SettingsWindow : Border
    {
        public event Action<bool, bool>? OnSettingsChanged;

        private ToggleSwitch? _themeToggle;
        private ToggleSwitch? _notifToggle;
        private readonly Canvas _workspace;

        public SettingsWindow(Canvas workspace, AppSettings settings)
        {
            _workspace = workspace;
            Width = 320;
            Height = 220;
            Canvas.SetLeft(this, (workspace.Bounds.Width - Width) / 2 > 0 ? (workspace.Bounds.Width - Width) / 2 : 100);
            Canvas.SetTop(this, 80);

            Background = SolidColorBrush.Parse("#252526");
            BorderBrush = SolidColorBrush.Parse("#007acc");
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(6);

            var rootLayout = new DockPanel();

            var titleBar = new Border
            {
                Background = SolidColorBrush.Parse("#007acc"),
                Height = 32,
                Child = new Grid()
            };
            var grid = (Grid)titleBar.Child;
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var lblTitle = new TextBlock
            {
                Text = "Configurações",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                FontWeight = FontWeight.Bold
            };
            Grid.SetColumn(lblTitle, 0);
            grid.Children.Add(lblTitle);

            var btnClose = new Button
            {
                Content = "✕",
                Width = 32, Height = 32,
                Background = Brushes.Transparent,
                Foreground = Brushes.White
            };
            btnClose.Click += (s, e) => workspace.Children.Remove(this);
            Grid.SetColumn(btnClose, 1);
            grid.Children.Add(btnClose);

            DockPanel.SetDock(titleBar, Dock.Top);
            rootLayout.Children.Add(titleBar);

            var contentPanel = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 16
            };

            var themePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            var lblTheme = new TextBlock { Text = "Modo Escuro (Dark)", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            _themeToggle = new ToggleSwitch { IsChecked = settings.IsDarkMode };
            _themeToggle.IsCheckedChanged += (s, e) => SalvarEAtualizar();
            themePanel.Children.Add(lblTheme);
            themePanel.Children.Add(_themeToggle);

            var notifPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            var lblNotif = new TextBlock { Text = "Ativar Notificações", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            _notifToggle = new ToggleSwitch { IsChecked = settings.EnableNotifications };
            _notifToggle.IsCheckedChanged += (s, e) => SalvarEAtualizar();
            notifPanel.Children.Add(lblNotif);
            notifPanel.Children.Add(_notifToggle);

            contentPanel.Children.Add(themePanel);
            contentPanel.Children.Add(notifPanel);

            rootLayout.Children.Add(contentPanel);
            Child = rootLayout;
            
            ZIndexManager.BringToFront(this);
        }

        private void SalvarEAtualizar()
        {
            bool isDark = _themeToggle?.IsChecked ?? true;
            bool notif = _notifToggle?.IsChecked ?? true;
            OnSettingsChanged?.Invoke(isDark, notif);
            ToastNotification.Mostrar(_workspace, "Configurações atualizadas!", "success");
        }
    }
}
