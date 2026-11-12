using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System;

namespace AvaloniaMdiApp
{
    /// <summary>
    /// Elemento visual de conexão estilo fluxograma entre duas janelas (DraggableWindow).
    /// Desenha uma curva Bézier suave com seta direcional, seletor de cor e botão para remover a ligação.
    /// </summary>
    public class FlowchartConnectionVisual
    {
        public string ConnectionId { get; }
        public DraggableWindow FromWindow { get; }
        public DraggableWindow ToWindow { get; }
        public string Label { get; set; }
        public string ColorHex { get; private set; }

        public Path CurvePath { get; }
        public Path ArrowPath { get; }
        public Ellipse OriginDot { get; }
        public Border MidpointBadge { get; }

        public event Action<FlowchartConnectionVisual>? OnRemoveRequested;
        public event Action<FlowchartConnectionVisual>? OnColorChanged;

        private readonly Canvas _workspace;
        private readonly Path _hitPath;
        private readonly Button _btnConfig;
        private readonly MenuFlyout _configFlyout;
        private bool _isDarkMode;
        private bool _isMenuOpen;
        private bool _isHoveringLine;
        private bool _isHoveringBadge;

        public static readonly (string Hex, string Nome)[] PaletaCoresLinha =
        {
            ("#3B82F6", "Azul"),
            ("#10B981", "Verde Esmeralda"),
            ("#F59E0B", "Laranja / Âmbar"),
            ("#EF4444", "Vermelho"),
            ("#8B5CF6", "Roxo / Violeta"),
            ("#EC4899", "Rosa / Magenta"),
            ("#06B6D4", "Ciano"),
            ("#94A3B8", "Cinza / Neutro")
        };

        public FlowchartConnectionVisual(
            Canvas workspace,
            DraggableWindow fromWindow,
            DraggableWindow toWindow,
            bool isDarkMode,
            string? connectionId = null,
            string label = "",
            string colorHex = "#3B82F6")
        {
            _workspace = workspace;
            FromWindow = fromWindow;
            ToWindow = toWindow;
            _isDarkMode = isDarkMode;
            ConnectionId = string.IsNullOrWhiteSpace(connectionId) ? Guid.NewGuid().ToString("N") : connectionId;
            Label = label ?? string.Empty;
            ColorHex = string.IsNullOrWhiteSpace(colorHex) ? "#3B82F6" : colorHex;

            CurvePath = new Path
            {
                StrokeThickness = 2.8,
                StrokeLineCap = PenLineCap.Round,
                IsHitTestVisible = false,
                ZIndex = 0
            };

            _hitPath = new Path
            {
                Stroke = Brushes.Transparent,
                StrokeThickness = 16,
                StrokeLineCap = PenLineCap.Round,
                IsHitTestVisible = true,
                Cursor = new Cursor(StandardCursorType.Hand),
                ZIndex = 0
            };

            ArrowPath = new Path
            {
                StrokeThickness = 1.5,
                IsHitTestVisible = false,
                ZIndex = 0
            };

            OriginDot = new Ellipse
            {
                Width = 9,
                Height = 9,
                StrokeThickness = 2,
                IsHitTestVisible = false,
                ZIndex = 0
            };

            _configFlyout = new MenuFlyout { Placement = PlacementMode.Bottom };
            _configFlyout.Opened += (s, e) =>
            {
                _isMenuOpen = true;
                AtualizarVisibilidadeBadge();
            };
            _configFlyout.Closed += (s, e) =>
            {
                _isMenuOpen = false;
                AtualizarVisibilidadeBadge();
            };

            var menuCores = new MenuItem
            {
                Icon = AppIcons.Create(AppIcons.Palette, 11, ThemePalette.IconLightGray),
                Header = "Mudar Cor da Linha"
            };
            foreach (var cor in PaletaCoresLinha)
            {
                IBrush dotBrush;
                try { dotBrush = SolidColorBrush.Parse(cor.Hex); }
                catch { dotBrush = ThemePalette.Accent; }

                var item = new MenuItem
                {
                    Icon = new Ellipse
                    {
                        Width = 10,
                        Height = 10,
                        Fill = dotBrush,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    Header = cor.Nome
                };
                string hex = cor.Hex;
                item.Click += (s, e) =>
                {
                    DefinirCor(hex);
                    OnColorChanged?.Invoke(this);
                };
                menuCores.Items.Add(item);
            }

            var menuRemover = new MenuItem
            {
                Icon = AppIcons.Create(AppIcons.Trash, 11, ThemePalette.IconLightGray),
                Header = "Remover Ligação"
            };
            menuRemover.Click += (s, e) => OnRemoveRequested?.Invoke(this);

            _configFlyout.Items.Add(menuCores);
            _configFlyout.Items.Add(new Separator());
            _configFlyout.Items.Add(menuRemover);

            _btnConfig = new Button
            {
                Content = string.IsNullOrWhiteSpace(Label)
                    ? (object)AppIcons.Create(AppIcons.Settings, 11, ThemePalette.IconLightGray)
                    : AppIcons.WithText(AppIcons.Settings, Label, 10.5, 4, ThemePalette.IconLightGray),
                Padding = new Thickness(5, 2),
                MinWidth = 24,
                MinHeight = 22,
                Height = 22,
                CornerRadius = new CornerRadius(11),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Hand),
                Flyout = _configFlyout
            };
            ToolTip.SetTip(_btnConfig, $"Configurar ligação \"{FromWindow.WindowTitle}\" → \"{ToWindow.WindowTitle}\"");

            MidpointBadge = new Border
            {
                CornerRadius = new CornerRadius(11),
                BorderThickness = new Thickness(1.5),
                Child = _btnConfig,
                IsVisible = !string.IsNullOrWhiteSpace(Label),
                ZIndex = 1
            };

            _hitPath.PointerEntered += (s, e) => { _isHoveringLine = true; AtualizarVisibilidadeBadge(); };
            _hitPath.PointerExited += (s, e) => { _isHoveringLine = false; AtualizarVisibilidadeBadge(); };
            MidpointBadge.PointerEntered += (s, e) => { _isHoveringBadge = true; AtualizarVisibilidadeBadge(); };
            MidpointBadge.PointerExited += (s, e) => { _isHoveringBadge = false; AtualizarVisibilidadeBadge(); };

            _workspace.Children.Insert(0, _hitPath);
            _workspace.Children.Insert(1, CurvePath);
            _workspace.Children.Insert(2, ArrowPath);
            _workspace.Children.Insert(3, OriginDot);
            _workspace.Children.Insert(4, MidpointBadge);

            FromWindow.OnBoundsChanged += OnEndpointChanged;
            ToWindow.OnBoundsChanged += OnEndpointChanged;

            AplicarTema(isDarkMode);
            AtualizarGeometria();
        }

