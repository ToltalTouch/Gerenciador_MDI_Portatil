using Avalonia;
using System;

namespace AvaloniaMdiApp
{
    class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            if (OperatingSystem.IsLinux())
            {
                Environment.SetEnvironmentVariable("GDK_BACKEND", "x11");
                Environment.SetEnvironmentVariable("WEBKIT_DISABLE_DMABUF_RENDERER", "1");
            }

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .With(new X11PlatformOptions
                {
                    WmClass = LinuxDesktopIntegration.WmClass
                })
                .LogToTrace();
    }
}
