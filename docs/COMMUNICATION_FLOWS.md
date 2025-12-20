# Communication Flows & Integration Guide

## Overview

This document details the communication flows between PostyFox components, showing how data and requests flow through the system for various scenarios.

---

## High-Level Message Flow

```
External Client
    ↓ (HTTP Request)
PostyFox-NetCore (API Gateway)
    ↓ (Validates, Configures)
Azure Storage (Tables, Queues, Blobs)
    ↓ (Stores, Queues)
PostyFox-Posting (Processing Engine)
    ├─→ PostyFox-TypeScript (Alternative Platforms)
    └─→ External Platform APIs
    ↓ (Updates Status)
Azure Storage (Result Tables)
    ↓ (Client polls)
External Client (Gets Results)
```

---

## Complete User Journey Flows

### Flow 1: Initial User Onboarding & API Key Generation

**Actors:** User, OIDC Provider, PostyFox-NetCore, Azure Storage

```
┌─────────┐
│  User   │
└────┬────┘
     │
     │ 1. Browser: GET /auth?redirect_uri=...
     ↓
┌──────────────────────┐
│  OIDC Provider       │
│  (Auth0, Okta, etc)  │
└────┬─────────────────┘
     │
     │ 2. User authenticates
     │    Receives JWT token
     │
     │ 3. Redirect to app with token
     ↓
┌─────────────────────────────────────────────┐
│  Client (Frontend/Mobile)                   │
│  Stores token in localStorage/secure storage│
└────┬────────────────────────────────────────┘
     │
     │ 4. GET /api/Profile_GenerateAPIToken
     │    Authorization: Bearer {jwt_token}
     ↓
┌────────────────────────────────────────────────┐
│  PostyFox-NetCore: Profile.cs                  │
│                                                │
│  1. Extract Authorization header               │
│  2. Validate JWT signature (OIDC public key)  │
│  3. Extract UserId from jwt.sub claim         │
│  4. Generate 40-char random API key           │
│  5. Create ProfileAPIKeyTableEntity           │
└────┬─────────────────────────────────────────┘
     │
     │ 6. POST to Azure Table Storage
     ↓
┌──────────────────────────────────────────┐
│  Azure Table Storage                     │
│  Table: UserProfilesAPIKeys              │
│  ┌──────────────────────────────────────┐│
│  │ PartitionKey: UserId                 ││
│  │ RowKey: key-guid-abc123              ││
│  │ APIKey: GENERATED_40_CHAR_KEY        ││
│  │ CreatedDate: 2025-12-18T10:00:00Z    ││
│  └──────────────────────────────────────┘│
└────┬──────────────────────────────────────┘
     │
     │ 7. Return 200 OK
     ↓
┌──────────────────────────────────────────┐
│  HTTP Response: 200 OK                   │
│  {                                       │
│    "APIKey": "GENERATED_40_CHAR_KEY",   │
│    "UserID": "user-123",                │
│    "ID": "key-guid-abc123"              │
│  }                                       │
└────┬──────────────────────────────────────┘
     │
     │ 8. Client stores API key
     ↓
┌─────────────────────────┐
│  Client: Secured Storage│
│  - JWT Token            │
│  - API Key              │
└─────────────────────────┘
```

**Key Points:**
- JWT token used for initial setup only
- API key used for subsequent requests
- Both stored securely on client
- Keys created on-demand, no pre-registration

---

### Flow 2: Fetching Available Services

**Actors:** Client, PostyFox-NetCore, Azure Storage

