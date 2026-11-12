using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AvaloniaMdiApp
{
    /// <summary>
    /// Ícones vetoriais minimalistas em cache estático com tons de cinza claro (#CBD5E1 / #94A3B8).
    /// Evita uso de emojis coloridos e mantém consumo zero de CPU/memória adicional.
    /// </summary>
    public static class AppIcons
    {
        public static readonly StreamGeometry Plus = StreamGeometry.Parse("M 7 2 L 9 2 L 9 7 L 14 7 L 14 9 L 9 9 L 9 14 L 7 14 L 7 9 L 2 9 L 2 7 L 7 7 Z");
        public static readonly StreamGeometry Minus = StreamGeometry.Parse("M 2 7 L 14 7 L 14 9 L 2 9 Z");
        public static readonly StreamGeometry Edit = StreamGeometry.Parse("M 11.8 1.5 L 14.5 4.2 L 5.2 13.5 L 2 14 L 2.5 10.8 Z");
        public static readonly StreamGeometry Trash = StreamGeometry.Parse("M 5 2 L 11 2 L 11 3.5 L 14 3.5 L 14 5 L 2 5 L 2 3.5 L 5 3.5 Z M 3.5 6 L 12.5 6 L 11.8 14 L 4.2 14 Z");
        public static readonly StreamGeometry Folder = StreamGeometry.Parse("M 1.5 3 L 6.5 3 L 8 4.8 L 14.5 4.8 L 14.5 13 L 1.5 13 Z");
        public static readonly StreamGeometry Workspace = StreamGeometry.Parse("M 1.5 2.5 L 7 2.5 L 7 7.5 L 1.5 7.5 Z M 9 2.5 L 14.5 2.5 L 14.5 7.5 L 9 7.5 Z M 1.5 9 L 7 9 L 7 14 L 1.5 14 Z M 9 9 L 14.5 9 L 14.5 14 L 9 14 Z");
        public static readonly StreamGeometry Save = StreamGeometry.Parse("M 2 2 L 11.5 2 L 14 4.5 L 14 14 L 2 14 Z M 4.5 2.5 L 4.5 6.5 L 10.5 6.5 L 10.5 2.5 Z M 4.5 9.5 L 4.5 13.5 L 11.5 13.5 L 11.5 9.5 Z");
        public static readonly StreamGeometry Note = StreamGeometry.Parse("M 3 1.5 L 10.5 1.5 L 13.5 4.5 L 13.5 14.5 L 3 14.5 Z M 5 6.5 L 11.5 6.5 L 11.5 7.8 L 5 7.8 Z M 5 9.2 L 11.5 9.2 L 11.5 10.5 L 5 10.5 Z M 5 11.8 L 9 11.8 L 9 13 L 5 13 Z");
        public static readonly StreamGeometry Star = StreamGeometry.Parse("M 8 1.5 L 9.9 5.6 L 14.4 6.2 L 11.1 9.3 L 11.9 13.8 L 8 11.6 L 4.1 13.8 L 4.9 9.3 L 1.6 6.2 L 6.1 5.6 Z");
        public static readonly StreamGeometry Checklist = StreamGeometry.Parse("M 2 3 L 6 3 L 6 7 L 2 7 Z M 8 4.2 L 14 4.2 L 14 5.8 L 8 5.8 Z M 2 9.5 L 6 9.5 L 6 13.5 L 2 13.5 Z M 8 10.7 L 14 10.7 L 14 12.3 L 8 12.3 Z");
        public static readonly StreamGeometry Spreadsheet = StreamGeometry.Parse("M 1.5 2 L 14.5 2 L 14.5 14 L 1.5 14 Z M 3 4.5 L 7.2 4.5 L 7.2 7.5 L 3 7.5 Z M 8.8 4.5 L 13 4.5 L 13 7.5 L 8.8 7.5 Z M 3 9 L 7.2 9 L 7.2 12.2 L 3 12.2 Z M 8.8 9 L 13 9 L 13 12.2 L 8.8 12.2 Z");
        public static readonly StreamGeometry Chart = StreamGeometry.Parse("M 2 12.5 L 14 12.5 L 14 14 L 2 14 Z M 3 8 L 5.2 8 L 5.2 11.5 L 3 11.5 Z M 6.8 5 L 9 5 L 9 11.5 L 6.8 11.5 Z M 10.6 2.5 L 12.8 2.5 L 12.8 11.5 L 10.6 11.5 Z");
        public static readonly StreamGeometry Image = StreamGeometry.Parse("M 1.5 2.5 L 14.5 2.5 L 14.5 13.5 L 1.5 13.5 Z M 3 4 L 3 11.5 L 6.5 8 L 9 10.5 L 11 8.5 L 13 10.5 L 13 4 Z M 5.5 5.2 A 1.2 1.2 0 1 1 5.5 7.6 A 1.2 1.2 0 1 1 5.5 5.2 Z");
        public static readonly StreamGeometry Globe = StreamGeometry.Parse("M 8 1.5 A 6.5 6.5 0 1 0 8 14.5 A 6.5 6.5 0 1 0 8 1.5 Z M 3.2 7.2 L 12.8 7.2 L 12.8 8.8 L 3.2 8.8 Z M 7.2 2.8 L 8.8 2.8 L 8.8 13.2 L 7.2 13.2 Z");
        public static readonly StreamGeometry Search = StreamGeometry.Parse("M 6.8 2 A 4.8 4.8 0 1 0 6.8 11.6 A 4.8 4.8 0 1 0 6.8 2 Z M 6.8 3.6 A 3.2 3.2 0 1 1 6.8 10 A 3.2 3.2 0 1 1 6.8 3.6 Z M 10.8 9.6 L 14.2 13 L 13 14.2 L 9.6 10.8 Z");
        public static readonly StreamGeometry Play = StreamGeometry.Parse("M 4 2.5 L 13 8 L 4 13.5 Z");
        public static readonly StreamGeometry Code = StreamGeometry.Parse("M 5.5 4 L 1.5 8 L 5.5 12 L 6.7 10.8 L 3.9 8 L 6.7 5.2 Z M 10.5 4 L 9.3 5.2 L 12.1 8 L 9.3 10.8 L 10.5 12 L 14.5 8 Z");
        public static readonly StreamGeometry Book = StreamGeometry.Parse("M 2.5 2 L 13.5 2 L 13.5 14 L 2.5 14 Z M 4.5 4 L 11.5 4 L 11.5 6 L 4.5 6 Z M 4.5 8 L 9.5 8 L 9.5 9.5 L 4.5 9.5 Z");
        public static readonly StreamGeometry Link = StreamGeometry.Parse("M 6.5 9.5 L 9.5 6.5 L 10.6 7.6 L 7.6 10.6 Z M 9 3.5 L 11.5 3.5 A 2.5 2.5 0 0 1 11.5 8.5 L 10 8.5 L 10 7 L 11.5 7 A 1 1 0 0 0 11.5 5 L 9 5 Z M 7 12.5 L 4.5 12.5 A 2.5 2.5 0 0 1 4.5 7.5 L 6 7.5 L 6 9 L 4.5 9 A 1 1 0 0 0 4.5 11 L 7 11 Z");
        public static readonly StreamGeometry Palette = StreamGeometry.Parse("M 8 1.8 A 6.2 6.2 0 1 0 8 14.2 A 2 2 0 0 0 10 12.2 C 10 11.4 9.6 10.8 9.6 10.2 C 9.6 9.5 10.2 9 11 9 L 12.2 9 A 4 4 0 0 0 14.2 5.5 A 6.2 6.2 0 0 0 8 1.8 Z M 5.2 5 A 1 1 0 1 1 5.2 7 A 1 1 0 1 1 5.2 5 Z M 8 3.8 A 1 1 0 1 1 8 5.8 A 1 1 0 1 1 8 3.8 Z M 10.8 5 A 1 1 0 1 1 10.8 7 A 1 1 0 1 1 10.8 5 Z");
        public static readonly StreamGeometry Cascade = StreamGeometry.Parse("M 2 2.5 L 10.5 2.5 L 10.5 9.5 L 2 9.5 Z M 5.5 6.5 L 14 6.5 L 14 13.5 L 5.5 13.5 Z");
        public static readonly StreamGeometry Target = StreamGeometry.Parse("M 8 1.5 A 6.5 6.5 0 1 0 8 14.5 A 6.5 6.5 0 1 0 8 1.5 Z M 8 3.5 A 4.5 4.5 0 1 1 8 12.5 A 4.5 4.5 0 1 1 8 3.5 Z M 8 6 A 2 2 0 1 0 8 10 A 2 2 0 1 0 8 6 Z");
        public static readonly StreamGeometry Settings = StreamGeometry.Parse("M 6.8 1.5 L 9.2 1.5 L 9.6 3.4 L 11.3 4.4 L 13.1 3.6 L 14.4 5.8 L 12.9 7.1 L 12.9 8.9 L 14.4 10.2 L 13.1 12.4 L 11.3 11.6 L 9.6 12.6 L 9.2 14.5 L 6.8 14.5 L 6.4 12.6 L 4.7 11.6 L 2.9 12.4 L 1.6 10.2 L 3.1 8.9 L 3.1 7.1 L 1.6 5.8 L 2.9 3.6 L 4.7 4.4 L 6.4 3.4 Z M 8 5.6 A 2.4 2.4 0 1 0 8 10.4 A 2.4 2.4 0 1 0 8 5.6 Z");
        public static readonly StreamGeometry Theme = StreamGeometry.Parse("M 8 1.8 A 6.2 6.2 0 1 0 14.2 8 A 4.8 4.8 0 0 1 8 1.8 Z");
        public static readonly StreamGeometry Pin = StreamGeometry.Parse("M 5 1.8 L 11 1.8 L 11 3.4 L 10 4.4 L 10 7.6 L 12 9.4 L 12 10.8 L 8.8 10.8 L 8.8 14.5 L 7.2 14.5 L 7.2 10.8 L 4 10.8 L 4 9.4 L 6 7.6 L 6 4.4 L 5 3.4 Z");
        public static readonly StreamGeometry Exit = StreamGeometry.Parse("M 2 2 L 9 2 L 9 3.6 L 3.6 3.6 L 3.6 12.4 L 9 12.4 L 9 14 L 2 14 Z M 10.2 5 L 14 8 L 10.2 11 L 10.2 8.8 L 6 8.8 L 6 7.2 L 10.2 7.2 Z");
        public static readonly StreamGeometry Sleep = StreamGeometry.Parse("M 3 3 L 9 3 L 9 4.4 L 5.2 8.2 L 9 8.2 L 9 9.6 L 3 9.6 L 3 8.2 L 6.8 4.4 L 3 4.4 Z M 9.5 7.5 L 14 7.5 L 14 8.7 L 11.2 11.5 L 14 11.5 L 14 12.7 L 9.5 12.7 L 9.5 11.5 L 12.3 8.7 L 9.5 8.7 Z");
        public static readonly StreamGeometry Home = StreamGeometry.Parse("M 8 2 L 14.5 7.8 L 12.8 7.8 L 12.8 13.8 L 9.4 13.8 L 9.4 9.8 L 6.6 9.8 L 6.6 13.8 L 3.2 13.8 L 3.2 7.8 L 1.5 7.8 Z");
        public static readonly StreamGeometry Window = StreamGeometry.Parse("M 1.5 2.5 L 14.5 2.5 L 14.5 13.5 L 1.5 13.5 Z M 3 5.5 L 13 5.5 L 13 12 L 3 12 Z");
        public static readonly StreamGeometry CircleDot = StreamGeometry.Parse("M 8 3.5 A 4.5 4.5 0 1 0 8 12.5 A 4.5 4.5 0 1 0 8 3.5 Z");
        public static readonly StreamGeometry Bolt = StreamGeometry.Parse("M 9 1.5 L 3.5 9 L 7.5 9 L 6.5 14.5 L 12.5 7 L 8.5 7 Z");
        public static readonly StreamGeometry Sparkle = StreamGeometry.Parse("M 8 1.5 L 9.6 6.4 L 14.5 8 L 9.6 9.6 L 8 14.5 L 6.4 9.6 L 1.5 8 L 6.4 6.4 Z");
        public static readonly StreamGeometry Sigma = StreamGeometry.Parse("M 3 2.5 L 13 2.5 L 13 4.2 L 6.2 4.2 L 9.8 8 L 6.2 11.8 L 13 11.8 L 13 13.5 L 3 13.5 L 3 11.8 L 7.4 8 L 3 4.2 Z");
        public static readonly StreamGeometry ArrowDown = StreamGeometry.Parse("M 7.2 2.5 L 8.8 2.5 L 8.8 10.2 L 11.8 7.2 L 13 8.4 L 8 13.4 L 3 8.4 L 4.2 7.2 L 7.2 10.2 Z");
        public static readonly StreamGeometry ArrowUp = StreamGeometry.Parse("M 8 2.6 L 13 7.6 L 11.8 8.8 L 8.8 5.8 L 8.8 13.5 L 7.2 13.5 L 7.2 5.8 L 4.2 8.8 L 3 7.6 Z");
        public static readonly StreamGeometry ArrowLeft = StreamGeometry.Parse("M 10.5 3 L 4.5 8 L 10.5 13 L 10.5 3 Z");
        public static readonly StreamGeometry ArrowRight = StreamGeometry.Parse("M 5.5 3 L 11.5 8 L 5.5 13 L 5.5 3 Z");
        public static readonly StreamGeometry ExternalLink = StreamGeometry.Parse("M 9 2 L 14 2 L 14 7 L 12.4 7 L 12.4 4.7 L 7.6 9.5 L 6.5 8.4 L 11.3 3.6 L 9 3.6 Z M 3 4 L 7.5 4 L 7.5 5.5 L 4.5 5.5 L 4.5 11.5 L 10.5 11.5 L 10.5 8.5 L 12 8.5 L 12 13 L 3 13 Z");
        public static readonly StreamGeometry AlignLeft = StreamGeometry.Parse("M 2 3 L 14 3 L 14 4.6 L 2 4.6 Z M 2 6.4 L 10 6.4 L 10 8 L 2 8 Z M 2 9.8 L 14 9.8 L 14 11.4 L 2 11.4 Z M 2 13.2 L 8 13.2 L 8 14.8 L 2 14.8 Z");
        public static readonly StreamGeometry AlignCenter = StreamGeometry.Parse("M 2 3 L 14 3 L 14 4.6 L 2 4.6 Z M 4 6.4 L 12 6.4 L 12 8 L 4 8 Z M 2 9.8 L 14 9.8 L 14 11.4 L 2 11.4 Z M 5 13.2 L 11 13.2 L 11 14.8 L 5 14.8 Z");
        public static readonly StreamGeometry AlignRight = StreamGeometry.Parse("M 2 3 L 14 3 L 14 4.6 L 2 4.6 Z M 6 6.4 L 14 6.4 L 14 8 L 6 8 Z M 2 9.8 L 14 9.8 L 14 11.4 L 2 11.4 Z M 8 13.2 L 14 13.2 L 14 14.8 L 8 14.8 Z");
        public static readonly StreamGeometry Refresh = StreamGeometry.Parse("M 8 2 A 6 6 0 1 0 13.6 10.2 L 12 9.6 A 4.3 4.3 0 1 1 11.2 5.2 L 9.2 5.2 L 9.2 6.8 L 14 6.8 L 14 2 L 12.4 2 L 12.4 4 A 6 6 0 0 0 8 2 Z");
        public static readonly StreamGeometry Download = StreamGeometry.Parse("M 7.2 2 L 8.8 2 L 8.8 8.6 L 11.4 6 L 12.5 7.1 L 8 11.6 L 3.5 7.1 L 4.6 6 L 7.2 8.6 Z M 2.5 12.5 L 13.5 12.5 L 13.5 14 L 2.5 14 Z");
        public static readonly StreamGeometry ShapeRect = StreamGeometry.Parse("M 2 3.5 L 14 3.5 L 14 12.5 L 2 12.5 Z M 3.5 5 L 3.5 11 L 12.5 11 L 12.5 5 Z");
        public static readonly StreamGeometry ShapeDiamond = StreamGeometry.Parse("M 8 1.5 L 14.5 8 L 8 14.5 L 1.5 8 Z M 8 3.6 L 3.6 8 L 8 12.4 L 12.4 8 Z");
        public static readonly StreamGeometry ShapeCircle = StreamGeometry.Parse("M 8 1.8 A 6.2 6.2 0 1 0 8 14.2 A 6.2 6.2 0 1 0 8 1.8 Z M 8 3.4 A 4.6 4.6 0 1 1 8 12.6 A 4.6 4.6 0 1 1 8 3.4 Z");
        public static readonly StreamGeometry Warning = StreamGeometry.Parse("M 8 2 L 14.5 13.5 L 1.5 13.5 Z M 7.3 6.2 L 8.7 6.2 L 8.7 9.8 L 7.3 9.8 Z M 7.3 11 L 8.7 11 L 8.7 12.3 L 7.3 12.3 Z");

        public static PathIcon Create(StreamGeometry geometry, double size = 12, IBrush? foreground = null)
        {
            return new PathIcon
            {
                Data = geometry,
                Width = size,
                Height = size,
                Foreground = foreground ?? ThemePalette.IconLightGray,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
        }

        public static StackPanel WithText(StreamGeometry geometry, string text, double iconSize = 11.5, double spacing = 6, IBrush? iconBrush = null)
        {
            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = spacing,
                VerticalAlignment = VerticalAlignment.Center
            };
            stack.Children.Add(Create(geometry, iconSize, iconBrush ?? ThemePalette.IconLightGray));
            stack.Children.Add(new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center
            });
            return stack;
        }

        public static StreamGeometry FromWindowType(string? windowType) => windowType switch
        {
            "notepad" => Note,
            "checklist" => Checklist,
            "spreadsheet" => Spreadsheet,
            "browser" => Globe,
            "gallery" => Image,
            "image" => Image,
            "shape_rect" => ShapeRect,
            "shape_diamond" => ShapeDiamond,
            "shape_circle" => ShapeCircle,
            _ => Window
        };
    }
}
