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
        private static readonly IBrush ToastBg = SolidColorBrush.Parse("#1E293B").ToImmutable();
        private static readonly IBrush SuccessBorder = SolidColorBrush.Parse("#10B981").ToImmutable();
        private static readonly IBrush WarningBorder = SolidColorBrush.Parse("#F59E0B").ToImmutable();
        private static readonly IBrush InfoBorder = SolidColorBrush.Parse("#3B82F6").ToImmutable();

        private static ToastNotification? _toastAtivo;
        private readonly DispatcherTimer _timer;
        private readonly Canvas _workspace;

        public ToastNotification(Canvas workspace, string mensagem, string tipo = "info")
        {
            _workspace = workspace;
            Width = 290;
            Height = 62;
            CornerRadius = new CornerRadius(8);
            Padding = new Thickness(12, 10);
            Background = ToastBg;
            BorderThickness = new Thickness(1);

            IBrush accentBrush = tipo == "success" ? SuccessBorder : tipo == "warning" ? WarningBorder : InfoBorder;
            BorderBrush = accentBrush;

            double viewWidth = workspace.Bounds.Width;
            double viewHeight = workspace.Bounds.Height;
            double offsetX = 0;
            double offsetY = 0;

            if (workspace.Parent is LayoutTransformControl zoomHost &&
                zoomHost.Parent is ScrollViewer scroller &&
                scroller.Viewport.Width > 0 &&
                scroller.Viewport.Height > 0)
            {
                double scale = (zoomHost.LayoutTransform as ScaleTransform)?.ScaleX ?? 1.0;
                if (scale <= 0.01) scale = 1.0;
                offsetX = scroller.Offset.X / scale;
                offsetY = scroller.Offset.Y / scale;
                viewWidth = scroller.Viewport.Width / scale;
                viewHeight = scroller.Viewport.Height / scale;
            }

            double posX = offsetX + viewWidth - Width - 20;
            double posY = offsetY + viewHeight - Height - 16;
            Canvas.SetLeft(this, Math.Max(16, posX));
            Canvas.SetTop(this, Math.Max(16, posY));
            ZIndex = 9500;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            var accentBar = new Border
            {
                Width = 4,
                CornerRadius = new CornerRadius(2),
                Background = accentBrush,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(accentBar, 0);
            grid.Children.Add(accentBar);

            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 2,
                VerticalAlignment = VerticalAlignment.Center
            };

            var lblTitulo = new TextBlock
            {
                Text = tipo == "success" ? "✓ Sucesso" : tipo == "warning" ? "⚠ Aviso" : "ℹ Informação",
                Foreground = ThemePalette.DarkTextPrimary,
                FontWeight = FontWeight.SemiBold,
                FontSize = 12
            };

            var lblMensagem = new TextBlock
            {
                Text = mensagem,
                Foreground = ThemePalette.DarkTextSecondary,
                FontSize = 11.5,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            panel.Children.Add(lblTitulo);
            panel.Children.Add(lblMensagem);
            Grid.SetColumn(panel, 1);
            grid.Children.Add(panel);

            Child = grid;

            ZIndexManager.BringToFront(this);

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            _timer.Tick += (s, e) => Remover();
            _timer.Start();
        }

        private void Remover()
        {
            _timer.Stop();
            if (_workspace.Children.Contains(this))
            {
                _workspace.Children.Remove(this);
            }
            if (_toastAtivo == this)
            {
                _toastAtivo = null;
            }
        }

        public static void Mostrar(Canvas workspace, string mensagem, string tipo = "info")
        {
            _toastAtivo?.Remover();
            var toast = new ToastNotification(workspace, mensagem, tipo);
            _toastAtivo = toast;
            workspace.Children.Add(toast);
        }
    }
}