```
┌─────────┐
│ Client  │
└────┬────┘
     │
     │ GET /api/Services_GetAvailable
     │ Authorization: Bearer {api_key}
     ↓
┌────────────────────────────────────────────────┐
│  PostyFox-NetCore: Services.cs                 │
│                                                │
│  1. Extract API key from Authorization header │
│  2. Query UserProfilesAPIKeys table            │
│     WHERE APIKey == provided_key              │
│  3. Get UserId from matching row               │
│  4. Validate API key is not expired           │
└────┬─────────────────────────────────────────┘
     │
     │ 5. Query AvailableServices table
     │    WHERE PartitionKey == "Service"
     ↓
┌──────────────────────────────────────────┐
│  Azure Table Storage                     │
│  Table: AvailableServices                │
│  ┌──────────────────────────────────────┐│
│  │ PartitionKey: "Service"              ││
│  │ RowKey: "twitch"                     ││
│  │ ServiceName: "Twitch"                ││
│  │ IsEnabled: true                      ││
│  │ Configuration: {...}                 ││
│  ├──────────────────────────────────────┤│
│  │ RowKey: "telegram"                   ││
│  │ ServiceName: "Telegram"              ││
│  │ IsEnabled: true                      ││
│  ├──────────────────────────────────────┤│
│  │ RowKey: "discord"                    ││
│  │ ServiceName: "Discord"               ││
│  │ IsEnabled: true                      ││
│  └──────────────────────────────────────┘│
└────┬──────────────────────────────────────┘
     │
     │ 6. Convert to DTOs
     │    For each service row:
     │    Create ServiceDTO
     │
     │ 7. Return List<ServiceDTO>
     ↓
┌──────────────────────────────────────────┐
│  HTTP Response: 200 OK                   │
│  [                                       │
│    {                                     │
│      "ID": "service-twitch",             │
│      "ServiceID": "twitch",              │
│      "ServiceName": "Twitch",            │
│      "IsEnabled": true,                  │
│      "Configuration": {                  │
│        "channel_name": "text",           │
│        "channel_id": "text",             │
│        "webhook_url": "url"              │
│      },                                  │
│      "SecureConfiguration": {            │
│        "oauth_token": "secret",          │
│        "broadcast_key": "secret"         │
│      }                                   │
│    },                                    │
│    { "ServiceID": "telegram", ... }      │
│  ]                                       │
└──────────────────────────────────────────┘
```

**Key Points:**
- API key validation gates all requests
- Services registry is global (shared across all users)
- Configuration shows required fields
- SecureConfiguration indicates sensitive data needed

---

### Flow 3: Creating a Post with Immediate Publishing

**Actors:** Client, PostyFox-Posting, PostyFox-Posting (Queue), Azure Storage, External APIs

```
┌─────────┐
│ Client  │
└────┬────┘
     │
     │ POST /api/Post_CreatePost
     │ Authorization: Bearer {api_key}
     │ Body: {
     │   "APIKey": "api_key",
     │   "TargetPlatforms": ["twitch", "telegram"],
     │   "Content": "Hello everyone!",
     │   "Media": ["blob-ref-1"],
     │   "PostAt": "2025-12-18T10:00:00Z"
     │ }
     ↓
┌────────────────────────────────────────────────┐
│  PostyFox-Posting: Post.cs                     │
│                                                │
│  1. Extract API key from body                  │
│  2. Query UserProfilesAPIKeys table            │
│  3. Validate API key exists & is active       │
│  4. Get UserId from matching key               │
│  5. Validate target platforms                  │
│  6. Validate media references                  │
└────┬─────────────────────────────────────────┘
     │
     │ 7. Create root post record
     │    Generate RootPostId (GUID)
     │
     │ 8. For each target platform:
     │    - Create QueueEntry message
     │    - Add to PostingQueue (generatequeue)
     ↓
┌──────────────────────────────────────────┐
│  Azure Queue Storage                     │
│  Queue: generatequeue                    │
│  ┌──────────────────────────────────────┐│
│  │ Message 1:                           ││
│  │ {                                    ││
│  │   "RootPostId": "root-123",          ││
│  │   "PostId": "post-twitch-456",       ││
│  │   "User": "user-123",                ││
│  │   "TargetPlatformServiceId": "twitch"││
│  │   "Status": 0,                       ││
│  │   "Media": ["blob-ref-1"],           ││
│  │   "PostAt": "2025-12-18T10:00:00Z"   ││
│  │ }                                    ││
│  ├──────────────────────────────────────┤│
│  │ Message 2:                           ││
│  │ {                                    ││
│  │   "RootPostId": "root-123",          ││
│  │   "PostId": "post-telegram-789",     ││
│  │   "User": "user-123",                ││
│  │   "TargetPlatformServiceId": "telegram"
│  │   "Status": 0,                       ││
│  │   "Media": ["blob-ref-1"],           ││
│  │   "PostAt": "2025-12-18T10:00:00Z"   ││
│  │ }                                    ││
│  └──────────────────────────────────────┘│
└────┬──────────────────────────────────────┘
     │
     │ 9. Return 202 Accepted
     ↓
┌──────────────────────────────────────────┐
│  HTTP Response: 202 Accepted             │
│  {                                       │
│    "RootPostId": "root-123",             │
│    "PostId": "post-123",                 │
│    "Status": "Queued",                   │
│    "Platforms": ["twitch", "telegram"]   │
│  }                                       │
└──────────────────────────────────────────┘
     │
     │ 10. Client polls Post_GetStatus
     │     for updates (every 2-5 seconds)
```

**Queue Trigger - GenerateEntry Activation:**

