using HanBridge.Core;
using HanBridge.Core.Ipc;
using HanBridge.Core.Logging;
using HanBridge.Core.Security;
using HanBridge.Core.Storage;
using HanBridge.Core.Translation;

namespace HanBridge.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        using var mutex = new Mutex(true, "HanBridge.Singleton", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("PinTrans is already running.", "PinTrans", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var paths = new AppPaths();
        var logger = new SafeLogger(paths.LogDirectory);
        var settingsStore = new SettingsStore(paths.SettingsFile);
        var secretStore = new SecretStore(paths.SecretsFile);
        var cache = new TranslationCache(paths.CacheFile);
        var providerClient = new OpenAiCompatibleClient();
        var translationService = new TranslationService(settingsStore, secretStore, cache, logger, providerClient);
        var ipcServer = new FileIpcServer(paths, translationService, logger);

        Application.ThreadException += (_, args) => logger.Error("unhandled-ui-exception", args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                logger.Error("unhandled-process-exception", exception);
            }
        };

        using var tray = new TrayApplicationContext(paths, settingsStore, secretStore, cache, ipcServer, logger);
        if (args.Any(argument => argument.Equals("--settings", StringComparison.OrdinalIgnoreCase)))
        {
            tray.ShowSettings();
        }

        Application.Run(tray);
    }
}