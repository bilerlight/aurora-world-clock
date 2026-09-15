using System;
using System.IO;
using System.Windows;

namespace AuroraClock
{
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += (_, e) =>
            {
                Log(e.Exception);
                MessageBox.Show(
                    e.Exception.Message,
                    "Aurora World Clock",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                e.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (_, e) => Log(e.ExceptionObject as Exception);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            if (!SingleInstance.TryAcquire())
            {
                Shutdown();
                return;
            }

            Services.AppController.Instance.Initialize();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Services.AppController.Instance.Shutdown();
            SingleInstance.Release();
            base.OnExit(e);
        }

        internal static void Log(Exception? ex)
        {
            if (ex == null) return;
            try
            {
                Directory.CreateDirectory(Services.ConfigService.Dir);
                File.AppendAllText(
                    Path.Combine(Services.ConfigService.Dir, "error.log"),
                    $"[{DateTime.Now:O}]\n{ex}\n\n");
            }
            catch { /* never fail while logging */ }
        }
    }
}
