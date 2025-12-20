# PostyFox Core Architecture

## Overview

PostyFox is a distributed, serverless platform built on Azure Functions that enables users to manage, schedule, and publish content across multiple social media and messaging platforms. The system consists of multiple Azure Function Apps working together with shared data layers and integration services.

## System Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                    PostyFox Core Platform                           │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────┐  ┌──────────────────┐  ┌──────────────────┐
│ PostyFox-NetCore    │  │ PostyFox-Posting │  │PostyFox-TypeScript│
│  (C# / .NET 10.0)   │  │ (C# / .NET 10.0) │  │ (TypeScript/Node) │
│  Azure Function App │  │ Azure Function   │  │  Azure Function   │
│       v4            │  │     App v4       │  │      App v4       │
└────────┬────────────┘  └────────┬─────────┘  └────────┬──────────┘
         │                        │                     │
         │ HTTP Triggers          │ Queue Triggers     │ HTTP Triggers
         │ EventSub               │ HTTP Triggers      │
         │ Service Bus            │                    │
         │                        │                    │
         └────────────────────────┼────────────────────┘
                                  │
                    ┌─────────────┴──────────────┐
                    │   Shared Resources        │
                    ├──────────────────────────┤
                    │ • ConfigTable            │
                    │ • StorageAccount (Blobs) │
                    │ • PostingQueue           │
                    │ • KeyVault (Secrets)     │
                    └──────────────────────────┘
                                  │
                    ┌─────────────┴──────────────┐
                    │  Data & Integration       │
                    ├──────────────────────────┤
                    │ PostyFox-DataLayer       │
                    │ PostyFox-Common          │
                    │ Vendor Libraries         │
                    └──────────────────────────┘
                                  │
                    ┌─────────────┴──────────────┐
                    │  External Integrations   │
                    ├──────────────────────────┤
                    │ • Twitch API             │
                    │ • Telegram (MTProto)     │
                    │ • Discord Webhooks       │
                    │ • ATProto (Bluesky)      │
                    │ • Tumblr API             │
                    └──────────────────────────┘
```

## Major Components

### 1. **PostyFox-NetCore** (API & Configuration)
**Purpose:** Core API layer for user management, authentication, and platform configuration.

**Key Responsibilities:**
- User authentication and authorization via OIDC
- Profile management and API token generation
- Service registry and configuration
- Platform integration setup (Twitch, Telegram, Discord)
- EventSub webhook management for Twitch

**Key Classes:**
- `Profile.cs` - User authentication, API key management
- `Services.cs` - Available services registry and configuration
- `PostingTemplate.cs` - Template management for posts
- `Integrations/*` - Platform-specific integration endpoints

**Framework:** Azure Functions v4, .NET 10.0

**Deployment:** Azure Function App

---

### 2. **PostyFox-Posting** (Post Processing & Delivery)
**Purpose:** Handles asynchronous post generation and delivery to external platforms.

**Key Responsibilities:**
- Process posting requests from queue
- Generate posts from templates
- Deliver content to target platforms
- Handle platform-specific formatting and requirements
- Manage posting workflow and status tracking

**Key Classes:**
- `Post.cs` - Main posting endpoint and status management
- `GenerateEntry.cs` - Queue-triggered post generation
- `GeneratePost.cs` - Template-to-post generation logic
- `Telegram.cs` - Telegram-specific posting logic
- `Twitch.cs` - Twitch-specific posting logic
- `QueueEntry.cs` - Queue message structure
- `QueuePost.cs` - Post queue processing

**Framework:** Azure Functions v4, .NET 10.0

**Deployment:** Azure Function App

**Queue Integration:** Reads from "generatequeue" for post generation

---

### 3. **PostyFox-TypeScript** (Supplementary Integrations)
**Purpose:** Handles additional platform integrations not covered by .NET services.

**Supported Platforms:**
- Bluesky (via @atproto/api)
- Tumblr (via tumblr.js)

**Key Features:**
- Alternative platform support
- TypeScript/Node.js based processing
- Modular function structure

**Framework:** Azure Functions v4, Node.js 24.x, TypeScript

**Deployment:** Azure Function App

---

### 4. **PostyFox-DataLayer**
**Purpose:** Shared data models and Azure Table Storage entities.

**Key Components:**
- **DTOs:** Data Transfer Objects for API communication
  - `ProfileAPIKeyDTO` - API key representation
  - `ServiceDTO` - Service configuration
  - `PostingTemplateDTO` - Post template structure
  
- **Table Entities:** Azure Table Storage entities
  - `ProfileAPIKeyTableEntity` - User API keys
  - `ServiceTableEntity` - Available services
  - `PostingTemplateTableEntity` - Saved templates
  - `ExternalInterestsTableEntity` - User interests/subscriptions
  - `ExternalTriggerTableEntity` - Event triggers
  - `TelegramStore.cs` - Telegram session storage

**Dependencies:** Used by all other projects

---

### 5. **PostyFox-Common**
**Purpose:** Shared utility code for templating and common functionality.

**Key Components:**
- `Templating.cs` - Post template processing (under development)

**Dependencies:** Used by DataLayer and Function apps

---

### 6. **Vendor Libraries**
**Purpose:** Third-party Twitch API implementations.

**Included Packages:**
- `Twitch.Net.Api` - Twitch API client
- `Twitch.Net.EventSub` - Twitch EventSub webhooks
- `Twitch.Net.Shared` - Shared Twitch utilities
- `Twitch.Net.PubSub` - Twitch PubSub support (potential)
- `Twitch.Net.Communication` - Twitch communication base

---

## Shared Infrastructure

### Azure Storage Resources

| Resource | Purpose | Used By |
|----------|---------|---------|
| **ConfigTable** | Azure Table Storage for all configuration data | NetCore, Posting, TypeScript |
| **StorageAccount** | Blob Storage for media files and assets | NetCore, Posting |
| **PostingQueue** | Azure Queue Storage for posting workflow | Posting (reads), External callers (writes) |
| **KeyVault** | Secrets management for API credentials | All projects via ISecretsProvider |

### Table Schemas

**UserProfilesAPIKeys:**
- PartitionKey: UserId
- RowKey: Unique key ID
- Data: APIKey, CreatedDate

**AvailableServices:**
- PartitionKey: "Service"
- RowKey: Service name (e.g., "twitch", "telegram", "discord")
- Data: ServiceID, ServiceName, IsEnabled, Configuration, SecureConfiguration

**PostingTemplates:**
- PartitionKey: UserId
- RowKey: Template ID
- Data: Title, MarkdownBody, CreatedDate

**UserTelegramSessions:**
- PartitionKey: UserId
- RowKey: SessionId
- Data: Phone, Session state, Auth tokens

**ExternalSubscriptions/Interests:**
- PartitionKey: UserId
- RowKey: Subscription ID
- Data: Platform, Channel ID, Post Template, Frequency

## Security Architecture

### Authentication Flow
1. User authenticates via OIDC provider
2. Bearer token is issued by OIDC provider
3. Token is validated in Azure Functions via auth headers
4. User identity extracted from JWT claims

### Secrets Management
- **Azure KeyVault:** Production environment
- **Infisical:** Alternative secrets backend
- **Environment Variables:** Local development with fallback

### API Key Management
- Users can generate multiple API keys via `Profile_GenerateAPIToken`
- Keys stored securely in Table Storage
- Keys truncated in responses for security

---

## Communication Flows

### Flow 1: User Profile Setup

```
User Browser/Client
    ↓ (HTTP GET)
[OIDC Provider] ← Authenticate
    ↓ (Returns Bearer Token)
PostyFox-NetCore: Profile_GenerateAPIToken
    ↓ (Validates auth)
PostyFox-DataLayer → Create ProfileAPIKeyTableEntity
    ↓ (Stores to)
Azure Table Storage (UserProfilesAPIKeys)
    ↓ (Returns)
Client: ProfileAPIKeyDTO
```

### Flow 2: Service Configuration

```
User/Client
    ↓ (HTTP GET with Bearer Token)
PostyFox-NetCore: Services_GetAvailable
    ↓ (Validates auth)
Azure Table Storage (AvailableServices)
    ↓ (Returns)
Client: List<ServiceDTO>
```

### Flow 3: Twitch Integration Setup

```
User
    ↓ (HTTP POST - Twitch_RegisterSubscription)
PostyFox-NetCore: Twitch.cs
    ↓ (Validates auth, prepares subscription)
[Twitch EventSub API]
    ↓ (Registers webhook)
[Twitch Webhook] ← Events come to
PostyFox-NetCore: EventSub handler
    ↓ (Triggers post generation)
Azure Queue: PostingQueue (Enqueues message)
```

### Flow 4: Post Creation & Publishing

```
Client
    ↓ (HTTP POST - Post_CreatePost)
PostyFox-Posting: Post.cs
    ↓ (Validates API key, queues work)
Azure Queue: PostingQueue (generatedqueue)
    ↓ (Triggered by)
PostyFox-Posting: GeneratePost.cs (QueueTrigger)
    ↓ (Generates post, queues publishing)
Azure Queue: PostingQueue (posting queue)
    ↓ (Triggered by)
PostyFox-Posting: QueuePost.cs
    ├─→ PostyFox-Posting: Twitch.cs (POST to Twitch API)
    ├─→ PostyFox-Posting: Telegram.cs (Send via Telegram API)
    ├─→ PostyFox-TypeScript: Bluesky Integration
    └─→ PostyFox-TypeScript: Tumblr Integration
    ↓ (Updates status)
Azure Table Storage (PostingStatus)
```

### Flow 5: Telegram Integration

```
User/Client
    ↓ (HTTP POST - Setup credentials)
PostyFox-Posting: Telegram.cs
    ↓ (Validates secrets from KeyVault)
[Telegram MTProto API]
    ↓ (Authenticates phone/credentials)
Azure Table Storage (UserTelegramSessions)
    ↓ (Stores encrypted session)
PostyFox-Posting: Telegram.cs
    ├─→ Send messages via MTProto
    └─→ Receive/process messages
```

### Flow 6: Post Template Workflow

```
User
    ↓ (Creates template)
PostyFox-NetCore or PostyFox-TypeScript
    ↓ (Stores to)
Azure Table Storage (PostingTemplates)
    ↓
PostyFox-Common: Templating.cs
    ├─→ GetTemplate() - Retrieves template
    └─→ GeneratePostFromTemplate() - Process template
    ↓ (Returns formatted post)
PostyFox-Posting: Post.cs (Uses for posting)
```

---

## Data Flow Summary

```
User Input
    ↓
PostyFox-NetCore (Validation, Configuration)
    ↓
Azure Storage (Tables, Queues, Blobs)
    ↓
PostyFox-Posting (Processing, Publishing)
    ├─→ External APIs (Twitch, Telegram, Discord)
    └─→ PostyFox-TypeScript (Alternative platforms)
    ↓
External Social Platforms
    ↓
User Followers/Audience
```

---

## Technology Stack

| Layer | Technology |
|-------|-----------|
| **Cloud Platform** | Microsoft Azure |
| **API Layer** | Azure Functions v4 |
| **Authentication** | OIDC + JWT Bearer Tokens |
| **Data Storage** | Azure Table Storage, Azure Blob Storage |
| **Message Queues** | Azure Queue Storage, Service Bus |
| **Secrets Management** | Azure KeyVault / Infisical |
| **.NET Runtime** | .NET 10.0 (C#) |
| **Node Runtime** | Node.js 24.x (TypeScript) |
| **External APIs** | Twitch, Telegram, Discord, Bluesky, Tumblr |
| **Monitoring** | Application Insights |
| **IaC** | Terraform |

---

## Development & Deployment

### Local Development Setup
1. Azurite Storage Emulator
2. Azure Function App Runtime v4
3. Visual Studio Code or Visual Studio 2022
4. Environment variables for local configuration

### CI/CD
- GitHub Actions for automated deployment
- Separate deployment pipelines per function app
- Terraform for infrastructure provisioning

### Environment Configuration
- **Development:** Local Azurite, environment variables
- **Staging/Production:** Azure resources, KeyVault secrets

---

## Key Design Patterns

1. **Serverless Functions:** Event-driven, stateless compute
2. **Asynchronous Queues:** Decouples post creation from delivery
3. **Shared Data Layer:** Centralized schemas across all services
4. **Dependency Injection:** Loose coupling via DI containers
5. **Multi-Platform Support:** Abstracted integration interfaces
6. **Secrets Management:** Centralized credential handling

