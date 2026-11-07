namespace SigCompanion.Lib.Sigen.Models;

public sealed class SigenSystemReference
{
    public required string SystemId { get; init; }
    public required string SystemName { get; init; }
    public string? TimeZone { get; init; }
}