```
┌──────────────────────────────────────────┐
│  Azure Functions Runtime                 │
│                                          │
│  Trigger: generatequeue message          │
│  Invokes: GenerateEntry.cs               │
└────┬───────────────────────────────────┘
     │
     │ 1. Parse QueueMessage (JSON deserialize)
     │ 2. Validate message structure
     │ 3. Lookup user from UserId
     │ 4. Fetch post template if specified
     ↓
┌────────────────────────────────────────────────┐
│  PostyFox-Common: Templating.cs                │
│                                                │
│  1. Load template from PostingTemplates table  │
│  2. Extract variables from post content        │
│  3. GeneratePostFromTemplate()                 │
│     - Replace {variables}                      │
│     - Format for each platform                 │
└────┬─────────────────────────────────────────┘
     │
     │ 4. Create platform-specific posts
     │    Twitch: Convert markdown to emote-friendly
     │    Telegram: Convert to HTML/Markdown
     │    Discord: Convert to embed format
     │
     │ 5. Create QueuePost messages
     │    Enqueue to PostingQueue (postingqueue)
     ↓
┌──────────────────────────────────────────┐
│  Azure Queue Storage                     │
│  Queue: postingqueue                     │
│  ┌──────────────────────────────────────┐│
│  │ Message 1:                           ││
│  │ {                                    ││
│  │   "RootPostId": "root-123",          ││
│  │   "PostId": "post-twitch-456",       ││
│  │   "User": "user-123",                ││
│  │   "TargetPlatformServiceId": "twitch"││
│  │   "FormattedContent": "Go live now!",││
│  │   "Status": 1,                       ││
│  │   "Media": ["blob-ref-1"]            ││
│  │ }                                    ││
│  └──────────────────────────────────────┘│
└────┬──────────────────────────────────────┘
     │
     │ 6. Update status in PostingStatus table
     │    Status = "Posting"
```

**Queue Trigger - QueuePost Activation:**

```
┌──────────────────────────────────────────┐
│  Azure Functions Runtime                 │
│                                          │
│  Trigger: postingqueue message           │
│  Invokes: QueuePost.Run()                │
└────┬───────────────────────────────────┘
     │
     │ 1. Parse QueuePost message
     │ 2. Determine platform
     │ 3. Route to platform handler
     │
     ├─→ Platform: "twitch"
     │  ↓
     │  ┌──────────────────────────────────────┐
     │  │ PostyFox-Posting: Twitch.cs          │
     │  │                                      │
     │  │ 1. Get user's Twitch token          │
     │  │    (from UserTwitchAuth table)      │
     │  │ 2. Call Twitch API:                 │
     │  │    POST /channels/{id}/announcements│
     │  │ 3. Handle response/errors            │
     │  │ 4. Update status: "Posted"          │
     │  └────┬─────────────────────────────────┘
     │       │
     │       ↓ (Twitch API)
     │      [External: Twitch Platform]
     │
     ├─→ Platform: "telegram"
     │  ↓
     │  ┌──────────────────────────────────────┐
     │  │ PostyFox-Posting: Telegram.cs        │
     │  │                                      │
     │  │ 1. Load encrypted Telegram session   │
     │  │    (from UserTelegramSessions)      │
     │  │ 2. Initialize WTelegramClient        │
     │  │ 3. Send message to channel           │
     │  │ 4. Handle media (if any)             │
     │  │ 5. Update status: "Posted"          │
     │  └────┬─────────────────────────────────┘
     │       │
     │       ↓ (Telegram MTProto)
     │      [External: Telegram Platform]
     │
     └─→ Platform: "bluesky" or "tumblr"
        ↓
        ┌──────────────────────────────────────┐
        │ PostyFox-TypeScript Function         │
        │                                      │
        │ 1. Route to appropriate function     │
        │ 2. Get user credentials from table   │
        │ 3. Call platform API                 │
        │ 4. Return status                     │
        └────┬─────────────────────────────────┘
             │
             ↓ (ATProto/Tumblr API)
            [External: Platform]
```

**Status Update & Result Storage:**

