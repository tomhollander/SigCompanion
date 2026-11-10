using System;

namespace SigCompanion.Models;

public sealed class AppSettings
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool SavePassword { get; set; }
    public string? AccessToken { get; set; }
    public DateTimeOffset? AccessTokenExpiresAt { get; set; }
    public string? SystemId { get; set; }
    public bool ShowClock { get; set; }
    public bool CyclePagesWhenIdle { get; set; }
    public string ChartData { get; set; } = "Power";
}
