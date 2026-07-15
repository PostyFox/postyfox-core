using Neillans.Adapters.Secrets.Core;

namespace PostyFox.Application.Abstractions;

public static class SecretsProviderExtensions
{
    /// <summary>
    /// Best-effort secret deletion used for cleanup. Some providers (e.g. BitWarden/VaultWarden)
    /// do not support deletion and throw <see cref="SecretsProviderException"/>; in that case the
    /// call is swallowed so callers that only want cleanup are not broken by the provider choice.
    /// </summary>
    public static async Task TryDeleteSecretAsync(this ISecretsProvider secrets, string key, CancellationToken ct = default)
    {
        try
        {
            await secrets.DeleteSecretAsync(key, ct);
        }
        catch (SecretsProviderException)
        {
            // Provider does not support deletion — best-effort cleanup, nothing more to do.
        }
    }
}
