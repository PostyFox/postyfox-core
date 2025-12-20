# PostyFox Data Layer & Common Libraries

## Overview

**PostyFox-DataLayer** and **PostyFox-Common** provide shared code, data models, and utilities used across all Function Apps and services. These projects ensure consistency in data structures, DTOs, and business logic across the platform.

**Technology:** .NET Class Libraries (.NET 10.0)  
**Usage:** Referenced by PostyFox-NetCore and PostyFox-Posting  

---

## PostyFox-DataLayer

### Purpose
Central repository for all data transfer objects (DTOs) and Azure Table Storage entities. Ensures consistent data models across the platform.

### Architecture

```
PostyFox-DataLayer/
├── PostyFox-DataLayer.csproj
├── DTOs/
│   ├── ProfileAPIKeyDTO.cs
│   ├── ServiceDTO.cs
│   ├── PostingTemplateDTO.cs
│   └── PostTemplateDTO.cs
├── TableEntities/
│   ├── ProfileAPIKeyTableEntity.cs
│   ├── ServiceTableEntity.cs
│   ├── PostingTemplateTableEntity.cs
│   ├── ExternalInterestsTableEntity.cs
│   ├── ExternalTriggerTableEntity.cs
│   └── (Other entities)
└── TelegramStore.cs
```

---

## Data Transfer Objects (DTOs)

### ProfileAPIKeyDTO

**Purpose:** Represents an API key for client authentication

```csharp
public class ProfileAPIKeyDTO
{
    [OpenApiProperty(Description = "The actual API key")]
    public string? APIKey { get; set; }

    [OpenApiProperty(Description = "User identifier who owns this key")]
    public string? UserID { get; set; }

    [OpenApiProperty(Description = "Unique identifier for this key")]
    public string? ID { get; set; }
}
```

**Usage:**
- Returned when generating new API keys
- Truncated (first 6 chars) in list responses
- Used to authenticate posting requests

**Key Features:**
- Single use case: API authentication
- Created via `Profile_GenerateAPIToken`
- Listed via `Profile_GetAPITokens`

---

### ServiceDTO

**Purpose:** Describes an available social platform or service

```csharp
public class ServiceDTO
{
    [OpenApiProperty(Description = "Internal unique identifier")]
    public string? ID { get; set; }

    [OpenApiProperty(Description = "Service identifier (twitch, telegram, etc)")]
    public string? ServiceID { get; set; }

    [OpenApiProperty(Description = "Display name for the service")]
    public string? ServiceName { get; set; }

    [OpenApiProperty(Description = "Whether service is enabled")]
    public bool? IsEnabled { get; set; }

    [OpenApiProperty(Description = "Configuration required from user")]
    public Dictionary<string, object>? Configuration { get; set; }

    [OpenApiProperty(Description = "Sensitive configuration fields")]
    public Dictionary<string, object>? SecureConfiguration { get; set; }
}
```

**Usage:**
- Describes Twitch, Telegram, Discord, Bluesky, Tumblr
- Lists required configuration fields
- Indicates which fields are sensitive

**Example Services:**

```json
{
  "ID": "service-twitch",
  "ServiceID": "twitch",
  "ServiceName": "Twitch",
  "IsEnabled": true,
  "Configuration": {
    "channel_name": "text",
    "channel_id": "text",
    "webhook_url": "url"
  },
  "SecureConfiguration": {
    "oauth_token": "secret",
    "broadcast_key": "secret"
  }
}
```

---

### PostingTemplateDTO

**Purpose:** Represents a template for creating posts

```csharp
public class PostingTemplateDTO
{
    [OpenApiProperty(Description = "Internal unique identifier")]
    public string? ID { get; set; }

    [OpenApiProperty(Description = "Title for services supporting titles")]
    public string? Title { get; set; }

    [OpenApiProperty(Description = "Markdown-compatible body content")]
    public string? MarkdownBody { get; set; }
}
```

**Usage:**
- Create template via API
- Retrieve template by ID
- Use in post generation workflow

**Features:**
- Markdown support for rich formatting
- Title field for platforms requiring it
- Variables/placeholders (future enhancement)

**Example:**

```json
{
  "ID": "template-123",
  "Title": "Daily Stream Announcement",
  "MarkdownBody": "🎮 **Going Live!**\n\nJoining stream now at [Twitch](https://twitch.tv/mychannel)\n\nPlaying: {game}\nExpected duration: {duration} hrs\n\nSee you there!"
}
```

---

## Azure Table Storage Entities

### ProfileAPIKeyTableEntity

**Purpose:** Stores API keys for user authentication

```csharp
public class ProfileAPIKeyTableEntity : ITableEntity
{
    public string? PartitionKey { get; set; }    // UserId
    public string? RowKey { get; set; }          // Key GUID
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string? APIKey { get; set; }          // The actual key
    public DateTime? CreatedDate { get; set; }
    public DateTime? ExpiresAt { get; set; }     // Optional expiration
}
```

