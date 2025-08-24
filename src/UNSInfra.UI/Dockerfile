# UNS Infrastructure UI - Dockerfile
# Multi-stage build for optimized container size and security

# Use the official .NET 9 runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Create non-root user for security
RUN groupadd -r unsinfra && useradd --no-log-init -r -g unsinfra unsinfra

# Use SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files and restore dependencies (optimized layer caching)
COPY ["UNSInfra.UI.csproj", "./UNSInfra.UI/"]
COPY ["../UNSInfra.Core/UNSInfra.Core.csproj", "./UNSInfra.Core/"]
COPY ["../UNSInfra.Services.V1/UNSInfra.Services.V1.csproj", "./UNSInfra.Services.V1/"]
COPY ["../UNSInfra.Services.SocketIO/UNSInfra.Services.SocketIO.csproj", "./UNSInfra.Services.SocketIO/"]
COPY ["../UNSInfra.Storage.InMemory/UNSInfra.Storage.InMemory.csproj", "./UNSInfra.Storage.InMemory/"]
COPY ["../UNSInfra.Storage.SQLite/UNSInfra.Storage.SQLite.csproj", "./UNSInfra.Storage.SQLite/"]
COPY ["../UNSInfra.ConnectionSDK/UNSInfra.ConnectionSDK.csproj", "./UNSInfra.ConnectionSDK/"]

# Restore NuGet packages
RUN dotnet restore "./UNSInfra.UI/UNSInfra.UI.csproj"

# Copy source code
COPY . ./UNSInfra.UI/
COPY ../UNSInfra.Core/ ./UNSInfra.Core/
COPY ../UNSInfra.Services.V1/ ./UNSInfra.Services.V1/
COPY ../UNSInfra.Services.SocketIO/ ./UNSInfra.Services.SocketIO/
COPY ../UNSInfra.Storage.InMemory/ ./UNSInfra.Storage.InMemory/
COPY ../UNSInfra.Storage.SQLite/ ./UNSInfra.Storage.SQLite/
COPY ../UNSInfra.ConnectionSDK/ ./UNSInfra.ConnectionSDK/

# Build the application
WORKDIR /src/UNSInfra.UI
RUN dotnet build "UNSInfra.UI.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "UNSInfra.UI.csproj" -c Release -o /app/publish \
    --no-restore \
    --no-build \
    /p:UseAppHost=false \
    /p:PublishReadyToRun=false \
    /p:PublishSingleFile=false

# Build runtime image
FROM base AS final
WORKDIR /app

# Create directories for data persistence
RUN mkdir -p /app/data /app/logs && \
    chown -R unsinfra:unsinfra /app

# Install required packages for SQLite and performance monitoring
RUN apt-get update && apt-get install -y --no-install-recommends \
    sqlite3 \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=publish /app/publish .
RUN chown -R unsinfra:unsinfra /app

# Configure environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# UNS Infrastructure specific configurations
ENV Storage__ConnectionString="Data Source=/app/data/unsinfra.db"
ENV HistoricalStorage__SQLite__DatabasePath="/app/data/unsinfra-historical.db"
ENV Logging__LogLevel__Default=Information
ENV Logging__LogLevel__UNSInfra=Debug

# Performance optimizations
ENV DOTNET_gcServer=1
ENV DOTNET_GCRetainVM=1
ENV DOTNET_EnableDiagnostics=0

# Health check endpoint
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Switch to non-root user
USER unsinfra

# Set entry point
ENTRYPOINT ["dotnet", "UNSInfra.UI.dll"]

# Add labels for better container management
LABEL org.opencontainers.image.title="UNS Infrastructure UI"
LABEL org.opencontainers.image.description="Unified Namespace Infrastructure Web UI - Blazor Server Application"
LABEL org.opencontainers.image.vendor="UNS Infrastructure Team"
LABEL org.opencontainers.image.version="1.5"
LABEL org.opencontainers.image.source="https://github.com/your-org/UNSInfra"
LABEL org.opencontainers.image.documentation="https://github.com/your-org/UNSInfra/blob/main/README.md"