# PostyFox Core Documentation

Welcome to the PostyFox Core documentation. This folder contains comprehensive guides for understanding the architecture, components, and communication flows of the PostyFox platform.

## Quick Navigation

### 📋 Start Here
- **[ARCHITECTURE.md](./ARCHITECTURE.md)** - High-level system overview, component relationships, and technology stack

### 🏗️ Project Documentation

Each major project has dedicated documentation:

| Project | Purpose | Documentation |
|---------|---------|---|
| **PostyFox-NetCore** | API Gateway & Configuration | [POSTYFOX_NETCORE.md](./POSTYFOX_NETCORE.md) |
| **PostyFox-Posting** | Post Processing & Delivery | [POSTYFOX_POSTING.md](./POSTYFOX_POSTING.md) |
| **PostyFox-TypeScript** | Alternative Platform Support | [POSTYFOX_TYPESCRIPT.md](./POSTYFOX_TYPESCRIPT.md) |
| **PostyFox-DataLayer** | Shared Data Models | [DATA_LAYER_AND_COMMON.md](./DATA_LAYER_AND_COMMON.md) |
| **PostyFox-Common** | Shared Business Logic | [DATA_LAYER_AND_COMMON.md](./DATA_LAYER_AND_COMMON.md) |

### 🔄 Integration & Communication

- **[COMMUNICATION_FLOWS.md](./COMMUNICATION_FLOWS.md)** - Detailed request/response flows between components, user journeys, and platform integrations

---

## Document Guide

### [ARCHITECTURE.md](./ARCHITECTURE.md)

**Read this first for understanding:**
- System overview and component layout
- Shared infrastructure (Azure Storage, KeyVault)
- Security architecture and authentication
- Design patterns and deployment strategy
- High-level communication diagrams

**Best for:**
- New team members getting oriented
- Architecture reviews
- System design decisions
- Technology stack overview

---

### [POSTYFOX_NETCORE.md](./POSTYFOX_NETCORE.md)

**Covers the API Gateway layer:**
- All HTTP endpoints and their signatures
- Profile management (authentication, API keys)
- Service registry and configuration
- Twitch, Telegram, Discord integrations
- OpenAPI/Swagger documentation
- Dependency injection setup
- Local development setup

**Best for:**
- API developers
- Authentication troubleshooting
- Adding new endpoints
- Integration setup

**Key Functions:**
- `Profile_Ping` - Health check
- `Profile_GenerateAPIToken` - Create API key
- `Profile_GetAPITokens` - List user's keys
- `Services_GetAvailable` - List platforms
- `Twitch_RegisterSubscription` - Setup event monitoring

---

### [POSTYFOX_POSTING.md](./POSTYFOX_POSTING.md)

**Covers the processing and delivery engine:**
- Post creation and validation
- Queue-based processing workflow
- Template generation
- Platform-specific posting logic
- Status tracking and error handling
- Telegram and Twitch integration details
- Testing and debugging
- Performance characteristics

**Best for:**
- Backend/processing developers
- Post delivery troubleshooting
- Adding new platforms
- Queue management

**Key Functions:**
- `Post_CreatePost` - Submit new post
- `Post_GetStatus` - Check posting progress
- `GenerateEntry` - Queue trigger for generation
- `QueuePost` - Queue trigger for delivery

---

### [POSTYFOX_TYPESCRIPT.md](./POSTYFOX_TYPESCRIPT.md)

**Covers alternative platform integrations:**
- Bluesky (AT Protocol) support
- Tumblr integration
- TypeScript/Node.js patterns
- Utility functions and helpers
- Data models for platform auth
- Local development setup
- Performance and security

**Best for:**
- TypeScript developers
- Alternative platform integration
- Node.js specific issues

**Supported Platforms:**
- Bluesky (via @atproto/api)
- Tumblr (via tumblr.js)

---

### [DATA_LAYER_AND_COMMON.md](./DATA_LAYER_AND_COMMON.md)

**Covers shared libraries:**
- PostyFox-DataLayer: All DTOs and table entities
- PostyFox-Common: Template engine and utilities
- Data models for each service
- Azure Table Storage schema
- DTOs used in APIs
- Design patterns and best practices

**Best for:**
- Data model changes
- Adding new entities/DTOs
- Schema design
- Understanding data flow

**Key DTOs:**
- `ProfileAPIKeyDTO` - API key representation
- `ServiceDTO` - Platform configuration
- `PostingTemplateDTO` - Post templates

