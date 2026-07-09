# Multi-stage build shared by all PostyFox .NET services.
# Build with: --build-arg PROJECT=<csproj path> --build-arg ASSEMBLY=<dll name>
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT
WORKDIR /src
COPY . .
RUN dotnet restore "$PROJECT"
RUN dotnet publish "$PROJECT" -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
ARG ASSEMBLY
ENV ASSEMBLY=$ASSEMBLY
# Shell form so $ASSEMBLY expands at runtime.
ENTRYPOINT ["sh", "-c", "exec dotnet \"$ASSEMBLY\""]
