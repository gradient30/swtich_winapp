namespace BrowserSwitcher.Models;

public enum BrowserStatus
{
    Installed,
    Manual,
    NotFound
}

public sealed class BrowserInfo
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public string? ExecutablePath { get; init; }
    public BrowserStatus Status { get; init; }
}