**Key Entities:**
- `ProfileAPIKeyTableEntity` - User keys
- `ServiceTableEntity` - Platform registry
- `PostingTemplateTableEntity` - Saved templates
- `ExternalTriggerTableEntity` - Event triggers

---

### [COMMUNICATION_FLOWS.md](./COMMUNICATION_FLOWS.md)

**Covers end-to-end request flows:**
- User onboarding (API key generation)
- Fetching available services
- Creating and publishing posts
- Queue processing workflows
- Twitch EventSub integration
- Telegram authentication
- Error handling flows
- Performance characteristics
- Troubleshooting guide

**Best for:**
- Understanding complete user journeys
- Integration testing
- Debugging multi-component issues
- System troubleshooting

**Major Flows:**
1. User onboarding & API key generation
2. Service discovery
3. Post creation and publishing
4. Queue processing
5. Twitch EventSub integration
6. Telegram authentication

---

## Common Tasks

### I want to...

#### ...understand how the system works
1. Read [ARCHITECTURE.md](./ARCHITECTURE.md) for overview
2. Review [COMMUNICATION_FLOWS.md](./COMMUNICATION_FLOWS.md) for example flows
3. Deep dive into specific project docs

#### ...add a new social platform
1. Check [ARCHITECTURE.md](./ARCHITECTURE.md) for integration patterns
2. Decide: .NET (NetCore or Posting) or TypeScript
3. Read [POSTYFOX_POSTING.md](./POSTYFOX_POSTING.md) or [POSTYFOX_TYPESCRIPT.md](./POSTYFOX_TYPESCRIPT.md)
4. Follow platform integration section
5. Add DTOs to [DATA_LAYER_AND_COMMON.md](./DATA_LAYER_AND_COMMON.md) entities

#### ...fix authentication issues
1. Check [POSTYFOX_NETCORE.md](./POSTYFOX_NETCORE.md) authentication section
2. Review [COMMUNICATION_FLOWS.md](./COMMUNICATION_FLOWS.md) for error handling
3. Verify OIDC configuration
4. Check API key in table storage

#### ...debug a failing post
1. Review post status via `Post_GetStatus`
2. Check [COMMUNICATION_FLOWS.md](./COMMUNICATION_FLOWS.md) error handling flow
3. Review logs in Application Insights
4. Check platform-specific handler (Twitch.cs, Telegram.cs, etc)

#### ...add an API endpoint
1. Choose appropriate project (NetCore for user-facing, Posting for processing)
2. Read project documentation
3. Follow OpenAPI decoration patterns
4. Add DTOs to DataLayer if needed
5. Test with Postman

#### ...deploy a change
1. Review [ARCHITECTURE.md](./ARCHITECTURE.md) deployment section
2. Ensure all projects build without errors
3. Deploy via GitHub Actions
4. Verify endpoints in Azure Functions portal

---

## Key Concepts

### Authentication
- **OIDC Provider:** Initial authentication, JWT tokens
- **API Keys:** Used for subsequent requests to PostyFox APIs
- **Secrets Management:** KeyVault for platform credentials

### Data Flow
1. User makes HTTP request with API key
2. NetCore validates key and processes request
3. Data stored in Azure Tables/Blobs
4. Messages enqueued to Azure Queues
5. Posting app processes queue messages
6. Posts sent to external platforms
7. Status updated in storage
8. Client polls for results

### Queue-Based Processing
- **generatequeue:** Post generation and formatting
- **postingqueue:** Post delivery to platforms
- Enables async, scalable processing
- Supports retries and error handling

### Multi-Platform Support
- **NetCore-handled:** Twitch, Telegram, Discord
- **TypeScript-handled:** Bluesky, Tumblr
- **Pattern:** Platform-specific formatter + external API call

---

## Architecture Highlights

### Component Separation

```
User/Client
    ↓
PostyFox-NetCore (HTTP API, Config)
    ↓
Azure Storage (Tables, Queues, Blobs)
    ↓
PostyFox-Posting (Processing)
PostyFox-TypeScript (Alt Platforms)
    ↓
External Social Platforms
    ↓
Audience/Followers
```

### Async Processing

```
Fast Path (User-facing):
HTTP Request → Validation → Queue Enqueue → Response (202)

Slow Path (Background):
Queue → Trigger Function → Process → External API → Status Update

Client polls for status updates
```

### Scalability

- **Auto-scaling:** Azure Functions scale based on queue depth
- **Multiple instances:** Independent platform handlers run in parallel
- **Idempotency:** Failed posts can be retried safely

---

## Technology Stack Summary

