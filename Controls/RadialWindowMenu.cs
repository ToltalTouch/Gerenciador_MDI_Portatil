using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AvaloniaMdiApp
{
    /// <summary>
    /// Menu Circular / Radial interativo para ações da janela (estilo The Sims 4 / Pie Menu).
    /// Suporta submenus concêntricos com atenuação do anel principal, seleção de cores,
    /// criticidade, conexões e ações rápidas.
    /// </summary>
    public class RadialWindowMenu : Canvas
    {
        private class RadialMenuItem
        {
            public string Id { get; set; } = string.Empty;
            public string Label { get; set; } = string.Empty;
            public StreamGeometry? Icon { get; set; }
            public IBrush? IconBrush { get; set; }
            public bool HasSubmenu { get; set; }
            public Action? OnExecute { get; set; }
            public Border? VisualElement { get; set; }
        }

        private readonly DraggableWindow _targetWindow;
        private readonly Point _center;
        private readonly Size _viewportSize;
        private string? _activeSubmenu = null;

        private readonly List<RadialMenuItem> _mainItems = new();
        private readonly List<RadialMenuItem> _subItems = new();
        private readonly Border _centerHub;
        private readonly TextBlock _centerHubText;
        private readonly PathIcon _centerHubIcon;
        private readonly bool _hasConnections;

        public event Action? OnClosed;

        public RadialWindowMenu(DraggableWindow targetWindow, Point centerPoint, bool hasConnections = false, Size? viewportSize = null)
        {
            _targetWindow = targetWindow;
            _center = centerPoint;
            _hasConnections = hasConnections;
            _viewportSize = viewportSize ?? new Size(1600, 1000);

            ClipToBounds = false;
            IsHitTestVisible = true;

            // Centro (Hub central)
            _centerHub = new Border
            {
                Width = 100,
                Height = 36,
                CornerRadius = new CornerRadius(18),
                Background = new SolidColorBrush(Color.Parse("#EE1E293B")),
                BorderBrush = new SolidColorBrush(Color.Parse("#38BDF8")),
                BorderThickness = new Thickness(1.8),
                BoxShadow = new BoxShadows(new BoxShadow
                {
                    Blur = 16,
                    Spread = 1,
                    Color = Color.FromArgb(140, 56, 189, 248)
                }),
                Cursor = new Cursor(StandardCursorType.Hand),
                ZIndex = 30
            };

            var hubPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _centerHubIcon = new PathIcon
            {
                Width = 13,
                Height = 13,
                Data = AppIcons.Settings,
                Foreground = new SolidColorBrush(Color.Parse("#38BDF8")),
                VerticalAlignment = VerticalAlignment.Center
            };

            _centerHubText = new TextBlock
            {
                Text = "Opções",
                FontSize = 11.5,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };

            hubPanel.Children.Add(_centerHubIcon);
            hubPanel.Children.Add(_centerHubText);
            _centerHub.Child = hubPanel;

            _centerHub.PointerPressed += (s, e) =>
            {
                e.Handled = true;
                if (_activeSubmenu != null)
                {
                    FecharSubmenu();
                }
                else
                {
                    FecharMenu();
                }
            };

            Canvas.SetLeft(_centerHub, _center.X - 50);
            Canvas.SetTop(_centerHub, _center.Y - 18);
            Children.Add(_centerHub);

            MontarItensMenuPrincipal();
            RenderizarMenuPrincipal();
        }

        private void MontarItensMenuPrincipal()
        {
            _mainItems.Clear();

            // 1. Mudar Cor (Submenu) - altera apenas esta janela
            _mainItems.Add(new RadialMenuItem
            {
                Id = "color",
                Label = "Mudar Cor",
                Icon = AppIcons.Palette,
                IconBrush = new SolidColorBrush(Color.Parse("#F43F5E")),
                HasSubmenu = true
            });

            // Opção extra: Mudar cor de todas as janelas conectadas no fluxo
            if (_hasConnections)
            {
                _mainItems.Add(new RadialMenuItem
                {
                    Id = "color_connected",
                    Label = "Cor Conectadas",
                    Icon = AppIcons.Flowchart,
                    IconBrush = new SolidColorBrush(Color.Parse("#38BDF8")),
                    HasSubmenu = true
                });
            }

            // 2. Criticidade (Submenu)
            _mainItems.Add(new RadialMenuItem
            {
                Id = "criticality",
                Label = "Criticidade",
                Icon = AppIcons.Warning,
                IconBrush = new SolidColorBrush(Color.Parse("#F59E0B")),
                HasSubmenu = true
            });

            // 3. Conectar Fluxograma
            _mainItems.Add(new RadialMenuItem
            {
                Id = "connect",
                Label = "Conectar",
                Icon = AppIcons.Link,
                IconBrush = new SolidColorBrush(Color.Parse("#38BDF8")),
                HasSubmenu = false,
                OnExecute = () =>
                {
                    _targetWindow.IniciarConexao();
                    FecharMenu();
                }
            });

            // 4. Salvar
            _mainItems.Add(new RadialMenuItem
            {
                Id = "save",
                Label = "Salvar",
                Icon = AppIcons.Save,
                IconBrush = new SolidColorBrush(Color.Parse("#10B981")),
                HasSubmenu = false,
                OnExecute = () =>
                {
                    _targetWindow.SalvarJanela();
                    FecharMenu();
                }
            });

            // 5. Renomear (se janela) ou Formato (se forma fluxograma)
            if (_targetWindow.IsFlowchartShape)
            {
                _mainItems.Add(new RadialMenuItem
                {
                    Id = "shape",
                    Label = "Formato",
                    Icon = AppIcons.ShapeDiamond,
                    IconBrush = new SolidColorBrush(Color.Parse("#A855F7")),
                    HasSubmenu = true
                });
            }
            else
            {
                _mainItems.Add(new RadialMenuItem
                {
                    Id = "rename",
                    Label = "Renomear",
                    Icon = AppIcons.Edit,
                    IconBrush = new SolidColorBrush(Color.Parse("#60A5FA")),
                    HasSubmenu = false,
                    OnExecute = () =>
                    {
                        _targetWindow.IniciarEdicaoTitulo();
                        FecharMenu();
                    }
                });
            }

            // 6. Excluir
            _mainItems.Add(new RadialMenuItem
            {
                Id = "delete",
                Label = "Excluir",
                Icon = AppIcons.Trash,
                IconBrush = new SolidColorBrush(Color.Parse("#EF4444")),
                HasSubmenu = false,
                OnExecute = () =>
                {
                    _targetWindow.Fechar();
                    FecharMenu();
                }
            });
        }

        private void RenderizarMenuPrincipal()
        {
            PosicionarItensRadiais(_mainItems, isSubmenu: false);
        }

        private void AoClicarItemPrincipal(RadialMenuItem item)
        {
            if (item.HasSubmenu)
            {
                if (_activeSubmenu == item.Id)
                {
                    // Clicou no mesmo submenu que já estava aberto -> fecha submenu e volta ao normal
                    FecharSubmenu();
                }
                else
                {
                    // Abre novo submenu (substituindo qualquer submenu anterior)
                    AbrirSubmenu(item.Id);
                }
            }
            else
            {
                // Ação direta
                FecharSubmenu();
                item.OnExecute?.Invoke();
            }
        }

        private void AbrirSubmenu(string submenuId)
        {
            _activeSubmenu = submenuId;
            LimparItensSubmenuVisual();

            // Oculta os itens do menu principal para que o submenu tenha espaço radial limpo e sem sobreposição
            foreach (var main in _mainItems)
            {
                if (main.VisualElement != null)
                {
                    main.VisualElement.IsVisible = false;
                }
            }

            // Atualiza centro
            _centerHubText.Text = submenuId switch
            {
                "color" => "Cores",
                "color_connected" => "Conectadas",
                "criticality" => "Criticidade",
                "shape" => "Formato",
                _ => "Voltar"
            };
            _centerHubIcon.Data = AppIcons.ArrowLeft;

            _subItems.Clear();

            if (submenuId == "color")
            {
                MontarSubmenuCores();
            }
            else if (submenuId == "color_connected")
            {
                MontarSubmenuCoresConectadas();
            }
            else if (submenuId == "criticality")
            {
                MontarSubmenuCriticidade();
            }
            else if (submenuId == "shape")
            {
                MontarSubmenuFormato();
            }

            RenderizarItensSubmenu();
        }

        private void MontarSubmenuCores()
        {
            foreach (var cor in DraggableWindow.PaletaCoresJanela)
            {
                string hex = cor.Hex;
                string nome = cor.Nome;
                IBrush dotBrush = string.IsNullOrWhiteSpace(hex)
                    ? new SolidColorBrush(Color.Parse("#94A3B8"))
                    : new SolidColorBrush(Color.Parse(hex));

                _subItems.Add(new RadialMenuItem
                {
                    Id = hex,
                    Label = nome,
                    Icon = AppIcons.CircleDot,
                    IconBrush = dotBrush,
                    HasSubmenu = false,
                    OnExecute = () =>
                    {
                        _targetWindow.DefinirCorJanela(hex, notificarMudanca: true);
                        FecharMenu();
                    }
                });
            }

            if (_hasConnections)
            {
                _subItems.Add(new RadialMenuItem
                {
                    Id = "replicate_connected",
                    Label = "🔗 Replicar para Conectadas",
                    Icon = AppIcons.Flowchart,
                    IconBrush = new SolidColorBrush(Color.Parse("#38BDF8")),
                    HasSubmenu = false,
                    OnExecute = () =>
                    {
                        _targetWindow.PropagarCorParaConectadas(_targetWindow.ObterCorEfetiva());
                        FecharMenu();
                    }
                });
            }
        }

        private void MontarSubmenuCoresConectadas()
        {
            foreach (var cor in DraggableWindow.PaletaCoresJanela)
            {
                string hex = cor.Hex;
                string nome = cor.Nome;
                IBrush dotBrush = string.IsNullOrWhiteSpace(hex)
                    ? new SolidColorBrush(Color.Parse("#94A3B8"))
                    : new SolidColorBrush(Color.Parse(hex));

                _subItems.Add(new RadialMenuItem
                {
                    Id = "conn_" + hex,
                    Label = nome,
                    Icon = AppIcons.CircleDot,
                    IconBrush = dotBrush,
                    HasSubmenu = false,
                    OnExecute = () =>
                    {
                        _targetWindow.DefinirCorJanela(hex, notificarMudanca: false);
                        _targetWindow.PropagarCorParaConectadas(hex);
                        FecharMenu();
                    }
                });
            }
        }

        private void MontarSubmenuCriticidade()
        {
            foreach (var crit in DraggableWindow.NiveisCriticidade)
            {
                string key = crit.Key;
                string label = crit.Label;
                IBrush critBrush = !string.IsNullOrWhiteSpace(crit.ColorHex)
                    ? new SolidColorBrush(Color.Parse(crit.ColorHex))
                    : new SolidColorBrush(Color.Parse("#94A3B8"));

                _subItems.Add(new RadialMenuItem
                {
                    Id = key,
                    Label = label,
                    Icon = key == "none" ? AppIcons.Minus : AppIcons.Warning,
                    IconBrush = critBrush,
                    HasSubmenu = false,
                    OnExecute = () =>
                    {
                        _targetWindow.DefinirCriticidade(key);
                        FecharMenu();
                    }
                });
            }
        }

        private void MontarSubmenuFormato()
        {
            _subItems.Add(new RadialMenuItem
            {
                Id = "shape_rect",
                Label = "Retângulo",
                Icon = AppIcons.ShapeRect,
                IconBrush = new SolidColorBrush(Color.Parse("#38BDF8")),
                HasSubmenu = false,
                OnExecute = () =>
                {
                    _targetWindow.AlterarFormatoGeometrico("shape_rect");
                    FecharMenu();
                }
            });

            _subItems.Add(new RadialMenuItem
            {
                Id = "shape_diamond",
                Label = "Losango",
                Icon = AppIcons.ShapeDiamond,
                IconBrush = new SolidColorBrush(Color.Parse("#A855F7")),
                HasSubmenu = false,
                OnExecute = () =>
                {
                    _targetWindow.AlterarFormatoGeometrico("shape_diamond");
                    FecharMenu();
                }
            });

            _subItems.Add(new RadialMenuItem
            {
                Id = "shape_circle",
                Label = "Círculo",
                Icon = AppIcons.ShapeCircle,
                IconBrush = new SolidColorBrush(Color.Parse("#10B981")),
                HasSubmenu = false,
                OnExecute = () =>
                {
                    _targetWindow.AlterarFormatoGeometrico("shape_circle");
                    FecharMenu();
                }
            });
        }

        private void RenderizarItensSubmenu()
        {
            PosicionarItensRadiais(_subItems, isSubmenu: true);
        }

        private void LimparItensSubmenuVisual()
        {
            foreach (var sub in _subItems)
            {
                if (sub.VisualElement != null)
                {
                    Children.Remove(sub.VisualElement);
                    sub.VisualElement = null;
                }
            }
            _subItems.Clear();
        }

        private void FecharSubmenu()
        {
            _activeSubmenu = null;
            LimparItensSubmenuVisual();

            // Restaura visibilidade e opacidade do anel principal
            foreach (var main in _mainItems)
            {
                if (main.VisualElement != null)
                {
                    main.VisualElement.IsVisible = true;
                    main.VisualElement.Opacity = 1.0;
                    main.VisualElement.Background = new SolidColorBrush(Color.Parse("#EE1E293B"));
                    main.VisualElement.BorderBrush = new SolidColorBrush(Color.Parse("#475569"));
                }
            }

            _centerHubText.Text = "Opções";
            _centerHubIcon.Data = AppIcons.Settings;
            Canvas.SetLeft(_centerHub, _center.X - 50);
            Canvas.SetTop(_centerHub, _center.Y - 18);
        }

        private void PosicionarItensRadiais(List<RadialMenuItem> items, bool isSubmenu)
        {
            int total = items.Count;
            if (total == 0) return;

            // 1. Cria e mede individualmente cada botão para saber a largura e altura exatas do texto e ícone
            var pillSizes = new List<Size>();
            double sumWidths = 0;
            double maxW = 0;
            double maxH = 0;

            for (int i = 0; i < total; i++)
            {
                var item = items[i];
                var pill = CriarPillButton(
                    item.Label,
                    item.Icon,
                    item.IconBrush,
                    item.HasSubmenu,
                    isSubitem: isSubmenu);

                item.VisualElement = pill;

                pill.PointerPressed += (s, e) =>
                {
                    e.Handled = true;
                    if (isSubmenu)
                    {
                        item.OnExecute?.Invoke();
                    }
                    else
                    {
                        AoClicarItemPrincipal(item);
                    }
                };

                pill.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double w = pill.DesiredSize.Width > 0 ? Math.Ceiling(pill.DesiredSize.Width) : (isSubmenu ? 110 : 120);
                double h = pill.DesiredSize.Height > 0 ? Math.Ceiling(pill.DesiredSize.Height) : (isSubmenu ? 29 : 33);

                pillSizes.Add(new Size(w, h));
                sumWidths += w;
                if (w > maxW) maxW = w;
                if (h > maxH) maxH = h;
            }

            // 2. Calcula os ângulos proporcionais para cada item de acordo com a largura do texto
            // Isso garante que botões mais largos (ex: "Replicar para Conectadas") recebam uma fatia angular proporcionalmente maior
            double gapPerItem = isSubmenu ? 22.0 : 20.0;
            double totalAllocated = sumWidths + (total * gapPerItem);

            var angles = new List<double>();
            double currentAngle = -Math.PI / 2.0; // Inicia no topo (-90 graus)

            for (int i = 0; i < total; i++)
            {
                double itemSlice = (pillSizes[i].Width + gapPerItem) / totalAllocated * (2.0 * Math.PI);
                double midAngle = currentAngle + (itemSlice / 2.0);
                angles.Add(midAngle);
                currentAngle += itemSlice;
            }

            // 3. Calcula o perímetro mínimo necessário e os raios horizontal (rx) e vertical (ry) da elipse
            double aspect = 1.45; // Formato elíptico harmonioso para telas widescreen
            double cFactor = 7.59; // Fator de perímetro aproximado para elipse com proporção 1.45
            double requiredPerimeter = totalAllocated;

            double ry = Math.Max(isSubmenu ? 110.0 : 85.0, requiredPerimeter / cFactor);
            double rx = ry * aspect;

            // Garante folga segura em relação ao hub central (hub mede 100x36)
            double minRx = 55.0 + (maxW / 2.0) + (isSubmenu ? 25.0 : 18.0);
            double minRy = 22.0 + (maxH / 2.0) + (isSubmenu ? 22.0 : 16.0);

            rx = Math.Max(rx, minRx);
            ry = Math.Max(ry, minRy);

            // 4. Verificação e relaxamento iterativo de colisões entre os retângulos dos botões
            var positions = new List<Point>();
            for (int pass = 0; pass < 12; pass++)
            {
                positions.Clear();
                for (int i = 0; i < total; i++)
                {
                    double a = angles[i];
                    double px = _center.X + rx * Math.Cos(a);
                    double py = _center.Y + ry * Math.Sin(a);
                    positions.Add(new Point(px, py));
                }

                bool hasOverlap = false;
                double marginH = 10.0;
                double marginV = 6.0;

                for (int i = 0; i < total; i++)
                {
                    Rect r1 = new Rect(
                        positions[i].X - pillSizes[i].Width / 2.0 - marginH / 2.0,
                        positions[i].Y - pillSizes[i].Height / 2.0 - marginV / 2.0,
                        pillSizes[i].Width + marginH,
                        pillSizes[i].Height + marginV);

                    for (int j = i + 1; j < total; j++)
                    {
                        Rect r2 = new Rect(
                            positions[j].X - pillSizes[j].Width / 2.0 - marginH / 2.0,
                            positions[j].Y - pillSizes[j].Height / 2.0 - marginV / 2.0,
                            pillSizes[j].Width + marginH,
                            pillSizes[j].Height + marginV);

                        if (r1.Intersects(r2))
                        {
                            hasOverlap = true;
                            break;
                        }
                    }
                    if (hasOverlap) break;
                }

                if (!hasOverlap)
                {
                    break;
                }

                // Expande os raios suavemente até que nenhum botão encoste no outro
                rx *= 1.07;
                ry *= 1.07;
            }

            // 5. Ajuste de translação do conjunto para garantir que nenhum botão vaze para fora da tela
            double minBoundX = double.PositiveInfinity;
            double maxBoundX = double.NegativeInfinity;
            double minBoundY = double.PositiveInfinity;
            double maxBoundY = double.NegativeInfinity;

            for (int i = 0; i < total; i++)
            {
                double x1 = positions[i].X - pillSizes[i].Width / 2.0;
                double x2 = positions[i].X + pillSizes[i].Width / 2.0;
                double y1 = positions[i].Y - pillSizes[i].Height / 2.0;
                double y2 = positions[i].Y + pillSizes[i].Height / 2.0;

                if (x1 < minBoundX) minBoundX = x1;
                if (x2 > maxBoundX) maxBoundX = x2;
                if (y1 < minBoundY) minBoundY = y1;
                if (y2 > maxBoundY) maxBoundY = y2;
            }

            double shiftX = 0;
            double shiftY = 0;
            double pad = 14.0;

            if (minBoundX < pad) shiftX = pad - minBoundX;
            else if (maxBoundX > _viewportSize.Width - pad) shiftX = (_viewportSize.Width - pad) - maxBoundX;

            if (minBoundY < pad) shiftY = pad - minBoundY;
            else if (maxBoundY > _viewportSize.Height - pad) shiftY = (_viewportSize.Height - pad) - maxBoundY;

            // Se houve deslocamento da borda da tela, translada o hub central também para manter o equilíbrio visual
            if (Math.Abs(shiftX) > 0.1 || Math.Abs(shiftY) > 0.1)
            {
                Canvas.SetLeft(_centerHub, _center.X - 50 + shiftX);
                Canvas.SetTop(_centerHub, _center.Y - 18 + shiftY);
            }
            else
            {
                Canvas.SetLeft(_centerHub, _center.X - 50);
                Canvas.SetTop(_centerHub, _center.Y - 18);
            }

            // 6. Posiciona visualmente cada botão no Canvas
            for (int i = 0; i < total; i++)
            {
                var pill = items[i].VisualElement!;
                double left = positions[i].X - (pillSizes[i].Width / 2.0) + shiftX;
                double top = positions[i].Y - (pillSizes[i].Height / 2.0) + shiftY;

                Canvas.SetLeft(pill, left);
                Canvas.SetTop(pill, top);
                pill.ZIndex = isSubmenu ? 26 : 20;

                Children.Add(pill);
            }
        }

        private Border CriarPillButton(
            string label,
            StreamGeometry? icon,
            IBrush? iconBrush,
            bool hasSubmenu,
            bool isSubitem)
        {
            var border = new Border
            {
                Height = isSubitem ? 29 : 33,
                Padding = new Thickness(10, 0, 10, 0),
                CornerRadius = new CornerRadius(isSubitem ? 14 : 16),
                Background = new SolidColorBrush(Color.Parse(isSubitem ? "#F40F172A" : "#EE1E293B")),
                BorderBrush = new SolidColorBrush(Color.Parse(isSubitem ? "#38BDF8" : "#475569")),
                BorderThickness = new Thickness(isSubitem ? 1.4 : 1.2),
                BoxShadow = new BoxShadows(new BoxShadow
                {
                    Blur = isSubitem ? 12 : 8,
                    Spread = 0,
                    Color = Color.FromArgb(isSubitem ? (byte)90 : (byte)60, 0, 0, 0)
                }),
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 7,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (icon != null)
            {
                var iconCtrl = new PathIcon
                {
                    Width = isSubitem ? 11 : 12.5,
                    Height = isSubitem ? 11 : 12.5,
                    Data = icon,
                    Foreground = iconBrush ?? new SolidColorBrush(Color.Parse("#CBD5E1")),
                    VerticalAlignment = VerticalAlignment.Center
                };
                stack.Children.Add(iconCtrl);
            }

            var txt = new TextBlock
            {
                Text = label,
                FontSize = isSubitem ? 11 : 11.5,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            stack.Children.Add(txt);

            if (hasSubmenu)
            {
                var arrow = new TextBlock
                {
                    Text = "▶",
                    FontSize = 8,
                    Foreground = new SolidColorBrush(Color.Parse("#94A3B8")),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2, 0, 0, 0)
                };
                stack.Children.Add(arrow);
            }

            border.Child = stack;

            // Hover styling
            border.PointerEntered += (s, e) =>
            {
                border.Background = new SolidColorBrush(Color.Parse(isSubitem ? "#FF1E293B" : "#FF2563EB"));
                border.BorderBrush = new SolidColorBrush(Color.Parse("#60A5FA"));
            };

            border.PointerExited += (s, e) =>
            {
                bool isSelected = !isSubitem && _activeSubmenu != null && _mainItems.Any(m => m.VisualElement == border && m.Id == _activeSubmenu);
                border.Background = new SolidColorBrush(Color.Parse(isSelected ? "#F02563EB" : (isSubitem ? "#F40F172A" : "#EE1E293B")));
                border.BorderBrush = new SolidColorBrush(Color.Parse(isSelected ? "#38BDF8" : (isSubitem ? "#38BDF8" : "#475569")));
            };

            return border;
        }

        public void FecharMenu()
        {
            OnClosed?.Invoke();
        }
    }
}