        private void AtualizarVisibilidadeBadge()
        {
            MidpointBadge.IsVisible = _isHoveringLine || _isHoveringBadge || _isMenuOpen || !string.IsNullOrWhiteSpace(Label);
        }

        private void OnEndpointChanged(DraggableWindow _)
        {
            AtualizarGeometria();
        }

        public void DefinirCor(string colorHex)
        {
            if (string.IsNullOrWhiteSpace(colorHex)) return;
            ColorHex = colorHex;
            AplicarTema(_isDarkMode);
        }

        public void AplicarTema(bool isDarkMode)
        {
            _isDarkMode = isDarkMode;
            IBrush lineBrush;
            try
            {
                lineBrush = SolidColorBrush.Parse(ColorHex);
            }
            catch
            {
                lineBrush = isDarkMode ? ThemePalette.AccentLight : ThemePalette.Accent;
            }

            var badgeBg = isDarkMode ? ThemePalette.DarkWindowBg : ThemePalette.LightWindowBg;
            var textPrimary = isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;

            CurvePath.Stroke = lineBrush;
            ArrowPath.Fill = lineBrush;
            ArrowPath.Stroke = lineBrush;
            OriginDot.Fill = badgeBg;
            OriginDot.Stroke = lineBrush;

            MidpointBadge.Background = badgeBg;
            MidpointBadge.BorderBrush = lineBrush;
            _btnConfig.Foreground = textPrimary;
        }

