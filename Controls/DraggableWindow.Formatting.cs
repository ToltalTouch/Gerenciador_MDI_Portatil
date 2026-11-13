using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System;

namespace AvaloniaMdiApp
{
    public partial class DraggableWindow
    {
        private Border CriarBarraFormatacao(
            out Button btnBold,
            out Button btnItalic,
            out Button btnFontDec,
            out TextBlock lblFontSize,
            out Button btnFontInc,
            out Button btnFormatMenu)
        {
            var bar = new Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(8, 4),
                Height = 32
            };

            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center
            };

            btnBold = CriarBotaoFormatacao("B", "Negrito (Ctrl+B)", FontWeight.Bold, FontStyle.Normal);
            btnBold.Click += (s, e) => AlternarNegrito();

            btnItalic = CriarBotaoFormatacao("I", "Itálico (Ctrl+I)", FontWeight.SemiBold, FontStyle.Italic);
            btnItalic.Click += (s, e) => AlternarItalico();

            btnFontDec = CriarBotaoFormatacao("A-", "Diminuir fonte (Ctrl+-)", FontWeight.SemiBold, FontStyle.Normal, width: 28);
            btnFontDec.Click += (s, e) => AlterarTamanhoFonte(-1);

            lblFontSize = new TextBlock
            {
                Text = $"{(int)_fontSize}pt",
                FontSize = 11,
                FontWeight = FontWeight.Medium,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0)
            };

            btnFontInc = CriarBotaoFormatacao("A+", "Aumentar fonte (Ctrl++)", FontWeight.SemiBold, FontStyle.Normal, width: 28);
            btnFontInc.Click += (s, e) => AlterarTamanhoFonte(1);

            btnFormatMenu = new Button
            {
                Content = "Aa ▾",
                Height = 22,
                Padding = new Thickness(8, 0),
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(btnFormatMenu, "Mais opções de formatação de texto");

            var flyout = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedLeft };

            var miBold = new MenuItem { Header = "Alternar Negrito (Ctrl+B)" };
            miBold.Click += (s, e) => AlternarNegrito();

            var miItalic = new MenuItem { Header = "Alternar Itálico (Ctrl+I)" };
            miItalic.Click += (s, e) => AlternarItalico();

            var miFontInc = new MenuItem { Icon = AppIcons.Create(AppIcons.Plus, 10), Header = "Aumentar Fonte" };
            miFontInc.Click += (s, e) => AlterarTamanhoFonte(2);

            var miFontDec = new MenuItem { Icon = AppIcons.Create(AppIcons.Minus, 10), Header = "Diminuir Fonte" };
            miFontDec.Click += (s, e) => AlterarTamanhoFonte(-2);

            var miAlignLeft = new MenuItem { Icon = AppIcons.Create(AppIcons.AlignLeft, 11), Header = "Alinhar à Esquerda" };
            miAlignLeft.Click += (s, e) => DefinirAlinhamento("left");

            var miAlignCenter = new MenuItem { Icon = AppIcons.Create(AppIcons.AlignCenter, 11), Header = "Centralizar Texto" };
            miAlignCenter.Click += (s, e) => DefinirAlinhamento("center");

            var miAlignRight = new MenuItem { Icon = AppIcons.Create(AppIcons.AlignRight, 11), Header = "Alinhar à Direita" };
            miAlignRight.Click += (s, e) => DefinirAlinhamento("right");

            var miFontDefault = new MenuItem { Header = "Fonte Padrão (Sans-Serif)" };
            miFontDefault.Click += (s, e) => DefinirFamiliaFonte("default");

            var miFontMono = new MenuItem { Header = "Fonte Monoespaçada (Código)" };
            miFontMono.Click += (s, e) => DefinirFamiliaFonte("mono");

            var miFontSerif = new MenuItem { Header = "Fonte Serifada (Leitura)" };
            miFontSerif.Click += (s, e) => DefinirFamiliaFonte("serif");

            var miReset = new MenuItem { Icon = AppIcons.Create(AppIcons.Refresh, 11), Header = "Restaurar Formatação Padrão" };
            miReset.Click += (s, e) => RestaurarFormatacaoPadrao();

            flyout.Items.Add(miBold);
            flyout.Items.Add(miItalic);
            flyout.Items.Add(new Separator());
            flyout.Items.Add(miFontInc);
            flyout.Items.Add(miFontDec);
            flyout.Items.Add(new Separator());
            flyout.Items.Add(miAlignLeft);
            flyout.Items.Add(miAlignCenter);
            flyout.Items.Add(miAlignRight);
            flyout.Items.Add(new Separator());
            flyout.Items.Add(miFontDefault);
            flyout.Items.Add(miFontMono);
            flyout.Items.Add(miFontSerif);
            flyout.Items.Add(new Separator());
            flyout.Items.Add(miReset);

            btnFormatMenu.Flyout = flyout;

            stack.Children.Add(btnBold);
            stack.Children.Add(btnItalic);
            stack.Children.Add(btnFontDec);
            stack.Children.Add(lblFontSize);
            stack.Children.Add(btnFontInc);
            stack.Children.Add(btnFormatMenu);

            bar.Child = stack;
            return bar;
        }

        private Button CriarBotaoFormatacao(string texto, string tooltip, FontWeight weight, FontStyle style, double width = 24)
        {
            var btn = new Button
            {
                Content = texto,
                Width = width,
                Height = 22,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                FontSize = 11,
                FontWeight = weight,
                FontStyle = style,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(btn, tooltip);
            return btn;
        }

        private void AlternarNegrito()
        {
            if (WindowType == "notepad" && _richLines.Count > 0)
            {
                bool targetBold = !_isBold;
                AplicarFormatacaoNaLinhaOuSelecao(sp => sp.IsBold = targetBold);
                AtualizarCores();
                return;
            }
            _isBold = !_isBold;
            AplicarFormatacaoTexto();
            AtualizarCores();
        }

        private void AlternarItalico()
        {
            if (WindowType == "notepad" && _richLines.Count > 0)
            {
                bool targetItalic = !_isItalic;
                AplicarFormatacaoNaLinhaOuSelecao(sp => sp.IsItalic = targetItalic);
                AtualizarCores();
                return;
            }
            _isItalic = !_isItalic;
            AplicarFormatacaoTexto();
            AtualizarCores();
        }

        private void AlterarTamanhoFonte(double delta)
        {
            if (WindowType == "notepad" && _richLines.Count > 0)
            {
                AplicarFormatacaoNaLinhaOuSelecao(sp => sp.FontSize = Math.Clamp((sp.FontSize > 0 ? sp.FontSize : 13) + delta, 9, 32));
                return;
            }
            _fontSize = Math.Clamp(_fontSize + delta, 9, 32);
            if (_lblFontSize != null)
            {
                _lblFontSize.Text = $"{(int)_fontSize}pt";
            }
            AplicarFormatacaoTexto();
        }

        private void DefinirAlinhamento(string alignment)
        {
            if (WindowType == "notepad" && _richLines.Count > 0)
            {
                AplicarFormatacaoNaLinhaOuSelecao(_ => { }, newAlignment: alignment);
                return;
            }
            _alignment = alignment;
            AplicarFormatacaoTexto();
        }

        private void DefinirFamiliaFonte(string familyName)
        {
            if (WindowType == "notepad" && _richLines.Count > 0)
            {
                AplicarFormatacaoNaLinhaOuSelecao(sp => sp.FontFamilyName = familyName);
                return;
            }
            _fontFamilyName = familyName;
            AplicarFormatacaoTexto();
        }

        private void RestaurarFormatacaoPadrao()
        {
            if (WindowType == "notepad" && _richLines.Count > 0)
            {
                AplicarFormatacaoNaLinhaOuSelecao(sp =>
                {
                    sp.IsBold = false;
                    sp.IsItalic = false;
                    sp.FontSize = 13;
                    sp.FontFamilyName = "default";
                }, newAlignment: "left");
                AtualizarCores();
                return;
            }
            _isBold = false;
            _isItalic = false;
            _fontSize = 13;
            _alignment = "left";
            _fontFamilyName = "default";
            if (_lblFontSize != null)
            {
                _lblFontSize.Text = "13pt";
            }
            AplicarFormatacaoTexto();
            AtualizarCores();
        }

        private static FontFamily ObterFontFamily(string familyName) => familyName switch
        {
            "mono" => new FontFamily("Consolas, Courier New, monospace"),
            "serif" => new FontFamily("Georgia, Times New Roman, serif"),
            _ => FontFamily.Default
        };

        private void AplicarFormatacaoTexto()
        {
            var weight = _isBold ? FontWeight.Bold : FontWeight.Normal;
            var style = _isItalic ? FontStyle.Italic : FontStyle.Normal;
            var align = _alignment == "center"
                ? TextAlignment.Center
                : _alignment == "right"
                    ? TextAlignment.Right
                    : TextAlignment.Left;
            var family = ObterFontFamily(_fontFamilyName);

            if (_noteTextBox != null)
            {
                _noteTextBox.FontWeight = weight;
                _noteTextBox.FontStyle = style;
                _noteTextBox.FontSize = _fontSize;
                _noteTextBox.TextAlignment = align;
                _noteTextBox.FontFamily = family;
            }
        }

        public TextFormatData ExportTextFormat() => new()
        {
            IsBold = _isBold,
            IsItalic = _isItalic,
            FontSize = _fontSize,
            Alignment = _alignment,
            FontFamilyName = _fontFamilyName
        };

        private void OnWindowKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                if (e.Key == Key.S)
                {
                    e.Handled = true;
                    SalvarJanela();
                }
                else if (e.Key == Key.B)
                {
                    e.Handled = true;
                    AlternarNegrito();
                }
                else if (e.Key == Key.I)
                {
                    e.Handled = true;
                    AlternarItalico();
                }
                else if (e.Key == Key.OemPlus || e.Key == Key.Add)
                {
                    e.Handled = true;
                    AlterarTamanhoFonte(1);
                }
                else if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
                {
                    e.Handled = true;
                    AlterarTamanhoFonte(-1);
                }
            }
        }
    }
}
