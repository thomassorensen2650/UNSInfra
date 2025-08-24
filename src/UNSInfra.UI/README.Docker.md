# UNS Infrastructure UI - Docker Setup

This directory contains Docker configurations for building and running the UNS Infrastructure UI as a containerized application.

## 🐳 Quick Start

### Build and Run with Docker Compose (Recommended)

```bash
# Build and start the UI application
docker-compose up --build -d

# Access the application
open http://localhost:5000

# View logs
docker-compose logs -f unsinfra-ui

# Stop the application
docker-compose down
```

### Build with Docker

```bash
# Build the Docker image
./build-docker.sh

# Or manually
docker build -t unsinfra-ui:latest .

# Run the container
docker run -d -p 5000:8080 --name unsinfra-ui unsinfra-ui:latest
```

## 📁 Files Overview

| File | Description |
|------|-------------|
| `Dockerfile` | Multi-stage Docker build for the UI application |
| `docker-compose.yml` | Complete development/testing environment |
| `.dockerignore` | Excludes unnecessary files from build context |
| `build-docker.sh` | Automated build script with options |
| `README.Docker.md` | This documentation file |

## 🏗️ Docker Image Features

### Multi-Stage Build
- **Base Stage**: .NET 9 runtime optimized for size
- **Build Stage**: Full .NET 9 SDK for compilation
- **Publish Stage**: Optimized publication settings
- **Final Stage**: Minimal runtime with security hardening

### Security Features
- ✅ Non-root user execution
- ✅ Minimal base image (aspnet:9.0)
- ✅ Health checks included
- ✅ Resource limits configured
- ✅ Read-only file system where possible

### Performance Optimizations
- ✅ Layer caching for faster rebuilds
- ✅ Multi-stage build for smaller final image
- ✅ GC server mode enabled
- ✅ ReadyToRun compilation disabled for size
- ✅ Single file publication disabled for compatibility

## 🔧 Configuration Options

### Environment Variables

```bash
# Core Configuration
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
DOTNET_RUNNING_IN_CONTAINER=true

# Database Paths
Storage__ConnectionString="Data Source=/app/data/unsinfra.db"
HistoricalStorage__SQLite__DatabasePath="/app/data/unsinfra-historical.db"

# Logging
Logging__LogLevel__Default=Information
Logging__LogLevel__UNSInfra=Debug

# Performance
DOTNET_gcServer=1
DOTNET_GCRetainVM=1
DOTNET_EnableDiagnostics=0

# External Services
MqttConfiguration__BrokerHost=localhost
MqttConfiguration__BrokerPort=1883
SocketIOConfiguration__ServerUrl=http://localhost:3000
```

### Volume Mounts

```bash
# Data persistence
-v unsinfra-data:/app/data          # Database files
-v unsinfra-logs:/app/logs          # Application logs

# Development (optional)
-v ./:/app/src:ro                   # Source code for hot reload
```

### Port Mappings

```bash
-p 5000:8080    # HTTP (recommended)
-p 5001:8081    # HTTPS (if configured)
```

## 🚀 Build Script Usage

The `build-docker.sh` script provides convenient building options:

```bash
# Basic build
./build-docker.sh

# Build with specific tag
./build-docker.sh v1.5

# Build and run immediately
./build-docker.sh latest --run

# Build without cache
./build-docker.sh dev --no-cache

# Build and push to registry
./build-docker.sh latest --push
```

### Script Options

| Option | Description |
|--------|-------------|
| `TAG` | Docker image tag (default: latest) |
| `--run` | Build and run the container |
| `--push` | Push image to registry after build |
| `--no-cache` | Build without using cache |
| `--help` | Show help message |

## 📦 Docker Compose Profiles

The docker-compose.yml includes optional services via profiles:

```bash
# Run with MQTT broker
docker-compose --profile with-mqtt up -d

# Run with database viewer for debugging
docker-compose --profile debug up -d

# Run with all optional services
docker-compose --profile with-mqtt --profile debug up -d
```

### Available Services

