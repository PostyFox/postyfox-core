# PostyFox-TypeScript Function App

## Overview

**PostyFox-TypeScript** provides additional platform integration coverage for services not handled by the .NET Function Apps. It enables posting to alternative social media platforms using TypeScript/Node.js runtime, complementing the PostyFox-Posting service.

**Technology:** Azure Functions v4, Node.js 24.x, TypeScript  
**Deployment:** Azure Function App  
**Supported Platforms:** Bluesky (AT Protocol), Tumblr  
**Integration:** Queue-triggered message processing  

---

## Architecture

### Project Structure

```
PostyFox-TypeScript/
├── package.json              # NPM dependencies
├── tsconfig.json            # TypeScript configuration
├── host.json                # Function runtime config
├── local.settings.json      # Local development config
├── src/
│   ├── functions/           # HTTP-triggered functions
│   │   ├── blueskyPost/
│   │   │   ├── function.json
│   │   │   └── index.ts
│   │   ├── tumblrPost/
│   │   │   ├── function.json
│   │   │   └── index.ts
│   │   └── ping/
│   │       ├── function.json
│   │       └── index.ts
│   └── Helpers/             # Shared utilities
│       ├── auth.ts
│       ├── queueHandler.ts
│       └── platformClients.ts
├── deprecated/              # Legacy code
├── dist/                    # Compiled JavaScript
└── build/
    └── npm dependencies
```

### Dependencies

**Azure:**
- `@azure/functions` - Azure Functions SDK
- `@azure/identity` - Azure authentication
- `@azure/data-tables` - Table Storage client
- `@azure/keyvault-secrets` - KeyVault integration

**Platform SDKs:**
- `@atproto/api` - Bluesky/AT Protocol client
- `tumblr.js` - Tumblr API client

**Utilities:**
- `debug` - Debugging output
- `typescript` - TypeScript compiler
- `rimraf` - File utilities

---

## Platform Integrations

### Bluesky (AT Protocol)

#### Purpose
- Post content to Bluesky (formerly Twitter alternative)
- Handle authentication with AT Protocol
- Support rich text formatting

#### Dependencies
```json
{
  "@atproto/api": "^0.15.27"
}
```

#### Integration Points

**`blueskyPost/` Function:**
- **Trigger:** HTTP POST or Queue message
- **Route:** `POST /api/blueskyPost`
- **Purpose:** Publish post to Bluesky

**Request Format:**
```json
{
  "userId": "user-id",
  "content": "Post content",
  "richText": {
    "text": "Post content",
    "facets": []
  },
  "images": [
    {
      "url": "blob-storage-url",
      "alt": "Image description"
    }
  ],
  "replyTo": "post-uri",
  "threadGate": "everyone"
}
```

#### Implementation Example
```typescript
import { AtpClient, AtUri } from '@atproto/api';

export async function postToBluesky(
    client: AtpClient,
    did: string,
    content: string,
    images?: Array<{url: string, alt: string}>
): Promise<string> {
    const record = {
        $type: 'app.bsky.feed.post',
        text: content,
        createdAt: new Date().toISOString(),
    };

    // Add rich text if available
    // Add images if provided

    const response = await client.com.atproto.repo.putRecord({
        repo: did,
        collection: 'app.bsky.feed.post',
        record: record,
    });

    return response.uri;
}
```

#### Authentication
- OAuth2 flow with Bluesky
- Credentials stored in KeyVault
- Token refresh handled by SDK

#### Data Storage
- User auth tokens in Table Storage
- Post URIs for tracking
- Media references in Blob Storage

---

### Tumblr

#### Purpose
- Post to Tumblr blogs
- Support media and text formatting
- Handle multi-platform cross-posting

#### Dependencies
```json
{
  "tumblr.js": "^5.0.1"
}
```

#### Integration Points

**`tumblrPost/` Function:**
- **Trigger:** HTTP POST or Queue message
- **Route:** `POST /api/tumblrPost`
- **Purpose:** Publish post to Tumblr blog

**Request Format:**
```json
{
  "userId": "user-id",
  "blogIdentifier": "blog-name",
  "type": "text|photo|video",
  "title": "Post title",
  "body": "Post body",
  "media": [
    {
      "url": "blob-storage-url",
      "type": "image|video"
    }
  ],
  "tags": ["tag1", "tag2"],
  "publish": true
}
```

#### Implementation Example
```typescript
import * as Tumblr from 'tumblr.js';

export async function postToTumblr(
    client: Tumblr.Client,
    blogIdentifier: string,
    postData: TumblrPostRequest
): Promise<{id: string, url: string}> {
    const createOptions: Tumblr.CreatePostOptions = {
        type: postData.type,
        title: postData.title,
        body: postData.body,
        tags: postData.tags,
        state: postData.publish ? 'published' : 'draft',
    };

    if (postData.media && postData.media.length > 0) {
        // Handle media upload
        // createOptions.data = media data
    }

    const response = await client.createPost(
        blogIdentifier,
        createOptions
    );

    return {
        id: response.id.toString(),
        url: response.post_url
    };
}
```

