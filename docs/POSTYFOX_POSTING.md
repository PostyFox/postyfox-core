# PostyFox-Posting Function App

## Overview

**PostyFox-Posting** handles the asynchronous post creation, generation from templates, and delivery to external social media platforms. It is the execution layer that transforms user requests into published content across Twitch, Telegram, Discord, Bluesky, Tumblr, and other supported platforms.

**Technology:** Azure Functions v4, .NET 10.0 (C#)  
**Deployment:** Azure Function App  
**Queue Integration:** Azure Queue Storage (PostingQueue)  
**Message Processing:** Queue-triggered async functions  

---

## Architecture

### Project Structure

```
PostyFox-Posting/
├── Program.cs                 # Startup, DI configuration
├── PostyFox-Posting.csproj   # Project dependencies
├── host.json                 # Function runtime config
├── local.settings.json       # Local development config
├── Dockerfile                # Container configuration
├── Post.cs                   # Main posting endpoint & status management
├── GenerateEntry.cs          # Entry point for post generation workflow
├── GeneratePost.cs           # Queue-triggered post generation
├── QueueEntry.cs             # Queue message structure
├── QueuePost.cs              # Queue-triggered post delivery
├── Telegram.cs              # Telegram platform integration
├── Twitch.cs                # Twitch platform integration
├── TwitchHelpers/           # Twitch utility functions
│   └── *.cs
├── .claude/                 # Dev notes
└── Properties/
    └── launchSettings.json
```

### Dependencies

**Azure Services:**
- `Azure.Data.Tables` - Table Storage client
- `Azure.Storage.Blobs` - Blob Storage client
- `Azure.Storage.Queues` - Queue Storage client
- `Microsoft.Azure.Functions.Worker` - Functions runtime
- `Microsoft.Azure.Functions.Worker.Extensions.Storage.Queues` - Queue triggers

**Platform Libraries:**
- `WTelegramClient` - Telegram MTProto client
- External platform SDKs (Twitch, Discord, etc.)

**Data & Secrets:**
- `PostyFox-DataLayer` - Shared DTOs and table entities
- `Neillans.Adapters.Secrets.*` - Secrets providers

---

## Core Functions

### Post Management

#### `Post_Ping`
- **Route:** `GET /api/Post_Ping`
- **Purpose:** Health check
- **Auth:** Anonymous
- **Returns:** Request headers

#### `Post_CreatePost`
- **Route:** `POST /api/Post_CreatePost`
- **Purpose:** Submit a new post for scheduling/publishing
- **Auth:** API Key (from ProfileAPIKey)
- **Request Body:**
  ```json
  {
    "APIKey": "user-generated-key",
    "TargetPlatforms": ["twitch", "telegram", "discord"],
    "Media": ["media-file-1", "media-file-2"],
    "PostTemplate": "template-id",
    "ScheduleTime": "2025-12-18T10:00:00Z",
    "Content": "Post content text"
  }
  ```
- **Process:**
  1. Validates API key against `UserProfilesAPIKeys` table
  2. Retrieves post template if specified
  3. Validates target platforms
  4. Creates root post record
  5. Enqueues messages to generation queue
  6. Returns post ID and status
- **Returns:**
  ```json
  {
    "PostId": "post-id",
    "RootPostId": "root-post-id",
    "Status": "Queued",
    "TargetPlatforms": ["twitch", "telegram"]
  }
  ```
- **Errors:**
  - 401 Unauthorized: Invalid API key
  - 400 Bad Request: Invalid platforms/template
  - 500 Error: Processing failed

#### `Post_GetStatus`
- **Route:** `GET /api/Post_GetStatus?postId={postId}`
- **Purpose:** Check posting progress
- **Returns:** 
  ```json
  {
    "PostId": "post-id",
    "Status": "Posting|Posted|Faulted|SomeFaults",
    "PlatformStatuses": {
      "twitch": "Posted",
      "telegram": "Failed",
      "discord": "Pending"
    }
  }
  ```

---

### Post Generation & Processing

#### `GenerateEntry` (Queue Trigger)
- **Trigger:** Azure Queue - `generatequeue` message
- **Purpose:** Entry point for post generation workflow
- **Process:**
  1. Receives posting request from queue
  2. Validates message format
  3. Extracts content, templates, media
  4. Calls template engine
  5. Creates formatted posts for each platform
  6. Enqueues to posting queue
  7. Updates status table

**Queue Message Format:**
```json
{
  "RootPostId": "root-id",
  "PostId": "post-id",
  "User": "user-id",
  "TargetPlatformServiceId": "platform-id",
  "Status": 0,
  "Media": ["media-1", "media-2"],
  "PostAt": "2025-12-18T10:00:00Z"
}
```

#### `GeneratePost` (Planned)
- **Trigger:** Queue or Function call
- **Purpose:** Generate post content from templates
- **Input:** Template ID + variables
- **Output:** Formatted post content for each platform
- **Status:** Placeholder implementation

---

### Post Delivery

#### `QueuePost` (Queue Trigger)
- **Trigger:** Azure Queue - `postingqueue` message
- **Purpose:** Deliver posts to external platforms
- **Process:**
  1. Receives formatted post message
  2. Determines target platform
  3. Routes to platform-specific handler
  4. Handles platform-specific formatting
  5. Sends via API/webhook
  6. Updates posting status
  7. Handles retries on failure
- **Platform Routing:**
  - Twitch → `Twitch.cs`
  - Telegram → `Telegram.cs`
  - Discord → Webhook
  - Bluesky → TypeScript function
  - Tumblr → TypeScript function

#### `QueueEntry` (Data Structure)
- **Purpose:** Represents a single post delivery task
- **Fields:**
  ```csharp
  public string RootPostId { get; set; }      // Root post identifier
  public string PostId { get; set; }          // Specific post variant
  public string User { get; set; }            // User ID
  public string? TargetPlatformServiceId { get; set; }  // Platform ID
  public int Status { get; set; }             // Status code
  public List<string> Media { get; set; }     // Media file refs
  public DateTime? PostAt { get; set; }       // Scheduled time
  ```

---

## Platform Integrations

### Telegram Integration (`Telegram.cs`)

#### Purpose
- Send messages/posts to Telegram channels
- Handle Telegram authentication
- Manage session storage

#### Configuration
```csharp
public class TelegramParameters
{
    public string UserId { get; set; }          // User identifier
    public string PhoneNumber { get; set; }     // For auth
    public string ChannelId { get; set; }       // Target channel
    public string Message { get; set; }         // Content
    public List<string> Media { get; set; }     // Attachments
}
```

#### Key Methods
- `Telegram_Ping()` - Health check
- `SendMessage()` - Publish to channel
- `HandleAuthentication()` - Auth flow
- `StoreSession()` - Session persistence

#### Secrets Required
```env
TelegramApiID=<api-id>
TelegramApiHash=<api-hash>
TelegramPhoneNumber=<bot-phone>
TelegramAuthCode=<from-auth-flow>
```

#### MTProto Client
- Uses `WTelegramClient` library
- Handles Telegram protocol
- Encrypts session data

#### Session Management
- Stores encrypted sessions in Table Storage
- Persists across function invocations
- Handles re-authentication if needed

### Twitch Integration (`Twitch.cs`)

#### Purpose
- Publish posts to Twitch channels
- Handle announcements
- Manage channel points integration (future)

#### Key Methods
- `Twitch_Ping()` - Health check
- `PostMessage()` - Send announcement
- `CreateClip()` - Create Twitch clip (future)

#### Integration Points
- Uses vendor `Twitch.Net.Api` client
- Validates broadcaster token
- Handles rate limiting

#### Twitch Helpers (`TwitchHelpers/`)
- Token validation
- Channel lookup
- Emote handling
- Stream status checking

---

## Post Status Workflow

```
States: Queued → Posting → Posted
                        ↓
                    Faulted (error on all platforms)
                        ↓
                    SomeFaults (error on some platforms)
```

### Enum: `Post.PostStatus`
```csharp
public enum PostStatus
{
    Queued,      // 0 - Waiting to process
    Posting,     // 1 - Currently sending
    Posted,      // 2 - Successfully posted
    Faulted,     // 3 - All platforms failed
    SomeFaults   // 4 - Partial success
}
```

---

## Configuration

### Environment Variables

```env
# Storage
ConfigTable=https://<account>.table.core.windows.net
StorageAccount=https://<account>.blob.core.windows.net
PostingQueue=https://<account>.queue.core.windows.net

# Twitch
TwitchClientId=<client-id>
TwitchClientSecret=<secret>
TwitchCallbackUrl=https://<function-app>.azurewebsites.net

# Telegram
TelegramApiID=<api-id>
TelegramApiHash=<api-hash>

# Development
PostyFoxDevMode=true  # Optional local mode
```

### Queue Names
- **generatequeue** - Post generation tasks
- **postingqueue** - Post delivery tasks

### Table Names
- **PostingStatus** - Current posting state
- **UserTelegramSessions** - Telegram auth
- **UserProfilesAPIKeys** - API key validation

---

## Dependency Injection

### Service Registration (Program.cs)

```csharp
services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddTableServiceClient(new Uri(tableAccount))
        .WithName("ConfigTable");
    clientBuilder.AddBlobServiceClient(new Uri(storageAccount))
        .WithName("StorageAccount");
    clientBuilder.AddQueueServiceClient(new Uri(queueAccount))
        .WithName("PostingQueue");
    clientBuilder.UseCredential(new DefaultAzureCredential());
});

services.AddSecretsProviderFactory();
services.AddTransient<Post>();
services.AddTransient<Telegram>();
services.AddTransient<Twitch>();
```

### Constructor Pattern

```csharp
public Post(
    ILoggerFactory loggerFactory,
    IAzureClientFactory<TableServiceClient> clientFactory,
    IAzureClientFactory<BlobServiceClient> blobClientFactory,
    IAzureClientFactory<QueueServiceClient> postingQueueFactory
)
{
    _logger = loggerFactory.CreateLogger<Post>();
    _configTable = clientFactory.CreateClient("ConfigTable");
    _blobStorageAccount = blobClientFactory.CreateClient("StorageAccount");
    _postingQueueClient = postingQueueFactory.CreateClient("PostingQueue");
}
```

---

## Data Flow

### Simplified Post Lifecycle

```
1. User creates post via PostyFox-NetCore
   ↓
2. Post_CreatePost validates API key
   ↓
3. Message enqueued to "generatequeue"
   ↓
4. GenerateEntry triggered (Queue Trigger)
   ↓
5. Template processing via PostyFox-Common
   ↓
6. Messages enqueued to "postingqueue"
   ↓
7. QueuePost triggered for each platform
   ├─→ Telegram.cs (send via MTProto)
   ├─→ Twitch.cs (post announcement)
   ├─→ TypeScript function (Bluesky/Tumblr)
   └─→ Webhook (Discord)
   ↓
8. Status updated in PostingStatus table
   ↓
9. User polls Post_GetStatus for results
```

---

## Error Handling & Retries

### Failure Scenarios

1. **API Key Invalid:** Return 401, don't queue
2. **Platform Down:** Queue message with retry attempts
3. **Authentication Failed:** Mark as faulted, log error
4. **Media Missing:** Skip media, post text only
5. **Rate Limited:** Back off and retry

### Retry Strategy

- Queue Service handles retries (exponential backoff)
- Max retry count configurable per platform
- Dead-letter queue for permanent failures

### Status Updates

- **Success:** Mark as "Posted"
- **Partial:** Mark as "SomeFaults" with per-platform status
- **All Failed:** Mark as "Faulted"

---

## Testing

### Unit Tests

**Project:** `PostyFox-Posting.Tests`

```csharp
[Test]
public void Post_CreatePost_ValidatesApiKey() { }

[Test]
public void GeneratePost_ProcessesTemplate() { }

[Test]
public void Telegram_SendsMessage() { }

[Test]
public void QueuePost_HandlesFailures() { }
```

### Local Testing

```powershell
# Start Azurite
# Build and run
func start

# Queue test message
$message = @{
    RootPostId = "test-id"
    PostId = "post-1"
    User = "user-1"
    TargetPlatformServiceId = "twitch"
    Status = 0
    Media = @()
    PostAt = [DateTime]::UtcNow.AddMinutes(5)
} | ConvertTo-Json

# Add to queue (via Storage Explorer or SDK)
```

---

## Local Development

### Prerequisites

1. Azurite storage emulator
2. Azure Functions Core Tools v4
3. .NET 10.0 SDK
4. Telegram API ID/Hash (optional for local Telegram testing)

### Running Functions

```powershell
# Start Azurite
# Start function host
func start

# Watch mode
func start --watch

# Specific function
func start --functions Telegram_Ping
```

### Debugging Queue Messages

1. Use Azure Storage Explorer to view queue messages
2. Add breakpoints in function code
3. Trigger manually via portal or CLI
4. View logs in integrated terminal

---

## Performance Considerations

### Throughput
- Queue-based processing enables parallel execution
- Multiple function instances auto-scale
- Async/await prevents blocking

### Concurrency
- Each platform handler runs independently
- Status updates atomic via table storage
- No shared state between instances

### Costs
- Billed per execution (first 1M free monthly)
- Storage operations counted separately
- Queue messages add minimal cost

---

## Security

### API Key Validation
- Extracted from request
- Validated against `UserProfilesAPIKeys` table
- User ID verified from table lookup

### Secrets Management
- Telegram API ID/Hash from KeyVault
- Platform tokens stored securely
- Never logged in plaintext

### Session Storage
- Telegram sessions encrypted at rest
- User-specific partitions in table
- Access restricted to function identity

---

## Monitoring

### Logging
- All operations logged via `ILogger`
- Failures logged with context
- External API calls tracked

### Application Insights
- Execution times tracked
- Dependency calls monitored
- Custom metrics for post delivery

### Alerts
- Failed queue messages
- Platform API errors
- High latency thresholds

---

## Known Limitations

1. **GeneratePost:** Placeholder implementation
2. **Discord:** Webhook-only, no bot integration yet
3. **Media Handling:** File references only, not inline
4. **Templates:** Basic implementation, complex variables TBD
5. **Scheduling:** PostAt support, but no workflow orchestration

---

## Future Enhancements

1. Implement `GeneratePost` with full template engine
2. Add Discord bot integration
3. Support media streaming/upload
4. Add scheduled posting with Durable Functions
5. Implement platform-specific formatting
6. Add post editing/deletion workflow
7. Support batch posting
8. Analytics integration