        public void AtualizarGeometria()
        {
            Rect r1 = ObterRetanguloAtual(FromWindow);
            Rect r2 = ObterRetanguloAtual(ToWindow);

            (Point p1, Vector dir1, Point p2, Vector dir2) = CalcularAncoras(r1, r2);

            double dist = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
            double offset = Math.Clamp(dist * 0.42, 40, 180);

            Point c1 = new Point(p1.X + dir1.X * offset, p1.Y + dir1.Y * offset);
            Point c2 = new Point(p2.X + dir2.X * offset, p2.Y + dir2.Y * offset);

            var curveGeo = new StreamGeometry();
            using (var ctx = curveGeo.Open())
            {
                ctx.BeginFigure(p1, false);
                ctx.CubicBezierTo(c1, c2, p2);
            }
            CurvePath.Data = curveGeo;
            _hitPath.Data = curveGeo;

            Canvas.SetLeft(OriginDot, p1.X - 4.5);
            Canvas.SetTop(OriginDot, p1.Y - 4.5);

            Vector tangent = new Vector(p2.X - c2.X, p2.Y - c2.Y);
            if (tangent.Length < 0.01)
            {
                tangent = new Vector(p2.X - p1.X, p2.Y - p1.Y);
            }
            double len = Math.Max(0.001, tangent.Length);
            Vector u = new Vector(tangent.X / len, tangent.Y / len);
            Vector perp = new Vector(-u.Y, u.X);

            double arrowLen = 11;
            double arrowWidth = 5.5;
            Point a1 = new Point(p2.X - u.X * arrowLen + perp.X * arrowWidth, p2.Y - u.Y * arrowLen + perp.Y * arrowWidth);
            Point a2 = new Point(p2.X - u.X * arrowLen - perp.X * arrowWidth, p2.Y - u.Y * arrowLen - perp.Y * arrowWidth);

            var arrowGeo = new StreamGeometry();
            using (var ctx = arrowGeo.Open())
            {
                ctx.BeginFigure(p2, true);
                ctx.LineTo(a1);
                ctx.LineTo(a2);
                ctx.EndFigure(true);
            }
            ArrowPath.Data = arrowGeo;

            double midX = 0.125 * p1.X + 0.375 * c1.X + 0.375 * c2.X + 0.125 * p2.X;
            double midY = 0.125 * p1.Y + 0.375 * c1.Y + 0.375 * c2.Y + 0.125 * p2.Y;

            Canvas.SetLeft(MidpointBadge, midX - 12);
            Canvas.SetTop(MidpointBadge, midY - 11);
        }

        private static Rect ObterRetanguloAtual(DraggableWindow win)
        {
            double x = Canvas.GetLeft(win);
            double y = Canvas.GetTop(win);
            if (double.IsNaN(x)) x = 0;
            if (double.IsNaN(y)) y = 0;
            double w = win.Width > 0 ? win.Width : 240;
            double h = win.Height > 0 ? win.Height : 34;
            return new Rect(x, y, w, h);
        }

        private static (Point p1, Vector d1, Point p2, Vector d2) CalcularAncoras(Rect r1, Rect r2)
        {
            Point c1 = r1.Center;
            Point c2 = r2.Center;
            double dx = c2.X - c1.X;
            double dy = c2.Y - c1.Y;

            if (Math.Abs(dx) >= Math.Abs(dy))
            {
                if (dx >= 0)
                {
                    return (
                        new Point(r1.Right, c1.Y), new Vector(1, 0),
                        new Point(r2.Left, c2.Y), new Vector(-1, 0)
                    );
                }
                else
                {
                    return (
                        new Point(r1.Left, c1.Y), new Vector(-1, 0),
                        new Point(r2.Right, c2.Y), new Vector(1, 0)
                    );
                }
            }
            else
            {
                if (dy >= 0)
                {
                    return (
                        new Point(c1.X, r1.Bottom), new Vector(0, 1),
                        new Point(c2.X, r2.Top), new Vector(0, -1)
                    );
                }
                else
                {
                    return (
                        new Point(c1.X, r1.Top), new Vector(0, -1),
                        new Point(c2.X, r2.Bottom), new Vector(0, 1)
                    );
                }
            }
        }

        public void RemoverDoCanvas()
        {
            FromWindow.OnBoundsChanged -= OnEndpointChanged;
            ToWindow.OnBoundsChanged -= OnEndpointChanged;

            _workspace.Children.Remove(_hitPath);
            _workspace.Children.Remove(CurvePath);
            _workspace.Children.Remove(ArrowPath);
            _workspace.Children.Remove(OriginDot);
            _workspace.Children.Remove(MidpointBadge);
        }
    }
}