#### Authentication
- OAuth1 flow with Tumblr
- API key and secret in KeyVault
- User tokens per blog

#### Data Storage
- User auth tokens in Table Storage
- Blog mappings (user → blogs)
- Post IDs for tracking

---

## Utility Functions

### Health Check

**`ping/` Function:**
- **Route:** `GET /api/ping`
- **Purpose:** Health check endpoint
- **Returns:** Status and timestamp

```typescript
export async function httpTrigger(
    context: InvocationContext,
    request: HttpRequest
): Promise<HttpResponse> {
    return {
        status: 200,
        jsonBody: {
            status: 'ok',
            timestamp: new Date().toISOString(),
            platform: 'TypeScript'
        }
    };
}
```

### Queue Handler Helper (`queueHandler.ts`)

**Purpose:** Process queue messages from PostyFox-Posting

```typescript
export interface QueueMessage {
    RootPostId: string;
    PostId: string;
    User: string;
    TargetPlatformServiceId: string;
    Status: number;
    Media: string[];
    PostAt: Date;
}

export async function processQueueMessage(
    message: QueueMessage,
    platform: 'bluesky' | 'tumblr'
): Promise<{success: boolean, postId?: string, error?: string}> {
    // Route to appropriate platform handler
    // Retrieve user credentials
    // Fetch media from blob storage
    // Format content for platform
    // Publish
    // Update status table
}
```

### Platform Clients Helper (`platformClients.ts`)

**Purpose:** Centralized client management

```typescript
export class PlatformClientManager {
    private blueskyClient: AtpClient;
    private tumblrClient: Tumblr.Client;

    constructor(credentials: {
        blueskyHandle?: string;
        blueskyPassword?: string;
        tumblrKey?: string;
        tumblrSecret?: string;
    }) {
        this.blueskyClient = new AtpClient({ ...blueskySettings });
        this.tumblrClient = new Tumblr.Client({ ...tumblrSettings });
    }

    async authenticateBluesky(): Promise<void> { }
    async authenticateTumblr(): Promise<void> { }
    getBlueskyClient(): AtpClient { }
    getTumblrClient(): Tumblr.Client { }
}
```

### Authentication Helper (`auth.ts`)

**Purpose:** Credential management and token refresh

```typescript
export async function getCredentials(
    userId: string,
    platform: 'bluesky' | 'tumblr',
    tableClient: TableClient
): Promise<PlatformCredentials> {
    const entity = await tableClient.getEntity(
        userId,
        `${platform}-credentials`
    );
    
    // Refresh tokens if expired
    // Return valid credentials
}

export async function storeCredentials(
    userId: string,
    platform: string,
    credentials: PlatformCredentials,
    tableClient: TableClient
): Promise<void> {
    await tableClient.upsertEntity({
        partitionKey: userId,
        rowKey: `${platform}-credentials`,
        ...credentials
    });
}
```

---

## Configuration

### Environment Variables

```env
# Azure Storage
AzureWebJobsStorage=DefaultEndpointsProtocol=...
ConfigTable=https://<account>.table.core.windows.net
StorageAccount=https://<account>.blob.core.windows.net

# Bluesky
BlueskySoftwareName=postyfox
BlueskyAuthHandle=<bot-handle>
BlueskyAuthPassword=<app-password>

# Tumblr
TumblrConsumerKey=<key>
TumblrConsumerSecret=<secret>
TumblrOAuthToken=<token>
TumblrOAuthTokenSecret=<token-secret>

# Runtime
FUNCTIONS_WORKER_RUNTIME=node
NODE_ENV=production
```

### Local Development

```json
{
  "AzureWebJobsStorage": "UseDevelopmentStorage=true",
  "ConfigTable": "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;...",
  "StorageAccount": "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;...",
  "FUNCTIONS_WORKER_RUNTIME": "node"
}
```

---

## Build & Deployment

### Build Process

```bash
# Install dependencies
npm install

# Compile TypeScript to JavaScript
npm run build

# Output to dist/ and build/
```

### Local Testing

```bash
# Watch mode (rebuild on changes)
npm run watch

# Start function host
func start

# Test endpoint
curl http://localhost:7071/api/blueskyPost \
  -X POST \
  -H "Content-Type: application/json" \
  -d '{"userId": "test", "content": "Hello Bluesky"}'
```

### Production Deployment

```bash
# Build
npm run build

# Publish to Azure
func azure functionapp publish <function-app-name>
```

