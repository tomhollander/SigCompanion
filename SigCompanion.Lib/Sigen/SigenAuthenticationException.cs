namespace SigCompanion.Lib.Sigen;

/// <summary>
/// Thrown when the Sigen API rejects login credentials (for example, incorrect credentials or a locked account).
/// </summary>
public sealed class SigenAuthenticationException : InvalidOperationException
{
    public SigenAuthenticationException(string message)
        : base(message)
    {
    }
}
