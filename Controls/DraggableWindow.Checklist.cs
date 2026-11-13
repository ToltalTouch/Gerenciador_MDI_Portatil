using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace AvaloniaMdiApp
{
    public partial class DraggableWindow
    {
        private void AdicionarLinhaChecklist(string texto, bool marcado, string notaVinculada, TextFormatData? formatoNotaVinculada, bool focusInput, string colorHex = "", string? linkedWindowId = null, string? rowId = null)
        {
            if (_checklistContainer == null) return;

            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var checkBox = new CheckBox
            {
                IsChecked = marcado,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 4, 0)
            };

            var inputBox = new TextBox
            {
                Text = texto,
                Watermark = "Digite um item...",
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(4, 6),
                FontSize = 13,
                FontWeight = FontWeight.Normal,
                FontStyle = FontStyle.Normal
            };

            var colorDot = new Border
            {
                Width = 12,
                Height = 12,
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.Parse("#64748B")),
                Background = string.IsNullOrWhiteSpace(colorHex) ? Brushes.Transparent : new SolidColorBrush(Color.Parse(colorHex))
            };

            var btnRowColor = new Button
            {
                Content = colorDot,
                Width = 24,
                Height = 24,
                Padding = new Thickness(0),
                Margin = new Thickness(2, 0, 1, 0),
                Background = Brushes.Transparent,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                CornerRadius = new CornerRadius(4)
            };
            ToolTip.SetTip(btnRowColor, "Cor do item");

            var btnLinkWindow = new Button
            {
                Content = AppIcons.Create(AppIcons.Link, 11.5, ThemePalette.IconLightGray),
                Width = 24,
                Height = 24,
                Padding = new Thickness(0),
                Margin = new Thickness(1, 0, 1, 0),
                Background = Brushes.Transparent,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                CornerRadius = new CornerRadius(4),
                Opacity = string.IsNullOrWhiteSpace(linkedWindowId) ? 0.65 : 1.0
            };
            ToolTip.SetTip(btnLinkWindow, "Vincular / focar janela para este item");

            var btnNote = new Button
            {
                Content = AppIcons.Create(AppIcons.Note, 11.5, ThemePalette.IconLightGray),
                Width = 24,
                Height = 24,
                Padding = new Thickness(0),
                Margin = new Thickness(1, 0, 1, 0),
                Background = Brushes.Transparent,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                CornerRadius = new CornerRadius(4),
                Opacity = string.IsNullOrWhiteSpace(notaVinculada) ? 0.65 : 1.0
            };
            ToolTip.SetTip(btnNote, "Abrir Bloco de Notas atrelado a esta linha");

            var btnRemove = new Button
            {
                Content = "✕",
                Width = 24,
                Height = 24,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 4, 0),
                Background = Brushes.Transparent,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                CornerRadius = new CornerRadius(4),
                FontSize = 10,
                Opacity = 0.6
            };
            ToolTip.SetTip(btnRemove, "Excluir linha");

            var strikeLine = new Border
            {
                Height = 1.5,
                Margin = new Thickness(6, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsHitTestVisible = false,
                IsVisible = marcado
            };

            var rowBorder = new Border
            {
                CornerRadius = new CornerRadius(5),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(2),
                Child = rowGrid
            };

            void AtualizarEstiloMarcacao()
            {
                bool isChecked = checkBox.IsChecked == true;
                strikeLine.IsVisible = isChecked;
                inputBox.Opacity = isChecked ? 0.5 : 1.0;
            }

            checkBox.IsCheckedChanged += (s, e) => AtualizarEstiloMarcacao();

            var entry = new ChecklistRowState
            {
                RowId = string.IsNullOrWhiteSpace(rowId) ? System.Guid.NewGuid().ToString("N") : rowId,
                Check = checkBox,
                Input = inputBox,
                StrikeLine = strikeLine,
                RowBorder = rowBorder,
                BtnNote = btnNote,
                ColorDot = colorDot,
                BtnLinkWindow = btnLinkWindow,
                ColorHex = colorHex ?? string.Empty,
                LinkedWindowId = linkedWindowId ?? string.Empty,
                LinkedNoteContent = notaVinculada ?? string.Empty,
                LinkedNoteFormat = formatoNotaVinculada ?? new TextFormatData()
            };
            _checklistRows.Add(entry);

            var colorFlyout = new Flyout { Placement = PlacementMode.BottomEdgeAlignedRight };
            var colorPanel = new WrapPanel { MaxWidth = 150, ItemWidth = 26, ItemHeight = 26 };
            string[] rowColors = { "", "#EF4444", "#F59E0B", "#10B981", "#3B82F6", "#8B5CF6", "#EC4899", "#06B6D4" };
            foreach (var hex in rowColors)
            {
                var cBtn = new Button
                {
                    Width = 22,
                    Height = 22,
                    Padding = new Thickness(0),
                    Margin = new Thickness(2),
                    CornerRadius = new CornerRadius(11),
                    Background = string.IsNullOrEmpty(hex) ? Brushes.Transparent : new SolidColorBrush(Color.Parse(hex)),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.Parse("#64748B")),
                    Content = string.IsNullOrEmpty(hex) ? new TextBlock { Text = "∅", FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } : null
                };
                cBtn.Click += (s, e) =>
                {
                    entry.ColorHex = hex;
                    AtualizarEstiloLinhaChecklist(entry);
                    colorFlyout.Hide();
                };
                colorPanel.Children.Add(cBtn);
            }
            colorFlyout.Content = colorPanel;
            btnRowColor.Flyout = colorFlyout;

            btnLinkWindow.Click += (s, e) => OnRequestLinkRowWindow?.Invoke(this, entry);

            rowBorder.AddHandler(InputElement.PointerPressedEvent, (s, e) =>
            {
                OnChecklistRowClicked?.Invoke(this, entry);
            }, Avalonia.Interactivity.RoutingStrategies.Tunnel);

            inputBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    AdicionarLinhaChecklist("", false, "", null, focusInput: true);
                }
            };

            inputBox.TextChanged += (s, e) =>
            {
                if (entry.OpenNoteWindow != null && !string.IsNullOrWhiteSpace(inputBox.Text))
                {
                    entry.OpenNoteWindow.AtualizarTituloExterno(inputBox.Text.Trim());
                }
            };

            btnNote.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(inputBox.Text))
                {
                    inputBox.Text = $"Item {_checklistRows.IndexOf(entry) + 1}";
                }
                OnRequestOpenRowNotepad?.Invoke(this, entry);
            };

            btnRemove.Click += (s, e) =>
            {
                entry.OpenNoteWindow?.Fechar();
                _checklistRows.Remove(entry);
                _checklistContainer.Children.Remove(rowBorder);
            };

            Grid.SetColumn(checkBox, 0);
            Grid.SetColumn(inputBox, 1);
            Grid.SetColumn(strikeLine, 1);
            Grid.SetColumn(btnRowColor, 2);
            Grid.SetColumn(btnLinkWindow, 3);
            Grid.SetColumn(btnNote, 4);
            Grid.SetColumn(btnRemove, 5);
            rowGrid.Children.Add(checkBox);
            rowGrid.Children.Add(inputBox);
            rowGrid.Children.Add(strikeLine);
            rowGrid.Children.Add(btnRowColor);
            rowGrid.Children.Add(btnLinkWindow);
            rowGrid.Children.Add(btnNote);
            rowGrid.Children.Add(btnRemove);

            _checklistContainer.Children.Add(rowBorder);

            AtualizarEstiloLinhaChecklist(entry);
            AtualizarIndicadorVinculoLinha(entry);
            AtualizarEstiloMarcacao();

            if (focusInput)
            {
                inputBox.Focus();
            }
        }

        public void AtualizarIndicadorNotaLinha(ChecklistRowState row)
        {
            bool temNota = !string.IsNullOrWhiteSpace(row.LinkedNoteContent);
            row.BtnNote.Opacity = temNota ? 1.0 : 0.65;
            row.BtnNote.BorderThickness = temNota ? new Thickness(1) : new Thickness(0);
            row.BtnNote.BorderBrush = temNota ? ThemePalette.AccentLight : Brushes.Transparent;
        }

        public void AtualizarIndicadorVinculoLinha(ChecklistRowState row)
        {
            if (row.BtnLinkWindow == null) return;
            bool temVinculo = !string.IsNullOrWhiteSpace(row.LinkedWindowId);
            row.BtnLinkWindow.Opacity = temVinculo ? 1.0 : 0.65;
            row.BtnLinkWindow.BorderThickness = temVinculo ? new Thickness(1) : new Thickness(0);
            row.BtnLinkWindow.BorderBrush = temVinculo ? ThemePalette.AccentLight : Brushes.Transparent;
        }

        public void AtualizarFiltroCorChecklist(string? hexFilter)
        {
            foreach (var row in _checklistRows)
            {
                bool rowMatches = CorCombinaComFiltro(row.ColorHex, hexFilter);
                row.RowBorder.Opacity = rowMatches ? 1.0 : 0.25;
            }
        }

        private void AtualizarEstiloLinhaChecklist(ChecklistRowState row)
        {
            if (!string.IsNullOrWhiteSpace(row.ColorHex) && Color.TryParse(row.ColorHex, out var rowColor))
            {
                row.RowBorder.Background = new SolidColorBrush(Color.FromArgb(_isDarkMode ? (byte)55 : (byte)42, rowColor.R, rowColor.G, rowColor.B));
                row.RowBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(160, rowColor.R, rowColor.G, rowColor.B));
                if (row.ColorDot != null) row.ColorDot.Background = new SolidColorBrush(rowColor);
            }
            else
            {
                row.RowBorder.Background = _isDarkMode ? ThemePalette.DarkContentBg : ThemePalette.LightContentBg;
                row.RowBorder.BorderBrush = _isDarkMode ? ThemePalette.DarkBorder : ThemePalette.LightBorder;
                if (row.ColorDot != null) row.ColorDot.Background = Brushes.Transparent;
            }
            row.Input.Foreground = _isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;
            row.StrikeLine.Background = _isDarkMode ? ThemePalette.DarkTextSecondary : ThemePalette.LightTextSecondary;
            row.BtnNote.Foreground = _isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;
            AtualizarIndicadorNotaLinha(row);
        }
    }
}
