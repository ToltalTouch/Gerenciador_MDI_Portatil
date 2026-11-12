using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace AvaloniaMdiApp
{
    public partial class DraggableWindow
    {
        private void AdicionarLinhaChecklist(string texto, bool marcado, string notaVinculada, TextFormatData? formatoNotaVinculada, bool focusInput)
        {
            if (_checklistContainer == null) return;

            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
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
                FontSize = _fontSize,
                FontWeight = _isBold ? FontWeight.Bold : FontWeight.Normal,
                FontStyle = _isItalic ? FontStyle.Italic : FontStyle.Normal,
                FontFamily = ObterFontFamily(_fontFamilyName)
            };

            var btnNote = new Button
            {
                Content = AppIcons.Create(AppIcons.Note, 11.5, ThemePalette.IconLightGray),
                Width = 26,
                Height = 24,
                Padding = new Thickness(0),
                Margin = new Thickness(2, 0, 2, 0),
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
                Check = checkBox,
                Input = inputBox,
                StrikeLine = strikeLine,
                RowBorder = rowBorder,
                BtnNote = btnNote,
                LinkedNoteContent = notaVinculada ?? string.Empty,
                LinkedNoteFormat = formatoNotaVinculada ?? new TextFormatData()
            };
            _checklistRows.Add(entry);

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
            Grid.SetColumn(btnNote, 2);
            Grid.SetColumn(btnRemove, 3);
            rowGrid.Children.Add(checkBox);
            rowGrid.Children.Add(inputBox);
            rowGrid.Children.Add(strikeLine);
            rowGrid.Children.Add(btnNote);
            rowGrid.Children.Add(btnRemove);

            _checklistContainer.Children.Add(rowBorder);

            AtualizarEstiloLinhaChecklist(entry);
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

        private void AtualizarEstiloLinhaChecklist(ChecklistRowState row)
        {
            row.RowBorder.Background = _isDarkMode ? ThemePalette.DarkContentBg : ThemePalette.LightContentBg;
            row.RowBorder.BorderBrush = _isDarkMode ? ThemePalette.DarkBorder : ThemePalette.LightBorder;
            row.Input.Foreground = _isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;
            row.StrikeLine.Background = _isDarkMode ? ThemePalette.DarkTextSecondary : ThemePalette.LightTextSecondary;
            row.BtnNote.Foreground = _isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;
            AtualizarIndicadorNotaLinha(row);
        }
    }
}