```
┌──────────────────────────────────────────────┐
│  PostyFox-Posting                            │
│  (Each platform handler)                     │
│                                              │
│  Platform post result:                       │
│  - Success: status = "Posted"                │
│  - Failure: status = "Faulted"               │
│  - Partial: status = "SomeFaults"            │
└────┬───────────────────────────────────────┘
     │
     │ Update PostingStatus table:
     │ PartitionKey: RootPostId
     │ RowKey: TargetPlatformServiceId
     │ Status: "Posted" | "Faulted" | "SomeFaults"
     │ ExternalPostId: platform_post_id
     │ ExternalPostUrl: platform_post_url
     │ Error: error_message_if_failed
     ↓
┌──────────────────────────────────────────┐
│  Azure Table Storage                     │
│  Table: PostingStatus                    │
│  ┌──────────────────────────────────────┐│
│  │ PartitionKey: root-123               ││
│  │ RowKey: twitch                       ││
│  │ Status: "Posted"                    ││
│  │ ExternalPostId: "123456789"          ││
│  │ ExternalPostUrl: "https://twitch..." ││
│  │ UpdatedDate: 2025-12-18T10:05:00Z    ││
│  └──────────────────────────────────────┘│
└────┬──────────────────────────────────────┘
     │
     │ 9. Client polls Post_GetStatus
     │    Returns aggregated status
     ↓
┌──────────────────────────────────────────┐
│  HTTP Response: 200 OK                   │
│  {                                       │
│    "RootPostId": "root-123",             │
│    "Status": "SomeFaults",               │
│    "PlatformStatuses": {                 │
│      "twitch": {                         │
│        "status": "Posted",               │
│        "postId": "123456789"             │
│      },                                  │
│      "telegram": {                       │
│        "status": "Faulted",              │
│        "error": "Auth token expired"     │
│      }                                   │
│    }                                     │
│  }                                       │
└──────────────────────────────────────────┘
```

---

### Flow 4: Twitch EventSub Integration (Reactive Posting)

**Actors:** Twitch, PostyFox-NetCore, PostyFox-Posting, Azure Storage

```
Twitch User performs action:
- Goes live
- Gets raided
- Reaches milestone
    │
    ↓
[Twitch EventSub Service]
    │
    │ Sends webhook to registered URL
    │ POST https://postyfox.azurewebsites.net/api/Twitch_EventHandler
    │ Header: Twitch-Eventsub-Signature
    │ Body: {
    │   "subscription": {
    │     "id": "...",
    │     "type": "stream.online",
    │     "condition": {"broadcaster_user_id": "123"}
    │   },
    │   "event": {
    │     "broadcaster_user_id": "123",
    │     "broadcaster_login": "channel_name",
    │     "broadcaster_user_login": "channel_name",
    │     "type": "live"
    │   }
    │ }
    ↓
┌────────────────────────────────────────────────┐
│  PostyFox-NetCore: Twitch.cs                   │
│  (EventSub Webhook Handler)                    │
│                                                │
│  1. Validate webhook signature                 │
│     (Using TwitchSignatureSecret)              │
│  2. Parse event type & data                    │
│  3. Lookup user who subscribed                 │
│  4. Find associated template                   │
│  5. Get target platforms                       │
│  6. Enqueue to generating queue                │
└────┬─────────────────────────────────────────┘
     │
     │ 7. Message enqueued to generatequeue
     │    Contains template & platform targets
     ↓
(Continues with GenerateEntry/QueuePost flow above)
```

**EventSub Webhook Setup:**

```
Client calls PostyFox-NetCore:
    ↓
POST /api/Twitch_RegisterSubscription
{
  "channelName": "popular_streamer",
  "channelId": "123456",
  "webhookPost": "https://postyfox.azurewebsites.net/events/twitch",
  "postTemplate": "template-456",
  "notifyFrequencyHrs": 24,
  "targetPlatform": "telegram"
}
    ↓
PostyFox-NetCore: Twitch.cs
    ↓
1. Validate user auth
2. Lookup channel on Twitch API
3. Call Twitch EventSub API:
   POST https://api.twitch.tv/helix/eventsub/subscriptions
   {
     "type": "stream.online",
     "version": "1",
     "condition": {
       "broadcaster_user_id": "123456"
     },
     "transport": {
       "method": "webhook",
       "callback": "https://postyfox.../events/twitch"
     }
   }
    ↓
4. Store subscription metadata in table:
   TwitchSubscriptions table
   PartitionKey: UserId
   RowKey: subscription_id
   ChannelId, ChannelName, Template, TargetPlatforms
    ↓
5. Subsequent Twitch events trigger webhook
   → EventSub handler processes
   → Auto-posts to user's platforms
```

---

## Platform-Specific Flows

### Telegram Authentication Flow

