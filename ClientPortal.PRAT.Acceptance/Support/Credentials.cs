namespace ClientPortal.PRAT.Acceptance.Support
{
    public class Credentials
    {
        public string? Email { get; set; } = string.Empty;
        public string? Password { get; set; } = string.Empty;
    }

    public class CredentialStore
    {
        public Dictionary<string, Credentials> Accounts { get; set; } = new();
    }
}