**Storage Schema:**
- **Table:** `UserProfilesAPIKeys`
- **PartitionKey:** UserId (user identifier from JWT)
- **RowKey:** Unique key GUID
- **TTL:** Optional expiration for security rotation

**Operations:**
- Create: Generate new key during `Profile_GenerateAPIToken`
- Read: Query by UserId to list keys
- Update: Extend expiration
- Delete: Revoke keys manually or on expiration

---

### ServiceTableEntity

**Purpose:** Registry of available platforms and their configuration

```csharp
public class ServiceTableEntity : ITableEntity
{
    public string? PartitionKey { get; set; }    // "Service"
    public string? RowKey { get; set; }          // Service name
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string? ServiceID { get; set; }
    public string? ServiceName { get; set; }
    public bool IsEnabled { get; set; }
    public string? Configuration { get; set; }   // JSON serialized
    public string? SecureConfiguration { get; set; }  // JSON serialized
}
```

**Storage Schema:**
- **Table:** `AvailableServices`
- **PartitionKey:** "Service" (constant)
- **RowKey:** Service name (twitch, telegram, discord, bluesky, tumblr)
- **Purpose:** Service discovery and configuration

**Data Example:**

| PartitionKey | RowKey | ServiceName | IsEnabled | Configuration |
|---|---|---|---|---|
| Service | twitch | Twitch | true | {...} |
| Service | telegram | Telegram | true | {...} |
| Service | discord | Discord | true | {...} |
| Service | bluesky | Bluesky | true | {...} |
| Service | tumblr | Tumblr | true | {...} |

---

### PostingTemplateTableEntity

**Purpose:** User-created post templates

```csharp
public class PostingTemplateTableEntity : ITableEntity
{
    public string? PartitionKey { get; set; }    // UserId
    public string? RowKey { get; set; }          // Template ID
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string? Title { get; set; }
    public string? MarkdownBody { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public string? CreatedBy { get; set; }       // UserId reference
}
```

**Storage Schema:**
- **Table:** `PostingTemplates`
- **PartitionKey:** UserId (template owner)
- **RowKey:** Template ID (GUID)
- **Data:** Template content and metadata

**Operations:**
- Create: Save new template
- Read: Query templates by UserId
- Update: Modify template
- Delete: Remove template

---

### ExternalInterestsTableEntity

**Purpose:** User interests/subscriptions for external triggers

```csharp
public class ExternalInterestsTableEntity : ITableEntity
{
    public string? PartitionKey { get; set; }    // UserId
    public string? RowKey { get; set; }          // Interest ID
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string? InterestType { get; set; }    // twitch_channel, telegram_user, etc
    public string? InterestValue { get; set; }   // Channel name, user ID, etc
    public string? AssociatedTemplate { get; set; }  // Template for posts
    public bool IsActive { get; set; }
    public DateTime? CreatedDate { get; set; }
}
```

**Purpose:**
- Track user interests in external events (e.g., specific Twitch channel went live)
- Trigger automated posting when conditions met

---

### ExternalTriggerTableEntity

**Purpose:** Manages triggers from external platforms

```csharp
public class ExternalTriggerTableEntity : ITableEntity
{
    public string? PartitionKey { get; set; }    // UserId
    public string? RowKey { get; set; }          // Trigger ID
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string? TriggerType { get; set; }     // twitch_live, telegram_message, etc
    public string? TriggerSource { get; set; }   // Source identifier
    public string? ActionPostTemplate { get; set; }  // Template to post
    public string? ActionTargetPlatforms { get; set; }  // Comma-separated
    public bool IsActive { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastTriggeredDate { get; set; }
}
```

**Trigger Types:**
- `twitch_live` - Channel goes live
- `twitch_raid` - Channel gets raided
- `telegram_message` - Message received
- `schedule` - Time-based trigger

---

## TelegramStore

**Purpose:** Manages Telegram session persistence

```csharp
public class TelegramStore
{
    public string? UserId { get; set; }
    public string? Phone { get; set; }
    public string? SessionData { get; set; }     // Encrypted session
    public DateTime? AuthDate { get; set; }
    public DateTime? LastActivity { get; set; }
    public bool IsAuthenticated { get; set; }
}
```

**Usage:**
- Store Telegram client sessions
- Handle re-authentication if session expires
- Track user Telegram accounts

**Security:**
- Session data encrypted
- Partitioned by UserId
- Access restricted to Telegram service

---

## PostyFox-Common

### Purpose

Shared business logic and utilities used across multiple projects.

### Architecture

```
PostyFox-Common/
├── PostyFox-Common.csproj
├── Templating.cs
└── (Other shared utilities)
```

