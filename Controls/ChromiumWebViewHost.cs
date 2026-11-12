using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.Web.WebView2.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace AvaloniaMdiApp
{
    /// <summary>
    /// Host nativo multiplataforma para navegador web:
    /// - No Windows: utiliza Microsoft Edge WebView2 (Chromium).
    /// - No Linux: utiliza WebKit2GTK (libwebkit2gtk-4.1 / 4.0) embutido via X11 GtkPlug.
    /// </summary>
    public class ChromiumWebViewHost : NativeControlHost
    {
        private static Task<CoreWebView2Environment>? _sharedEnvTask;
        private static bool _linuxGtkInitialized;
        private static bool _linuxGtkAvailable;
        private static DispatcherTimer? _linuxGtkPumpTimer;
        private static int _activeLinuxBrowsers;
        private static bool _resolverRegistered;

        // Windows handles
        private IntPtr _hwnd;
        private CoreWebView2Controller? _controller;

        // Linux GTK + WebKit2 handles
        private IntPtr _gtkPlug;
        private IntPtr _webkitView;
        private DispatcherTimer? _linuxUrlPollTimer;

        private string _pendingUrl = "https://www.google.com";
        private bool _isDisposed;

        public event Action<string>? UrlChanged;
        public event Action<string>? StatusChanged;

        public string CurrentUrl { get; private set; } = "https://www.google.com";

        static ChromiumWebViewHost()
        {
            RegistrarResolverLinux();
        }

        public ChromiumWebViewHost(string initialUrl = "https://www.google.com")
        {
            if (!string.IsNullOrWhiteSpace(initialUrl))
            {
                _pendingUrl = NormalizarUrlOuBusca(initialUrl);
                CurrentUrl = _pendingUrl;
            }

            SizeChanged += (s, e) => AtualizarLimitesNativos();
        }

        private static void RegistrarResolverLinux()
        {
            if (_resolverRegistered || !OperatingSystem.IsLinux()) return;
            _resolverRegistered = true;

            NativeLibrary.SetDllImportResolver(typeof(ChromiumWebViewHost).Assembly, (libraryName, assembly, searchPath) =>
            {
                if (libraryName == "webkit2gtk")
                {
                    string[] candidates =
                    {
                        "libwebkit2gtk-4.1.so.0",
                        "libwebkit2gtk-4.0.so.37",
                        "/usr/lib/x86_64-linux-gnu/libwebkit2gtk-4.1.so.0",
                        "/usr/lib/x86_64-linux-gnu/libwebkit2gtk-4.0.so.37"
                    };

                    foreach (var candidate in candidates)
                    {
                        if (NativeLibrary.TryLoad(candidate, out IntPtr handle))
                        {
                            return handle;
                        }
                    }
                }
                return IntPtr.Zero;
            });
        }

        private static bool GarantirGtkLinuxInicializado()
        {
            if (_linuxGtkInitialized) return _linuxGtkAvailable;
            _linuxGtkInitialized = true;

            try
            {
                // Evita falhas de renderização/sandbox do WebKit2GTK no Ubuntu 24.04/26.04 e Wayland/XWayland
                Environment.SetEnvironmentVariable("WEBKIT_DISABLE_DMABUF_RENDERER", "1");
                Environment.SetEnvironmentVariable("WEBKIT_DISABLE_COMPOSITING_MODE", "1");
                Environment.SetEnvironmentVariable("WEBKIT_DISABLE_SANDBOX_THIS_IS_DANGEROUS", "1");

                // Força o backend X11 no GDK para compatibilidade com o XID do Avalonia
                gdk_set_allowed_backends("x11");

                int argc = 0;
                IntPtr argv = IntPtr.Zero;
                _linuxGtkAvailable = gtk_init_check(ref argc, ref argv);

                if (_linuxGtkAvailable && _linuxGtkPumpTimer == null)
                {
                    _linuxGtkPumpTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(15)
                    };
                    _linuxGtkPumpTimer.Tick += (s, e) =>
                    {
                        try
                        {
                            int maxIter = 12;
                            while (maxIter-- > 0 && gtk_events_pending())
                            {
                                gtk_main_iteration_do(false);
                            }
                        }
                        catch { }
                    };
                    _linuxGtkPumpTimer.Start();
                }
            }
            catch
            {
                _linuxGtkAvailable = false;
            }

            return _linuxGtkAvailable;
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
            else if (OperatingSystem.IsLinux())
            {
                try
                {
                    if (GarantirGtkLinuxInicializado())
                    {
                        IntPtr plug = gtk_plug_new(IntPtr.Zero);
                        if (plug != IntPtr.Zero)
                        {
                            IntPtr webView = webkit_web_view_new();
                            if (webView != IntPtr.Zero)
                            {
                                _gtkPlug = plug;
                                _webkitView = webView;

                                int w = Math.Max(200, (int)Bounds.Width);
                                int h = Math.Max(140, (int)Bounds.Height);
                                gtk_window_resize(_gtkPlug, w, h);
                                gtk_widget_set_size_request(_webkitView, w, h);

                                gtk_container_add(_gtkPlug, _webkitView);
                                gtk_widget_realize(_gtkPlug);
                                gtk_widget_show_all(_gtkPlug);

                                IntPtr xid = gtk_plug_get_id(_gtkPlug);
                                if (xid != IntPtr.Zero)
                                {
                                    _activeLinuxBrowsers++;
                                    _linuxGtkPumpTimer?.Start();
                                    webkit_web_view_load_uri(_webkitView, _pendingUrl);
                                    StatusChanged?.Invoke("Pronto (WebKit2GTK)");
                                    IniciarMonitorUrlLinux();
                                    return new PlatformHandle(xid, "XID");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(() =>
                        StatusChanged?.Invoke($"Erro no motor web Linux: {ex.Message}"));
                }
            }

            return base.CreateNativeControlCore(parent);
        }

        private void IniciarMonitorUrlLinux()
        {
            _linuxUrlPollTimer?.Stop();
            _linuxUrlPollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(350)
            };
            _linuxUrlPollTimer.Tick += (s, e) =>
            {
                if (_isDisposed || _webkitView == IntPtr.Zero) return;
                try
                {
                    IntPtr uriPtr = webkit_web_view_get_uri(_webkitView);
                    if (uriPtr != IntPtr.Zero)
                    {
                        string? uri = Marshal.PtrToStringUTF8(uriPtr);
                        if (!string.IsNullOrWhiteSpace(uri) && !string.Equals(uri, CurrentUrl, StringComparison.Ordinal))
                        {
                            CurrentUrl = uri;
                            UrlChanged?.Invoke(uri);
                        }
                    }
                }
                catch { }
            };
            _linuxUrlPollTimer.Start();
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
                    Dispatcher.UIThread.Post(() => StatusChanged?.Invoke(e.IsSuccess ? "Pronto" : "Página indisponível"));
                };

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
            if (_isDisposed) return;

            if (OperatingSystem.IsWindows())
            {
                if (_controller == null || _hwnd == IntPtr.Zero) return;

                if (GetClientRect(_hwnd, out RECT rc))
                {
                    int w = Math.Max(0, rc.Right - rc.Left);
                    int h = Math.Max(0, rc.Bottom - rc.Top);
                    _controller.Bounds = new System.Drawing.Rectangle(0, 0, w, h);
                    _controller.NotifyParentWindowPositionChanged();
                }
            }
            else if (OperatingSystem.IsLinux())
            {
                if (_gtkPlug != IntPtr.Zero && _webkitView != IntPtr.Zero)
                {
                    int w = Math.Max(1, (int)Bounds.Width);
                    int h = Math.Max(1, (int)Bounds.Height);
                    try
                    {
                        gtk_widget_set_size_request(_webkitView, w, h);
                        gtk_window_resize(_gtkPlug, w, h);
                    }
                    catch { }
                }
            }
        }

        public void SetBrowserVisible(bool visible)
        {
            IsVisible = visible;
            if (OperatingSystem.IsWindows() && _controller != null && !_isDisposed)
            {
                _controller.IsVisible = visible;
            }
            else if (OperatingSystem.IsLinux() && _gtkPlug != IntPtr.Zero && !_isDisposed)
            {
                try
                {
                    if (visible)
                    {
                        gtk_widget_show_all(_gtkPlug);
                        _linuxUrlPollTimer?.Start();
                    }
                    else
                    {
                        gtk_widget_hide(_gtkPlug);
                        _linuxUrlPollTimer?.Stop();
                    }
                }
                catch { }
            }
        }

        public void SetHibernated(bool hibernated)
        {
            if (_isDisposed) return;

            if (OperatingSystem.IsLinux())
            {
                if (hibernated)
                {
                    _linuxUrlPollTimer?.Stop();
                }
                else if (IsVisible && _webkitView != IntPtr.Zero)
                {
                    _linuxUrlPollTimer?.Start();
                }
            }
        }

        public void Navigate(string rawInput)
        {
            string target = NormalizarUrlOuBusca(rawInput);
            _pendingUrl = target;
            CurrentUrl = target;
            UrlChanged?.Invoke(target);

            if (OperatingSystem.IsWindows() && _controller?.CoreWebView2 != null && !_isDisposed)
            {
                try
                {
                    _controller.CoreWebView2.Navigate(target);
                }
                catch { }
            }
            else if (OperatingSystem.IsLinux() && _webkitView != IntPtr.Zero && !_isDisposed)
            {
                try
                {
                    webkit_web_view_load_uri(_webkitView, target);
                }
                catch { }
            }
        }

        public void AbrirNoNavegadorDoSistema()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = CurrentUrl,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        public void GoBack()
        {
            if (OperatingSystem.IsWindows() && _controller?.CoreWebView2?.CanGoBack == true)
            {
                _controller.CoreWebView2.GoBack();
            }
            else if (OperatingSystem.IsLinux() && _webkitView != IntPtr.Zero && !_isDisposed)
            {
                try
                {
                    if (webkit_web_view_can_go_back(_webkitView))
                    {
                        webkit_web_view_go_back(_webkitView);
                    }
                }
                catch { }
            }
        }

        public void GoForward()
        {
            if (OperatingSystem.IsWindows() && _controller?.CoreWebView2?.CanGoForward == true)
            {
                _controller.CoreWebView2.GoForward();
            }
            else if (OperatingSystem.IsLinux() && _webkitView != IntPtr.Zero && !_isDisposed)
            {
                try
                {
                    if (webkit_web_view_can_go_forward(_webkitView))
                    {
                        webkit_web_view_go_forward(_webkitView);
                    }
                }
                catch { }
            }
        }

        public void Reload()
        {
            if (OperatingSystem.IsWindows())
            {
                _controller?.CoreWebView2?.Reload();
            }
            else if (OperatingSystem.IsLinux() && _webkitView != IntPtr.Zero && !_isDisposed)
            {
                try
                {
                    webkit_web_view_reload(_webkitView);
                }
                catch { }
            }
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

            _linuxUrlPollTimer?.Stop();
            _linuxUrlPollTimer = null;

            try
            {
                _controller?.Close();
                _controller = null;
            }
            catch { }

            if (OperatingSystem.IsLinux() && _gtkPlug != IntPtr.Zero)
            {
                try
                {
                    gtk_widget_destroy(_gtkPlug);
                }
                catch { }
                _gtkPlug = IntPtr.Zero;
                _webkitView = IntPtr.Zero;

                _activeLinuxBrowsers = Math.Max(0, _activeLinuxBrowsers - 1);
                if (_activeLinuxBrowsers == 0)
                {
                    _linuxGtkPumpTimer?.Stop();
                }
            }
        }

        // Win32 P/Invoke
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

        // Linux GTK3 / GDK / WebKit2GTK P/Invoke
        [DllImport("libgdk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
        private static extern void gdk_set_allowed_backends([MarshalAs(UnmanagedType.LPUTF8Str)] string backends);

        [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool gtk_init_check(ref int argc, ref IntPtr argv);

        [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool gtk_events_pending();

        [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool gtk_main_iteration_do(bool blocking);

        [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr gtk_plug_new(IntPtr socketId);

        [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr gtk_plug_get_id(IntPtr plug);

        [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
        private static extern void gtk_container_add(IntPtr container, IntPtr widget);

        [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
        private static extern void gtk_widget_realize(IntPtr widget);

        [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
        private static extern void gtk_widget_show_all(IntPtr widget);

        [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
        private static extern void gtk_widget_hide(IntPtr widget);

        [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
        private static extern void gtk_widget_destroy(IntPtr widget);

        [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
        private static extern void gtk_widget_set_size_request(IntPtr widget, int width, int height);

        [DllImport("libgtk-3.so.0", CallingConvention = CallingConvention.Cdecl)]
        private static extern void gtk_window_resize(IntPtr window, int width, int height);

        [DllImport("webkit2gtk", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr webkit_web_context_get_default();

        [DllImport("webkit2gtk", CallingConvention = CallingConvention.Cdecl)]
        private static extern void webkit_web_context_set_sandbox_enabled(IntPtr context, bool enabled);

        [DllImport("webkit2gtk", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr webkit_web_view_new();

        [DllImport("webkit2gtk", CallingConvention = CallingConvention.Cdecl)]
        private static extern void webkit_web_view_load_uri(IntPtr webView, [MarshalAs(UnmanagedType.LPUTF8Str)] string uri);

        [DllImport("webkit2gtk", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr webkit_web_view_get_uri(IntPtr webView);

        [DllImport("webkit2gtk", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool webkit_web_view_can_go_back(IntPtr webView);

        [DllImport("webkit2gtk", CallingConvention = CallingConvention.Cdecl)]
        private static extern void webkit_web_view_go_back(IntPtr webView);

        [DllImport("webkit2gtk", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool webkit_web_view_can_go_forward(IntPtr webView);

        [DllImport("webkit2gtk", CallingConvention = CallingConvention.Cdecl)]
        private static extern void webkit_web_view_go_forward(IntPtr webView);

        [DllImport("webkit2gtk", CallingConvention = CallingConvention.Cdecl)]
        private static extern void webkit_web_view_reload(IntPtr webView);
    }
}
