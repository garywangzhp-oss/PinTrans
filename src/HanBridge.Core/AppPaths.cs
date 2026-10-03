namespace HanBridge.Core;

public sealed class AppPaths
{
    public AppPaths(string? root = null)
    {
        Root = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HanBridge");

        IpcDirectory = Path.Combine(Root, "ipc");
        LogDirectory = Path.Combine(Root, "logs");
        SettingsFile = Path.Combine(Root, "settings.json");
        SecretsFile = Path.Combine(Root, "secrets.dat");
        CacheFile = Path.Combine(Root, "cache.db");
        RequestFile = Path.Combine(IpcDirectory, "request.json");
        ResponseFile = Path.Combine(IpcDirectory, "response.txt");

        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(IpcDirectory);
        Directory.CreateDirectory(LogDirectory);
    }

    public string Root { get; }
    public string IpcDirectory { get; }
    public string LogDirectory { get; }
    public string SettingsFile { get; }
    public string SecretsFile { get; }
    public string CacheFile { get; }
    public string RequestFile { get; }
    public string ResponseFile { get; }
}