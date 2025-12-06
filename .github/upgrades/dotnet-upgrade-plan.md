# .NET 10.0 Upgrade Plan

## Execution Steps

Execute steps below sequentially one at a time in the order listed.

1. Validate that an .NET 10.0 SDK required for this upgrade is installed on the machine and if not, help to get it installed.
2. Ensure that the SDK version specified in global.json files is compatible with the .NET 10.0 upgrade.

3. Upgrade PostyFox-NetCore\PostyFox-NetCore.csproj


## Settings

### Excluded projects

| Project name                                   | Description                                         |
|:-----------------------------------------------|:---------------------------------------------------:|
| Vendor\Twitch.Net.Shared\Twitch.Net.Shared.csproj | Excluded by user request (do not upgrade)           |
| Vendor\Twitch.Net.Api\Twitch.Net.Api.csproj        | Excluded by user request (do not upgrade)           |
| Vendor\Twitch.Net.EventSub\Twitch.Net.EventSub.csproj | Excluded by user request (do not upgrade)       |
| PostyFox-DataLayer\PostyFox-DataLayer.csproj        | Excluded by user request (do not upgrade)           |
| PostyFox-Posting\PostyFox-Posting.csproj            | Excluded by user request (do not upgrade)           |
| PostyFox-Posting.Tests\PostyFox-Posting.Tests.csproj| Excluded by user request (do not upgrade)           |


### Aggregate NuGet packages modifications for `PostyFox-NetCore\PostyFox-NetCore.csproj`

| Package Name                                            | Current Version | New Version | Description                                                                                     |
|:--------------------------------------------------------|:---------------:|:-----------:|:------------------------------------------------------------------------------------------------|
| Microsoft.Azure.Functions.Worker                         | 2.0.0           | 2.51.0      | Recommended update for compatibility with .NET 10                                              |
| Microsoft.Azure.Functions.Worker.ApplicationInsights      | 2.0.0           | 2.50.0      | Recommended update for compatibility with .NET 10                                              |
| Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore | 2.0.2       | 2.1.0       | Recommended update for functions HTTP integration compatibility                                  |
| Microsoft.Azure.Functions.Worker.Extensions.ServiceBus   | 5.23.0          | 5.24.0      | Recommended update; consider also adding Microsoft.Azure.Functions.Worker.Extensions.EventHubs   |
| Microsoft.Azure.Functions.Worker.Extensions.EventHubs    |                 | 5.6.0       | Addition recommended alongside ServiceBus updates                                                |
| Microsoft.Azure.Functions.Worker.Sdk                     | 2.0.5           | 2.0.7       | SDK update recommended                                                                          |
| Microsoft.Extensions.Azure                               | 1.12.0          | 1.13.1      | Replace deprecated version which depends on deprecated Azure.Identity                          |
| Microsoft.Extensions.Configuration.Abstractions          | 9.0.8           | 10.0.0      | Update to match .NET 10 versions                                                                |
| Microsoft.Extensions.Logging                             | 9.0.8           | 10.0.0      | Update to match .NET 10 versions                                                                |
| Microsoft.VisualStudio.Azure.Containers.Tools.Targets    | 1.22.1          |             | No supported version found for .NET 10; remove or replace with alternative container tooling     |
| System.Net.Http                                         | 4.3.4           |             | Functionality included with new framework reference; remove package reference                   |
| System.Text.RegularExpressions                          | 4.3.1           |             | Functionality included with new framework reference; remove package reference                   |


### Project upgrade details

#### PostyFox-NetCore\PostyFox-NetCore.csproj

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

NuGet packages changes:
  - `Microsoft.Azure.Functions.Worker` update from `2.0.0` to `2.51.0`
  - `Microsoft.Azure.Functions.Worker.ApplicationInsights` update from `2.0.0` to `2.50.0`
  - `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore` update from `2.0.2` to `2.1.0`
  - `Microsoft.Azure.Functions.Worker.Extensions.ServiceBus` update from `5.23.0` to `5.24.0` (consider adding `Microsoft.Azure.Functions.Worker.Extensions.EventHubs` `5.6.0`)
  - `Microsoft.Azure.Functions.Worker.Sdk` update from `2.0.5` to `2.0.7`
  - `Microsoft.Extensions.Azure` update from `1.12.0` to `1.13.1`
  - `Microsoft.Extensions.Configuration.Abstractions` update from `9.0.8` to `10.0.0`
  - `Microsoft.Extensions.Logging` update from `9.0.8` to `10.0.0`

Other changes:
  - Remove `Microsoft.VisualStudio.Azure.Containers.Tools.Targets` package reference; no supported version found for .NET 10.
  - Remove package references for `System.Net.Http` and `System.Text.RegularExpressions` if present; functionality is included with framework.

Notes:
- Only `PostyFox-NetCore\PostyFox-NetCore.csproj` will be upgraded per your request. All other projects (Vendor and internal) will remain targeting `net8.0`.
- Leaving referenced projects targeting `net8.0` may cause build or runtime compatibility issues; if you want, I can upgrade dependencies after this project or adjust references.
