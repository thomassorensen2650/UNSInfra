# Use the official .NET 9 runtime as a parent image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files and restore as distinct layers
COPY ["src/UNSInfra.UI/UNSInfra.UI.csproj", "src/UNSInfra.UI/"]
COPY ["src/UNSInfra.Core/UNSInfra.Core.csproj", "src/UNSInfra.Core/"]
COPY ["src/UNSInfra.Services.V1/UNSInfra.Services.V1.csproj", "src/UNSInfra.Services.V1/"]
COPY ["src/UNSInfra.Services.SocketIO/UNSInfra.Services.SocketIO.csproj", "src/UNSInfra.Services.SocketIO/"]
COPY ["src/UNSInfra.Storage.InMemory/UNSInfra.Storage.InMemory.csproj", "src/UNSInfra.Storage.InMemory/"]
COPY ["src/UNSInfra.Storage.SQLite/UNSInfra.Storage.SQLite.csproj", "src/UNSInfra.Storage.SQLite/"]
COPY ["src/UNSInfra.MCP.Server/UNSInfra.MCP.Server.csproj", "src/UNSInfra.MCP.Server/"]

# Restore dependencies
RUN dotnet restore "src/UNSInfra.UI/UNSInfra.UI.csproj"

# Copy the entire source code
COPY . .

# Build the application
WORKDIR "/src/src/UNSInfra.UI"
RUN dotnet build "UNSInfra.UI.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "UNSInfra.UI.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Build runtime image
FROM base AS final
WORKDIR /app

# Create directory for SQLite database
RUN mkdir -p /app/data

# Copy published application
COPY --from=publish /app/publish .

# Set environment variables for containerized deployment
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Configure SQLite database path for container
ENV Storage__ConnectionString="Data Source=/app/data/unsinfra.db"
ENV HistoricalStorage__SQLite__DatabasePath="/app/data/unsinfra-historical.db"

# Set the user to non-root for security
USER $APP_UID

ENTRYPOINT ["dotnet", "UNSInfra.UI.dll"]