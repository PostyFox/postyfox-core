# syntax=docker/dockerfile:1
# Multi-stage build shared by all PostyFox .NET services.
# Build with: --build-arg PROJECT=<csproj path> --build-arg ASSEMBLY=<dll name>
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT
WORKDIR /src
COPY . .
RUN dotnet restore "$PROJECT"
# ImageSharp licence. CI passes it as a build secret so it never lands in a layer or build arg:
#   docker build --secret id=sixlabors_license,src=sixlabors.lic ...
# Without the secret, a sixlabors.lic in the repo root (local dev) arrives via COPY above and is
# picked up by Directory.Build.props. Either way it stays in this build stage, never the final image.
RUN --mount=type=secret,id=sixlabors_license,target=/run/secrets/sixlabors.lic \
    LIC=""; [ -s /run/secrets/sixlabors.lic ] && LIC="/p:SixLaborsLicenseFile=/run/secrets/sixlabors.lic"; \
    dotnet publish "$PROJECT" -c Release -o /app --no-restore /p:UseAppHost=false $LIC

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
# curl is required by the container healthcheck (not present in the aspnet base image).
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*
COPY --from=build /app .
ARG ASSEMBLY
ENV ASSEMBLY=$ASSEMBLY
# Human-readable build version, surfaced by the /api/version endpoint.
ARG VERSION=0.0.0-dev
ENV POSTYFOX_VERSION=$VERSION
# Shell form so $ASSEMBLY expands at runtime.
ENTRYPOINT ["sh", "-c", "exec dotnet \"$ASSEMBLY\""]

# ffmpeg (FFMpegCore shells out to it for media normalisation) is only ever invoked by the posting
# worker's delivery pipeline: core-api and post-api link the same Infrastructure assembly but
# never reach that code path. Build with --target with-ffmpeg for the worker only; core-api/post-api
# use the (default) `final` target above and save the ~150MB the package + its codec deps pull in.
FROM final AS with-ffmpeg
RUN apt-get update \
 && apt-get install -y --no-install-recommends ffmpeg \
 && rm -rf /var/lib/apt/lists/*
