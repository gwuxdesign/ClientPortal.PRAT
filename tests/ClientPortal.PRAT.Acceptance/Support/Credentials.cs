namespace ClientPortal.PRAT.Acceptance.Support
{
    public class Credentials
    {
        public string? email { get; set; } = string.Empty;
        public string? password { get; set; } = string.Empty;
    }

    public class CredentialStore
    {
        public Dictionary<string, Credentials> Accounts { get; set; } = new();
    }
}