---

## Templating Engine

### Templating.cs

**Purpose:** Template processing for post generation

```csharp
namespace PostyFox_Common
{
    public class Templating
    {
        public void GetTemplate()
        {
            // Get a template from the store
            // Retrieve by template ID
            // Return template object
        }

        public void GeneratePostFromTemplate()
        {
            // Generate a post from a template
            // Replace variables/placeholders
            // Format for target platform
            // Return formatted post
        }
    }
}
```

### Features (Planned)

1. **Variable Substitution**
   ```markdown
   {game} → Current game
   {duration} → Stream duration
   {viewers} → Current viewer count
   {date} → Current date
   {time} → Current time
   ```

2. **Markdown Processing**
   ```markdown
   **Bold** → Bold text
   *Italic* → Italic text
   [Link](url) → Hyperlink
   # Heading → Platform-specific heading
   ```

3. **Platform-Specific Formatting**
   - Twitch: Emotes, channel references
   - Telegram: HTML/Markdown formatting
   - Bluesky: AT Protocol facets (mentions, links)
   - Twitter: Character limits, hashtags

4. **Conditional Content**
   ```
   {if game}Playing {game}{/if}
   {if viewers > 100}Large audience!{/if}
   ```

---

## Data Flow Through Layer

### Creating a Post with Template

```
User API Call
    ↓
PostyFox-Posting: Post_CreatePost()
    ↓
Request Body includes:
  - PostTemplate: "template-123"
  - TargetPlatforms: ["twitch", "telegram"]
    ↓
PostyFox-DataLayer.PostingTemplateDTO
  - Load template from table
  - ID, Title, MarkdownBody
    ↓
PostyFox-Common.Templating
  - GetTemplate() by ID
  - GeneratePostFromTemplate()
    ↓
Platform-Specific Post
  - Formatted for Twitch
  - Formatted for Telegram
    ↓
QueueEntry Message
  - One per platform
  - Includes formatted content
    ↓
External Platform Post
```

---

## Dependencies

### Project References

**PostyFox-DataLayer:**
- No external project dependencies
- Minimal NuGet dependencies:
  - `Azure.Data.Tables` - Table entity interfaces
  - `Microsoft.Azure.WebJobs.Extensions.OpenApi` - OpenAPI attributes

**PostyFox-Common:**
- References: `PostyFox-DataLayer`
- Provides: `Templating` to PostyFox-Posting

### Dependency Graph

```
PostyFox-NetCore
    ↓
    └─→ PostyFox-DataLayer
            ↓
            └─→ PostyFox-Common

PostyFox-Posting
    ↓
    ├─→ PostyFox-DataLayer
    │       ↓
    │       └─→ PostyFox-Common
    └─→ PostyFox-Common
```

---

## Extension Points

### Adding New DTOs

1. Create new class in `DTOs/`
2. Inherit from appropriate base
3. Add `[OpenApiProperty]` attributes
4. Document usage

```csharp
public class NewDTO
{
    [OpenApiProperty(Description = "...")]
    public string? Property { get; set; }
}
```

### Adding New Table Entities

1. Create class in `TableEntities/`
2. Inherit from `ITableEntity`
3. Implement required properties
4. Document storage schema

```csharp
public class NewTableEntity : ITableEntity
{
    public string? PartitionKey { get; set; }
    public string? RowKey { get; set; }
    // ... other properties
}
```

### Adding Common Logic

1. Create new class in root or subfolder
2. Follow naming conventions
3. Document public methods
4. Add unit tests

---

## Design Patterns

### DTO vs Entity

- **DTOs:** Used in HTTP requests/responses
- **Entities:** Used for Azure Table Storage
- **Mapping:** Convert between in Function code

### Partition Key Strategy

- **User Data:** PartitionKey = UserId
- **Global Data:** PartitionKey = "Global" or "Service"
- **Time-Series:** PartitionKey = "YYYY-MM"

### Row Key Strategy

- **Unique Data:** RowKey = GUID
- **Identifiable Data:** RowKey = Meaningful ID
- **Time-Series:** RowKey = Timestamp + ID

---

## Best Practices

1. **Immutability:** DTOs are typically immutable
2. **Validation:** Validate on receipt in Functions
3. **Serialization:** Use Newtonsoft.Json attributes
4. **Documentation:** Add `[OpenApiProperty]` to all public properties
5. **Naming:** Use PascalCase for class members
6. **Comments:** Document non-obvious logic

---

## Future Enhancements

1. **Versioning:** Support multiple DTO versions
2. **Validation Rules:** Add FluentValidation
3. **Mapping:** Implement AutoMapper for entity↔DTO conversion
4. **Advanced Templates:** Implement full template language
5. **Analytics:** Add tracking entities
6. **Audit:** Add audit trail entities

