import { GetObjectCommand, S3Client } from "@aws-sdk/client-s3";

/**
 * Fetches media bytes from an object store.
 *
 * Injected into connectors so tests can supply a fake with no network access.
 */
export interface MediaStore {
  /** Fetch the raw bytes for a media reference. */
  fetch(container: string, key: string): Promise<Buffer>;
}

export interface S3MediaStoreConfig {
  /** S3-compatible endpoint (e.g. http://minio:9000). Optional for AWS. */
  endpoint?: string;
  accessKey?: string;
  secretKey?: string;
  /** Single bucket that holds every container. */
  bucket: string;
  /** Path-style addressing (required by MinIO and most self-hosted stores). */
  forcePathStyle: boolean;
  region: string;
}

/**
 * Default S3-backed {@link MediaStore}.
 *
 * The C# side stores everything in a single bucket, keyed by
 * `` `${container}/${key}` ``, so `container` acts as a key prefix rather than a
 * separate bucket.
 */
export class S3MediaStore implements MediaStore {
  private readonly client: S3Client;
  private readonly bucket: string;

  constructor(config: S3MediaStoreConfig) {
    this.bucket = config.bucket;
    this.client = new S3Client({
      endpoint: config.endpoint,
      region: config.region,
      forcePathStyle: config.forcePathStyle,
      credentials:
        config.accessKey && config.secretKey
          ? { accessKeyId: config.accessKey, secretAccessKey: config.secretKey }
          : undefined,
    });
  }

  async fetch(container: string, key: string): Promise<Buffer> {
    const objectKey = `${container}/${key}`;
    const response = await this.client.send(
      new GetObjectCommand({ Bucket: this.bucket, Key: objectKey }),
    );
    if (!response.Body) {
      throw new Error(`empty object body for ${this.bucket}/${objectKey}`);
    }
    const bytes = await response.Body.transformToByteArray();
    return Buffer.from(bytes);
  }
}

/** Build the default S3-backed media store from environment variables. */
export function mediaStoreFromEnv(env: NodeJS.ProcessEnv = process.env): S3MediaStore {
  return new S3MediaStore({
    endpoint: env.OBJECT_STORE_SERVICE_URL,
    accessKey: env.OBJECT_STORE_ACCESS_KEY,
    secretKey: env.OBJECT_STORE_SECRET_KEY,
    bucket: env.OBJECT_STORE_BUCKET ?? "postyfox",
    forcePathStyle: (env.OBJECT_STORE_FORCE_PATH_STYLE ?? "true") !== "false",
    region: env.OBJECT_STORE_REGION ?? "us-east-1",
  });
}
