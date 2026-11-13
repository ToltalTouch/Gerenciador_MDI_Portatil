using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AvaloniaMdiApp
{
    public partial class MainWindow
    {
        private bool _isCalendarOpen;
        private bool _calendarInitialized;
        private bool _isPickingCalendarTarget;
        private DateTime _calendarMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
        private DateTime _calSelStart = DateTime.Today;
        private DateTime _calSelEnd = DateTime.Today;
        private string _pendingCalendarTimeRange = string.Empty;
        private bool _isDraggingCalendar;

        private TextBlock? _lblCalTitle;
        private TextBlock? _lblCalMonth;
        private TextBlock? _lblCalSelectionInfo;
        private TextBlock? _lblCalHint;
        private TextBlock? _lblCalHolidaysHeader;
        private TextBlock? _lblCalLinksHeader;
        private Grid? _calDaysGrid;
        private StackPanel? _calHolidaysPanel;
        private StackPanel? _calLinksPanel;
        private Button? _btnCalPrev;
        private Button? _btnCalNext;
        private Button? _btnCalToday;
        private Button? _btnCalClose;
        private Border? _calHeaderBorder;
        private Border? _calSelectionBox;
        private Border? _calHolidaysBox;
        private Button? _btnCalHolidaysToggle;
        private bool _isHolidaysExpanded;
        private Border? _calPickBanner;
        private readonly List<(Border Cell, TextBlock DayText, DateTime Date)> _calCells = new();

        private static readonly IBrush HolidayGrayBrush = SolidColorBrush.Parse("#64748B").ToImmutable();

        private void ToggleCalendarPanel_Click(object? sender, RoutedEventArgs e)
        {
            if (!_calendarInitialized)
            {
                InicializarPainelCalendario();
            }

            _isCalendarOpen = !_isCalendarOpen;
            RightCalendarPanel.Width = _isCalendarOpen ? 300 : 0;

            if (_isCalendarOpen)
            {
                RenderizarCalendarioMes();
                RenderizarFeriadosDoMes();
                RenderizarEtapasDoPeriodo();
            }
            else if (_isPickingCalendarTarget)
            {
                CancelarModoVinculoCalendario();
            }
        }

        private void InicializarPainelCalendario()
        {
            _calendarInitialized = true;

            var rootDock = new DockPanel { Margin = new Thickness(10, 10, 10, 10) };

            // Cabeçalho
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            _lblCalTitle = new TextBlock
            {
                Text = "Calendário de Etapas",
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };

            _btnCalClose = new Button
            {
                Content = "✕",
                Width = 26,
                Height = 26,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(6),
                Background = Brushes.Transparent,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _btnCalClose.Click += (_, _) =>
            {
                _isCalendarOpen = false;
                RightCalendarPanel.Width = 0;
                CancelarModoVinculoCalendario();
            };

            Grid.SetColumn(_lblCalTitle, 0);
            Grid.SetColumn(_btnCalClose, 1);
            headerGrid.Children.Add(_lblCalTitle);
            headerGrid.Children.Add(_btnCalClose);

            _calHeaderBorder = new Border
            {
                Padding = new Thickness(0, 0, 0, 8),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = headerGrid
            };
            DockPanel.SetDock(_calHeaderBorder, Dock.Top);
            rootDock.Children.Add(_calHeaderBorder);

            // Navegação do mês
            var navGrid = new Grid { Margin = new Thickness(0, 8, 0, 6) };
            navGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            navGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            navGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            navGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            _btnCalPrev = new Button
            {
                Content = "◀",
                Width = 26,
                Height = 26,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(6),
                FontSize = 10,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _btnCalPrev.Click += (_, _) =>
            {
                _calendarMonth = _calendarMonth.AddMonths(-1);
                RenderizarCalendarioMes();
                RenderizarFeriadosDoMes();
                RenderizarEtapasDoPeriodo();
            };

            _lblCalMonth = new TextBlock
            {
                FontSize = 12.5,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _btnCalNext = new Button
            {
                Content = "▶",
                Width = 26,
                Height = 26,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(6),
                FontSize = 10,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _btnCalNext.Click += (_, _) =>
            {
                _calendarMonth = _calendarMonth.AddMonths(1);
                RenderizarCalendarioMes();
                RenderizarFeriadosDoMes();
                RenderizarEtapasDoPeriodo();
            };

            _btnCalToday = new Button
            {
                Content = "Hoje",
                Height = 26,
                Padding = new Thickness(8, 0),
                Margin = new Thickness(4, 0, 0, 0),
                CornerRadius = new CornerRadius(6),
                FontSize = 11,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _btnCalToday.Click += (_, _) =>
            {
                _calendarMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                _calSelStart = DateTime.Today;
                _calSelEnd = DateTime.Today;
                RenderizarCalendarioMes();
                RenderizarFeriadosDoMes();
                RenderizarEtapasDoPeriodo();
            };

            Grid.SetColumn(_btnCalPrev, 0);
            Grid.SetColumn(_lblCalMonth, 1);
            Grid.SetColumn(_btnCalNext, 2);
            Grid.SetColumn(_btnCalToday, 3);
            navGrid.Children.Add(_btnCalPrev);
            navGrid.Children.Add(_lblCalMonth);
            navGrid.Children.Add(_btnCalNext);
            navGrid.Children.Add(_btnCalToday);
            DockPanel.SetDock(navGrid, Dock.Top);
            rootDock.Children.Add(navGrid);

            // Dias da semana
            var weekGrid = new Grid { Margin = new Thickness(0, 2, 0, 4) };
            for (int c = 0; c < 7; c++)
            {
                weekGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            }
            string[] diasSemana = { "D", "S", "T", "Q", "Q", "S", "S" };
            for (int c = 0; c < 7; c++)
            {
                var lbl = new TextBlock
                {
                    Text = diasSemana[c],
                    FontSize = 11,
                    FontWeight = FontWeight.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Opacity = 0.7
                };
                Grid.SetColumn(lbl, c);
                weekGrid.Children.Add(lbl);
            }
            DockPanel.SetDock(weekGrid, Dock.Top);
            rootDock.Children.Add(weekGrid);

            // Grade 6x7 de dias (seleção contínua por clique + arraste)
            _calDaysGrid = new Grid { Background = Brushes.Transparent };
            for (int c = 0; c < 7; c++) _calDaysGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            for (int r = 0; r < 6; r++) _calDaysGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            _calDaysGrid.AddHandler(InputElement.PointerMovedEvent, (_, e) =>
            {
                if (!_isDraggingCalendar || _calDaysGrid.Bounds.Width <= 0 || _calDaysGrid.Bounds.Height <= 0) return;
                var pos = e.GetPosition(_calDaysGrid);
                int col = Math.Clamp((int)(pos.X / (_calDaysGrid.Bounds.Width / 7.0)), 0, 6);
                int row = Math.Clamp((int)(pos.Y / (_calDaysGrid.Bounds.Height / 6.0)), 0, 5);
                int idx = row * 7 + col;
                if (idx >= 0 && idx < _calCells.Count)
                {
                    var hoveredDate = _calCells[idx].Date;
                    if (hoveredDate != _calSelEnd)
                    {
                        _calSelEnd = hoveredDate;
                        AtualizarDestaquesSelecaoCalendario();
                    }
                }
            }, RoutingStrategies.Tunnel);

            _calDaysGrid.AddHandler(InputElement.PointerReleasedEvent, (_, _) => FinalizarArrasteCalendario(), RoutingStrategies.Tunnel);

            DockPanel.SetDock(_calDaysGrid, Dock.Top);
            rootDock.Children.Add(_calDaysGrid);

            // Banner de seleção interativa com o mouse
            var btnCancelPick = new Button
            {
                Content = "Cancelar",
                FontSize = 10.5,
                Padding = new Thickness(8, 2),
                CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            btnCancelPick.Click += (_, _) => CancelarModoVinculoCalendario();

            _calPickBanner = new Border
            {
                IsVisible = false,
                Margin = new Thickness(0, 6, 0, 0),
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(7),
                Background = ThemePalette.AccentSubtle,
                BorderBrush = ThemePalette.AccentLight,
                BorderThickness = new Thickness(1),
                Child = new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Clique na janela ou no item da lista que deseja vincular ao período selecionado.",
                            FontSize = 11,
                            Foreground = Brushes.White,
                            TextWrapping = TextWrapping.Wrap
                        },
                        btnCancelPick
                    }
                }
            };
            DockPanel.SetDock(_calPickBanner, Dock.Top);
            rootDock.Children.Add(_calPickBanner);

            // Barra de seleção
            _lblCalSelectionInfo = new TextBlock
            {
                FontSize = 11.5,
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap
            };
            _lblCalHint = new TextBlock
            {
                Text = "Arraste nos dias para selecionar vários • Botão direito para vincular/hora",
                FontSize = 10.5,
                TextWrapping = TextWrapping.Wrap
            };

            _calSelectionBox = new Border
            {
                Margin = new Thickness(0, 8, 0, 6),
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Child = new StackPanel
                {
                    Spacing = 3,
                    Children = { _lblCalSelectionInfo, _lblCalHint }
                }
            };
            DockPanel.SetDock(_calSelectionBox, Dock.Top);
            rootDock.Children.Add(_calSelectionBox);

            // Espaço dedicado aos feriados nacionais do mês (dropdown retrátil)
            _lblCalHolidaysHeader = new TextBlock
            {
                Text = "▸ Feriados nacionais do mês",
                FontSize = 11.5,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            _btnCalHolidaysToggle = new Button
            {
                Content = _lblCalHolidaysHeader,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            _calHolidaysPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 3,
                Margin = new Thickness(0, 6, 0, 0),
                IsVisible = _isHolidaysExpanded
            };
            _btnCalHolidaysToggle.Click += (_, _) =>
            {
                _isHolidaysExpanded = !_isHolidaysExpanded;
                _calHolidaysPanel.IsVisible = _isHolidaysExpanded;
                RenderizarFeriadosDoMes();
            };
            _calHolidaysBox = new Border
            {
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(8, 6),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Child = new StackPanel
                {
                    Spacing = 0,
                    Children = { _btnCalHolidaysToggle, _calHolidaysPanel }
                }
            };
            DockPanel.SetDock(_calHolidaysBox, Dock.Top);
            rootDock.Children.Add(_calHolidaysBox);

            // Cabeçalho da visão geral do mês
            _lblCalLinksHeader = new TextBlock
            {
                Text = "Visão geral do mês",
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            };
            DockPanel.SetDock(_lblCalLinksHeader, Dock.Top);
            rootDock.Children.Add(_lblCalLinksHeader);

            // Lista inferior com visão geral do mês e resumo
            _calLinksPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 6
            };
            var linksScroller = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _calLinksPanel
            };
            rootDock.Children.Add(linksScroller);

            RightCalendarContentHost.Content = rootDock;
            AtualizarTemaCalendario();
        }

        private void FinalizarArrasteCalendario()
        {
            if (!_isDraggingCalendar) return;
            _isDraggingCalendar = false;
            RenderizarEtapasDoPeriodo();
        }

        private (DateTime Min, DateTime Max) ObterIntervaloSelecionado()
        {
            return _calSelStart <= _calSelEnd ? (_calSelStart.Date, _calSelEnd.Date) : (_calSelEnd.Date, _calSelStart.Date);
        }

        private static Dictionary<DateTime, string> ObterFeriadosNacionais(int ano)
        {
            var dict = new Dictionary<DateTime, string>
            {
                [new DateTime(ano, 1, 1)] = "Confraternização Universal",
                [new DateTime(ano, 4, 21)] = "Tiradentes",
                [new DateTime(ano, 5, 1)] = "Dia do Trabalho",
                [new DateTime(ano, 9, 7)] = "Independência do Brasil",
                [new DateTime(ano, 10, 12)] = "Nossa Senhora Aparecida",
                [new DateTime(ano, 11, 2)] = "Finados",
                [new DateTime(ano, 11, 15)] = "Proclamação da República",
                [new DateTime(ano, 11, 20)] = "Dia da Consciência Negra",
                [new DateTime(ano, 12, 25)] = "Natal"
            };

            // Cálculo da Páscoa (Algoritmo de Meeus/Jones/Butcher)
            int a = ano % 19;
            int b = ano / 100;
            int c = ano % 100;
            int d = b / 4;
            int e = b % 4;
            int f = (b + 8) / 25;
            int g = (b - f + 1) / 3;
            int h = (19 * a + b - d - g + 15) % 30;
            int i = c / 4;
            int k = c % 4;
            int l = (32 + 2 * e + 2 * i - h - k) % 7;
            int m = (a + 11 * h + 22 * l) / 451;
            int mes = (h + l - 7 * m + 114) / 31;
            int dia = ((h + l - 7 * m + 114) % 31) + 1;
            var pascoa = new DateTime(ano, mes, dia);

            dict[pascoa.AddDays(-47)] = "Carnaval";
            dict[pascoa.AddDays(-2)] = "Sexta-Feira Santa";
            dict[pascoa.AddDays(60)] = "Corpus Christi";

            return dict;
        }

        private void RenderizarCalendarioMes()
        {
            if (_calDaysGrid == null || _lblCalMonth == null) return;

            var ptBr = CultureInfo.GetCultureInfo("pt-BR");
            string nomeMes = ptBr.DateTimeFormat.GetMonthName(_calendarMonth.Month);
            _lblCalMonth.Text = $"{char.ToUpper(nomeMes[0])}{nomeMes.Substring(1)} {_calendarMonth.Year}";

            _calDaysGrid.Children.Clear();
            _calCells.Clear();

            var firstDay = new DateTime(_calendarMonth.Year, _calendarMonth.Month, 1);
            int startOffset = (int)firstDay.DayOfWeek;
            var gridStart = firstDay.AddDays(-startOffset);

            var feriadosAno = ObterFeriadosNacionais(_calendarMonth.Year);

            for (int i = 0; i < 42; i++)
            {
                DateTime cellDate = gridStart.AddDays(i);
                int row = i / 7;
                int col = i % 7;
                bool isCurrentMonth = cellDate.Month == _calendarMonth.Month;

                var txtDay = new TextBlock
                {
                    Text = cellDate.Day.ToString(),
                    FontSize = 11.5,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = isCurrentMonth ? 1.0 : 0.35
                };

                var cellBorder = new Border
                {
                    Height = 32,
                    Margin = new Thickness(1.5),
                    CornerRadius = new CornerRadius(6),
                    BorderThickness = new Thickness(1.5),
                    Background = Brushes.Transparent,
                    BorderBrush = Brushes.Transparent,
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Child = txtDay
                };

                if (cellDate.Year != _calendarMonth.Year)
                {
                    var feriadosOutroAno = ObterFeriadosNacionais(cellDate.Year);
                    if (feriadosOutroAno.TryGetValue(cellDate.Date, out var nomeFeriadoOutro))
                    {
                        ToolTip.SetTip(cellBorder, $"Feriado Nacional: {nomeFeriadoOutro}");
                    }
                }
                else if (feriadosAno.TryGetValue(cellDate.Date, out var nomeFeriado))
                {
                    ToolTip.SetTip(cellBorder, $"Feriado Nacional: {nomeFeriado}");
                }

                DateTime capturedDate = cellDate;
                cellBorder.PointerPressed += (_, e) =>
                {
                    var props = e.GetCurrentPoint(cellBorder).Properties;
                    if (props.IsLeftButtonPressed)
                    {
                        _isDraggingCalendar = true;
                        _calSelStart = capturedDate;
                        _calSelEnd = capturedDate;
                        e.Pointer.Capture(_calDaysGrid);
                        AtualizarDestaquesSelecaoCalendario();
                        RenderizarEtapasDoPeriodo();
                    }
                    else if (props.IsRightButtonPressed)
                    {
                        var (min, max) = ObterIntervaloSelecionado();
                        if (capturedDate < min || capturedDate > max)
                        {
                            _calSelStart = capturedDate;
                            _calSelEnd = capturedDate;
                            AtualizarDestaquesSelecaoCalendario();
                            RenderizarEtapasDoPeriodo();
                        }
                        AbrirMenuContextoDiaCalendario(cellBorder, capturedDate);
                        e.Handled = true;
                    }
                };

                Grid.SetRow(cellBorder, row);
                Grid.SetColumn(cellBorder, col);
                _calDaysGrid.Children.Add(cellBorder);
                _calCells.Add((cellBorder, txtDay, cellDate));
            }

            AtualizarDestaquesSelecaoCalendario();
        }

        private void RenderizarFeriadosDoMes()
        {
            if (_calHolidaysPanel == null) return;
            _calHolidaysPanel.Children.Clear();

            var textSecondary = _isDarkMode ? ThemePalette.DarkTextSecondary : ThemePalette.LightTextSecondary;
            var textPrimary = _isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;

            var feriadosMes = ObterFeriadosNacionais(_calendarMonth.Year)
                .Where(kv => kv.Key.Month == _calendarMonth.Month)
                .OrderBy(kv => kv.Key)
                .ToList();

            if (_lblCalHolidaysHeader != null)
            {
                string seta = _isHolidaysExpanded ? "▾" : "▸";
                _lblCalHolidaysHeader.Text = $"{seta} Feriados nacionais do mês ({feriadosMes.Count})";
            }

            if (feriadosMes.Count == 0)
            {
                _calHolidaysPanel.Children.Add(new TextBlock
                {
                    Text = "Nenhum feriado nacional neste mês.",
                    FontSize = 11,
                    Foreground = textSecondary
                });
                return;
            }

            foreach (var f in feriadosMes)
            {
                var row = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6
                };
                row.Children.Add(new Border
                {
                    Width = 7,
                    Height = 7,
                    CornerRadius = new CornerRadius(3.5),
                    Background = HolidayGrayBrush,
                    VerticalAlignment = VerticalAlignment.Center
                });
                row.Children.Add(new TextBlock
                {
                    Text = $"{f.Key:dd/MM} — {f.Value}",
                    FontSize = 11,
                    Foreground = textPrimary,
                    TextWrapping = TextWrapping.Wrap
                });
                _calHolidaysPanel.Children.Add(row);
            }
        }

        private void AbrirMenuContextoDiaCalendario(Control anchor, DateTime clickedDate)
        {
            var menu = new MenuFlyout { Placement = PlacementMode.LeftEdgeAlignedTop };

            var miVincular = new MenuItem
            {
                Header = "Vincular janela ou item com o mouse...",
                Icon = AppIcons.Create(AppIcons.Link, 12, ThemePalette.IconLightGray)
            };
            miVincular.Click += (_, _) => AtivarModoVinculoCalendario();
            menu.Items.Add(miVincular);

            var (min, max) = ObterIntervaloSelecionado();
            if (max > min)
            {
                var miHoraDia = new MenuItem
                {
                    Header = $"Marcar hora para este dia ({clickedDate:dd/MM})...",
                    Icon = AppIcons.Create(AppIcons.Calendar, 12, ThemePalette.IconLightGray)
                };
                miHoraDia.Click += (_, _) =>
                {
                    _calSelStart = clickedDate;
                    _calSelEnd = clickedDate;
                    AtualizarDestaquesSelecaoCalendario();
                    RenderizarEtapasDoPeriodo();
                    AbrirSeletorHoraPeriodo(anchor, $"Horário do dia {clickedDate:dd/MM}");
                };
                menu.Items.Add(miHoraDia);

                var miHoraPeriodo = new MenuItem
                {
                    Header = $"Marcar hora para o período ({min:dd/MM} – {max:dd/MM})...",
                    Icon = AppIcons.Create(AppIcons.Calendar, 12, ThemePalette.IconLightGray)
                };
                miHoraPeriodo.Click += (_, _) => AbrirSeletorHoraPeriodo(anchor, $"Horário do período ({min:dd/MM} – {max:dd/MM})");
                menu.Items.Add(miHoraPeriodo);
            }
            else
            {
                var miHora = new MenuItem
                {
                    Header = $"Marcar hora para o dia ({min:dd/MM})...",
                    Icon = AppIcons.Create(AppIcons.Calendar, 12, ThemePalette.IconLightGray)
                };
                miHora.Click += (_, _) => AbrirSeletorHoraPeriodo(anchor, $"Horário do dia {min:dd/MM}");
                menu.Items.Add(miHora);
            }

            var ws = ObterWorkspaceAtual();
            var linksNoPeriodo = ws.CalendarLinks.Where(l => LinkSobrepoePeriodo(l, min, max)).ToList();
            if (linksNoPeriodo.Count > 0)
            {
                menu.Items.Add(new Separator());
                var miLimpar = new MenuItem
                {
                    Header = $"Remover {linksNoPeriodo.Count} vínculo(s) deste período",
                    Icon = AppIcons.Create(AppIcons.Trash, 12, ThemePalette.IconLightGray)
                };
                miLimpar.Click += (_, _) =>
                {
                    RegistrarEstadoUndo();
                    foreach (var lnk in linksNoPeriodo)
                    {
                        ws.CalendarLinks.Remove(lnk);
                    }
                    SettingsManager.Salvar(_settings);
                    RenderizarCalendarioMes();
                    RenderizarEtapasDoPeriodo();
                };
                menu.Items.Add(miLimpar);
            }

            menu.ShowAt(anchor);
        }

        private void AbrirSeletorHoraPeriodo(Control anchor, string tituloHorario = "Horário do período selecionado")
        {
            var flyout = new Flyout { Placement = PlacementMode.LeftEdgeAlignedTop };
            var txtHora = new TextBox
            {
                Text = _pendingCalendarTimeRange,
                Watermark = "Ex: 09:00 ou 09:00 - 11:30",
                Width = 195,
                FontSize = 12
            };

            var presetsPanel = new WrapPanel { MaxWidth = 205 };
            foreach (var preset in new[] { "08:00", "09:00", "10:00", "14:00", "16:00", "18:00" })
            {
                var p = preset;
                var chip = new Button
                {
                    Content = p,
                    FontSize = 10.5,
                    Padding = new Thickness(7, 3),
                    Margin = new Thickness(0, 0, 4, 4),
                    CornerRadius = new CornerRadius(5)
                };
                chip.Click += (_, _) => txtHora.Text = p;
                presetsPanel.Children.Add(chip);
            }

            var btnClear = new Button
            {
                Content = "Limpar",
                FontSize = 11,
                Padding = new Thickness(8, 4),
                CornerRadius = new CornerRadius(5)
            };
            btnClear.Click += (_, _) =>
            {
                _pendingCalendarTimeRange = string.Empty;
                AtualizarHoraNosLinksDoPeriodo(string.Empty);
                AtualizarDestaquesSelecaoCalendario();
                RenderizarEtapasDoPeriodo();
                flyout.Hide();
            };

            var btnApply = new Button
            {
                Content = "Aplicar",
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                Padding = new Thickness(12, 4),
                CornerRadius = new CornerRadius(5),
                Background = ThemePalette.Accent,
                Foreground = Brushes.White
            };
            btnApply.Click += (_, _) =>
            {
                _pendingCalendarTimeRange = (txtHora.Text ?? string.Empty).Trim();
                AtualizarHoraNosLinksDoPeriodo(_pendingCalendarTimeRange);
                AtualizarDestaquesSelecaoCalendario();
                RenderizarEtapasDoPeriodo();
                flyout.Hide();
            };

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Right,
                Children = { btnClear, btnApply }
            };

            flyout.Content = new StackPanel
            {
                Spacing = 8,
                Margin = new Thickness(4),
                Children =
                {
                    new TextBlock { Text = tituloHorario, FontSize = 12, FontWeight = FontWeight.SemiBold },
                    txtHora,
                    presetsPanel,
                    actions
                }
            };

            flyout.ShowAt(anchor);
            txtHora.Focus();
        }

        private void AtualizarHoraNosLinksDoPeriodo(string hora)
        {
            var (min, max) = ObterIntervaloSelecionado();
            var ws = ObterWorkspaceAtual();
            var links = ws.CalendarLinks.Where(l => LinkSobrepoePeriodo(l, min, max)).ToList();
            if (links.Count == 0) return;

            RegistrarEstadoUndo();
            foreach (var lnk in links)
            {
                lnk.TimeRange = hora;
            }
            SettingsManager.Salvar(_settings);
        }

        private void AtivarModoVinculoCalendario()
        {
            _isPickingCalendarTarget = true;
            if (_calPickBanner != null) _calPickBanner.IsVisible = true;
            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, "Clique na janela ou no item da lista para vincular ao período selecionado.", "info");
            }
        }

        private void CancelarModoVinculoCalendario()
        {
            _isPickingCalendarTarget = false;
            if (_calPickBanner != null) _calPickBanner.IsVisible = false;
        }

        private void AoSelecionarAlvoParaCalendario(DraggableWindow win, string checklistRowId)
        {
            if (!_isPickingCalendarTarget) return;
            RegistrarEstadoUndo();
            VincularJanelaAoPeriodoSelecionado(win, checklistRowId);
            CancelarModoVinculoCalendario();

            if (_settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, "Etapa vinculada ao calendário!", "success");
            }
        }

        private void AtualizarDestaquesSelecaoCalendario()
        {
            var (min, max) = ObterIntervaloSelecionado();
            var textPrimary = _isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;
            var ws = ObterWorkspaceAtual();
            var feriadosCache = new Dictionary<int, Dictionary<DateTime, string>>();

            foreach (var (cell, dayText, date) in _calCells)
            {
                if (!feriadosCache.TryGetValue(date.Year, out var feriadosAno))
                {
                    feriadosAno = ObterFeriadosNacionais(date.Year);
                    feriadosCache[date.Year] = feriadosAno;
                }

                bool selected = date >= min && date <= max;
                bool isToday = date == DateTime.Today;
                bool isHoliday = feriadosAno.ContainsKey(date.Date);
                var linkNoDia = ws.CalendarLinks.FirstOrDefault(l => LinkCobreData(l, date));

                if (linkNoDia != null)
                {
                    cell.Background = ObterCorDoLink(linkNoDia);
                    cell.BorderBrush = selected
                        ? (_isDarkMode ? Brushes.White : ThemePalette.LightTextPrimary)
                        : (isToday ? ThemePalette.AccentLight : Brushes.Transparent);
                    dayText.Foreground = Brushes.White;
                    dayText.FontWeight = FontWeight.Bold;
                }
                else if (selected)
                {
                    cell.Background = ThemePalette.Accent;
                    cell.BorderBrush = ThemePalette.AccentLight;
                    dayText.Foreground = Brushes.White;
                    dayText.FontWeight = FontWeight.Bold;
                }
                else if (isHoliday)
                {
                    cell.Background = HolidayGrayBrush;
                    cell.BorderBrush = isToday ? ThemePalette.AccentLight : Brushes.Transparent;
                    dayText.Foreground = Brushes.White;
                    dayText.FontWeight = FontWeight.SemiBold;
                }
                else
                {
                    cell.Background = Brushes.Transparent;
                    cell.BorderBrush = isToday ? ThemePalette.AccentLight : Brushes.Transparent;
                    dayText.Foreground = textPrimary;
                    dayText.FontWeight = isToday ? FontWeight.SemiBold : FontWeight.Normal;
                }
            }

            if (_lblCalSelectionInfo != null)
            {
                int dias = (max - min).Days + 1;
                string baseInfo = dias == 1
                    ? $"Dia selecionado: {min:dd/MM/yyyy}"
                    : $"Período: {min:dd/MM} até {max:dd/MM/yyyy} ({dias} dias)";
                _lblCalSelectionInfo.Text = string.IsNullOrWhiteSpace(_pendingCalendarTimeRange)
                    ? baseInfo
                    : $"{baseInfo} • {_pendingCalendarTimeRange}";
            }
        }

        private void VincularJanelaAoPeriodoSelecionado(DraggableWindow win, string checklistRowId)
        {
            var (min, max) = ObterIntervaloSelecionado();
            var ws = ObterWorkspaceAtual();

            ws.CalendarLinks.Add(new CalendarStageLinkData
            {
                StartDate = min.ToString("yyyy-MM-dd"),
                EndDate = max.ToString("yyyy-MM-dd"),
                TimeRange = _pendingCalendarTimeRange,
                WindowId = win.WindowId,
                ChecklistRowId = checklistRowId ?? string.Empty
            });

            SettingsManager.Salvar(_settings);
            RenderizarCalendarioMes();
            RenderizarEtapasDoPeriodo();
        }

        private void RenderizarEtapasDoPeriodo()
        {
            if (_calLinksPanel == null) return;
            _calLinksPanel.Children.Clear();

            var (min, max) = ObterIntervaloSelecionado();
            var ws = ObterWorkspaceAtual();

            var monthStart = new DateTime(_calendarMonth.Year, _calendarMonth.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var linksDoMes = ws.CalendarLinks
                .Where(l => LinkSobrepoePeriodo(l, monthStart, monthEnd))
                .OrderBy(l => l.StartDate)
                .ToList();

            var textPrimary = _isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;
            var textSecondary = _isDarkMode ? ThemePalette.DarkTextSecondary : ThemePalette.LightTextSecondary;
            var cardBg = _isDarkMode ? ThemePalette.DarkContentBg : ThemePalette.LightContentBg;
            var borderBrush = _isDarkMode ? ThemePalette.DarkBorder : ThemePalette.LightBorder;

            if (linksDoMes.Count == 0)
            {
                _calLinksPanel.Children.Add(new TextBlock
                {
                    Text = "Nenhuma etapa vinculada neste mês.",
                    FontSize = 11.5,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = textSecondary,
                    Margin = new Thickness(2, 4)
                });
                return;
            }

            foreach (var link in linksDoMes)
            {
                var capturedLink = link;
                bool emDestaqueSelecao = LinkSobrepoePeriodo(link, min, max);
                var targetWin = _ativasJanelas.FirstOrDefault(w => w.WindowId == link.WindowId);
                string periodoTxt = FormatarPeriodoLink(link);
                string tituloAlvo = ObterTituloDoLink(link, targetWin);
                string resumo = targetWin != null
                    ? targetWin.ObterResumoParaCalendario(link.ChecklistRowId)
                    : "(Janela fechada ou em outro estado)";

                var dot = new Border
                {
                    Width = 8,
                    Height = 8,
                    CornerRadius = new CornerRadius(4),
                    Background = ObterCorDoLink(link),
                    VerticalAlignment = VerticalAlignment.Center
                };

                var lblPeriodo = new TextBlock
                {
                    Text = periodoTxt,
                    FontSize = 10.5,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = ThemePalette.AccentLight,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                };

                var btnRemover = new Button
                {
                    Content = "✕",
                    Width = 20,
                    Height = 20,
                    Padding = new Thickness(0),
                    FontSize = 10,
                    Background = Brushes.Transparent,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Foreground = textSecondary
                };
                btnRemover.Click += (_, _) =>
                {
                    RegistrarEstadoUndo();
                    ws.CalendarLinks.Remove(capturedLink);
                    SettingsManager.Salvar(_settings);
                    RenderizarCalendarioMes();
                    RenderizarEtapasDoPeriodo();
                };

                var topRow = new Grid();
                topRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                topRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                topRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                Grid.SetColumn(dot, 0);
                dot.Margin = new Thickness(0, 0, 6, 0);
                Grid.SetColumn(lblPeriodo, 1);
                Grid.SetColumn(btnRemover, 2);
                topRow.Children.Add(dot);
                topRow.Children.Add(lblPeriodo);
                topRow.Children.Add(btnRemover);

                var lblTitulo = new TextBlock
                {
                    Text = tituloAlvo,
                    FontSize = 12,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = textPrimary,
                    TextWrapping = TextWrapping.Wrap
                };

                var lblResumo = new TextBlock
                {
                    Text = resumo,
                    FontSize = 11,
                    Foreground = textSecondary,
                    TextWrapping = TextWrapping.Wrap,
                    MaxLines = 3
                };

                var card = new Border
                {
                    Padding = new Thickness(8),
                    CornerRadius = new CornerRadius(7),
                    Background = cardBg,
                    BorderBrush = emDestaqueSelecao ? ThemePalette.AccentLight : borderBrush,
                    BorderThickness = new Thickness(emDestaqueSelecao ? 1.5 : 1),
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Child = new StackPanel
                    {
                        Spacing = 3,
                        Children = { topRow, lblTitulo, lblResumo }
                    }
                };

                card.PointerPressed += (_, e) =>
                {
                    if (e.Source == btnRemover) return;
                    if (targetWin != null)
                    {
                        if (targetWin.IsMinimized) targetWin.ToggleMinimize();
                        ZIndexManager.BringToFront(targetWin);
                    }
                };

                _calLinksPanel.Children.Add(card);
            }
        }

        private string ObterTituloDoLink(CalendarStageLinkData link, DraggableWindow? win)
        {
            if (win == null) return "Janela não encontrada";
            if (win.WindowType == "checklist" && !string.IsNullOrWhiteSpace(link.ChecklistRowId))
            {
                var row = win.ObterItensChecklist().FirstOrDefault(r => r.RowId == link.ChecklistRowId);
                string itemTxt = row != null && !string.IsNullOrWhiteSpace(row.Input.Text)
                    ? row.Input.Text.Trim()
                    : "Item da lista";
                return $"{win.WindowTitle} › {itemTxt}";
            }
            return win.WindowTitle;
        }

        private IBrush ObterCorDoLink(CalendarStageLinkData link)
        {
            var win = _ativasJanelas.FirstOrDefault(w => w.WindowId == link.WindowId);
            if (win != null)
            {
                if (win.WindowType == "checklist" && !string.IsNullOrWhiteSpace(link.ChecklistRowId))
                {
                    var row = win.ObterItensChecklist().FirstOrDefault(r => r.RowId == link.ChecklistRowId);
                    if (row != null && !string.IsNullOrWhiteSpace(row.ColorHex) && Color.TryParse(row.ColorHex, out var rc))
                    {
                        return new SolidColorBrush(rc);
                    }
                }
                if (!string.IsNullOrWhiteSpace(win.CustomColor) && Color.TryParse(win.CustomColor, out var wc))
                {
                    return new SolidColorBrush(wc);
                }
            }
            return ThemePalette.AccentLight;
        }

        private static bool LinkCobreData(CalendarStageLinkData link, DateTime date)
        {
            if (!DateTime.TryParseExact(link.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var s)) return false;
            if (!DateTime.TryParseExact(link.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var e)) e = s;
            return date.Date >= s.Date && date.Date <= e.Date;
        }

        private static bool LinkSobrepoePeriodo(CalendarStageLinkData link, DateTime min, DateTime max)
        {
            if (!DateTime.TryParseExact(link.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var s)) return false;
            if (!DateTime.TryParseExact(link.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var e)) e = s;
            return s.Date <= max.Date && e.Date >= min.Date;
        }

        private static string FormatarPeriodoLink(CalendarStageLinkData link)
        {
            if (!DateTime.TryParseExact(link.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var s))
                return link.StartDate;
            string datePart = (!DateTime.TryParseExact(link.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var e) || e.Date == s.Date)
                ? s.ToString("dd/MM/yyyy")
                : $"{s:dd/MM} – {e:dd/MM/yyyy}";
            return string.IsNullOrWhiteSpace(link.TimeRange)
                ? datePart
                : $"{datePart} • {link.TimeRange}";
        }

        private void AtualizarTemaCalendario()
        {
            var barBg = _isDarkMode ? ThemePalette.DarkBarBg : ThemePalette.LightBarBg;
            var borderBrush = _isDarkMode ? ThemePalette.DarkBorder : ThemePalette.LightBorder;
            var textPrimary = _isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;
            var textSecondary = _isDarkMode ? ThemePalette.DarkTextSecondary : ThemePalette.LightTextSecondary;
            var btnBg = _isDarkMode ? ThemePalette.DarkButtonBg : ThemePalette.LightButtonBg;
            var contentBg = _isDarkMode ? ThemePalette.DarkContentBg : ThemePalette.LightContentBg;

            RightCalendarBar.Background = barBg;
            RightCalendarBar.BorderBrush = borderBrush;
            BtnRightCalendario.Foreground = textPrimary;
            RightCalendarPanel.Background = barBg;
            RightCalendarPanel.BorderBrush = borderBrush;

            if (!_calendarInitialized) return;

            if (_lblCalTitle != null) _lblCalTitle.Foreground = textPrimary;
            if (_lblCalMonth != null) _lblCalMonth.Foreground = textPrimary;
            if (_lblCalSelectionInfo != null) _lblCalSelectionInfo.Foreground = textPrimary;
            if (_lblCalHint != null) _lblCalHint.Foreground = textSecondary;
            if (_lblCalHolidaysHeader != null) _lblCalHolidaysHeader.Foreground = textPrimary;
            if (_lblCalLinksHeader != null) _lblCalLinksHeader.Foreground = textPrimary;
            if (_calHeaderBorder != null) _calHeaderBorder.BorderBrush = borderBrush;
            if (_calSelectionBox != null)
            {
                _calSelectionBox.Background = contentBg;
                _calSelectionBox.BorderBrush = borderBrush;
            }
            if (_calHolidaysBox != null)
            {
                _calHolidaysBox.Background = contentBg;
                _calHolidaysBox.BorderBrush = borderBrush;
            }
            if (_btnCalClose != null) _btnCalClose.Foreground = textPrimary;

            foreach (var b in new[] { _btnCalPrev, _btnCalNext, _btnCalToday })
            {
                if (b != null)
                {
                    b.Background = btnBg;
                    b.Foreground = textPrimary;
                }
            }

            if (_isCalendarOpen)
            {
                RenderizarCalendarioMes();
                RenderizarFeriadosDoMes();
                RenderizarEtapasDoPeriodo();
            }
        }
    }
}
