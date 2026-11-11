using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using System.Collections.Generic;

namespace AvaloniaMdiApp
{
    public class TaskBar : Border
    {
        private readonly StackPanel _taskContainer;
        private readonly TextBlock _lblStatus;
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

            _lblStatus = new TextBlock
            {
                Text = "0 janelas",
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 16, 0)
            };
            Grid.SetColumn(_lblStatus, 1);
            grid.Children.Add(_lblStatus);

            Child = grid;
            AplicarTema(true);
        }

        public void AplicarTema(bool isDarkMode)
        {
            _isDarkMode = isDarkMode;
            Background = isDarkMode ? ThemePalette.DarkBarBg : ThemePalette.LightBarBg;
            BorderBrush = isDarkMode ? ThemePalette.DarkBorder : ThemePalette.LightBorder;
            _lblStatus.Foreground = isDarkMode ? ThemePalette.DarkTextSecondary : ThemePalette.LightTextSecondary;

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
            AtualizarContador();
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

        public void AtualizarEstadoMinimizado(DraggableWindow window, bool isMinimized)
        {
            AtualizarVisualBotao(window);
        }

        public void AtualizarEstadoAtivo(DraggableWindow window, bool isActive)
        {
            if (_windowIndicators.TryGetValue(window, out var indicator))
            {
                indicator.Background = isActive ? ThemePalette.AccentLight : (_isDarkMode ? ThemePalette.DarkTextSecondary : ThemePalette.LightTextSecondary);
            }
            AtualizarVisualBotao(window);
        }

        private void AtualizarVisualBotao(DraggableWindow window)
        {
            if (!_windowButtons.TryGetValue(window, out var btn)) return;

            bool isMinimized = window.IsMinimized;
            btn.Opacity = isMinimized ? 0.65 : 1.0;

            if (_isDarkMode)
            {
                btn.Background = isMinimized ? ThemePalette.DarkWorkspaceBg : ThemePalette.DarkButtonBg;
                btn.BorderBrush = ThemePalette.DarkBorder;
                btn.Foreground = ThemePalette.DarkTextPrimary;
            }
            else
            {
                btn.Background = isMinimized ? ThemePalette.LightWorkspaceBg : ThemePalette.LightButtonBg;
                btn.BorderBrush = ThemePalette.LightBorder;
                btn.Foreground = ThemePalette.LightTextPrimary;
            }
        }

        private void AtualizarContador()
        {
            int count = _windowButtons.Count;
            _lblStatus.Text = count == 1 ? "1 janela ativa" : $"{count} janelas ativas";
        }
    }
}
