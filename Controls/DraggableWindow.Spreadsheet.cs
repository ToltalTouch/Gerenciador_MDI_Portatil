using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AvaloniaMdiApp
{
    public partial class DraggableWindow
    {
        // Campos do Widget de Planilha ("spreadsheet")
        private Border? _sheetToolbar;
        private Border? _sheetFormulaBar;
        private Border? _sheetCondPanel;
        private ScrollViewer? _sheetScrollViewer;
        private Border? _sheetHibernatedBanner;
        private TextBlock? _lblSheetHibernatedTitle;
        private TextBlock? _lblSheetHibernatedInfo;
        private Button? _btnWakeSheet;
        private TextBlock? _lblActiveCellRef;
        private TextBox? _txtFormulaBar;
        private Button? _btnFormulasMenu;
        private Button? _btnCondFormatMenu;
        private Button? _btnSheetAddRow;
        private Button? _btnSheetAddCol;
        private Grid? _sheetGrid;
        private StackPanel? _condRulesListPanel;

        private int _sheetRows = 12;
        private int _sheetCols = 6;
        private string _activeCellKey = "A1";
        private bool _updatingFormulaBar;
        private bool _isSheetHibernated;

        public bool IsSheetHibernated => _isSheetHibernated;

        private readonly Dictionary<string, string> _sheetRawValues = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _sheetComputedDisplay = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, double?> _sheetComputedNumeric = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TextBox> _sheetCellBoxes = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Border> _sheetCellBorders = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<Border> _sheetHeaderBorders = new();
        private readonly List<TextBlock> _sheetHeaderLabels = new();
        private readonly List<ConditionalFormatRuleData> _conditionalRules = new();

        private Control CriarConteudoPlanilha(
            int initialRows,
            int initialCols,
            List<SpreadsheetCellData>? initialCells,
            List<ConditionalFormatRuleData>? initialRules)
        {
            _sheetRows = Math.Clamp(initialRows > 0 ? initialRows : 12, 4, 60);
            _sheetCols = Math.Clamp(initialCols > 0 ? initialCols : 6, 3, 26);

            _sheetRawValues.Clear();
            if (initialCells != null)
            {
                foreach (var c in initialCells)
                {
                    if (!string.IsNullOrWhiteSpace(c.CellKey) && !string.IsNullOrEmpty(c.RawValue))
                    {
                        _sheetRawValues[c.CellKey.Trim().ToUpperInvariant()] = c.RawValue;
                    }
                }
            }

            _conditionalRules.Clear();
            if (initialRules != null)
            {
                _conditionalRules.AddRange(initialRules);
            }

            var rootDock = new DockPanel();

            // 1. Barra de ferramentas superior (Fórmulas, Formatação Condicional, +Linha, +Coluna)
            _sheetToolbar = new Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(8, 5)
            };

            var toolsStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center
            };

            _btnFormulasMenu = new Button
            {
                Content = "∑ Fórmulas ▾",
                Height = 26,
                Padding = new Thickness(9, 0),
                CornerRadius = new CornerRadius(5),
                Background = ThemePalette.Accent,
                Foreground = Brushes.White,
                FontSize = 11.5,
                FontWeight = FontWeight.SemiBold,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(_btnFormulasMenu, "Inserir função de cálculo estilo Excel na célula selecionada");
            _btnFormulasMenu.Flyout = CriarMenuFormulasExcel();

            _btnCondFormatMenu = new Button
            {
                Content = "🎨 Formatação Condicional ▾",
                Height = 26,
                Padding = new Thickness(9, 0),
                CornerRadius = new CornerRadius(5),
                BorderThickness = new Thickness(1),
                FontSize = 11.5,
                FontWeight = FontWeight.SemiBold,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(_btnCondFormatMenu, "Realçar células com regras condicionais estilo Microsoft Excel");
            _btnCondFormatMenu.Flyout = CriarMenuFormatacaoCondicional();

            _btnSheetAddRow = new Button
            {
                Content = "+ Linha",
                Height = 26,
                Padding = new Thickness(8, 0),
                CornerRadius = new CornerRadius(5),
                BorderThickness = new Thickness(1),
                FontSize = 11,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _btnSheetAddRow.Click += (s, e) =>
            {
                if (_sheetRows < 60)
                {
                    _sheetRows++;
                    ReconstruirGradePlanilha();
                }
            };

            _btnSheetAddCol = new Button
            {
                Content = "+ Coluna",
                Height = 26,
                Padding = new Thickness(8, 0),
                CornerRadius = new CornerRadius(5),
                BorderThickness = new Thickness(1),
                FontSize = 11,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _btnSheetAddCol.Click += (s, e) =>
            {
                if (_sheetCols < 26)
                {
                    _sheetCols++;
                    ReconstruirGradePlanilha();
                }
            };

            toolsStack.Children.Add(_btnFormulasMenu);
            toolsStack.Children.Add(_btnCondFormatMenu);
            toolsStack.Children.Add(_btnSheetAddRow);
            toolsStack.Children.Add(_btnSheetAddCol);

            _sheetToolbar.Child = toolsStack;
            DockPanel.SetDock(_sheetToolbar, Dock.Top);
            rootDock.Children.Add(_sheetToolbar);

            // 2. Barra de Fórmula estilo Excel ([A1] | fx | [=SOMA(A1:A5)])
            _sheetFormulaBar = new Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(8, 4)
            };

            var formulaGrid = new Grid();
            formulaGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(46)));
            formulaGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            formulaGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            _lblActiveCellRef = new TextBlock
            {
                Text = "A1",
                FontSize = 12,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var lblFx = new TextBlock
            {
                Text = "fx",
                FontSize = 12,
                FontStyle = FontStyle.Italic,
                FontWeight = FontWeight.Bold,
                Foreground = ThemePalette.AccentLight,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 8, 0)
            };

            _txtFormulaBar = new TextBox
            {
                Watermark = "Digite um valor ou fórmula (ex: =SOMA(A1:A5), =MEDIA(B1:B4), =SE(A1>10;\"Sim\";\"Não\"))...",
                Padding = new Thickness(8, 3),
                MinHeight = 25,
                Height = 25,
                FontSize = 12,
                CornerRadius = new CornerRadius(4)
            };

            _txtFormulaBar.TextChanged += (s, e) =>
            {
                if (_updatingFormulaBar) return;
                string val = _txtFormulaBar.Text ?? string.Empty;
                DefinirValorBrutoCelula(_activeCellKey, val);
                if (_sheetCellBoxes.TryGetValue(_activeCellKey, out var cellBox) && cellBox.IsFocused)
                {
                    cellBox.Text = val;
                }
            };

            _txtFormulaBar.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    RecalcularPlanilhaCompleta();
                }
            };

            Grid.SetColumn(_lblActiveCellRef, 0);
            Grid.SetColumn(lblFx, 1);
            Grid.SetColumn(_txtFormulaBar, 2);
            formulaGrid.Children.Add(_lblActiveCellRef);
            formulaGrid.Children.Add(lblFx);
            formulaGrid.Children.Add(_txtFormulaBar);

            _sheetFormulaBar.Child = formulaGrid;
            DockPanel.SetDock(_sheetFormulaBar, Dock.Top);
            rootDock.Children.Add(_sheetFormulaBar);

            // 3. Painel expansível de Regras de Formatação Condicional
            _sheetCondPanel = CriarPainelRegrasCondicionais();
            DockPanel.SetDock(_sheetCondPanel, Dock.Top);
            rootDock.Children.Add(_sheetCondPanel);

            // 4. Painel leve de Hibernação (exibido quando outra janela/planilha está ativa)
            _sheetHibernatedBanner = CriarPainelHibernacaoPlanilha();
            rootDock.Children.Add(_sheetHibernatedBanner);

            // 5. Grade de Células da Planilha
            _sheetGrid = new Grid();
            _sheetScrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _sheetGrid
            };

            rootDock.Children.Add(_sheetScrollViewer);

            ReconstruirGradePlanilha();
            return rootDock;
        }

        private Border CriarPainelHibernacaoPlanilha()
        {
            var banner = new Border
            {
                IsVisible = false,
                Padding = new Thickness(20),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            var stack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _lblSheetHibernatedTitle = new TextBlock
            {
                Text = "💤 Planilha Hibernada (Economia de Recursos)",
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            _lblSheetHibernatedInfo = new TextBlock
            {
                Text = "Grade visual descarregada da memória enquanto outra janela está em uso.",
                FontSize = 11.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                LineHeight = 18
            };

            _btnWakeSheet = new Button
            {
                Content = "⚡ Ativar Planilha Agora",
                Height = 28,
                Padding = new Thickness(14, 0),
                Margin = new Thickness(0, 4, 0, 0),
                CornerRadius = new CornerRadius(5),
                Background = ThemePalette.Accent,
                Foreground = Brushes.White,
                FontSize = 11.5,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _btnWakeSheet.Click += (s, e) =>
            {
                ZIndexManager.BringToFront(this);
            };

            stack.Children.Add(_lblSheetHibernatedTitle);
            stack.Children.Add(_lblSheetHibernatedInfo);
            stack.Children.Add(_btnWakeSheet);

            banner.Child = stack;
            banner.PointerPressed += (s, e) =>
            {
                ZIndexManager.BringToFront(this);
            };

            return banner;
        }

        public void AtualizarHibernacaoPlanilha(bool hibernar)
        {
            if (WindowType != "spreadsheet" || _sheetGrid == null) return;
            if (_isSheetHibernated == hibernar) return;

            _isSheetHibernated = hibernar;

            if (hibernar)
            {
                // Descarrega todos os controles pesados da grade (TextBoxes, Borders, Cabeçalhos e caches)
                _sheetGrid.Children.Clear();
                _sheetGrid.RowDefinitions.Clear();
                _sheetGrid.ColumnDefinitions.Clear();
                _sheetCellBoxes.Clear();
                _sheetCellBorders.Clear();
                _sheetHeaderBorders.Clear();
                _sheetHeaderLabels.Clear();
                _sheetComputedDisplay.Clear();
                _sheetComputedNumeric.Clear();

                if (_sheetToolbar != null) _sheetToolbar.IsVisible = false;
                if (_sheetFormulaBar != null) _sheetFormulaBar.IsVisible = false;
                if (_sheetCondPanel != null) _sheetCondPanel.IsVisible = false;
                if (_sheetScrollViewer != null) _sheetScrollViewer.IsVisible = false;

                if (_lblSheetHibernatedInfo != null)
                {
                    int preenchidas = _sheetRawValues.Count;
                    int formulas = _sheetRawValues.Values.Count(v => v.TrimStart().StartsWith('='));
                    _lblSheetHibernatedInfo.Text =
                        $"Dados preservados em memória leve: {preenchidas} célula(s) preenchida(s), {formulas} fórmula(s) e {_conditionalRules.Count} regra(s).\n" +
                        $"Grade visual ({_sheetRows} × {_sheetCols}) hibernada — clique nesta janela para reativar.";
                }

                if (_sheetHibernatedBanner != null)
                {
                    _sheetHibernatedBanner.IsVisible = true;
                }
            }
            else
            {
                if (_sheetHibernatedBanner != null) _sheetHibernatedBanner.IsVisible = false;
                if (_sheetToolbar != null) _sheetToolbar.IsVisible = true;
                if (_sheetFormulaBar != null) _sheetFormulaBar.IsVisible = true;
                if (_sheetScrollViewer != null) _sheetScrollViewer.IsVisible = true;

                ReconstruirGradePlanilha();
            }

            AtualizarCoresPlanilha();
            _taskBar.AtualizarEstadoAtivo(this, _isActive);
        }

        private void LiberarRecursosPlanilha()
        {
            _sheetGrid?.Children.Clear();
            _sheetGrid?.RowDefinitions.Clear();
            _sheetGrid?.ColumnDefinitions.Clear();
            _sheetCellBoxes.Clear();
            _sheetCellBorders.Clear();
            _sheetHeaderBorders.Clear();
            _sheetHeaderLabels.Clear();
            _sheetComputedDisplay.Clear();
            _sheetComputedNumeric.Clear();
            _sheetRawValues.Clear();
            _conditionalRules.Clear();
        }

        private MenuFlyout CriarMenuFormulasExcel()
        {
            var flyout = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedLeft };

            (string Label, string Template)[] formulas =
            {
                ("➕ SOMA / SUM — Somar intervalo", "=SOMA(A1:A5)"),
                ("📊 MEDIA / AVERAGE — Média aritmética", "=MEDIA(A1:A5)"),
                ("⬇ MINIMO / MIN — Menor valor", "=MINIMO(A1:A5)"),
                ("⬆ MAXIMO / MAX — Maior valor", "=MAXIMO(A1:A5)"),
                ("🔢 CONT.NUM / COUNT — Contar números", "=CONT.NUM(A1:A5)"),
                ("📋 CONT.VALORES / COUNTA — Contar preenchidos", "=CONT.VALORES(A1:A5)"),
                ("❓ SE / IF — Condição lógica", "=SE(A1>=60; \"Aprovado\"; \"Reprovado\")"),
                ("🔍 CONT.SE / COUNTIF — Contar com condição", "=CONT.SE(A1:A5; \">10\")"),
                ("💰 SOMASE / SUMIF — Somar com condição", "=SOMASE(A1:A5; \">10\")"),
                ("📐 MED / MEDIAN — Mediana", "=MED(A1:A5)"),
                ("🎯 ARRED / ROUND — Arredondar casas", "=ARRED(A1; 2)"),
                ("⚡ POTENCIA / POWER — Elevar potência", "=POTENCIA(A1; 2)"),
                ("√ RAIZ / SQRT — Raiz quadrada", "=RAIZ(A1)"),
                ("📏 ABS — Valor absoluto", "=ABS(A1)")
            };

            foreach (var f in formulas)
            {
                var item = new MenuItem { Header = $"{f.Label}   [{f.Template}]" };
                string tmpl = f.Template;
                item.Click += (s, e) => InserirFormulaNaCelulaAtiva(tmpl);
                flyout.Items.Add(item);
            }

            return flyout;
        }

        private MenuFlyout CriarMenuFormatacaoCondicional()
        {
            var flyout = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedLeft };

            var itemPainel = new MenuItem { Header = "⚙ Gerenciar / Criar Regra Condicional..." };
            itemPainel.Click += (s, e) =>
            {
                if (_sheetCondPanel != null)
                {
                    _sheetCondPanel.IsVisible = !_sheetCondPanel.IsVisible;
                    AtualizarListaRegrasCondicionaisUI();
                }
            };
            flyout.Items.Add(itemPainel);
            flyout.Items.Add(new Separator());

            var menuRealcar = new MenuItem { Header = "✨ Realçar Regras das Células (Rápido)" };

            var rPositivos = new MenuItem { Header = "🟢 É Maior do que 0 (Preenchimento Verde Claro)" };
            rPositivos.Click += (s, e) => AdicionarRegraCondicional("*", ">", "0", "", "green");

            var rNegativos = new MenuItem { Header = "🔴 É Menor do que 0 (Preenchimento Vermelho Claro)" };
            rNegativos.Click += (s, e) => AdicionarRegraCondicional("*", "<", "0", "", "red");

            var rZero = new MenuItem { Header = "🟡 É Igual a 0 (Preenchimento Amarelo Claro)" };
            rZero.Click += (s, e) => AdicionarRegraCondicional("*", "=", "0", "", "yellow");

            var rCem = new MenuItem { Header = "🔵 É Maior ou Igual a 100 (Destaque Azul)" };
            rCem.Click += (s, e) => AdicionarRegraCondicional("*", ">=", "100", "", "blue");

            var rAprovado = new MenuItem { Header = "🟢 Texto que Contém \"Aprovado\" (Verde)" };
            rAprovado.Click += (s, e) => AdicionarRegraCondicional("*", "contains", "Aprovado", "", "green");

            var rReprovado = new MenuItem { Header = "🔴 Texto que Contém \"Reprovado\" (Vermelho)" };
            rReprovado.Click += (s, e) => AdicionarRegraCondicional("*", "contains", "Reprovado", "", "red");

            menuRealcar.Items.Add(rPositivos);
            menuRealcar.Items.Add(rNegativos);
            menuRealcar.Items.Add(rZero);
            menuRealcar.Items.Add(rCem);
            menuRealcar.Items.Add(new Separator());
            menuRealcar.Items.Add(rAprovado);
            menuRealcar.Items.Add(rReprovado);

            flyout.Items.Add(menuRealcar);
            flyout.Items.Add(new Separator());

            var itemLimpar = new MenuItem { Header = "🗑 Limpar Todas as Regras Condicionais" };
            itemLimpar.Click += (s, e) =>
            {
                _conditionalRules.Clear();
                AtualizarListaRegrasCondicionaisUI();
                AplicarFormatacaoCondicionalNaGrade();
            };
            flyout.Items.Add(itemLimpar);

            return flyout;
        }

        private Border CriarPainelRegrasCondicionais()
        {
            var panelBorder = new Border
            {
                IsVisible = false,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(8, 6)
            };

            var mainStack = new StackPanel { Spacing = 6 };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var lblTitle = new TextBlock
            {
                Text = "🎨 Regras de Formatação Condicional (Estilo Excel)",
                FontSize = 11.5,
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };

            var btnClosePanel = new Button
            {
                Content = "✕",
                Width = 22,
                Height = 20,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(4),
                Background = Brushes.Transparent,
                FontSize = 10
            };
            btnClosePanel.Click += (s, e) => panelBorder.IsVisible = false;

            Grid.SetColumn(lblTitle, 0);
            Grid.SetColumn(btnClosePanel, 1);
            headerGrid.Children.Add(lblTitle);
            headerGrid.Children.Add(btnClosePanel);

            var formStack = new WrapPanel
            {
                Orientation = Orientation.Horizontal
            };

            var txtRange = new TextBox
            {
                Text = "*",
                Watermark = "Ex: * ou A1:C10",
                Width = 74,
                Height = 25,
                MinHeight = 25,
                Padding = new Thickness(6, 2),
                Margin = new Thickness(0, 2, 4, 2),
                FontSize = 11,
                CornerRadius = new CornerRadius(4)
            };
            ToolTip.SetTip(txtRange, "Intervalo de células (ex: A1:D10 ou * para toda a planilha)");

            var cmbOp = new ComboBox
            {
                Width = 128,
                Height = 25,
                MinHeight = 25,
                FontSize = 11,
                Margin = new Thickness(0, 2, 4, 2),
                CornerRadius = new CornerRadius(4),
                SelectedIndex = 0
            };
            (string Key, string Label)[] ops =
            {
                (">", "É Maior que (>)"),
                ("<", "É Menor que (<)"),
                (">=", "Maior ou Igual (>=)"),
                ("<=", "Menor ou Igual (<=)"),
                ("=", "É Igual a (=)"),
                ("!=", "Diferente de (!=)"),
                ("between", "Está Entre"),
                ("contains", "Texto Contém")
            };
            foreach (var op in ops) cmbOp.Items.Add(op.Label);

            var txtVal1 = new TextBox
            {
                Text = "0",
                Watermark = "Valor 1",
                Width = 68,
                Height = 25,
                MinHeight = 25,
                Padding = new Thickness(6, 2),
                Margin = new Thickness(0, 2, 4, 2),
                FontSize = 11,
                CornerRadius = new CornerRadius(4)
            };

            var txtVal2 = new TextBox
            {
                Text = "100",
                Watermark = "Valor 2",
                IsVisible = false,
                Width = 68,
                Height = 25,
                MinHeight = 25,
                Padding = new Thickness(6, 2),
                Margin = new Thickness(0, 2, 4, 2),
                FontSize = 11,
                CornerRadius = new CornerRadius(4)
            };

            cmbOp.SelectionChanged += (s, e) =>
            {
                int idx = Math.Clamp(cmbOp.SelectedIndex, 0, ops.Length - 1);
                txtVal2.IsVisible = ops[idx].Key == "between";
            };

            var cmbStyle = new ComboBox
            {
                Width = 148,
                Height = 25,
                MinHeight = 25,
                FontSize = 11,
                Margin = new Thickness(0, 2, 4, 2),
                CornerRadius = new CornerRadius(4),
                SelectedIndex = 0
            };
            (string Key, string Label)[] styles =
            {
                ("green", "🟢 Verde Claro (Excel)"),
                ("red", "🔴 Vermelho Claro (Excel)"),
                ("yellow", "🟡 Amarelo Claro (Excel)"),
                ("blue", "🔵 Azul Claro (Excel)"),
                ("purple", "🟣 Roxo Claro")
            };
            foreach (var st in styles) cmbStyle.Items.Add(st.Label);

            var btnAddRule = new Button
            {
                Content = "+ Regra",
                Height = 25,
                Padding = new Thickness(10, 0),
                Margin = new Thickness(0, 2, 0, 2),
                CornerRadius = new CornerRadius(4),
                Background = ThemePalette.Accent,
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            btnAddRule.Click += (s, e) =>
            {
                int opIdx = Math.Clamp(cmbOp.SelectedIndex, 0, ops.Length - 1);
                int stIdx = Math.Clamp(cmbStyle.SelectedIndex, 0, styles.Length - 1);
                string range = string.IsNullOrWhiteSpace(txtRange.Text) ? "*" : txtRange.Text.Trim().ToUpperInvariant();
                AdicionarRegraCondicional(range, ops[opIdx].Key, txtVal1.Text ?? "0", txtVal2.Text ?? "", styles[stIdx].Key);
            };

            formStack.Children.Add(txtRange);
            formStack.Children.Add(cmbOp);
            formStack.Children.Add(txtVal1);
            formStack.Children.Add(txtVal2);
            formStack.Children.Add(cmbStyle);
            formStack.Children.Add(btnAddRule);

            _condRulesListPanel = new StackPanel { Spacing = 3 };

            mainStack.Children.Add(headerGrid);
            mainStack.Children.Add(formStack);
            mainStack.Children.Add(_condRulesListPanel);

            panelBorder.Child = mainStack;
            return panelBorder;
        }

        private void AdicionarRegraCondicional(string range, string op, string val1, string val2, string styleKey)
        {
            _conditionalRules.Add(new ConditionalFormatRuleData
            {
                Range = string.IsNullOrWhiteSpace(range) ? "*" : range.Trim().ToUpperInvariant(),
                Operator = op,
                Value1 = val1.Trim(),
                Value2 = val2.Trim(),
                StyleKey = styleKey
            });

            AtualizarListaRegrasCondicionaisUI();
            AplicarFormatacaoCondicionalNaGrade();
        }

        private void AtualizarListaRegrasCondicionaisUI()
        {
            if (_condRulesListPanel == null) return;
            _condRulesListPanel.Children.Clear();

            foreach (var rule in _conditionalRules.ToList())
            {
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

                string desc = rule.Operator == "between"
                    ? $"[{rule.Range}] Entre {rule.Value1} e {rule.Value2} → Estilo: {rule.StyleKey.ToUpperInvariant()}"
                    : $"[{rule.Range}] {rule.Operator} {rule.Value1} → Estilo: {rule.StyleKey.ToUpperInvariant()}";

                var lbl = new TextBlock
                {
                    Text = desc,
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = _isDarkMode ? ThemePalette.DarkTextSecondary : ThemePalette.LightTextSecondary
                };

                var btnDel = new Button
                {
                    Content = "✕",
                    Width = 20,
                    Height = 18,
                    Padding = new Thickness(0),
                    Background = Brushes.Transparent,
                    FontSize = 9.5
                };
                btnDel.Click += (s, e) =>
                {
                    _conditionalRules.Remove(rule);
                    AtualizarListaRegrasCondicionaisUI();
                    AplicarFormatacaoCondicionalNaGrade();
                };

                Grid.SetColumn(lbl, 0);
                Grid.SetColumn(btnDel, 1);
                row.Children.Add(lbl);
                row.Children.Add(btnDel);
                _condRulesListPanel.Children.Add(row);
            }
        }

        private void InserirFormulaNaCelulaAtiva(string formulaTemplate)
        {
            if (string.IsNullOrWhiteSpace(_activeCellKey))
            {
                _activeCellKey = "A1";
            }

            DefinirValorBrutoCelula(_activeCellKey, formulaTemplate);
            if (_txtFormulaBar != null)
            {
                _updatingFormulaBar = true;
                _txtFormulaBar.Text = formulaTemplate;
                _updatingFormulaBar = false;
                _txtFormulaBar.Focus();
            }

            RecalcularPlanilhaCompleta();
        }

        private void DefinirValorBrutoCelula(string cellKey, string rawValue)
        {
            string key = cellKey.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(rawValue))
            {
                _sheetRawValues.Remove(key);
            }
            else
            {
                _sheetRawValues[key] = rawValue;
            }
        }

        private void ReconstruirGradePlanilha()
        {
            if (_sheetGrid == null) return;

            _sheetGrid.Children.Clear();
            _sheetGrid.RowDefinitions.Clear();
            _sheetGrid.ColumnDefinitions.Clear();
            _sheetCellBoxes.Clear();
            _sheetCellBorders.Clear();
            _sheetHeaderBorders.Clear();
            _sheetHeaderLabels.Clear();

            // Coluna 0: cabeçalho de número da linha
            _sheetGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(38)));
            for (int c = 0; c < _sheetCols; c++)
            {
                _sheetGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(96)));
            }

            // Linha 0: cabeçalho de letras das colunas
            _sheetGrid.RowDefinitions.Add(new RowDefinition(new GridLength(26)));
            for (int r = 0; r < _sheetRows; r++)
            {
                _sheetGrid.RowDefinitions.Add(new RowDefinition(new GridLength(27)));
            }

            // Canto superior esquerdo vazio
            var cornerHeader = CriarCelulaCabecalho("");
            Grid.SetRow(cornerHeader, 0);
            Grid.SetColumn(cornerHeader, 0);
            _sheetGrid.Children.Add(cornerHeader);

            // Cabeçalhos A, B, C...
            for (int c = 0; c < _sheetCols; c++)
            {
                string colLetter = ObterLetraColuna(c);
                var colHeader = CriarCelulaCabecalho(colLetter);
                Grid.SetRow(colHeader, 0);
                Grid.SetColumn(colHeader, c + 1);
                _sheetGrid.Children.Add(colHeader);
            }

            // Linhas e células
            for (int r = 0; r < _sheetRows; r++)
            {
                var rowHeader = CriarCelulaCabecalho((r + 1).ToString());
                Grid.SetRow(rowHeader, r + 1);
                Grid.SetColumn(rowHeader, 0);
                _sheetGrid.Children.Add(rowHeader);

                for (int c = 0; c < _sheetCols; c++)
                {
                    string cellKey = $"{ObterLetraColuna(c)}{r + 1}";

                    var txtCell = new TextBox
                    {
                        BorderThickness = new Thickness(0),
                        Background = Brushes.Transparent,
                        Padding = new Thickness(6, 3),
                        MinHeight = 24,
                        Height = 25,
                        FontSize = 12,
                        VerticalContentAlignment = VerticalAlignment.Center
                    };

                    var cellBorder = new Border
                    {
                        BorderThickness = new Thickness(0, 0, 1, 1),
                        Child = txtCell
                    };

                    _sheetCellBoxes[cellKey] = txtCell;
                    _sheetCellBorders[cellKey] = cellBorder;

                    txtCell.GotFocus += (s, e) =>
                    {
                        _activeCellKey = cellKey;
                        if (_lblActiveCellRef != null)
                        {
                            _lblActiveCellRef.Text = cellKey;
                        }

                        string raw = _sheetRawValues.TryGetValue(cellKey, out var rv) ? rv : string.Empty;
                        txtCell.Text = raw;

                        if (_txtFormulaBar != null)
                        {
                            _updatingFormulaBar = true;
                            _txtFormulaBar.Text = raw;
                            _updatingFormulaBar = false;
                        }
                    };

                    txtCell.TextChanged += (s, e) =>
                    {
                        if (!txtCell.IsFocused) return;
                        string raw = txtCell.Text ?? string.Empty;
                        DefinirValorBrutoCelula(cellKey, raw);

                        if (_txtFormulaBar != null && _activeCellKey == cellKey)
                        {
                            _updatingFormulaBar = true;
                            _txtFormulaBar.Text = raw;
                            _updatingFormulaBar = false;
                        }
                    };

                    txtCell.LostFocus += (s, e) =>
                    {
                        RecalcularPlanilhaCompleta();
                    };

                    txtCell.KeyDown += (s, e) =>
                    {
                        if (e.Key == Key.Enter)
                        {
                            e.Handled = true;
                            RecalcularPlanilhaCompleta();
                            // Move para a linha de baixo, igual ao Excel
                            string nextKey = $"{ObterLetraColuna(c)}{Math.Min(_sheetRows, r + 2)}";
                            if (_sheetCellBoxes.TryGetValue(nextKey, out var nextBox))
                            {
                                nextBox.Focus();
                            }
                        }
                    };

                    Grid.SetRow(cellBorder, r + 1);
                    Grid.SetColumn(cellBorder, c + 1);
                    _sheetGrid.Children.Add(cellBorder);
                }
            }

            RecalcularPlanilhaCompleta();
            AtualizarCoresPlanilha();
        }

        private Border CriarCelulaCabecalho(string texto)
        {
            var lbl = new TextBlock
            {
                Text = texto,
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var border = new Border
            {
                BorderThickness = new Thickness(0, 0, 1, 1),
                Child = lbl
            };
            _sheetHeaderBorders.Add(border);
            _sheetHeaderLabels.Add(lbl);
            return border;
        }

        private static string ObterLetraColuna(int index)
        {
            return ((char)('A' + Math.Clamp(index, 0, 25))).ToString();
        }

        // ==================== MOTOR DE CÁLCULO DE FÓRMULAS EXCEL ====================

        public void RecalcularPlanilhaCompleta()
        {
            if (_sheetCellBoxes.Count == 0) return;

            _sheetComputedDisplay.Clear();
            _sheetComputedNumeric.Clear();
            var visitando = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in _sheetCellBoxes.Keys)
            {
                AvaliarCelula(key, visitando);
            }

            foreach (var kvp in _sheetCellBoxes)
            {
                string key = kvp.Key;
                var box = kvp.Value;
                if (!box.IsFocused)
                {
                    box.Text = _sheetComputedDisplay.TryGetValue(key, out var disp) ? disp : string.Empty;
                }
            }

            AplicarFormatacaoCondicionalNaGrade();
        }

        private (string Display, double? Numeric) AvaliarCelula(string cellKey, HashSet<string> visitando)
        {
            string key = cellKey.Trim().ToUpperInvariant();
            if (_sheetComputedDisplay.TryGetValue(key, out var cachedDisp))
            {
                _sheetComputedNumeric.TryGetValue(key, out var cachedNum);
                return (cachedDisp, cachedNum);
            }

            if (!_sheetRawValues.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                _sheetComputedDisplay[key] = string.Empty;
                _sheetComputedNumeric[key] = null;
                return (string.Empty, null);
            }

            string trimmed = raw.Trim();
            if (!trimmed.StartsWith('='))
            {
                if (TentarConverterNumero(trimmed, out double num))
                {
                    _sheetComputedDisplay[key] = trimmed;
                    _sheetComputedNumeric[key] = num;
                    return (trimmed, num);
                }

                _sheetComputedDisplay[key] = trimmed;
                _sheetComputedNumeric[key] = null;
                return (trimmed, null);
            }

            if (visitando.Contains(key))
            {
                _sheetComputedDisplay[key] = "#REF!";
                _sheetComputedNumeric[key] = null;
                return ("#REF!", null);
            }

            visitando.Add(key);
            try
            {
                string expr = trimmed[1..].Trim();
                var res = AvaliarExpressaoFormula(expr, visitando);
                _sheetComputedDisplay[key] = res.Display;
                _sheetComputedNumeric[key] = res.Numeric;
                return res;
            }
            catch
            {
                _sheetComputedDisplay[key] = "#ERRO!";
                _sheetComputedNumeric[key] = null;
                return ("#ERRO!", null);
            }
            finally
            {
                visitando.Remove(key);
            }
        }

        private (string Display, double? Numeric) AvaliarExpressaoFormula(string expr, HashSet<string> visitando)
        {
            // Verifica se é chamada de função de topo, ex: SOMA(...), SE(...), MEDIA(...)
            var funcMatch = Regex.Match(expr, @"^([A-Za-zÀ-ÿ0-9_.]+)\s*\((.*)\)$");
            if (funcMatch.Success && ParantesesBalanceadosInternos(funcMatch.Groups[2].Value))
            {
                string funcName = funcMatch.Groups[1].Value.ToUpperInvariant();
                string argsRaw = funcMatch.Groups[2].Value;
                var args = DividirArgumentosFormula(argsRaw);
                return ExecutarFuncaoExcel(funcName, args, visitando);
            }

            // Substitui sub-funções aninhadas e referências de células na expressão matemática
            string resolvida = Regex.Replace(expr, @"\b([A-Z])([0-9]{1,2})\b", m =>
            {
                var val = AvaliarCelula(m.Value, visitando);
                return (val.Numeric ?? 0.0).ToString(CultureInfo.InvariantCulture);
            }, RegexOptions.IgnoreCase);

            double resultado = AvaliarAritmetica(resolvida);
            if (double.IsNaN(resultado) || double.IsInfinity(resultado))
            {
                return ("#DIV/0!", null);
            }

            string formatado = Math.Round(resultado, 4).ToString(CultureInfo.CurrentCulture);
            return (formatado, resultado);
        }

        private (string Display, double? Numeric) ExecutarFuncaoExcel(string funcName, List<string> args, HashSet<string> visitando)
        {
            switch (funcName)
            {
                case "SOMA":
                case "SUM":
                {
                    double soma = ObterValoresNumericosDeArgumentos(args, visitando).Sum();
                    return (FormatarNumero(soma), soma);
                }
                case "MEDIA":
                case "MÉDIA":
                case "AVERAGE":
                {
                    var vals = ObterValoresNumericosDeArgumentos(args, visitando);
                    if (vals.Count == 0) return ("0", 0);
                    double media = vals.Average();
                    return (FormatarNumero(media), media);
                }
                case "MINIMO":
                case "MÍNIMO":
                case "MIN":
                {
                    var vals = ObterValoresNumericosDeArgumentos(args, visitando);
                    double min = vals.Count > 0 ? vals.Min() : 0;
                    return (FormatarNumero(min), min);
                }
                case "MAXIMO":
                case "MÁXIMO":
                case "MAX":
                {
                    var vals = ObterValoresNumericosDeArgumentos(args, visitando);
                    double max = vals.Count > 0 ? vals.Max() : 0;
                    return (FormatarNumero(max), max);
                }
                case "CONT.NUM":
                case "COUNT":
                {
                    int count = ObterValoresNumericosDeArgumentos(args, visitando).Count;
                    return (count.ToString(), count);
                }
                case "CONT.VALORES":
                case "COUNTA":
                {
                    int count = ObterTodosValoresDeArgumentos(args, visitando)
                        .Count(v => !string.IsNullOrWhiteSpace(v.Display));
                    return (count.ToString(), count);
                }
                case "MED":
                case "MEDIAN":
                {
                    var vals = ObterValoresNumericosDeArgumentos(args, visitando).OrderBy(x => x).ToList();
                    if (vals.Count == 0) return ("0", 0);
                    int mid = vals.Count / 2;
                    double med = vals.Count % 2 != 0 ? vals[mid] : (vals[mid - 1] + vals[mid]) / 2.0;
                    return (FormatarNumero(med), med);
                }
                case "ABS":
                {
                    var vals = ObterValoresNumericosDeArgumentos(args, visitando);
                    double v = vals.Count > 0 ? Math.Abs(vals[0]) : 0;
                    return (FormatarNumero(v), v);
                }
                case "RAIZ":
                case "SQRT":
                {
                    var vals = ObterValoresNumericosDeArgumentos(args, visitando);
                    if (vals.Count == 0 || vals[0] < 0) return ("#NUM!", null);
                    double v = Math.Sqrt(vals[0]);
                    return (FormatarNumero(v), v);
                }
                case "POTENCIA":
                case "POTÊNCIA":
                case "POWER":
                {
                    var vals = ObterValoresNumericosDeArgumentos(args, visitando);
                    if (vals.Count < 2) return ("#ERRO!", null);
                    double v = Math.Pow(vals[0], vals[1]);
                    return (FormatarNumero(v), v);
                }
                case "ARRED":
                case "ROUND":
                {
                    var vals = ObterValoresNumericosDeArgumentos(args, visitando);
                    if (vals.Count == 0) return ("0", 0);
                    int digits = vals.Count >= 2 ? Math.Clamp((int)vals[1], 0, 10) : 0;
                    double v = Math.Round(vals[0], digits);
                    return (FormatarNumero(v), v);
                }
                case "SE":
                case "IF":
                {
                    if (args.Count < 2) return ("#ERRO!", null);
                    bool cond = AvaliarCondicaoLogica(args[0], visitando);
                    string ramoEscolhido = cond ? args[1] : (args.Count >= 3 ? args[2] : "");
                    return AvaliarArgumentoEscalar(ramoEscolhido, visitando);
                }
                case "CONT.SE":
                case "COUNTIF":
                {
                    if (args.Count < 2) return ("0", 0);
                    var celulas = ObterTodosValoresDeArgumentos(new List<string> { args[0] }, visitando);
                    string criterio = args[1].Trim().Trim('"');
                    int count = celulas.Count(c => AtendeCriterioExcel(c.Display, c.Numeric, criterio));
                    return (count.ToString(), count);
                }
                case "SOMASE":
                case "SUMIF":
                {
                    if (args.Count < 2) return ("0", 0);
                    var celulas = ObterTodosValoresDeArgumentos(new List<string> { args[0] }, visitando);
                    string criterio = args[1].Trim().Trim('"');
                    double soma = celulas
                        .Where(c => c.Numeric.HasValue && AtendeCriterioExcel(c.Display, c.Numeric, criterio))
                        .Sum(c => c.Numeric!.Value);
                    return (FormatarNumero(soma), soma);
                }
                default:
                    return ("#NOME?", null);
            }
        }

        private (string Display, double? Numeric) AvaliarArgumentoEscalar(string arg, HashSet<string> visitando)
        {
            string trimmed = arg.Trim();
            if (trimmed.StartsWith('"') && trimmed.EndsWith('"') && trimmed.Length >= 2)
            {
                string texto = trimmed[1..^1];
                return (texto, null);
            }

            if (TentarConverterNumero(trimmed, out double num))
            {
                return (FormatarNumero(num), num);
            }

            if (Regex.IsMatch(trimmed, @"^[A-Za-z][0-9]{1,2}$"))
            {
                return AvaliarCelula(trimmed, visitando);
            }

            return AvaliarExpressaoFormula(trimmed, visitando);
        }

        private bool AvaliarCondicaoLogica(string condExpr, HashSet<string> visitando)
        {
            string[] ops = { ">=", "<=", "<>", "!=", "=", ">", "<" };
            foreach (var op in ops)
            {
                int idx = condExpr.IndexOf(op, StringComparison.Ordinal);
                if (idx > 0)
                {
                    string leftRaw = condExpr[..idx].Trim();
                    string rightRaw = condExpr[(idx + op.Length)..].Trim();

                    var left = AvaliarArgumentoEscalar(leftRaw, visitando);
                    var right = AvaliarArgumentoEscalar(rightRaw, visitando);

                    if (left.Numeric.HasValue && right.Numeric.HasValue)
                    {
                        double a = left.Numeric.Value;
                        double b = right.Numeric.Value;
                        return op switch
                        {
                            ">=" => a >= b,
                            "<=" => a <= b,
                            "<>" or "!=" => Math.Abs(a - b) > 1e-9,
                            "=" => Math.Abs(a - b) < 1e-9,
                            ">" => a > b,
                            "<" => a < b,
                            _ => false
                        };
                    }

                    int cmp = string.Compare(left.Display, right.Display, StringComparison.OrdinalIgnoreCase);
                    return op switch
                    {
                        "=" => cmp == 0,
                        "<>" or "!=" => cmp != 0,
                        ">" => cmp > 0,
                        "<" => cmp < 0,
                        ">=" => cmp >= 0,
                        "<=" => cmp <= 0,
                        _ => false
                    };
                }
            }
            return false;
        }

        private static bool AtendeCriterioExcel(string display, double? numeric, string criterio)
        {
            string c = criterio.Trim();
            string[] ops = { ">=", "<=", "<>", "!=", "=", ">", "<" };
            foreach (var op in ops)
            {
                if (c.StartsWith(op, StringComparison.Ordinal))
                {
                    string valStr = c[op.Length..].Trim();
                    if (numeric.HasValue && TentarConverterNumero(valStr, out double target))
                    {
                        double v = numeric.Value;
                        return op switch
                        {
                            ">=" => v >= target,
                            "<=" => v <= target,
                            "<>" or "!=" => Math.Abs(v - target) > 1e-9,
                            "=" => Math.Abs(v - target) < 1e-9,
                            ">" => v > target,
                            "<" => v < target,
                            _ => false
                        };
                    }
                    return string.Equals(display, valStr, StringComparison.OrdinalIgnoreCase);
                }
            }

            if (numeric.HasValue && TentarConverterNumero(c, out double eqNum))
            {
                return Math.Abs(numeric.Value - eqNum) < 1e-9;
            }

            return string.Equals(display, c, StringComparison.OrdinalIgnoreCase);
        }

        private List<double> ObterValoresNumericosDeArgumentos(List<string> args, HashSet<string> visitando)
        {
            var result = new List<double>();
            foreach (var item in ObterTodosValoresDeArgumentos(args, visitando))
            {
                if (item.Numeric.HasValue)
                {
                    result.Add(item.Numeric.Value);
                }
            }
            return result;
        }

        private List<(string Display, double? Numeric)> ObterTodosValoresDeArgumentos(List<string> args, HashSet<string> visitando)
        {
            var list = new List<(string Display, double? Numeric)>();
            foreach (var rawArg in args)
            {
                string arg = rawArg.Trim();
                if (arg.Contains(':'))
                {
                    foreach (var cellKey in ExpandirIntervaloCelulas(arg))
                    {
                        list.Add(AvaliarCelula(cellKey, visitando));
                    }
                }
                else if (Regex.IsMatch(arg, @"^[A-Za-z][0-9]{1,2}$"))
                {
                    list.Add(AvaliarCelula(arg, visitando));
                }
                else
                {
                    list.Add(AvaliarArgumentoEscalar(arg, visitando));
                }
            }
            return list;
        }

        private static IEnumerable<string> ExpandirIntervaloCelulas(string range)
        {
            var parts = range.Split(':');
            if (parts.Length != 2) yield break;

            if (!TentarDecomporCelula(parts[0], out int c1, out int r1) ||
                !TentarDecomporCelula(parts[1], out int c2, out int r2))
            {
                yield break;
            }

            int minC = Math.Min(c1, c2);
            int maxC = Math.Max(c1, c2);
            int minR = Math.Min(r1, r2);
            int maxR = Math.Max(r1, r2);

            for (int r = minR; r <= maxR; r++)
            {
                for (int c = minC; c <= maxC; c++)
                {
                    yield return $"{ObterLetraColuna(c)}{r}";
                }
            }
        }

        private static bool TentarDecomporCelula(string cellRef, out int colIndex, out int rowNumber)
        {
            colIndex = 0;
            rowNumber = 1;
            string s = cellRef.Trim().ToUpperInvariant();
            var m = Regex.Match(s, @"^([A-Z])([0-9]{1,2})$");
            if (!m.Success) return false;

            colIndex = m.Groups[1].Value[0] - 'A';
            return int.TryParse(m.Groups[2].Value, out rowNumber);
        }

        private static List<string> DividirArgumentosFormula(string input)
        {
            var result = new List<string>();
            int depth = 0;
            bool inQuotes = false;
            int lastStart = 0;

            for (int i = 0; i < input.Length; i++)
            {
                char ch = input[i];
                if (ch == '"') inQuotes = !inQuotes;
                else if (!inQuotes)
                {
                    if (ch == '(') depth++;
                    else if (ch == ')') depth--;
                    else if ((ch == ';' || ch == ',') && depth == 0)
                    {
                        result.Add(input[lastStart..i]);
                        lastStart = i + 1;
                    }
                }
            }

            if (lastStart <= input.Length)
            {
                string tail = input[lastStart..];
                if (!string.IsNullOrWhiteSpace(tail))
                {
                    result.Add(tail);
                }
            }

            return result;
        }

        private static bool ParantesesBalanceadosInternos(string s)
        {
            int depth = 0;
            foreach (char c in s)
            {
                if (c == '(') depth++;
                else if (c == ')')
                {
                    depth--;
                    if (depth < 0) return false;
                }
            }
            return depth == 0;
        }

        private static double AvaliarAritmetica(string expr)
        {
            int pos = 0;
            string s = expr.Replace(" ", "");

            double ParseExpression()
            {
                double x = ParseTerm();
                while (pos < s.Length)
                {
                    if (s[pos] == '+') { pos++; x += ParseTerm(); }
                    else if (s[pos] == '-') { pos++; x -= ParseTerm(); }
                    else break;
                }
                return x;
            }

            double ParseTerm()
            {
                double x = ParsePower();
                while (pos < s.Length)
                {
                    if (s[pos] == '*') { pos++; x *= ParsePower(); }
                    else if (s[pos] == '/') { pos++; x /= ParsePower(); }
                    else break;
                }
                return x;
            }

            double ParsePower()
            {
                double x = ParseFactor();
                if (pos < s.Length && s[pos] == '^')
                {
                    pos++;
                    x = Math.Pow(x, ParsePower());
                }
                return x;
            }

            double ParseFactor()
            {
                if (pos < s.Length && s[pos] == '+') { pos++; return ParseFactor(); }
                if (pos < s.Length && s[pos] == '-') { pos++; return -ParseFactor(); }

                double x = 0;
                int startPos = pos;
                if (pos < s.Length && s[pos] == '(')
                {
                    pos++;
                    x = ParseExpression();
                    if (pos < s.Length && s[pos] == ')') pos++;
                }
                else
                {
                    while (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '.' || s[pos] == ','))
                    {
                        pos++;
                    }
                    string sub = s[startPos..pos].Replace(',', '.');
                    double.TryParse(sub, NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                }
                return x;
            }

            return ParseExpression();
        }

        private static bool TentarConverterNumero(string texto, out double valor)
        {
            string norm = (texto ?? string.Empty).Trim().Replace(',', '.');
            return double.TryParse(norm, NumberStyles.Float, CultureInfo.InvariantCulture, out valor);
        }

        private static string FormatarNumero(double valor)
        {
            return Math.Round(valor, 4).ToString(CultureInfo.CurrentCulture);
        }

        // ==================== FORMATAÇÃO CONDICIONAL ESTILO EXCEL ====================

        private void AplicarFormatacaoCondicionalNaGrade()
        {
            var defaultCellBg = _isDarkMode ? ThemePalette.DarkWindowBg : ThemePalette.LightWindowBg;
            var defaultCellFg = _isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;

            foreach (var kvp in _sheetCellBoxes)
            {
                string key = kvp.Key;
                var box = kvp.Value;
                var border = _sheetCellBorders[key];

                border.Background = defaultCellBg;
                box.Foreground = defaultCellFg;
                box.FontWeight = FontWeight.Normal;
            }

            foreach (var rule in _conditionalRules)
            {
                IEnumerable<string> targetCells = (string.IsNullOrWhiteSpace(rule.Range) || rule.Range == "*")
                    ? _sheetCellBoxes.Keys
                    : rule.Range.Contains(':')
                        ? ExpandirIntervaloCelulas(rule.Range)
                        : new[] { rule.Range.Trim().ToUpperInvariant() };

                (IBrush bgBrush, IBrush fgBrush) = ObterCoresEstiloCondicionalExcel(rule.StyleKey);

                foreach (var cellKey in targetCells)
                {
                    if (!_sheetCellBoxes.TryGetValue(cellKey, out var box) ||
                        !_sheetCellBorders.TryGetValue(cellKey, out var border))
                    {
                        continue;
                    }

                    string disp = _sheetComputedDisplay.TryGetValue(cellKey, out var d) ? d : string.Empty;
                    if (string.IsNullOrWhiteSpace(disp)) continue;

                    _sheetComputedNumeric.TryGetValue(cellKey, out var num);

                    if (CelulaAtendeRegraCondicional(disp, num, rule))
                    {
                        border.Background = bgBrush;
                        box.Foreground = fgBrush;
                        box.FontWeight = FontWeight.SemiBold;
                    }
                }
            }
        }

        private static bool CelulaAtendeRegraCondicional(string display, double? numeric, ConditionalFormatRuleData rule)
        {
            if (rule.Operator == "contains")
            {
                return !string.IsNullOrEmpty(rule.Value1) &&
                       display.Contains(rule.Value1, StringComparison.OrdinalIgnoreCase);
            }

            if (numeric.HasValue && TentarConverterNumero(rule.Value1, out double v1))
            {
                double n = numeric.Value;
                return rule.Operator switch
                {
                    ">" => n > v1,
                    "<" => n < v1,
                    ">=" => n >= v1,
                    "<=" => n <= v1,
                    "=" => Math.Abs(n - v1) < 1e-9,
                    "!=" => Math.Abs(n - v1) > 1e-9,
                    "between" => TentarConverterNumero(rule.Value2, out double v2) &&
                                 n >= Math.Min(v1, v2) && n <= Math.Max(v1, v2),
                    _ => false
                };
            }

            return rule.Operator switch
            {
                "=" => string.Equals(display, rule.Value1, StringComparison.OrdinalIgnoreCase),
                "!=" => !string.Equals(display, rule.Value1, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }

        private (IBrush Bg, IBrush Fg) ObterCoresEstiloCondicionalExcel(string styleKey)
        {
            return (styleKey ?? "green").ToLowerInvariant() switch
            {
                "red" => _isDarkMode
                    ? (SolidColorBrush.Parse("#4C1D24"), SolidColorBrush.Parse("#FCA5A5"))
                    : (SolidColorBrush.Parse("#FFC7CE"), SolidColorBrush.Parse("#9C0006")),
                "yellow" => _isDarkMode
                    ? (SolidColorBrush.Parse("#453815"), SolidColorBrush.Parse("#FDE68A"))
                    : (SolidColorBrush.Parse("#FFEB9C"), SolidColorBrush.Parse("#9C6500")),
                "blue" => _isDarkMode
                    ? (SolidColorBrush.Parse("#1E3A5F"), SolidColorBrush.Parse("#93C5FD"))
                    : (SolidColorBrush.Parse("#DDEBF7"), SolidColorBrush.Parse("#1F4E78")),
                "purple" => _isDarkMode
                    ? (SolidColorBrush.Parse("#3B1F5E"), SolidColorBrush.Parse("#D8B4FE"))
                    : (SolidColorBrush.Parse("#E9D5FF"), SolidColorBrush.Parse("#581C87")),
                _ => _isDarkMode
                    ? (SolidColorBrush.Parse("#14432A"), SolidColorBrush.Parse("#86EFAC"))
                    : (SolidColorBrush.Parse("#C6EFCE"), SolidColorBrush.Parse("#006100"))
            };
        }

        private void AtualizarCoresPlanilha()
        {
            if (_sheetToolbar == null) return;

            var barBg = _isDarkMode ? ThemePalette.DarkContentBg : ThemePalette.LightContentBg;
            var headerBg = _isDarkMode ? ThemePalette.DarkWindowHeaderBg : ThemePalette.LightWindowHeaderBg;
            var borderBrush = _isDarkMode ? ThemePalette.DarkBorder : ThemePalette.LightBorder;
            var btnBg = _isDarkMode ? ThemePalette.DarkButtonBg : ThemePalette.LightButtonBg;
            var textPrimary = _isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;
            var textSecondary = _isDarkMode ? ThemePalette.DarkTextSecondary : ThemePalette.LightTextSecondary;
            var inputBg = _isDarkMode ? ThemePalette.DarkWindowBg : ThemePalette.LightWindowBg;

            _sheetToolbar.Background = barBg;
            _sheetToolbar.BorderBrush = borderBrush;

            foreach (var btn in new[] { _btnCondFormatMenu, _btnSheetAddRow, _btnSheetAddCol })
            {
                if (btn != null)
                {
                    btn.Background = btnBg;
                    btn.BorderBrush = borderBrush;
                    btn.Foreground = textPrimary;
                }
            }

            if (_sheetFormulaBar != null)
            {
                _sheetFormulaBar.Background = barBg;
                _sheetFormulaBar.BorderBrush = borderBrush;
            }

            if (_lblActiveCellRef != null)
            {
                _lblActiveCellRef.Foreground = textPrimary;
            }

            if (_txtFormulaBar != null)
            {
                _txtFormulaBar.Background = inputBg;
                _txtFormulaBar.BorderBrush = borderBrush;
                _txtFormulaBar.Foreground = textPrimary;
            }

            if (_sheetCondPanel != null)
            {
                _sheetCondPanel.Background = barBg;
                _sheetCondPanel.BorderBrush = borderBrush;
            }

            if (_sheetHibernatedBanner != null)
            {
                _sheetHibernatedBanner.Background = inputBg;
                if (_lblSheetHibernatedTitle != null)
                {
                    _lblSheetHibernatedTitle.Foreground = textPrimary;
                }
                if (_lblSheetHibernatedInfo != null)
                {
                    _lblSheetHibernatedInfo.Foreground = textSecondary;
                }
            }

            foreach (var hb in _sheetHeaderBorders)
            {
                hb.Background = headerBg;
                hb.BorderBrush = borderBrush;
            }

            foreach (var hl in _sheetHeaderLabels)
            {
                hl.Foreground = textSecondary;
            }

            foreach (var cb in _sheetCellBorders.Values)
            {
                cb.BorderBrush = borderBrush;
            }

            AplicarFormatacaoCondicionalNaGrade();
        }
    }
}
