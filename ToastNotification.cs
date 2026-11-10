using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System;

namespace AvaloniaMdiApp
{
    public class ToastNotification : Border
    {
        private readonly DispatcherTimer _timer;

        public ToastNotification(Canvas workspace, string mensagem, string tipo = "info")
        {
            Width = 280;
            Height = 60;
            CornerRadius = new CornerRadius(6);
            Padding = new Thickness(12);

            if (tipo == "success")
            {
                Background = SolidColorBrush.Parse("#1b5e20");
                BorderBrush = SolidColorBrush.Parse("#2e7d32");
            }
            else if (tipo == "warning")
            {
                Background = SolidColorBrush.Parse("#e65100");
                BorderBrush = SolidColorBrush.Parse("#ef6c00");
            }
            else
            {
                Background = SolidColorBrush.Parse("#0d47a1");
                BorderBrush = SolidColorBrush.Parse("#1565c0");
            }

            BorderThickness = new Thickness(1);

            double posX = workspace.Bounds.Width - Width - 20;
            double posY = workspace.Bounds.Height - Height - 20;
            Canvas.SetLeft(this, posX > 0 ? posX : 20);
            Canvas.SetTop(this, posY > 0 ? posY : 20);

            var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };

            var lblTitulo = new TextBlock
            {
                Text = tipo == "success" ? "Sucesso" : tipo == "warning" ? "Aviso" : "Informação",
                Foreground = Brushes.White,
                FontWeight = FontWeight.Bold,
                FontSize = 12
            };

            var lblMensagem = new TextBlock
            {
                Text = mensagem,
                Foreground = Brushes.LightGray,
                FontSize = 11
            };

            panel.Children.Add(lblTitulo);
            panel.Children.Add(lblMensagem);
            Child = panel;

            ZIndexManager.BringToFront(this);

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _timer.Tick += (s, e) =>
            {
                _timer.Stop();
                if (workspace.Children.Contains(this))
                {
                    workspace.Children.Remove(this);
                }
            };
            _timer.Start();
        }

        public static void Mostrar(Canvas workspace, string mensagem, string tipo = "info")
        {
            var toast = new ToastNotification(workspace, mensagem, tipo);
            workspace.Children.Add(toast);
        }
    }
}