| Component | Technology |
|-----------|-----------|
| Cloud | Microsoft Azure |
| Compute | Azure Functions v4 |
| APIs | C# .NET 10.0 + TypeScript/Node.js |
| Data | Azure Table Storage |
| Media | Azure Blob Storage |
| Queues | Azure Queue Storage |
| Secrets | Azure KeyVault |
| Auth | OIDC + Bearer Tokens |
| Monitoring | Application Insights |
| IaC | Terraform |

---

## Getting Started for Developers

### Prerequisites
- Azure Functions Core Tools v4
- Azurite storage emulator
- Visual Studio Code or Visual Studio 2022
- .NET 10.0 SDK
- Node.js 24.x (for TypeScript functions)

### First Steps
1. Read [ARCHITECTURE.md](./ARCHITECTURE.md)
2. Choose your component:
   - API work → [POSTYFOX_NETCORE.md](./POSTYFOX_NETCORE.md)
   - Processing → [POSTYFOX_POSTING.md](./POSTYFOX_POSTING.md)
   - Alternative platforms → [POSTYFOX_TYPESCRIPT.md](./POSTYFOX_TYPESCRIPT.md)
   - Data models → [DATA_LAYER_AND_COMMON.md](./DATA_LAYER_AND_COMMON.md)
3. Review [COMMUNICATION_FLOWS.md](./COMMUNICATION_FLOWS.md) for example flows
4. Set up local environment (see project docs)
5. Start developing!

---

## Troubleshooting Quick Links

- **Post stuck in "Queued":** See [COMMUNICATION_FLOWS.md - Troubleshooting](./COMMUNICATION_FLOWS.md#troubleshooting-guide)
- **Auth failures:** See [POSTYFOX_NETCORE.md - Authentication Architecture](./POSTYFOX_NETCORE.md#authentication-architecture)
- **Platform integration issues:** See specific platform section in [POSTYFOX_POSTING.md](./POSTYFOX_POSTING.md)
- **Queue processing problems:** See [POSTYFOX_POSTING.md - Error Handling & Retries](./POSTYFOX_POSTING.md#error-handling--retries)

---

## Contributing

When adding new features or components:

1. **Update DTOs:** Add to [DATA_LAYER_AND_COMMON.md](./DATA_LAYER_AND_COMMON.md)
2. **Update Flows:** Add sequence diagrams to [COMMUNICATION_FLOWS.md](./COMMUNICATION_FLOWS.md)
3. **Document API:** Add to appropriate project doc
4. **Update Architecture:** Update [ARCHITECTURE.md](./ARCHITECTURE.md) if adding new components
5. **Test:** Follow patterns in project docs

---

## Document Maintenance

Last Updated: December 2025

These documents are living documentation. Please keep them updated as the system evolves:
- New endpoints → Update project doc
- Architecture changes → Update ARCHITECTURE.md
- New integrations → Add to appropriate docs
- Breaking changes → Update COMMUNICATION_FLOWS.md

---

## Quick Reference Tables

### HTTP Endpoints by Project

**PostyFox-NetCore:**
- `GET /api/Profile_Ping`
- `GET /api/Profile_GenerateAPIToken`
- `GET /api/Profile_GetAPITokens`
- `GET /api/Services_GetAvailable`
- `GET /api/Services_GetAvailableService?service={name}`
- `POST /api/Twitch_RegisterSubscription`

**PostyFox-Posting:**
- `GET /api/Post_Ping`
- `POST /api/Post_CreatePost`
- `GET /api/Post_GetStatus?postId={postId}`

**PostyFox-TypeScript:**
- `GET /api/ping`
- `POST /api/blueskyPost`
- `POST /api/tumblrPost`

### Azure Storage Tables

| Table | Purpose | Partition Key |
|-------|---------|---|
| UserProfilesAPIKeys | API key storage | UserId |
| AvailableServices | Service registry | "Service" |
| PostingTemplates | User templates | UserId |
| PostingStatus | Post progress | RootPostId |
| UserTelegramSessions | Telegram auth | UserId |
| TwitchSubscriptions | EventSub registrations | UserId |

### Queues

| Queue | Purpose | Processor |
|-------|---------|-----------|
| generatequeue | Post generation | GenerateEntry.cs |
| postingqueue | Post delivery | QueuePost.cs |

---

## Support & Questions

For questions about:
- **Specific project:** See that project's documentation
- **Architecture decisions:** See ARCHITECTURE.md
- **How data flows:** See COMMUNICATION_FLOWS.md
- **Data models:** See DATA_LAYER_AND_COMMON.md
- **Integration issues:** See COMMUNICATION_FLOWS.md and project docs

