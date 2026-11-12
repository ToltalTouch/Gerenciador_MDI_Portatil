using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace AvaloniaMdiApp
{
    public partial class DraggableWindow
    {
        private Border? _browserToolbar;
        private Button? _btnBrowserBack;
        private Button? _btnBrowserForward;
        private Button? _btnBrowserReload;
        private Button? _btnBrowserHome;
        private Button? _btnBrowserGo;
        private Button? _btnBrowserExternal;
        private TextBox? _txtBrowserUrl;
        private ChromiumWebViewHost? _browserHost;

        private Control CriarConteudoNavegador(string initialUrl)
        {
            string urlInicial = string.IsNullOrWhiteSpace(initialUrl)
                ? "https://www.google.com"
                : initialUrl;

            var browserDock = new DockPanel();

            _browserToolbar = new Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(8, 5),
                Height = 38
            };

            var navGrid = new Grid();
            navGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            navGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            navGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            navGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var leftBtns = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };

            _btnBrowserBack = CriarBotaoFormatacao("", "Voltar", FontWeight.Bold, FontStyle.Normal, width: 26);
            _btnBrowserBack.Content = AppIcons.Create(AppIcons.ArrowLeft, 11, ThemePalette.IconLightGray);
            _btnBrowserForward = CriarBotaoFormatacao("", "Avançar", FontWeight.Bold, FontStyle.Normal, width: 26);
            _btnBrowserForward.Content = AppIcons.Create(AppIcons.ArrowRight, 11, ThemePalette.IconLightGray);
            _btnBrowserReload = CriarBotaoFormatacao("", "Recarregar página", FontWeight.Bold, FontStyle.Normal, width: 26);
            _btnBrowserReload.Content = AppIcons.Create(AppIcons.Refresh, 11, ThemePalette.IconLightGray);
            _btnBrowserHome = CriarBotaoFormatacao("", "Página inicial (Google)", FontWeight.Normal, FontStyle.Normal, width: 26);
            _btnBrowserHome.Content = AppIcons.Create(AppIcons.Home, 11.5, ThemePalette.IconLightGray);

            leftBtns.Children.Add(_btnBrowserBack);
            leftBtns.Children.Add(_btnBrowserForward);
            leftBtns.Children.Add(_btnBrowserReload);
            leftBtns.Children.Add(_btnBrowserHome);

            _txtBrowserUrl = new TextBox
            {
                Text = urlInicial,
                Watermark = "Digite uma URL ou pesquise no Google...",
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 3),
                MinHeight = 26,
                Height = 26,
                FontSize = 12,
                CornerRadius = new CornerRadius(5)
            };

            _btnBrowserGo = new Button
            {
                Content = "Ir",
                Height = 26,
                Padding = new Thickness(10, 0),
                Margin = new Thickness(6, 0, 4, 0),
                CornerRadius = new CornerRadius(5),
                Background = ThemePalette.Accent,
                Foreground = Brushes.White,
                FontSize = 11.5,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            _btnBrowserExternal = CriarBotaoFormatacao("", "Abrir página no navegador externo do sistema", FontWeight.Bold, FontStyle.Normal, width: 26);
            _btnBrowserExternal.Content = AppIcons.Create(AppIcons.ExternalLink, 11.5, ThemePalette.IconLightGray);

            _browserHost = new ChromiumWebViewHost(urlInicial, this)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            _btnBrowserBack.Click += (s, e) => _browserHost.GoBack();
            _btnBrowserForward.Click += (s, e) => _browserHost.GoForward();
            _btnBrowserReload.Click += (s, e) => _browserHost.Reload();
            _btnBrowserHome.Click += (s, e) => _browserHost.Navigate("https://www.google.com");
            _btnBrowserGo.Click += (s, e) => _browserHost.Navigate(_txtBrowserUrl.Text ?? string.Empty);
            _btnBrowserExternal.Click += (s, e) => _browserHost.AbrirNoNavegadorDoSistema();

            _txtBrowserUrl.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    _browserHost.Navigate(_txtBrowserUrl.Text ?? string.Empty);
                }
            };

            _browserHost.UrlChanged += novaUrl =>
            {
                if (_txtBrowserUrl != null && !_txtBrowserUrl.IsFocused)
                {
                    _txtBrowserUrl.Text = novaUrl;
                }
            };

            Grid.SetColumn(leftBtns, 0);
            Grid.SetColumn(_txtBrowserUrl, 1);
            Grid.SetColumn(_btnBrowserGo, 2);
            Grid.SetColumn(_btnBrowserExternal, 3);
            navGrid.Children.Add(leftBtns);
            navGrid.Children.Add(_txtBrowserUrl);
            navGrid.Children.Add(_btnBrowserGo);
            navGrid.Children.Add(_btnBrowserExternal);

            _browserToolbar.Child = navGrid;
            DockPanel.SetDock(_browserToolbar, Dock.Top);

            var browserBodyHost = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            browserBodyHost.Children.Add(new TextBlock
            {
                Text = "Página Web ativa",
                FontSize = 12,
                Foreground = ThemePalette.IconMutedGray,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            browserBodyHost.Children.Add(_browserHost);

            browserDock.Children.Add(_browserToolbar);
            browserDock.Children.Add(browserBodyHost);

            return browserDock;
        }

        public void SetModalOverlaySuspended(bool suspended)
        {
            _browserHost?.SetModalOverlaySuspended(suspended);
        }

        private void AtualizarCoresNavegador()
        {
            if (_browserToolbar == null) return;

            var barBg = _isDarkMode ? ThemePalette.DarkContentBg : ThemePalette.LightContentBg;
            var borderBrush = _isDarkMode ? ThemePalette.DarkBorder : ThemePalette.LightBorder;
            var btnBg = _isDarkMode ? ThemePalette.DarkButtonBg : ThemePalette.LightButtonBg;
            var textBrush = _isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;
            var inputBg = _isDarkMode ? ThemePalette.DarkWindowBg : ThemePalette.LightWindowBg;

            _browserToolbar.Background = barBg;
            _browserToolbar.BorderBrush = borderBrush;

            foreach (var btn in new[] { _btnBrowserBack, _btnBrowserForward, _btnBrowserReload, _btnBrowserHome, _btnBrowserExternal })
            {
                if (btn != null)
                {
                    btn.Background = btnBg;
                    btn.BorderBrush = borderBrush;
                    btn.Foreground = textBrush;
                }
            }

            if (_txtBrowserUrl != null)
            {
                _txtBrowserUrl.Background = inputBg;
                _txtBrowserUrl.BorderBrush = borderBrush;
                _txtBrowserUrl.Foreground = textBrush;
            }
        }
    }
}
