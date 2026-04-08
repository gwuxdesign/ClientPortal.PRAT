using System.Text.Json;
using ClientPortal.PRAT.Acceptance.Support;

public static class CredentialReader
{
    private static CredentialStore? _store;

    private static void EnsureLoaded()
    {
        if (_store != null) return;
        var json = File.ReadAllText("credentials.json");
        _store = JsonSerializer.Deserialize<CredentialStore>(json)
                ?? throw new InvalidOperationException("Failed to load credential store.");
    }

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