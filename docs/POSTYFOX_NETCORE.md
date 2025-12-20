# PostyFox-NetCore Function App

## Overview

**PostyFox-NetCore** is the primary API gateway for the PostyFox platform. It handles user authentication, profile management, API key generation, service configuration, and platform integration setup for Twitch, Telegram, and Discord.

**Technology:** Azure Functions v4, .NET 10.0 (C#)  
**Deployment:** Azure Function App  
**Authentication:** OIDC + Bearer Token validation  

---

## Architecture

### Project Structure

```
PostyFox-NetCore/
├── Program.cs                 # Startup configuration, DI setup
├── PostyFox-NetCore.csproj   # Project dependencies
├── host.json                 # Function runtime config
├── local.settings.json       # Local development config
├── Dockerfile                # Container configuration
├── Core/
│   ├── Profile.cs           # User profile & API key management
│   ├── PostingTemplate.cs   # Template management
│   └── Services.cs          # Service registry
├── Integrations/
│   ├── Twitch.cs           # Twitch EventSub & registration
│   ├── Telegram.cs         # Telegram integration
│   └── Discord.cs          # Discord webhook integration
├── Helpers/
│   ├── AuthHelper.cs       # Authentication utilities
│   ├── ClaimsPrincipalParser.cs  # JWT parsing
│   └── StaticState.cs      # Shared state
└── Properties/
    └── launchSettings.json
```

### Dependencies

**Azure Services:**
- `Azure.Data.Tables` - Table Storage client
- `Azure.Storage.Blobs` - Blob Storage client
- `Azure.Security.KeyVault.Secrets` - KeyVault integration
- `Microsoft.Azure.Functions.Worker` - Functions runtime
- `Microsoft.Azure.Functions.Worker.Extensions.*` - Function triggers & bindings

**Authentication & Secrets:**
- `Neillans.Adapters.Secrets.Core` - Secrets provider abstraction
- `Neillans.Adapters.Secrets.AzureKeyVault` - KeyVault provider
- `Neillans.Adapters.Secrets.Infisical` - Infisical provider

**External APIs:**
- `Twitch.Net.Api` - Twitch API client
- `Twitch.Net.EventSub` - Twitch EventSub support

**Other:**
- `Microsoft.Azure.Functions.Worker.Extensions.OpenApi` - Swagger/OpenAPI
- `Microsoft.ApplicationInsights.WorkerService` - Application Insights
- `Newtonsoft.Json` - JSON serialization

---

## Core Functions

### Profile Management

#### `Profile_Ping`
- **Route:** `GET /api/Profile_Ping`
- **Purpose:** Health check endpoint
- **Returns:** Request headers
- **Auth:** Anonymous

#### `Profile_GenerateAPIToken`
- **Route:** `GET /api/Profile_GenerateAPIToken`
- **Purpose:** Generate a new API token for the authenticated user
- **Auth:** Bearer Token (OIDC)
- **Process:**
  1. Validates OIDC bearer token
  2. Extracts user ID from JWT claims
  3. Generates 40-character random key
  4. Stores in `UserProfilesAPIKeys` table
  5. Returns `ProfileAPIKeyDTO`
- **Returns:** 
  ```json
  {
    "APIKey": "GENERATED_KEY",
    "UserID": "user-id",
    "ID": "key-id"
  }
  ```
- **Errors:**
  - 401 Unauthorized if token invalid
  - 500 if storage write fails

#### `Profile_GetAPITokens`
- **Route:** `GET /api/Profile_GetAPITokens`
- **Purpose:** Retrieve all API tokens for user
- **Auth:** Bearer Token (OIDC)
- **Returns:** List of truncated API keys (first 6 chars only)
- **Notes:** Only first 6 characters returned for security

---

### Service Management

#### `Services_Ping`
- **Route:** `GET /api/Services_Ping`
- **Purpose:** Health check
- **Returns:** Request headers

#### `Services_GetAvailable`
- **Route:** `GET /api/Services_GetAvailable`
- **Purpose:** List all available social platforms for posting
- **Auth:** Bearer Token (OIDC)
- **Query Parameters:** None
- **Returns:** 
  ```json
  [
    {
      "ID": "service-id",
      "ServiceID": "twitch",
      "ServiceName": "Twitch",
      "IsEnabled": true,
      "Configuration": {...},
      "SecureConfiguration": {...}
    }
  ]
  ```
- **Data Source:** `AvailableServices` table with PartitionKey="Service"

#### `Services_GetAvailableService`
- **Route:** `GET /api/Services_GetAvailableService?service={serviceName}`
- **Purpose:** Get specific service configuration
- **Auth:** Bearer Token (OIDC)
- **Query Parameters:**
  - `service` (required): Service name (e.g., "twitch", "telegram")
- **Returns:** Single `ServiceDTO`

---

### Twitch Integration

#### `Twitch_RegisterSubscription`
- **Route:** `POST /api/Twitch_RegisterSubscription`
- **Purpose:** Register a Twitch channel for event monitoring
- **Auth:** Bearer Token (OIDC)
- **Request Body:**
  ```json
  {
    "channelName": "channel_name",
    "channelId": "123456",
    "webhookPost": "webhook_url",
    "postTemplate": "template_id",
    "notifyFrequencyHrs": 24,
    "targetPlatform": "twitch|telegram|discord"
  }
  ```
- **Process:**
  1. Validates user authentication
  2. Verifies channel exists via Twitch API
  3. Sets up EventSub subscription
  4. Stores subscription config in table
  5. Returns success/error status
- **Returns:** 
  - 200 OK: Subscription registered
  - 401 Unauthorized: Auth failed
  - 404 Not Found: Channel not found
  - 500 Error: Subscription setup failed

---

### Posting Templates

#### `PostingTemplate_*` (Planned)
- **Purpose:** CRUD operations for post templates
- **Status:** Under development
- **Storage:** `PostingTemplates` table

---

### Telegram Integration

#### `Telegram_Ping`
- **Route:** `GET /api/Telegram_Ping`
- **Purpose:** Health check
- **Returns:** Request headers

#### Additional Telegram functions
- **Status:** Placeholder implementation
- **Purpose:** Future webhook integration for Telegram

---

### Discord Integration

#### `Discord_Ping`
- **Route:** `GET /api/Discord_Ping`
- **Purpose:** Health check
- **Returns:** Request headers

#### Additional Discord functions
- **Status:** Placeholder for future webhook integration
- **Note:** Currently supports webhook calls only, not bot integration

---

## Configuration

### Environment Variables

```env
# Storage
ConfigTable=https://<account>.table.core.windows.net
StorageAccount=https://<account>.blob.core.windows.net

# Twitch
TwitchClientId=<client-id>
TwitchClientSecret=<secret>  # Stored in KeyVault
TwitchCallbackUrl=https://<function-app>.azurewebsites.net/auth/callback
TwitchSignatureSecret=<signature-secret>  # Stored in KeyVault

# Secrets Management
SecretStore=https://<keyvault-name>.vault.azure.net/
Infisical_Url=https://infisical.example.com
Infisical_ApiKey=<api-key>

# Development Mode
PostyFoxDevMode=true  # Optional, enables local credential fallback
```

### Local Development (Azurite)

```json
{
  "AzureWebJobsStorage": "UseDevelopmentStorage=true",
  "ConfigTable": "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;...",
  "StorageAccount": "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;...",
  "TwitchClientId": "...",
  "TwitchClientSecret": "...",
  "TwitchCallbackUrl": "http://localhost:7071",
  "TwitchSignatureSecret": "...",
  "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
}
```

---

## Authentication Architecture

### Bearer Token Validation

The `AuthHelper.ValidateAuth()` method:
1. Extracts `Authorization` header
2. Parses "Bearer {token}" format
3. Validates token signature using OIDC provider's public key
4. Checks token expiration
5. Verifies issuer and audience claims

### User Identity Extraction

`AuthHelper.GetAuthId()` extracts user identifier from JWT claims:
- Typically uses `sub` claim (subject/unique identifier)
- Falls back to other claims if needed

### Sample JWT Claims
```json
{
  "iss": "https://oidc-provider.example.com",
  "sub": "user-unique-id",
  "aud": "postyfox-api",
  "exp": 1234567890,
  "iat": 1234567800,
  "email": "user@example.com",
  "email_verified": true
}
```

---

## Data Storage

### Tables Used

| Table Name | Purpose | Partition Key | Row Key |
|-----------|---------|---------------|---------|
| **UserProfilesAPIKeys** | User API tokens | UserId | Key GUID |
| **AvailableServices** | Platform registry | "Service" | Service name |
| **PostingTemplates** | User templates (planned) | UserId | Template ID |
| **TwitchSubscriptions** | Twitch event subs | UserId | Subscription ID |
| **UserTelegramSessions** | Telegram auth | UserId | SessionId |

### Blob Storage
- Profile images
- Media uploads for posts
- Telegram session files

---

## OpenAPI/Swagger

All endpoints are documented via `Microsoft.Azure.Functions.Worker.Extensions.OpenApi`:

- **Decorators:** `[OpenApiOperation]`, `[OpenApiRequestBody]`, `[OpenApiResponseWithBody]`
- **Generated Swagger:** Available at `{function-app-url}/api/swagger/ui`
- **Visibility:** Tagged by type (profile, services, twitch, etc.)

---

## Dependency Injection

### Service Registration (Program.cs)

```csharp
services.AddApplicationInsightsTelemetryWorkerService();
services.ConfigureFunctionsApplicationInsights();

services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddTableServiceClient(new Uri(tableAccount))
        .WithName("ConfigTable");
    clientBuilder.AddBlobServiceClient(new Uri(storageAccount))
        .WithName("StorageAccount");
    clientBuilder.UseCredential(new DefaultAzureCredential());
});

services.AddSecretsProviderFactory();
services.AddTransient<Profile>();
services.AddTransient<Services>();
services.AddTransient<Twitch>();
services.AddTransient<Telegram>();
services.AddTransient<Discord>();
```

### Constructor Injection Pattern

```csharp
public Profile(
    ILoggerFactory loggerFactory, 
    IAzureClientFactory<TableServiceClient> clientFactory,
    ISecretsProvider? secretsProvider,  // Optional
    IAzureClientFactory<BlobServiceClient> blobClientFactory
)
{
    _logger = loggerFactory.CreateLogger<Profile>();
    _configTable = clientFactory.CreateClient("ConfigTable");
    _secretsProvider = secretsProvider;
    _blobStorageAccount = blobClientFactory.CreateClient("StorageAccount");
}
```

---

## Error Handling

### Standard Response Codes

- **200 OK:** Success
- **400 Bad Request:** Invalid parameters
- **401 Unauthorized:** Missing/invalid auth token
- **404 Not Found:** Resource not found
- **500 Internal Server Error:** Server error

### Logging

All operations logged via `ILogger`:
- Auth attempts
- API calls
- Storage operations
- External API interactions

### Secrets Handling

- Never logged directly
- Loaded via KeyVault/Infisical at startup
- Passed to external APIs via headers

---

## Local Development

### Prerequisites

1. Azure Functions Core Tools v4
2. Azurite storage emulator
3. .NET 10.0 SDK
4. Visual Studio Code + Azure Functions Extension (recommended)

### Running Locally

```powershell
# Ensure Azurite is running
# Start the function app
func start

# Test endpoint
curl http://localhost:7071/api/Services_GetAvailable \
  -H "Authorization: Bearer {token}"
```

### Debugging

- VS Code: Launch configuration in `.vscode/launch.json`
- Breakpoints work in function classes
- View logs in integrated terminal

---

## Deployment

### Azure Deployment

```powershell
# Build
dotnet build

# Publish
dotnet publish -c Release

# Deploy to Azure
func azure functionapp publish <function-app-name>
```

### GitHub Actions

- Automated builds on push
- Runs tests
- Deploys to Azure environments

---

## Security Considerations

1. **API Keys:** Truncated in responses
2. **Secrets:** Never logged, always KeyVault-managed
3. **Auth:** OIDC provider handles authentication
4. **HTTPS:** All connections use TLS
5. **CORS:** Configured per deployment environment
6. **Rate Limiting:** Handled by Azure Functions

---

## Monitoring & Diagnostics

### Application Insights

- Integrated via `Microsoft.ApplicationInsights.WorkerService`
- Tracks:
  - Function execution times
  - Exceptions and errors
  - External API calls
  - Storage operations

### Health Checks

- `*_Ping` endpoints on all services
- Used by load balancers and monitoring

### Logging

- Structured logging via `ILogger`
- Log levels: Trace, Debug, Information, Warning, Error, Critical

