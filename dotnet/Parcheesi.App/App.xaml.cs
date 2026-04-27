using System.IO;
using System.Windows;
using System.Windows.Threading;
using Parcheesi.Core.Localization;

namespace Parcheesi.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Capture toute exception non gérée pour qu'elle soit visible (et journalisée)
        // au lieu d'un crash silencieux.
        DispatcherUnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, ev) =>
            LogCrash(ev.ExceptionObject as Exception);
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash(e.Exception);
        MessageBox.Show(
            Loc.Format("crash.message", e.Exception.Message),
            Loc.Get("crash.title"),
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void LogCrash(Exception? ex)
    {
        if (ex == null) return;
        try
        {
            var path = UserDataPaths.Get("crash.log");
            File.AppendAllText(path,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{ex}\n\n");
        }
        catch { }
    }
}
