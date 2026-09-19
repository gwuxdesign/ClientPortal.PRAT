using System.Text.Json;
using ClientPortal.PRAT.Acceptance.Support;

public static class CredentialReader
{
    // Lazy<T> guarantees the factory runs exactly once even under
    // concurrent first access, regardless of test parallelism settings.
    // Safe today because reqnroll.json sets testThreadCount: 1 and
    // credentials are always loaded synchronously before Concurrency's
    // background sessions ever start — but that safety was previously
    // implicit rather than guaranteed by the code itself.
    private static readonly Lazy<CredentialStore> _store = new(LoadStore);

    private static CredentialStore LoadStore()
    {
        var path = File.Exists("credentials.local.json")
            ? "credentials.local.json"
            : "credentials.json";
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<CredentialStore>(json)
               ?? throw new InvalidOperationException("Failed to load credential store.");
    }

    public static Credentials Get(string key)
    {
        if (_store.Value.Accounts.TryGetValue(key, out var creds))
        {
            return creds;
        }
        throw new KeyNotFoundException($"No credentials found for key '{key}'.");
    }
}