```
┌─────────┐
│ Client  │
└────┬────┘
     │
     │ POST /api/Telegram_Authenticate
     │ {
     │   "phoneNumber": "+1234567890"
     │ }
     ↓
┌────────────────────────────────────────────┐
│  PostyFox-Posting: Telegram.cs             │
│                                            │
│  1. Initialize WTelegramClient             │
│  2. Request auth code via phone            │
│  3. Store session state (encrypted)        │
│  4. Return "waiting for code" status       │
└────┬───────────────────────────────────────┘
     │
     │ Client receives code via SMS
     │
     │ POST /api/Telegram_AuthCode
     │ {
     │   "phoneNumber": "+1234567890",
     │   "code": "12345"
     │ }
     ↓
┌────────────────────────────────────────────┐
│  PostyFox-Posting: Telegram.cs             │
│                                            │
│  1. Load partial session from table        │
│  2. Verify code                            │
│  3. Complete authentication                │
│  4. Encrypt and store session              │
│     UserTelegramSessions table             │
│     PartitionKey: UserId                   │
│     RowKey: SessionId                      │
│     SessionData: encrypted_session         │
│  5. Return success status                  │
└────┬───────────────────────────────────────┘
     │
     │ Session ready for posting
     │ Stored for future use
     ↓
(Later posting uses stored session)
```

---

## Error Handling Flows

### API Key Validation Failure

```
Client Request
    │ (Invalid/expired API key)
    ↓
PostyFox-Posting: Post_CreatePost
    │
    │ Query UserProfilesAPIKeys table
    │ WHERE APIKey == provided_key
    │
    │ No match found OR key expired
    ↓
HTTP 401 Unauthorized
{
  "error": "Invalid or expired API key",
  "message": "Please generate a new API key"
}
    │
    │ Client must call Profile_GenerateAPIToken
    │ to get new key
```

### Platform API Failure

```
QueuePost processes message
    │
    ├─→ Call Twitch API
    │   HTTP 429: Rate limited
    │   OR HTTP 401: Token expired
    │   OR Network error
    ↓
Failure handling:
    │
    ├─ Retry logic activated
    │  ├─ Exponential backoff (2^n seconds)
    │  ├─ Max retries: 3
    │  └─ After max retries → Dead letter queue
    │
    ├─ Update status: "Faulted" (all failed)
    │             or "SomeFaults" (partial)
    │
    └─ Log error with context
       Platform, UserId, PostId, Error message
```

---

## Data Consistency & Atomicity

### Transactional Boundaries

**Atomic Operations:**
- Single table row insert/update
- Queue message enqueue (transactional)

**Non-Atomic Sequences:**
- Multi-table updates (eventual consistency)
- Queue + table updates (possible orphans on failure)

### Handling Failures

```
Ideal: Enqueue & Store atomically
Real: Queue first, then store status

If store fails after queue:
  → Function processes, posts, but can't update status
  → Manual reconciliation required via logs

Mitigation:
  → Implement dead-letter handling
  → Store status before queue when possible
  → Add reconciliation jobs
```

---

## Performance Characteristics

### Latency (End-to-End)

| Step | Duration |
|------|----------|
| API validation | 10-50ms |
| Queue enqueue | 5-10ms |
| Queue trigger (avg) | 1-5 seconds |
| Template processing | 100-500ms |
| Platform API call | 500ms-2s |
| Status update | 10-50ms |
| **Total** | **~3-10 seconds** |

### Throughput

| Metric | Value |
|--------|-------|
| Requests/sec per function | 1000+ |
| Auto-scale instances | 1-100 |
| Queue depth before throttle | 10,000 messages |
| Max parallel posts | 1000s (across instances) |

---

## Integration Checklist

When adding new platform/integration:

1. **Data Storage**
   - [ ] Add table entity for credentials
   - [ ] Add DTO for configuration
   - [ ] Update ServiceDTO registry

2. **Authentication**
   - [ ] Implement auth flow
   - [ ] Store secrets in KeyVault
   - [ ] Add session management

3. **Posting**
   - [ ] Create platform-specific handler
   - [ ] Implement content formatting
   - [ ] Handle retries/errors

4. **Status Tracking**
   - [ ] Update PostingStatus table
   - [ ] Return status to client
   - [ ] Handle partial failures

5. **Testing**
   - [ ] Unit tests for formatter
   - [ ] Integration tests with platform API
   - [ ] Load testing

---

## Troubleshooting Guide

### Post stuck in "Queued" status
- Check queue length (Azure portal)
- Verify function is running
- Review logs in Application Insights
- Manually trigger queue processing if needed

### Authentication failures
- Verify tokens not expired
- Check KeyVault has secrets
- Validate OIDC configuration
- Review auth logs

### Partial failures (SomeFaults)
- Expected for multi-platform posts
- Check individual platform status
- Review platform-specific error messages
- Retry manual posting to failed platform

### Performance degradation
- Check auto-scale metrics
- Review dependency (API call) latency
- Analyze queue depth
- Check Application Insights for bottlenecks

