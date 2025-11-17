namespace PostyFox_Secrets;

public interface IInfisicalStore : ISecureStore
{
    /// <summary>
    /// Optionally set environment / workspace context for Infisical API calls.
    /// </summary>
    string? Workspace { get; set; }
}
