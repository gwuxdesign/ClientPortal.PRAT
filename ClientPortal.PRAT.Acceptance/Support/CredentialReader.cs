using System.Text.Json;
using ClientPortal.PRAT.Acceptance.Support;

public static class CredentialReader
{
    private static CredentialStore? _store;

    private static void EnsureLoaded()
    {
        if (_store != null) return;
        var path = File.Exists("credentials.local.json")
            ? "credentials.local.json"
            : "credentials.json";
        var json = File.ReadAllText(path);
        _store = JsonSerializer.Deserialize<CredentialStore>(json)
                 ?? throw new InvalidOperationException("Failed to load credential store.");
    }

    // private static void EnsureLoaded()
    // {
    //     if (_store != null) return;
    //     var path = File.Exists("credentials.local.json")
    //         ? "credentials.local.json"
    //         : "credentials.json";
    //     Console.WriteLine($"[CredentialReader] Loading from: {path}");
    //     var json = File.ReadAllText(path);
    //     _store = JsonSerializer.Deserialize<CredentialStore>(json)
    //              ?? throw new InvalidOperationException("Failed to load credential store.");
    // }

    public static Credentials Get(string key)
    {
        EnsureLoaded();
        if (_store!.Accounts.TryGetValue(key, out var creds))
        {
            return creds;
        }
        throw new KeyNotFoundException($"No credentials found for key '{key}'.");
    }
}