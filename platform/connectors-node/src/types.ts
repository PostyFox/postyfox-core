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
  container: string;
  key: string;
  contentType: string;
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
}
