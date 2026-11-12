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
        private IntPtr _parentHwnd;
        private IntPtr _oldParentWndProc;
        private WndProcDelegate? _parentWndProcDelegate;
        private CoreWebView2Controller? _controller;
        private TopLevel? _attachedTopLevel;
        private readonly DraggableWindow? _ownerWindow;
        private bool _userVisible = true;
        private bool _isModalSuspended;
        private int _lastPhysX = -1;
        private int _lastPhysY = -1;
        private int _lastPhysW = -1;
        private int _lastPhysH = -1;
        private long _lastRegionHash;

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

        public ChromiumWebViewHost(string initialUrl = "https://www.google.com", DraggableWindow? ownerWindow = null)
        {
            _ownerWindow = ownerWindow;
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

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            if (_attachedTopLevel != null)
            {
                _attachedTopLevel.LayoutUpdated -= OnTopLevelLayoutUpdated;
            }

            _attachedTopLevel = TopLevel.GetTopLevel(this);
            if (_attachedTopLevel != null)
            {
                _attachedTopLevel.LayoutUpdated += OnTopLevelLayoutUpdated;
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            if (_attachedTopLevel != null)
            {
                _attachedTopLevel.LayoutUpdated -= OnTopLevelLayoutUpdated;
                _attachedTopLevel = null;
            }

            base.OnDetachedFromVisualTree(e);
        }

        private void OnTopLevelLayoutUpdated(object? sender, EventArgs e)
        {
            AtualizarLimitesNativos();
        }

        protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
        {
            if (OperatingSystem.IsWindows())
            {
                _parentHwnd = parent.Handle;
                InstalarFiltroParentHwnd(_parentHwnd);

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
            RemoverFiltroParentHwnd();
            DisposeBrowser();

            if (OperatingSystem.IsWindows() && _hwnd != IntPtr.Zero)
            {
                DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
            }
            _parentHwnd = IntPtr.Zero;

            base.DestroyNativeControlCore(control);
        }

        private void InstalarFiltroParentHwnd(IntPtr parentHwnd)
        {
            if (!OperatingSystem.IsWindows() || parentHwnd == IntPtr.Zero || _oldParentWndProc != IntPtr.Zero)
                return;

            try
            {
                _parentWndProcDelegate = ParentHwndWndProc;
                IntPtr fnPtr = Marshal.GetFunctionPointerForDelegate(_parentWndProcDelegate);
                _oldParentWndProc = SetWindowLongPtrW(parentHwnd, GWLP_WNDPROC, fnPtr);
            }
            catch { }
        }

        private void RemoverFiltroParentHwnd()
        {
            if (!OperatingSystem.IsWindows() || _parentHwnd == IntPtr.Zero || _oldParentWndProc == IntPtr.Zero)
                return;

            try
            {
                SetWindowLongPtrW(_parentHwnd, GWLP_WNDPROC, _oldParentWndProc);
            }
            catch { }
            _oldParentWndProc = IntPtr.Zero;
            _parentWndProcDelegate = null;
        }

        private IntPtr ParentHwndWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_WINDOWPOSCHANGING && lParam != IntPtr.Zero && _lastPhysW > 0 && _lastPhysH > 0)
            {
                try
                {
                    var wp = Marshal.PtrToStructure<WINDOWPOS>(lParam);
                    if (wp.cx != _lastPhysW || wp.cy != _lastPhysH)
                    {
                        wp.cx = _lastPhysW;
                        wp.cy = _lastPhysH;
                        Marshal.StructureToPtr(wp, lParam, false);
                    }
                }
                catch { }
            }
            else if (msg == WM_ERASEBKGND)
            {
                return new IntPtr(1);
            }

            return _oldParentWndProc != IntPtr.Zero
                ? CallWindowProcW(_oldParentWndProc, hWnd, msg, wParam, lParam)
                : DefWindowProcW(hWnd, msg, wParam, lParam);
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
                if (_hwnd == IntPtr.Zero) return;

                IntPtr parentHandle = _parentHwnd != IntPtr.Zero ? _parentHwnd : GetParent(_hwnd);
                if (parentHandle != IntPtr.Zero && _oldParentWndProc == IntPtr.Zero)
                {
                    _parentHwnd = parentHandle;
                    InstalarFiltroParentHwnd(parentHandle);
                }

                if (!_userVisible || _isModalSuspended)
                {
                    return;
                }

                var topLevel = _attachedTopLevel ?? TopLevel.GetTopLevel(this);
                if (topLevel == null || Bounds.Width <= 0 || Bounds.Height <= 0) return;

                Point? topLeft = this.TranslatePoint(new Point(0, 0), topLevel);
                Point? bottomRight = this.TranslatePoint(new Point(Bounds.Width, Bounds.Height), topLevel);
                if (topLeft == null || bottomRight == null) return;

                double scaling = topLevel.RenderScaling > 0 ? topLevel.RenderScaling : 1.0;
                double dipX = topLeft.Value.X;
                double dipY = topLeft.Value.Y;
                double dipW = Math.Max(1, bottomRight.Value.X - topLeft.Value.X);
                double dipH = Math.Max(1, bottomRight.Value.Y - topLeft.Value.Y);

                int physX = (int)Math.Round(dipX * scaling);
                int physY = (int)Math.Round(dipY * scaling);
                int physW = Math.Max(1, (int)Math.Round(dipW * scaling));
                int physH = Math.Max(1, (int)Math.Round(dipH * scaling));

                bool clientMismatch = false;
                if (GetClientRect(_hwnd, out RECT rc))
                {
                    int curW = rc.Right - rc.Left;
                    int curH = rc.Bottom - rc.Top;
                    if (Math.Abs(curW - physW) > 1 || Math.Abs(curH - physH) > 1)
                    {
                        clientMismatch = true;
                    }
                }

                if (!clientMismatch && parentHandle != IntPtr.Zero && GetClientRect(parentHandle, out RECT prc))
                {
                    int pW = prc.Right - prc.Left;
                    int pH = prc.Bottom - prc.Top;
                    if (Math.Abs(pW - physW) > 1 || Math.Abs(pH - physH) > 1)
                    {
                        clientMismatch = true;
                    }
                }

                bool posOrSizeChanged =
                    clientMismatch ||
                    physX != _lastPhysX ||
                    physY != _lastPhysY ||
                    physW != _lastPhysW ||
                    physH != _lastPhysH;

                if (posOrSizeChanged)
                {
                    _lastPhysX = physX;
                    _lastPhysY = physY;
                    _lastPhysW = physW;
                    _lastPhysH = physH;

                    if (parentHandle != IntPtr.Zero)
                    {
                        SetWindowPos(parentHandle, IntPtr.Zero, physX, physY, physW, physH, SWP_NOZORDER | SWP_NOACTIVATE);
                    }
                    SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, physW, physH, SWP_NOZORDER | SWP_NOACTIVATE);
                }

                if (parentHandle != IntPtr.Zero)
                {
                    AtualizarRegiaoRecorteWin32(parentHandle, topLevel, scaling, physX, physY, physW, physH, posOrSizeChanged);
                }

                if (_controller != null)
                {
                    var currentBounds = _controller.Bounds;
                    if (posOrSizeChanged || currentBounds.Width != physW || currentBounds.Height != physH)
                    {
                        _controller.Bounds = new System.Drawing.Rectangle(0, 0, physW, physH);
                        _controller.NotifyParentWindowPositionChanged();
                    }

                    double visualScale = Bounds.Width > 1 ? Math.Clamp(dipW / Bounds.Width, 0.25, 3.0) : 1.0;
                    if (Math.Abs(_controller.ZoomFactor - visualScale) > 0.02)
                    {
                        try
                        {
                            _controller.ZoomFactor = visualScale;
                        }
                        catch { }
                    }
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

        private void AtualizarRegiaoRecorteWin32(
            IntPtr parentHandle,
            TopLevel topLevel,
            double scaling,
            int physX,
            int physY,
            int physW,
            int physH,
            bool forceUpdate)
        {
            long hash = HashCode.Combine(physX, physY, physW, physH);
            var workspace = _ownerWindow?.Workspace;

            if (workspace != null && _ownerWindow != null)
            {
                int ownerIdx = workspace.Children.IndexOf(_ownerWindow);
                int ownerZ = _ownerWindow.ZIndex;

                for (int i = 0; i < workspace.Children.Count; i++)
                {
                    var child = workspace.Children[i];
                    if (child == _ownerWindow || !child.IsVisible || child is Avalonia.Controls.Shapes.Path)
                        continue;

                    bool isAbove = child.ZIndex > ownerZ || (child.ZIndex == ownerZ && i > ownerIdx);
                    if (!isAbove)
                        continue;

                    Point? cTL = child.TranslatePoint(new Point(0, 0), topLevel);
                    Point? cBR = child.TranslatePoint(new Point(child.Bounds.Width, child.Bounds.Height), topLevel);
                    if (cTL == null || cBR == null)
                        continue;

                    int cx1 = (int)Math.Floor(cTL.Value.X * scaling) - physX;
                    int cy1 = (int)Math.Floor(cTL.Value.Y * scaling) - physY;
                    int cx2 = (int)Math.Ceiling(cBR.Value.X * scaling) - physX;
                    int cy2 = (int)Math.Ceiling(cBR.Value.Y * scaling) - physY;

                    if (cx2 > 0 && cy2 > 0 && cx1 < physW && cy1 < physH)
                    {
                        hash = HashCode.Combine(hash, cx1, cy1, cx2, cy2);
                    }
                }
            }

            if (!forceUpdate && hash == _lastRegionHash)
                return;

            _lastRegionHash = hash;

            IntPtr finalRgn = CreateRectRgn(0, 0, physW, physH);
            if (finalRgn == IntPtr.Zero)
                return;

            if (workspace != null && _ownerWindow != null)
            {
                int ownerIdx = workspace.Children.IndexOf(_ownerWindow);
                int ownerZ = _ownerWindow.ZIndex;

                for (int i = 0; i < workspace.Children.Count; i++)
                {
                    var child = workspace.Children[i];
                    if (child == _ownerWindow || !child.IsVisible || child is Avalonia.Controls.Shapes.Path)
                        continue;

                    bool isAbove = child.ZIndex > ownerZ || (child.ZIndex == ownerZ && i > ownerIdx);
                    if (!isAbove)
                        continue;

                    Point? cTL = child.TranslatePoint(new Point(0, 0), topLevel);
                    Point? cBR = child.TranslatePoint(new Point(child.Bounds.Width, child.Bounds.Height), topLevel);
                    if (cTL == null || cBR == null)
                        continue;

                    int cx1 = (int)Math.Floor(cTL.Value.X * scaling) - physX;
                    int cy1 = (int)Math.Floor(cTL.Value.Y * scaling) - physY;
                    int cx2 = (int)Math.Ceiling(cBR.Value.X * scaling) - physX;
                    int cy2 = (int)Math.Ceiling(cBR.Value.Y * scaling) - physY;

                    if (cx2 > 0 && cy2 > 0 && cx1 < physW && cy1 < physH)
                    {
                        IntPtr occRgn = CreateRectRgn(cx1, cy1, cx2, cy2);
                        if (occRgn != IntPtr.Zero)
                        {
                            CombineRgn(finalRgn, finalRgn, occRgn, RGN_DIFF);
                            DeleteObject(occRgn);
                        }
                    }
                }
            }

            if (SetWindowRgn(parentHandle, finalRgn, true) == 0)
            {
                DeleteObject(finalRgn);
            }
        }

        public void SetBrowserVisible(bool visible)
        {
            _userVisible = visible;
            AplicarVisibilidadeEfetiva();
        }

        public void SetModalOverlaySuspended(bool suspended)
        {
            if (_isModalSuspended == suspended) return;
            _isModalSuspended = suspended;
            AplicarVisibilidadeEfetiva();
        }

        private void AplicarVisibilidadeEfetiva()
        {
            bool effectiveVisible = _userVisible && !_isModalSuspended;
            IsVisible = effectiveVisible;

            if (OperatingSystem.IsWindows() && !_isDisposed)
            {
                if (_controller != null)
                {
                    try
                    {
                        _controller.IsVisible = effectiveVisible;
                    }
                    catch { }
                }

                IntPtr parentHandle = _parentHwnd != IntPtr.Zero ? _parentHwnd : (_hwnd != IntPtr.Zero ? GetParent(_hwnd) : IntPtr.Zero);
                if (parentHandle != IntPtr.Zero)
                {
                    ShowWindow(parentHandle, effectiveVisible ? SW_SHOWNOACTIVATE : SW_HIDE);
                }
                if (_hwnd != IntPtr.Zero)
                {
                    ShowWindow(_hwnd, effectiveVisible ? SW_SHOWNOACTIVATE : SW_HIDE);
                }

                if (effectiveVisible)
                {
                    _lastRegionHash = 0;
                    AtualizarLimitesNativos();
                }
            }
            else if (OperatingSystem.IsLinux() && _gtkPlug != IntPtr.Zero && !_isDisposed)
            {
                try
                {
                    if (effectiveVisible)
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
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const int GWLP_WNDPROC = -4;
        private const uint WM_WINDOWPOSCHANGING = 0x0046;
        private const uint WM_ERASEBKGND = 0x0014;
        private const int SW_HIDE = 0;
        private const int SW_SHOWNOACTIVATE = 4;
        private const int RGN_DIFF = 4;

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public uint flags;
        }

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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetWindowLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CallWindowProcW(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateRectRgn(int x1, int y1, int x2, int y2);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int CombineRgn(IntPtr hrgnDest, IntPtr hrgnSrc1, IntPtr hrgnSrc2, int iMode);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

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
