using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System.Collections.Generic;

namespace AvaloniaMdiApp
{
    public class TaskBar : Border
    {
        private readonly StackPanel _taskContainer;
        private readonly Dictionary<DraggableWindow, Button> _windowButtons = new();

        public TaskBar()
        {
            Background = SolidColorBrush.Parse("#2d2d30");
            BorderBrush = SolidColorBrush.Parse("#3f3f46");
            BorderThickness = new Thickness(0, 1, 0, 0);
            Height = 40;

            _taskContainer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(8, 4)
            };

            Child = _taskContainer;
        }

        public void RegistrarJanela(DraggableWindow window, string title)
        {
            var btn = new Button
            {
                Content = title,
                Width = 140,
                Height = 30,
                Margin = new Thickness(0, 0, 6, 0),
                Background = SolidColorBrush.Parse("#3e3e42"),
                Foreground = Brushes.White,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                CornerRadius = new CornerRadius(3)
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
            _taskContainer.Children.Add(btn);
        }

        public void RemoverJanela(DraggableWindow window)
        {
            if (_windowButtons.ContainsKey(window))
            {
                _taskContainer.Children.Remove(_windowButtons[window]);
                _windowButtons.Remove(window);
            }
        }
        
        public void AtualizarEstadoMinimizado(DraggableWindow window, bool isMinimized)
        {
            if (_windowButtons.ContainsKey(window))
            {
                _windowButtons[window].Background = isMinimized 
                    ? SolidColorBrush.Parse("#252526") 
                    : SolidColorBrush.Parse("#3e3e42");
            }
        }
    }
}
