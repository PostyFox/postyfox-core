# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0 AS base
WORKDIR /home/site/wwwroot
EXPOSE 8080


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["PostyFox-NetCore/nuget.config", "PostyFox-NetCore/"]
COPY ["nuget.config", "."]
COPY ["PostyFox-NetCore/PostyFox-NetCore.csproj", "PostyFox-NetCore/"]
COPY ["PostyFox-DataLayer/PostyFox-DataLayer.csproj", "PostyFox-DataLayer/"]
COPY ["Vendor/Twitch.Net.Api/Twitch.Net.Api.csproj", "Vendor/Twitch.Net.Api/"]
COPY ["Vendor/Twitch.Net.Shared/Twitch.Net.Shared.csproj", "Vendor/Twitch.Net.Shared/"]
COPY ["Vendor/Twitch.Net.EventSub/Twitch.Net.EventSub.csproj", "Vendor/Twitch.Net.EventSub/"]
RUN dotnet restore "./PostyFox-NetCore/PostyFox-NetCore.csproj"
COPY . .
WORKDIR "/src/PostyFox-NetCore"
RUN dotnet build "./PostyFox-NetCore.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./PostyFox-NetCore.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /home/site/wwwroot
COPY --from=publish /app/publish .
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true