| Service | Description | Profile | Port |
|---------|-------------|---------|------|
| `unsinfra-ui` | Main UI application | default | 5000:8080 |
| `mqtt-broker` | Eclipse Mosquitto broker | `with-mqtt` | 1883, 9001 |
| `sqlite-web` | Database web viewer | `debug` | 8081:8080 |

## 🔍 Monitoring and Debugging

### Health Checks

The container includes built-in health checks:

```bash
# Check container health
docker ps --filter name=unsinfra-ui

# View health status
docker inspect unsinfra-ui | grep -A 10 '"Health"'
```

### Viewing Logs

```bash
# Follow logs in real-time
docker logs -f unsinfra-ui

# View last 100 lines
docker logs --tail 100 unsinfra-ui

# Search logs
docker logs unsinfra-ui 2>&1 | grep "ERROR"
```

### Container Shell Access

```bash
# Interactive shell
docker exec -it unsinfra-ui /bin/bash

# Run commands directly
docker exec unsinfra-ui dotnet --version
docker exec unsinfra-ui ls -la /app/data
```

### Database Access

```bash
# Connect to SQLite database
docker exec -it unsinfra-ui sqlite3 /app/data/unsinfra.db

# Or use the web viewer (with debug profile)
docker-compose --profile debug up -d
open http://localhost:8081
```

## 🔧 Development Setup

### Hot Reload Development

For development with hot reload, modify the docker-compose.yml:

```yaml
volumes:
  - ./:/app/src:ro
environment:
  - ASPNETCORE_ENVIRONMENT=Development
  - DOTNET_USE_POLLING_FILE_WATCHER=true
```

### VS Code Integration

Create `.vscode/launch.json` for debugging:

```json
{
  "configurations": [
    {
      "name": "Docker: UNS Infrastructure UI",
      "type": "docker",
      "request": "launch",
      "preLaunchTask": "docker-run: debug",
      "netCore": {
        "appProject": "${workspaceFolder}/UNSInfra.UI.csproj"
      }
    }
  ]
}
```

## 📊 Performance Considerations

### Resource Limits

Default resource limits in docker-compose.yml:

```yaml
deploy:
  resources:
    limits:
      memory: 1G      # Maximum memory usage
      cpus: '1.0'     # Maximum CPU usage
    reservations:
      memory: 256M    # Reserved memory
      cpus: '0.25'    # Reserved CPU
```

### Image Size Optimization

Current image size is approximately **200-300MB** due to:
- Multi-stage build removing build dependencies
- Minimal aspnet:9.0 base image
- Optimized layer caching
- Excluded unnecessary files via .dockerignore

### Performance Tips

1. **Volume Mounting**: Mount `/app/data` for database persistence
2. **Memory Settings**: Adjust `DOTNET_gcServer` based on available memory
3. **CPU Settings**: Use `DOTNET_GCRetainVM=1` for better performance
4. **Logging**: Set appropriate log levels to reduce I/O overhead

## 🚨 Troubleshooting

### Common Issues

1. **Permission Denied**
   ```bash
   # Fix: Ensure volumes are writable
   sudo chown -R 1000:1000 /path/to/volume
   ```

2. **Port Already in Use**
   ```bash
   # Fix: Change port mapping
   docker run -p 5001:8080 unsinfra-ui
   ```

3. **Database Connection Issues**
   ```bash
   # Fix: Verify volume mount and permissions
   docker exec -it unsinfra-ui ls -la /app/data
   ```

4. **Out of Memory**
   ```bash
   # Fix: Increase memory limit
   docker run -m 2g unsinfra-ui
   ```

### Debug Commands

```bash
# Container information
docker inspect unsinfra-ui

# Resource usage
docker stats unsinfra-ui

# Process list
docker exec unsinfra-ui ps aux

# Disk usage
docker exec unsinfra-ui df -h

# Network connectivity
docker exec unsinfra-ui netstat -tlnp
```

## 📚 Additional Resources

- [Docker Best Practices](https://docs.docker.com/develop/dev-best-practices/)
- [.NET Docker Images](https://hub.docker.com/_/microsoft-dotnet)
- [ASP.NET Core in Docker](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/docker/)
- [UNS Infrastructure Main README](../../README.md)

---

**For production deployment, ensure proper security configurations, resource limits, and monitoring are in place.**