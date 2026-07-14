// Shared types for the connectors-node service.

/** Context object shared by all connector operations. */
export interface ConnectorContext {
  connectorId: string;
  userId: string;
  /** JSON *string* holding non-secret platform config. */
  configJson: string;
  /** JSON *string* holding secret credentials, or null. */
  secretJson: string | null;
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
}

export interface Post {
  title: string | null;
  body: string;
  tags: string[];
  media: PostMedia[];
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

/** Contract implemented by every platform connector. */
export interface Connector {
  isAuthenticated(ctx: ConnectorContext): Promise<IsAuthenticatedResult>;
  listTargets(ctx: ConnectorContext): Promise<ListTargetsResult>;
  deliver(ctx: ConnectorContext, post: Post): Promise<DeliverResult>;
  /** Present when the platform supports an interactive OAuth "connect" flow. */
  oauth?: OAuthProvider;
}

/** Result of beginning an OAuth1 authorization. */
export interface OAuthStartResult {
  /** URL to send the user's browser to, to grant access. */
  authorizeUrl: string;
  /** OAuth1 request token — echoed back by the provider on callback. */
  requestToken: string;
  /** OAuth1 request-token secret — the caller holds this between start and callback. */
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
  startAuthorization(input: { callbackUrl: string }): Promise<OAuthStartResult>;
  completeAuthorization(input: {
    requestToken: string;
    requestTokenSecret: string;
    verifier: string;
  }): Promise<OAuthCompleteResult>;
}
