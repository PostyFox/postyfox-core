namespace PostyFox.Application.Options;

/// <summary>
/// Tunables for the raw media upload endpoint (<c>POST /api/media</c>). <see cref="MaxUploadSizeBytes"/>
/// should mirror whatever the deployment's gateway actually enforces for that route (e.g. nginx's
/// <c>client_max_body_size</c>), so the frontend can reject an oversized file before spending time
/// uploading it, instead of only discovering the gateway's 413 after the transfer completes.
/// There's no way to introspect the gateway's own limit from in here, so this has to be kept in
/// sync by hand whenever the gateway config changes. The Helm chart derives both values from the
/// same <c>config.media.maxUploadSizeBytes</c> so they can't drift for Helm deployments; the
/// docker-compose stacks set this and the gateway's <c>client_max_body_size</c> separately (see
/// deploy/gateway/conf.d/routes/10-apis.conf) and must be kept in sync manually.
/// </summary>
public sealed class MediaOptions
{
    public const string SectionName = "Media";

    /// <summary>
    /// Max upload size in bytes the gateway will accept for <c>POST /api/media</c>. Null means not
    /// configured, in which case the frontend enforces no client-side cap and simply discovers the
    /// gateway's limit (if any) from the upload's own response, as before.
    /// </summary>
    public long? MaxUploadSizeBytes { get; set; }
}
