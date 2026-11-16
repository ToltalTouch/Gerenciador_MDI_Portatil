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
        private string? _activeSubmenu = null;

        private readonly List<RadialMenuItem> _mainItems = new();
        private readonly List<RadialMenuItem> _subItems = new();
        private readonly Border _centerHub;
        private readonly TextBlock _centerHubText;
        private readonly PathIcon _centerHubIcon;

        public event Action? OnClosed;

        public RadialWindowMenu(DraggableWindow targetWindow, Point centerPoint)
        {
            _targetWindow = targetWindow;
            _center = centerPoint;

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

            // 1. Mudar Cor (Submenu)
            _mainItems.Add(new RadialMenuItem
            {
                Id = "color",
                Label = "Mudar Cor",
                Icon = AppIcons.Palette,
                IconBrush = new SolidColorBrush(Color.Parse("#F43F5E")),
                HasSubmenu = true
            });

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
            double rx = 115;
            double ry = 75;
            int total = _mainItems.Count;

            for (int i = 0; i < total; i++)
            {
                var item = _mainItems[i];
                double angulo = -Math.PI / 2.0 + (i * 2.0 * Math.PI / total);

                double px = _center.X + rx * Math.Cos(angulo);
                double py = _center.Y + ry * Math.Sin(angulo);

                var pill = CriarPillButton(
                    item.Label,
                    item.Icon,
                    item.IconBrush,
                    item.HasSubmenu,
                    isSubitem: false);

                item.VisualElement = pill;

                pill.PointerPressed += (s, e) =>
                {
                    e.Handled = true;
                    AoClicarItemPrincipal(item);
                };

                // Medição dinâmica para centralizar pill
                pill.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double w = pill.DesiredSize.Width > 0 ? pill.DesiredSize.Width : 110;
                double h = pill.DesiredSize.Height > 0 ? pill.DesiredSize.Height : 32;

                Canvas.SetLeft(pill, px - w / 2.0);
                Canvas.SetTop(pill, py - h / 2.0);
                pill.ZIndex = 20;

                Children.Add(pill);
            }
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

            // Atualiza atenuação do anel principal (deixa os outros itens meio invisíveis)
            foreach (var main in _mainItems)
            {
                if (main.VisualElement != null)
                {
                    bool isSelected = main.Id == submenuId;
                    main.VisualElement.Opacity = isSelected ? 1.0 : 0.22;
                    main.VisualElement.BorderBrush = isSelected
                        ? new SolidColorBrush(Color.Parse("#38BDF8"))
                        : new SolidColorBrush(Color.Parse("#475569"));
                    main.VisualElement.Background = isSelected
                        ? new SolidColorBrush(Color.Parse("#F02563EB"))
                        : new SolidColorBrush(Color.Parse("#CC1E293B"));
                }
            }

            // Atualiza centro
            _centerHubText.Text = submenuId switch
            {
                "color" => "Cores",
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
            double rx = 185;
            double ry = 125;
            int total = _subItems.Count;
            if (total == 0) return;

            for (int i = 0; i < total; i++)
            {
                var sub = _subItems[i];
                double angulo = -Math.PI / 2.0 + (i * 2.0 * Math.PI / total);

                double px = _center.X + rx * Math.Cos(angulo);
                double py = _center.Y + ry * Math.Sin(angulo);

                var pill = CriarPillButton(
                    sub.Label,
                    sub.Icon,
                    sub.IconBrush,
                    hasSubmenu: false,
                    isSubitem: true);

                sub.VisualElement = pill;

                pill.PointerPressed += (s, e) =>
                {
                    e.Handled = true;
                    sub.OnExecute?.Invoke();
                };

                pill.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double w = pill.DesiredSize.Width > 0 ? pill.DesiredSize.Width : 105;
                double h = pill.DesiredSize.Height > 0 ? pill.DesiredSize.Height : 28;

                Canvas.SetLeft(pill, px - w / 2.0);
                Canvas.SetTop(pill, py - h / 2.0);
                pill.ZIndex = 25;

                Children.Add(pill);
            }
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

            // Restaura opacidade do anel principal
            foreach (var main in _mainItems)
            {
                if (main.VisualElement != null)
                {
                    main.VisualElement.Opacity = 1.0;
                    main.VisualElement.Background = new SolidColorBrush(Color.Parse("#EE1E293B"));
                    main.VisualElement.BorderBrush = new SolidColorBrush(Color.Parse("#475569"));
                }
            }

            _centerHubText.Text = "Opções";
            _centerHubIcon.Data = AppIcons.Settings;
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
