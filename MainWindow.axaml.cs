using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using System.Collections.Generic;

namespace AvaloniaMdiApp
{
    public partial class MainWindow : Window
    {
        private int _contadorJanelas = 0;
        private readonly List<DraggableWindow> _ativasJanelas = new();

        public MainWindow()
        {
            InitializeComponent();
            
            Loaded += (s, e) => {
                CarregarConfiguracoesIniciais();
                CarregarJanelasSalvas();
            };
            
            Closing += (s, e) => SalvarJanelasAtuais();
        }

        private void CarregarConfiguracoesIniciais()
        {
            var settings = SettingsManager.Carregar();
            AplicarTema(settings.IsDarkMode);
        }

        private void AplicarTema(bool isDarkMode)
        {
            if (Workspace != null)
            {
                Workspace.Background = isDarkMode 
                    ? SolidColorBrush.Parse("#2d2d30") 
                    : SolidColorBrush.Parse("#f3f3f3");
            }
        }

        private void CarregarJanelasSalvas()
        {
            var settings = SettingsManager.Carregar();

            if (settings.SavedWindows.Count == 0)
            {
                CriarJanelaInterna("Janela Inicial 1", 50, 50, 320, 220);
                return;
            }

            foreach (var winData in settings.SavedWindows)
            {
                CriarJanelaInterna(winData.Title, winData.X, winData.Y, winData.Width, winData.Height);
            }
        }

        private void SalvarJanelasAtuais()
        {
            var settings = SettingsManager.Carregar();
            settings.SavedWindows.Clear();

            foreach (var win in _ativasJanelas)
            {
                settings.SavedWindows.Add(new WindowStateData
                {
                    Title = win.WindowTitle,
                    X = Canvas.GetLeft(win),
                    Y = Canvas.GetTop(win),
                    Width = win.Width,
                    Height = win.Height
                });
            }

            SettingsManager.Salvar(settings);
        }

        private void AbrirNovaJanela_Click(object? sender, RoutedEventArgs e)
        {
            _contadorJanelas++;
            CriarJanelaInterna($"Sub-janela #{_contadorJanelas}", 50 + (_contadorJanelas * 20) % 300, 50 + (_contadorJanelas * 20) % 200, 320, 220);
            
            var settings = SettingsManager.Carregar();
            if (settings.EnableNotifications)
            {
                ToastNotification.Mostrar(Workspace, $"Sub-janela #{_contadorJanelas} aberta!", "success");
            }
        }

        private void AbrirConfiguracoes_Click(object? sender, RoutedEventArgs e)
        {
            var settings = SettingsManager.Carregar();
            var settingsWin = new SettingsWindow(Workspace, settings);
            
            settingsWin.OnSettingsChanged += (isDark, notif) => {
                settings.IsDarkMode = isDark;
                settings.EnableNotifications = notif;
                AplicarTema(isDark);
                SettingsManager.Salvar(settings);
            };

            Workspace.Children.Add(settingsWin);
        }

        private void CriarJanelaInterna(string title, double x, double y, double width, double height)
        {
            var win = new DraggableWindow(Workspace, AppTaskBar, title, width, height, x, y);
            
            _ativasJanelas.Add(win);
            win.OnClosed += () => _ativasJanelas.Remove(win);

            AppTaskBar.RegistrarJanela(win, title);
            Workspace.Children.Add(win);
            ZIndexManager.BringToFront(win);
        }
    }
}