---

## Data Models

### Bluesky Session Entity

```typescript
interface BlueskySession {
    partitionKey: string;        // UserId
    rowKey: string;              // "bluesky-session"
    did: string;                 // Decentralized identifier
    handle: string;              // @username
    accessJwt: string;           // Auth token
    refreshJwt: string;          // Refresh token
    expiresAt: Date;             // Token expiration
}
```

### Tumblr Authorization Entity

```typescript
interface TumblrAuth {
    partitionKey: string;        // UserId
    rowKey: string;              // "tumblr-auth"
    blogs: Array<{
        name: string;            // Blog name
        uuid: string;            // Blog UUID
        oauthToken: string;       // OAuth token
        oauthTokenSecret: string; // OAuth secret
    }>;
    expiresAt: Date;
}
```

### Post Record

```typescript
interface PostRecord {
    partitionKey: string;        // UserId
    rowKey: string;              // PostId-platform
    platform: 'bluesky' | 'tumblr';
    rootPostId: string;          // For tracking
    externalPostId: string;      // Platform post ID
    externalPostUrl: string;     // Platform post URL
    status: 'Posted' | 'Failed' | 'Scheduled';
    createdAt: Date;
    error?: string;              // Error message if failed
}
```

---

## Error Handling

### Common Failures

1. **Authentication Expired:**
   - Attempt token refresh
   - If refresh fails, mark as faulted
   - Log for user action

2. **Rate Limited:**
   - Exponential backoff
   - Retry with jitter
   - Queue message for later

3. **Media Not Found:**
   - Log error
   - Post text-only version
   - Mark as partial success

4. **Platform Down:**
   - Retry with backoff
   - Dead-letter after max attempts

### Retry Strategy

```typescript
async function retryWithBackoff<T>(
    operation: () => Promise<T>,
    maxRetries: number = 3
): Promise<T> {
    for (let i = 0; i < maxRetries; i++) {
        try {
            return await operation();
        } catch (error) {
            if (i === maxRetries - 1) throw error;
            const delay = Math.pow(2, i) * 1000;
            await new Promise(resolve => setTimeout(resolve, delay));
        }
    }
}
```

---

## Logging & Monitoring

### Structured Logging

```typescript
context.log({
    timestamp: new Date().toISOString(),
    userId: userId,
    platform: 'bluesky',
    action: 'postCreated',
    postId: result.uri,
    duration: Date.now() - startTime
});
```

### Application Insights Integration

- Automatic performance tracking
- Exception logging
- Custom metrics for platform posts
- Dependency monitoring

---

## Security Considerations

1. **Token Storage:**
   - Encrypted at rest in Table Storage
   - Never logged in plaintext
   - Rotation strategy implemented

2. **Secrets Management:**
   - API keys from KeyVault
   - Environment variables never hardcoded
   - Principle of least privilege

3. **User Data:**
   - Partitioned by UserId
   - Access restricted to function identity
   - Audit logging enabled

---

## Performance

### Concurrency
- Node.js async/await prevents blocking
- Multiple function instances auto-scale
- Independent platform handlers

### Throughput
- Can handle multiple posts simultaneously
- Media streaming doesn't block
- Efficient queue processing

### Latency
- Typical post: 2-5 seconds
- Media upload: 1-10 seconds depending on size
- Rate limits respected per platform

---

## Known Limitations

1. **Rich Text:** Basic support, complex formatting TBD
2. **Media:** URL references only, not streaming uploads
3. **Scheduling:** PostAt support not yet implemented
4. **Threads:** No reply chain support yet
5. **Analytics:** Basic logging, advanced metrics TBD

---

## Future Enhancements

1. Implement rich text formatting (mentions, hashtags, links)
2. Add media streaming and upload
3. Support scheduled posting
4. Add thread/reply chain support
5. Implement platform-specific analytics
6. Add batch posting
7. Support media editing/deletion workflow
8. Implement carousel/gallery support

---

## Development Tips

### Testing Queue Messages Locally

```typescript
// Queue message structure
const testMessage: QueueMessage = {
    RootPostId: "test-root-123",
    PostId: "test-post-456",
    User: "test-user",
    TargetPlatformServiceId: "bluesky",
    Status: 0,
    Media: [],
    PostAt: new Date()
};

// Add to queue via Storage Explorer or:
const queueClient = new QueueClient(queueUri, credential);
await queueClient.sendMessage(JSON.stringify(testMessage));
```

### Debugging TypeScript

```bash
# Enable debug output
DEBUG=postyfox:* func start

# Or set in .env
DEBUG=postyfox:* npm run watch
```

### Local Platform Testing

- Create test accounts on Bluesky and Tumblr
- Use sandbox/test credentials for development
- Never commit real credentials

