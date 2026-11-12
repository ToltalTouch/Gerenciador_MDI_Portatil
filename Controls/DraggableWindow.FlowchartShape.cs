using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System;

namespace AvaloniaMdiApp
{
    public partial class DraggableWindow
    {
        private Avalonia.Controls.Shapes.Path? _shapeVectorPath;
        private Border? _shapeInnerBadge;
        private TextBlock? _shapeInnerBadgeText;

        public static readonly (string Key, string Label, string BadgeText, string ColorHex)[] NiveisCriticidade =
        {
            ("none", "Sem Aviso de Criticidade", "", ""),
            ("low", "Baixa / Informativo", "BAIXA", "#2563EB"),
            ("medium", "Média / Atenção", "ATENÇÃO", "#D97706"),
            ("high", "Alta / Importante", "ALTA", "#EA580C"),
            ("critical", "Crítica / Urgente", "CRÍTICO", "#DC2626")
        };

        public static bool EhTipoFormaFluxograma(string? windowType) =>
            windowType is "shape_rect" or "shape_diamond" or "shape_circle";

        public bool IsFlowchartShape => EhTipoFormaFluxograma(WindowType);

        public static (string BadgeText, string ColorHex) ObterInfoCriticidade(string? level)
        {
            string key = (level ?? "none").Trim().ToLowerInvariant();
            foreach (var item in NiveisCriticidade)
            {
                if (item.Key == key)
                {
                    return (item.BadgeText, item.ColorHex);
                }
            }
            return ("", "");
        }

        private Control CriarConteudoFormaFluxograma(string initialText)
        {
            var container = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(4)
            };

            _shapeVectorPath = new Avalonia.Controls.Shapes.Path
            {
                StrokeThickness = 2.5,
                StrokeJoin = PenLineJoin.Round,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                IsHitTestVisible = false
            };

            string watermark = WindowType switch
            {
                "shape_diamond" => "Decisão / Condição...",
                "shape_circle" => "Início / Fim / Ponto...",
                _ => "Etapa / Processo..."
            };

            var noteBox = new TextBox
            {
                Text = initialText,
                Watermark = watermark,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                FontWeight = FontWeight.SemiBold,
                FontSize = _fontSize
            };
            _noteTextBox = noteBox;

            // Emblema interno de criticidade no topo da forma geométrica
            _shapeInnerBadgeText = new TextBlock
            {
                FontSize = 10,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };

            var badgeContent = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center
            };
            badgeContent.Children.Add(AppIcons.Create(AppIcons.Warning, 10, Brushes.White));
            badgeContent.Children.Add(_shapeInnerBadgeText);

            _shapeInnerBadge = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 6, 0, 0),
                IsVisible = false,
                IsHitTestVisible = false,
                Child = badgeContent
            };

            container.Children.Add(_shapeVectorPath);
            container.Children.Add(noteBox);
            container.Children.Add(_shapeInnerBadge);

            container.SizeChanged += (s, e) => AtualizarGeometriaFormaFluxograma(e.NewSize.Width, e.NewSize.Height);

            return container;
        }

        private void AtualizarGeometriaFormaFluxograma(double w, double h)
        {
            if (_shapeVectorPath == null) return;
            if (w <= 8 || h <= 8) return;

            double pad = 3.0;
            double left = pad;
            double top = pad;
            double right = Math.Max(left + 4, w - pad);
            double bottom = Math.Max(top + 4, h - pad);
            double midX = (left + right) / 2.0;
            double midY = (top + bottom) / 2.0;

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                if (WindowType == "shape_diamond")
                {
                    ctx.BeginFigure(new Point(midX, top), true);
                    ctx.LineTo(new Point(right, midY));
                    ctx.LineTo(new Point(midX, bottom));
                    ctx.LineTo(new Point(left, midY));
                    ctx.EndFigure(true);

                    if (_noteTextBox != null)
                    {
                        double insetX = Math.Max(18, w * 0.20);
                        double insetY = Math.Max(14, h * 0.18);
                        _noteTextBox.Margin = new Thickness(insetX, insetY);
                    }
                }
                else if (WindowType == "shape_circle")
                {
                    double rx = (right - left) / 2.0;
                    double ry = (bottom - top) / 2.0;

                    ctx.BeginFigure(new Point(midX, top), true);
                    ctx.ArcTo(new Point(midX, bottom), new Size(rx, ry), 0, false, SweepDirection.Clockwise);
                    ctx.ArcTo(new Point(midX, top), new Size(rx, ry), 0, false, SweepDirection.Clockwise);
                    ctx.EndFigure(true);

                    if (_noteTextBox != null)
                    {
                        double insetX = Math.Max(14, w * 0.14);
                        double insetY = Math.Max(12, h * 0.14);
                        _noteTextBox.Margin = new Thickness(insetX, insetY);
                    }
                }
                else
                {
                    // Retângulo de fluxograma com cantos suavemente arredondados
                    double r = Math.Min(12.0, Math.Min(right - left, bottom - top) * 0.18);
                    ctx.BeginFigure(new Point(left + r, top), true);
                    ctx.LineTo(new Point(right - r, top));
                    ctx.ArcTo(new Point(right, top + r), new Size(r, r), 0, false, SweepDirection.Clockwise);
                    ctx.LineTo(new Point(right, bottom - r));
                    ctx.ArcTo(new Point(right - r, bottom), new Size(r, r), 0, false, SweepDirection.Clockwise);
                    ctx.LineTo(new Point(left + r, bottom));
                    ctx.ArcTo(new Point(left, bottom - r), new Size(r, r), 0, false, SweepDirection.Clockwise);
                    ctx.LineTo(new Point(left, top + r));
                    ctx.ArcTo(new Point(left + r, top), new Size(r, r), 0, false, SweepDirection.Clockwise);
                    ctx.EndFigure(true);

                    if (_noteTextBox != null)
                    {
                        _noteTextBox.Margin = new Thickness(12, 18, 12, 10);
                    }
                }
            }

            _shapeVectorPath.Data = geo;
        }

        private void AtualizarCoresFormaFluxograma(IBrush? customOrCritBrush)
        {
            if (_shapeVectorPath == null) return;

            var strokeBrush = customOrCritBrush
                ?? (_isActive ? ThemePalette.AccentLight : (_isDarkMode ? ThemePalette.IconLightGray : ThemePalette.Accent));

            _shapeVectorPath.Stroke = strokeBrush;

            // Preenchimento interno sutil combinando com a cor da forma ou criticidade
            if (customOrCritBrush is ISolidColorBrush solid)
            {
                var c = solid.Color;
                byte alpha = _isDarkMode ? (byte)48 : (byte)36;
                _shapeVectorPath.Fill = new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
            }
            else
            {
                _shapeVectorPath.Fill = _isDarkMode
                    ? SolidColorBrush.Parse("#CC1E293B")
                    : SolidColorBrush.Parse("#E6FFFFFF");
            }

            var (badgeText, critHex) = ObterInfoCriticidade(CriticalityLevel);
            if (_shapeInnerBadge != null && _shapeInnerBadgeText != null)
            {
                if (!string.IsNullOrEmpty(badgeText) && !string.IsNullOrEmpty(critHex))
                {
                    _shapeInnerBadgeText.Text = badgeText;
                    try
                    {
                        _shapeInnerBadge.Background = SolidColorBrush.Parse(critHex);
                    }
                    catch
                    {
                        _shapeInnerBadge.Background = ThemePalette.Danger;
                    }
                    _shapeInnerBadge.IsVisible = true;
                }
                else
                {
                    _shapeInnerBadge.IsVisible = false;
                }
            }
        }
    }
}
