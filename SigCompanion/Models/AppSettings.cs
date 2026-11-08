namespace SigCompanion.Models;

public sealed class AppSettings
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool SavePassword { get; set; }
    public string? SystemId { get; set; }
    public bool ShowClock { get; set; }
}
