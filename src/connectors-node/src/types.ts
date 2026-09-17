/** Context object shared by all connector operations. */
export interface ConnectorContext {
  connectorId: string;
  userId: string;
  /** JSON *string* holding non-secret platform config. */
  configJson: string;
  /** JSON *string* holding secret credentials, or null. */
  secretJson: string | null;
  /** JSON *string* holding operator-managed platform credentials, or null. */
  operationalSecretJson?: string | null;
  targetId: string | null;
}

export interface PostMedia {
  /** Object-store container (logical bucket/prefix) the item lives in. */
  container: string;
  /** Object key within the container. */
  key: string;
  /** MIME type of the media item (e.g. "image/png"). */
  contentType: string;
  /** Alternative text describing the media, or null when not provided. */
  alt: string | null;
  /**
   * Author-chosen "primary" item among several attachments. Platforms limited to a single image
   * (FurAffinity) use this one instead of rejecting the post; platforms that accept multiple images
   * ignore it and attach everything. Undefined/false for every item means "no explicit choice": a
   * single-image connector then falls back to the first item.
   */
  isDefault?: boolean;
}

export interface Post {
  title: string | null;
  body: string;
  tags: string[];
  media: PostMedia[];
  /** Author-supplied classification. Scraped sites may require this field. */
  rating?: "general" | "mature" | "adult" | "extreme" | null;
}

export interface IsAuthenticatedResult {
  isAuthenticated: boolean;
  detail?: string;
}

export interface Target {
  id: string;
  name: string;
}

export interface ListTargetsResult {
  targets: Target[];
}

export interface DeliverResult {
  success: boolean;
  externalId?: string;
  externalUrl?: string;
  error?: string;
}

/**
 * Live, per-connector-instance limits. Fediverse instances each configure their own caps, so these
 * are fetched from the instance rather than assumed per platform. `null` means "not reported / no
 * client-side cap".
 */
export interface ConnectorLimits {
  maxContentLength: number | null;
  maxMediaAttachments: number | null;
  /** Accepted media MIME types; null means "not reported / no restriction". */
  supportedMimeTypes: string[] | null;
  /** Max image file size in bytes; null means "not reported / no cap". */
  imageSizeLimit: number | null;
  /** Max video (and audio) file size in bytes; null means "not reported / no cap". */
  videoSizeLimit: number | null;
}

/** Result of deleting an already-delivered post from its platform. */
export interface DeleteResult {
  success: boolean;
  error?: string;
}

/** Contract implemented by every platform connector. */
export interface Connector {
  isAuthenticated(ctx: ConnectorContext): Promise<IsAuthenticatedResult>;
  listTargets(ctx: ConnectorContext): Promise<ListTargetsResult>;
  deliver(ctx: ConnectorContext, post: Post): Promise<DeliverResult>;
  /** Present when the platform supports an interactive OAuth "connect" flow. */
  oauth?: OAuthProvider;
  /** Present when the connector can report live per-instance limits (e.g. Fediverse). */
  getLimits?(ctx: ConnectorContext): Promise<ConnectorLimits>;
  /**
   * Present when the connector can repost/reblog/boost one of its own already-delivered posts
   * (issue #323's "repost after X hours"). `externalId` is whatever `deliver` returned.
   */
  repost?(ctx: ConnectorContext, externalId: string): Promise<DeliverResult>;
  /**
   * Present when the connector can delete one of its own already-delivered posts from the platform
   * (issue #323's "delete after X hours"). `externalId` is whatever `deliver` returned.
   */
  deleteRemote?(ctx: ConnectorContext, externalId: string): Promise<DeleteResult>;
  /**
   * Present when the connector's access token needs periodic renewal ahead of a hard expiry
   * (Instagram's long-lived token: refreshable once ≥24h old, must be refreshed within 60 days).
   * Returns the new secret JSON to persist, or null if the stored token could not be refreshed
   * (e.g. it was revoked — the caller leaves the existing secret in place and the user must
   * reconnect).
   */
  refresh?(ctx: ConnectorContext): Promise<{ secretJson: string } | null>;
}

/** Result of beginning an OAuth1 authorization. */
export interface OAuthStartResult {
  /** URL to send the user's browser to, to grant access. */
  authorizeUrl: string;
  /** OAuth1 request token, echoed back by the provider on callback. */
  requestToken: string;
  /** OAuth1 request-token secret, the caller holds this between start and callback. */
  requestTokenSecret: string;
}

export interface OAuthCompleteResult {
  /** JSON string to persist as the connector's secret (platform-specific shape). */
  secretJson: string;
}

/**
 * Interactive OAuth flow a connector can expose. OAuth1.0a for Tumblr: begin → the user authorizes
 * at `authorizeUrl` → the provider calls back with a verifier → complete exchanges for the token.
 */
export interface OAuthProvider {
  startAuthorization(input: {
    callbackUrl: string;
    operationalSecretJson?: string | null;
    /** JSON string of the connector's non-secret config. Needed by providers whose authorization is
     * instance-scoped (e.g. Fediverse: the instance URL lives in config). OAuth1 providers ignore it. */
    configJson?: string;
  }): Promise<OAuthStartResult>;
  completeAuthorization(input: {
    requestToken: string;
    requestTokenSecret: string;
    verifier: string;
    operationalSecretJson?: string | null;
  }): Promise<OAuthCompleteResult>;
}
