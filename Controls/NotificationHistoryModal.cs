using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;

namespace AvaloniaMdiApp
{
    public class NotificationHistoryModal : Border
    {
        public event Action? OnClosed;

        private readonly Border _card;
        private readonly StackPanel _listPanel;
        private readonly TextBlock _lblVazio;
        private readonly TextBlock _lblContador;
        private readonly Button _btnClear;
        private readonly Button _btnClose;
        private readonly bool _isDarkMode;

        public NotificationHistoryModal(bool isDarkMode)
        {
            _isDarkMode = isDarkMode;

            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;
            Background = new SolidColorBrush(Color.FromArgb(170, 9, 13, 22));
            ZIndex = 9995;

            PointerPressed += (s, e) =>
            {
                if (e.Source == this)
                {
                    Fechar();
                }
            };

            _card = new Border
            {
                Width = 540,
                Height = 520,
                MaxWidth = 620,
                MaxHeight = 680,
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1),
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                BoxShadow = new BoxShadows(new BoxShadow
                {
                    Blur = 32,
                    Color = Color.FromArgb(160, 0, 0, 0),
                    OffsetY = 10
                })
            };

            var mainDock = new DockPanel();

            // Header
            var headerBorder = new Border
            {
                Padding = new Thickness(18, 14),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var iconTitle = AppIcons.Create(AppIcons.Bell, 16, ThemePalette.Accent);
            iconTitle.Margin = new Thickness(0, 0, 10, 0);
            Grid.SetColumn(iconTitle, 0);

            var titleStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 2,
                VerticalAlignment = VerticalAlignment.Center
            };
            var lblTitle = new TextBlock
            {
                Text = "Histórico de Notificações",
                FontSize = 14,
                FontWeight = FontWeight.Bold
            };
            _lblContador = new TextBlock
            {
                Text = "Registro das notificações e avisos do sistema",
                FontSize = 11,
                Foreground = isDarkMode ? ThemePalette.DarkTextSecondary : ThemePalette.LightTextSecondary
            };
            titleStack.Children.Add(lblTitle);
            titleStack.Children.Add(_lblContador);
            Grid.SetColumn(titleStack, 1);

            var headerButtons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };

            _btnClear = new Button
            {
                Content = AppIcons.WithText(AppIcons.Trash, "Limpar", 11, 4, isDarkMode ? ThemePalette.DarkTextSecondary : ThemePalette.LightTextSecondary),
                Height = 28,
                Padding = new Thickness(10, 0),
                CornerRadius = new CornerRadius(6),
                Background = isDarkMode ? ThemePalette.DarkButtonBg : ThemePalette.LightButtonBg,
                Foreground = isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary,
                FontSize = 11,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(_btnClear, "Limpar todo o histórico de notificações");
            _btnClear.Click += (s, e) =>
            {
                NotificationHistoryService.LimparHistorico();
                CarregarLista();
            };

            _btnClose = new Button
            {
                Content = AppIcons.Create(AppIcons.Close, 11, ThemePalette.IconLightGray),
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(6),
                Background = Brushes.Transparent,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(_btnClose, "Fechar");
            _btnClose.Click += (s, e) => Fechar();

            headerButtons.Children.Add(_btnClear);
            headerButtons.Children.Add(_btnClose);
            Grid.SetColumn(headerButtons, 2);

            headerGrid.Children.Add(iconTitle);
            headerGrid.Children.Add(titleStack);
            headerGrid.Children.Add(headerButtons);
            headerBorder.Child = headerGrid;

            DockPanel.SetDock(headerBorder, Dock.Top);
            mainDock.Children.Add(headerBorder);

            // Conteúdo (Lista de Notificações)
            _listPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 8,
                Margin = new Thickness(16, 12, 16, 16)
            };

            _lblVazio = new TextBlock
            {
                Text = "Nenhuma notificação registrada no momento.",
                FontSize = 12,
                Foreground = isDarkMode ? ThemePalette.DarkTextSecondary : ThemePalette.LightTextSecondary,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 60, 0, 0)
            };

            var listHost = new Panel();
            listHost.Children.Add(_lblVazio);
            listHost.Children.Add(_listPanel);

            var scroller = new ScrollViewer
            {
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = listHost
            };
            mainDock.Children.Add(scroller);

            _card.Child = mainDock;
            Child = _card;

            AplicarCores();
            CarregarLista();

            // Marca todos como lidos ao abrir
            NotificationHistoryService.MarcarTodosComoLidos();
        }

        private void AplicarCores()
        {
            _card.Background = _isDarkMode ? ThemePalette.DarkWindowBg : ThemePalette.LightWindowBg;
            _card.BorderBrush = _isDarkMode ? ThemePalette.DarkBorder : ThemePalette.LightBorder;
        }

        private void CarregarLista()
        {
            _listPanel.Children.Clear();
            var itens = NotificationHistoryService.ObterItens();

            if (itens.Count == 0)
            {
                _lblVazio.IsVisible = true;
                _lblContador.Text = "Nenhuma notificação salva";
                _btnClear.IsEnabled = false;
                return;
            }

            _lblVazio.IsVisible = false;
            _btnClear.IsEnabled = true;
            _lblContador.Text = $"{itens.Count} notificação(ões) no histórico";

            foreach (var item in itens)
            {
                _listPanel.Children.Add(CriarItemCard(item));
            }
        }

        private Border CriarItemCard(NotificationHistoryItem item)
        {
            IBrush typeColor = item.Type switch
            {
                "success" => SolidColorBrush.Parse("#10B981").ToImmutable(),
                "warning" => SolidColorBrush.Parse("#F59E0B").ToImmutable(),
                "danger" => SolidColorBrush.Parse("#EF4444").ToImmutable(),
                _ => SolidColorBrush.Parse("#3B82F6").ToImmutable()
            };

            var border = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                BorderBrush = _isDarkMode ? ThemePalette.DarkBorder : ThemePalette.LightBorder,
                Background = _isDarkMode ? ThemePalette.DarkContentBg : ThemePalette.LightContentBg,
                Padding = new Thickness(12, 10)
            };

            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var stripe = new Border
            {
                Width = 4,
                CornerRadius = new CornerRadius(2),
                Background = typeColor,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Grid.SetColumn(stripe, 0);

            var textStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 3
            };

            var headerLine = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8
            };

            var lblTitle = new TextBlock
            {
                Text = item.Title,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = typeColor
            };
            headerLine.Children.Add(lblTitle);

            var lblMessage = new TextBlock
            {
                Text = item.Message,
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 16,
                Foreground = _isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary
            };

            textStack.Children.Add(headerLine);
            textStack.Children.Add(lblMessage);
            Grid.SetColumn(textStack, 1);

            var lblHora = new TextBlock
            {
                Text = item.FormattedTime,
                FontSize = 10.5,
                Foreground = _isDarkMode ? ThemePalette.DarkTextSecondary : ThemePalette.LightTextSecondary,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(8, 0, 0, 0)
            };
            ToolTip.SetTip(lblHora, item.FormattedDate);
            Grid.SetColumn(lblHora, 2);

            rowGrid.Children.Add(stripe);
            rowGrid.Children.Add(textStack);
            rowGrid.Children.Add(lblHora);

            border.Child = rowGrid;
            return border;
        }

        public void Fechar()
        {
            if (Parent is Panel parentPanel)
            {
                parentPanel.Children.Remove(this);
            }
            OnClosed?.Invoke();
        }
    }
}
