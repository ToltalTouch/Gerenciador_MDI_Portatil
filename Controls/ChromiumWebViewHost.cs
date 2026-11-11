using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace AvaloniaMdiApp
{
    /// <summary>
    /// Host nativo leve para o motor Chromium (Microsoft Edge WebView2).
    /// Compartilha o mesmo CoreWebView2Environment entre janelas para reduzir uso de RAM
    /// e encerra o processo nativo imediatamente ao fechar a janela.
    /// </summary>
    public class ChromiumWebViewHost : NativeControlHost
    {
        private static Task<CoreWebView2Environment>? _sharedEnvTask;

        private IntPtr _hwnd;
        private CoreWebView2Controller? _controller;
        private string _pendingUrl = "https://www.google.com";
        private bool _isDisposed;

        public event Action<string>? UrlChanged;
        public event Action<string>? StatusChanged;

        public string CurrentUrl { get; private set; } = "https://www.google.com";

        public ChromiumWebViewHost(string initialUrl = "https://www.google.com")
        {
            if (!string.IsNullOrWhiteSpace(initialUrl))
            {
                _pendingUrl = NormalizarUrlOuBusca(initialUrl);
                CurrentUrl = _pendingUrl;
            }

            SizeChanged += (s, e) => AtualizarLimitesNativos();
        }

        protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
        {
            if (OperatingSystem.IsWindows())
            {
                _hwnd = CreateWindowExW(
                    0,
                    "Static",
                    string.Empty,
                    WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN,
                    0, 0,
                    Math.Max(1, (int)Bounds.Width),
                    Math.Max(1, (int)Bounds.Height),
                    parent.Handle,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero);

                if (_hwnd != IntPtr.Zero)
                {
                    _ = InicializarWebViewAsync(_hwnd);
                    return new PlatformHandle(_hwnd, "HWND");
                }
            }

            return base.CreateNativeControlCore(parent);
        }

        protected override void DestroyNativeControlCore(IPlatformHandle control)
        {
            DisposeBrowser();

            if (OperatingSystem.IsWindows() && _hwnd != IntPtr.Zero)
            {
                DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
            }

            base.DestroyNativeControlCore(control);
        }

        private static Task<CoreWebView2Environment> ObterAmbienteCompartilhadoAsync()
        {
            if (_sharedEnvTask == null || _sharedEnvTask.IsFaulted)
            {
                string userDataFolder = Path.Combine(Path.GetTempPath(), "AvaloniaMdiApp_WebView2");
                var options = new CoreWebView2EnvironmentOptions("--disable-background-timer-throttling --disable-renderer-backgrounding");
                _sharedEnvTask = CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
            }
            return _sharedEnvTask;
        }

        private async Task InicializarWebViewAsync(IntPtr hwnd)
        {
            try
            {
                StatusChanged?.Invoke("Iniciando Chromium...");
                var env = await ObterAmbienteCompartilhadoAsync();
                if (_isDisposed || _hwnd == IntPtr.Zero) return;

                var controller = await env.CreateCoreWebView2ControllerAsync(hwnd);
                if (_isDisposed || _hwnd == IntPtr.Zero)
                {
                    controller.Close();
                    return;
                }

                _controller = controller;
                _controller.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 15, 23, 42);

                var settings = _controller.CoreWebView2.Settings;
                settings.IsStatusBarEnabled = false;
                settings.AreDefaultContextMenusEnabled = true;
                settings.AreDevToolsEnabled = false;

                _controller.CoreWebView2.SourceChanged += (s, e) =>
                {
                    if (_controller?.CoreWebView2 != null)
                    {
                        string src = _controller.CoreWebView2.Source;
                        CurrentUrl = src;
                        Dispatcher.UIThread.Post(() => UrlChanged?.Invoke(src));
                    }
                };

                _controller.CoreWebView2.NavigationStarting += (s, e) =>
                {
                    Dispatcher.UIThread.Post(() => StatusChanged?.Invoke("Carregando..."));
                };

                _controller.CoreWebView2.NavigationCompleted += (s, e) =>
                {
                    Dispatcher.UIThread.Post(() => StatusChanged?.Invoke(e.IsSuccess ? "Pronto" : " Página indisponível"));
                };

                // Caso um link tente abrir nova janela (target="_blank"), abre na mesma janela
                _controller.CoreWebView2.NewWindowRequested += (s, e) =>
                {
                    e.Handled = true;
                    _controller.CoreWebView2.Navigate(e.Uri);
                };

                AtualizarLimitesNativos();

                if (!string.IsNullOrWhiteSpace(_pendingUrl))
                {
                    _controller.CoreWebView2.Navigate(_pendingUrl);
                }
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(() =>
                    StatusChanged?.Invoke($"WebView2 não encontrado: {ex.Message}"));
            }
        }

        public void AtualizarLimitesNativos()
        {
            if (_controller == null || _hwnd == IntPtr.Zero || _isDisposed) return;

            if (GetClientRect(_hwnd, out RECT rc))
            {
                int w = Math.Max(0, rc.Right - rc.Left);
                int h = Math.Max(0, rc.Bottom - rc.Top);
                _controller.Bounds = new System.Drawing.Rectangle(0, 0, w, h);
                _controller.NotifyParentWindowPositionChanged();
            }
        }

        public void SetBrowserVisible(bool visible)
        {
            IsVisible = visible;
            if (_controller != null && !_isDisposed)
            {
                _controller.IsVisible = visible;
            }
        }

        public void Navigate(string rawInput)
        {
            string target = NormalizarUrlOuBusca(rawInput);
            _pendingUrl = target;
            CurrentUrl = target;
            UrlChanged?.Invoke(target);

            if (_controller?.CoreWebView2 != null && !_isDisposed)
            {
                try
                {
                    _controller.CoreWebView2.Navigate(target);
                }
                catch { }
            }
        }

        public void GoBack()
        {
            if (_controller?.CoreWebView2?.CanGoBack == true)
            {
                _controller.CoreWebView2.GoBack();
            }
        }

        public void GoForward()
        {
            if (_controller?.CoreWebView2?.CanGoForward == true)
            {
                _controller.CoreWebView2.GoForward();
            }
        }

        public void Reload()
        {
            _controller?.CoreWebView2?.Reload();
        }

        public static string NormalizarUrlOuBusca(string input)
        {
            string trimmed = (input ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
                return "https://www.google.com";

            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            if (!trimmed.Contains(' ') && trimmed.Contains('.'))
            {
                return "https://" + trimmed;
            }

            return "https://www.google.com/search?q=" + Uri.EscapeDataString(trimmed);
        }

        public void DisposeBrowser()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            try
            {
                _controller?.Close();
                _controller = null;
            }
            catch { }
        }

        private const int WS_CHILD = 0x40000000;
        private const int WS_VISIBLE = 0x10000000;
        private const int WS_CLIPCHILDREN = 0x02000000;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowExW(
            int dwExStyle,
            string lpClassName,
            string lpWindowName,
            int dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
    }
}
