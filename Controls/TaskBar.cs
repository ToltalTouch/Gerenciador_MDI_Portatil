using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;

namespace AvaloniaMdiApp
{
    public class TaskBar : Border
    {
        public event Action? OnNotificationHistoryClicked;

        private readonly StackPanel _taskContainer;
        private readonly TextBlock _lblStatus;
        private readonly Button _btnNotifHistory;
        private readonly TextBlock _lblNotifBadge;
        private readonly Border _notifBadgeBorder;
        private readonly Dictionary<DraggableWindow, Button> _windowButtons = new();
        private readonly Dictionary<DraggableWindow, Border> _windowIndicators = new();
        private readonly Dictionary<DraggableWindow, TextBlock> _windowLabels = new();
        private bool _isDarkMode = true;

        public TaskBar()
        {
            BorderThickness = new Thickness(0, 1, 0, 0);
            Height = 42;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            _taskContainer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Margin = new Thickness(12, 5)
            };

            var scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _taskContainer
            };
            Grid.SetColumn(scrollViewer, 0);
            grid.Children.Add(scrollViewer);

            var rightControls = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 12, 0)
            };

            _lblStatus = new TextBlock
            {
                Text = "0 janelas",
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center
            };

            var notifIconPanel = new Panel();
            var iconBell = AppIcons.Create(AppIcons.Bell, 14, ThemePalette.IconLightGray);
            notifIconPanel.Children.Add(iconBell);

            _lblNotifBadge = new TextBlock
            {
                Text = "0",
                FontSize = 9,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _notifBadgeBorder = new Border
            {
                Background = ThemePalette.Accent,
                CornerRadius = new CornerRadius(7),
                MinWidth = 14,
                Height = 14,
                Padding = new Thickness(3, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -6, -8, 0),
                IsVisible = false,
                Child = _lblNotifBadge
            };
            notifIconPanel.Children.Add(_notifBadgeBorder);

            _btnNotifHistory = new Button
            {
                Content = notifIconPanel,
                Width = 32,
                Height = 30,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(6),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(_btnNotifHistory, "Histórico de Notificações");
            _btnNotifHistory.Click += (s, e) => OnNotificationHistoryClicked?.Invoke();

            rightControls.Children.Add(_lblStatus);
            rightControls.Children.Add(_btnNotifHistory);

            Grid.SetColumn(rightControls, 1);
            grid.Children.Add(rightControls);

            Child = grid;
            AplicarTema(true);

            NotificationHistoryService.OnHistoryUpdated += AtualizarContadorNotificacoes;
            AtualizarContadorNotificacoes();
        }

        private void AtualizarContadorNotificacoes()
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                int total = NotificationHistoryService.TotalNaoLidos;
                if (total > 0)
                {
                    _lblNotifBadge.Text = total > 99 ? "99+" : total.ToString();
                    _notifBadgeBorder.IsVisible = true;
                }
                else
                {
                    _notifBadgeBorder.IsVisible = false;
                }
            });
        }

        public void AplicarTema(bool isDarkMode)
        {
            _isDarkMode = isDarkMode;
            Background = isDarkMode ? ThemePalette.DarkBarBg : ThemePalette.LightBarBg;
            BorderBrush = isDarkMode ? ThemePalette.DarkBorder : ThemePalette.LightBorder;
            _lblStatus.Foreground = isDarkMode ? ThemePalette.DarkTextSecondary : ThemePalette.LightTextSecondary;
            _btnNotifHistory.BorderBrush = isDarkMode ? ThemePalette.DarkBorder : ThemePalette.LightBorder;

            foreach (var kvp in _windowButtons)
            {
                AtualizarVisualBotao(kvp.Key);
            }
        }

        public void RegistrarJanela(DraggableWindow window, string title)
        {
            var indicator = new Border
            {
                Width = 6,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = ThemePalette.AccentLight,
                VerticalAlignment = VerticalAlignment.Center
            };

            var lbl = new TextBlock
            {
                Text = title,
                FontSize = 12,
                FontWeight = FontWeight.Medium,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 120
            };

            var contentStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };
            contentStack.Children.Add(indicator);
            contentStack.Children.Add(lbl);

            var btn = new Button
            {
                Content = contentStack,
                Height = 30,
                Padding = new Thickness(10, 0),
                BorderThickness = new Thickness(1),
                VerticalContentAlignment = VerticalAlignment.Center,
                CornerRadius = new CornerRadius(6)
            };

            btn.Click += (s, e) =>
            {
                if (window.IsMinimized)
                {
                    window.ToggleMinimize();
                }
                ZIndexManager.BringToFront(window);
            };

            _windowButtons[window] = btn;
            _windowIndicators[window] = indicator;
            _windowLabels[window] = lbl;
            _taskContainer.Children.Add(btn);

            AtualizarVisualBotao(window);
        }

        public void AtualizarTituloJanela(DraggableWindow window, string novoTitulo)
        {
            if (_windowLabels.TryGetValue(window, out var lbl))
            {
                lbl.Text = novoTitulo;
            }
        }

        public void RemoverJanela(DraggableWindow window)
        {
            if (_windowButtons.TryGetValue(window, out var btn))
            {
                _taskContainer.Children.Remove(btn);
                _windowButtons.Remove(window);
                _windowIndicators.Remove(window);
                _windowLabels.Remove(window);
                AtualizarContador();
            }
        }

        private string? _activeColorFilterHex;

        public void AtualizarCorJanela(DraggableWindow window)
        {
            AtualizarVisualBotao(window);
        }

        public void AplicarFiltroCor(string? hexFilter)
        {
            _activeColorFilterHex = hexFilter;
            foreach (var win in _windowButtons.Keys)
            {
                AtualizarVisualBotao(win);
            }
        }

        public void AtualizarEstadoMinimizado(DraggableWindow window, bool isMinimized)
        {
            AtualizarVisualBotao(window);
        }

        public void AtualizarEstadoAtivo(DraggableWindow window, bool isActive)
        {
            AtualizarVisualBotao(window);
        }

        private void AtualizarVisualBotao(DraggableWindow window)
        {
            if (!_windowButtons.TryGetValue(window, out var btn)) return;

            bool isMinimized = window.IsMinimized;
            bool isHibernated = window.WindowType == "spreadsheet" && window.IsSheetHibernated;
            bool matchesFilter = DraggableWindow.CorCombinaComFiltro(window.CustomColor, _activeColorFilterHex);

            double baseOpacity = (isMinimized || isHibernated) ? 0.70 : 1.0;
            btn.Opacity = matchesFilter ? baseOpacity : 0.25;

            if (_windowLabels.TryGetValue(window, out var lbl))
            {
                lbl.Text = isHibernated ? $"◦ {window.WindowTitle}" : window.WindowTitle;
            }

            Color customColor = default;
            bool hasCustomColor = !string.IsNullOrWhiteSpace(window.CustomColor) && Color.TryParse(window.CustomColor, out customColor);
            if (_windowIndicators.TryGetValue(window, out var indicator))
            {
                indicator.Background = hasCustomColor
                    ? new SolidColorBrush(customColor)
                    : (_isDarkMode ? ThemePalette.DarkTextSecondary : ThemePalette.LightTextSecondary);
            }

            if (hasCustomColor)
            {
                byte bgAlpha = (isMinimized || isHibernated)
                    ? (_isDarkMode ? (byte)42 : (byte)30)
                    : (_isDarkMode ? (byte)85 : (byte)60);
                btn.Background = new SolidColorBrush(Color.FromArgb(bgAlpha, customColor.R, customColor.G, customColor.B));
                btn.BorderBrush = new SolidColorBrush(Color.FromArgb(190, customColor.R, customColor.G, customColor.B));
                btn.Foreground = _isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;
            }
            else if (_isDarkMode)
            {
                btn.Background = (isMinimized || isHibernated) ? ThemePalette.DarkWorkspaceBg : ThemePalette.DarkButtonBg;
                btn.BorderBrush = ThemePalette.DarkBorder;
                btn.Foreground = ThemePalette.DarkTextPrimary;
            }
            else
            {
                btn.Background = (isMinimized || isHibernated) ? ThemePalette.LightWorkspaceBg : ThemePalette.LightButtonBg;
                btn.BorderBrush = ThemePalette.LightBorder;
                btn.Foreground = ThemePalette.LightTextPrimary;
            }
            AtualizarContador();
        }

        private void AtualizarContador()
        {
            int count = _windowButtons.Count;
            int hibernated = 0;
            foreach (var win in _windowButtons.Keys)
            {
                if (win.IsMinimized || (win.WindowType == "spreadsheet" && win.IsSheetHibernated))
                {
                    hibernated++;
                }
            }

            string baseText = count == 1 ? "1 janela" : $"{count} janelas";
            _lblStatus.Text = hibernated > 0
                ? $"{baseText} ({hibernated} hibernada(s))"
                : baseText;
        }
    }
}
