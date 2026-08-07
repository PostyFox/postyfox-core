namespace PostyFox.Application.Connectors;

/// <summary>
/// Thrown when a connector upsert — or a post's per-target platform options — fails schema
/// validation. The <see cref="Exception.Message"/> is user-facing and surfaced to the client as the
/// <c>error</c> field of a 400 response.
/// </summary>
public sealed class ConnectorValidationException(string message) : Exception(message);
