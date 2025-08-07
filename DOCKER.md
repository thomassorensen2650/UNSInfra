# Docker Deployment Guide

This document describes how to deploy UNS Infrastructure using Docker and Docker Compose.

## Quick Start

1. **Build and run with Docker Compose:**
   ```bash
   docker-compose up -d
   ```

2. **Access the application:**
   - Main UI: http://localhost:8080
   - MCP Server: http://localhost:5001 (if enabled)
   - MQTT Broker: tcp://localhost:1883
   - Database Admin: http://localhost:8081 (start with `--profile admin`)

## Architecture

The Docker setup includes:

- **UNSInfra UI**: Main Blazor Server application (.NET 9.0)
- **Mosquitto MQTT Broker**: Eclipse Mosquitto for MQTT communication
- **Adminer** (optional): Database administration interface
- **Volumes**: Persistent storage for SQLite databases and MQTT data

## Configuration

### Environment Variables

Key environment variables in `docker-compose.yml`:

#### Database Configuration
```yaml
- Storage__Provider=SQLite
- Storage__ConnectionString=Data Source=/app/data/unsinfra.db
- HistoricalStorage__StorageType=SQLite
- HistoricalStorage__SQLite__DatabasePath=/app/data/unsinfra-historical.db
```

#### MQTT Configuration
```yaml
- Mqtt__BrokerHost=mosquitto
- Mqtt__BrokerPort=1883
- Mqtt__ClientId=UNSInfra-Docker-Client
```

#### SocketIO Configuration
```yaml
- SocketIO__ServerUrl=https://virtualfactory.online:3000
- SocketIO__EnableReconnection=true
```

### Volumes

- `unsinfra-data:/app/data` - SQLite databases (persistent)
- `./logs:/app/logs` - Application logs (optional)
- MQTT broker data volumes for configuration and persistence

## Usage Commands

### Basic Operations
```bash
# Start all services
docker-compose up -d

# Start with database admin
docker-compose --profile admin up -d

# View logs
docker-compose logs -f unsinfra-ui

# Stop services
docker-compose down

# Stop and remove volumes (CAUTION: deletes all data)
docker-compose down -v
```

### Development
```bash
# Build without cache
docker-compose build --no-cache

# Start only specific service
docker-compose up -d unsinfra-ui

# Scale services (if needed)
docker-compose up -d --scale unsinfra-ui=2
```

### Backup and Restore
```bash
# Backup SQLite databases
docker run --rm -v unsinfra_unsinfra-data:/data -v $(pwd):/backup alpine tar czf /backup/unsinfra-backup.tar.gz -C /data .

# Restore SQLite databases
docker run --rm -v unsinfra_unsinfra-data:/data -v $(pwd):/backup alpine sh -c "cd /data && tar xzf /backup/unsinfra-backup.tar.gz"
```

## Port Mapping

| Service | Internal Port | External Port | Description |
|---------|---------------|---------------|-------------|
| UNSInfra UI | 8080 | 8080 | Main web interface |
| MCP Server | 5001 | 5001 | Model Context Protocol server |
| Mosquitto | 1883 | 1883 | MQTT broker |
| Mosquitto WS | 9001 | 9001 | MQTT WebSocket |
| Adminer | 8080 | 8081 | Database admin (optional) |

## Health Checks

The application includes health check endpoints:

- **UNSInfra UI**: `GET /health`
- **Mosquitto**: MQTT publish test

Health checks help ensure services are running correctly and enable proper container orchestration.

## Persistent Data

### SQLite Databases
- **Primary DB**: `/app/data/unsinfra.db` - Main application data
- **Historical DB**: `/app/data/unsinfra-historical.db` - Historical data storage

### MQTT Broker
- Configuration: `mosquitto-config` volume
- Data: `mosquitto-data` volume  
- Logs: `mosquitto-logs` volume

## Troubleshooting

### Common Issues

1. **Port conflicts**: Change external ports in `docker-compose.yml`
2. **Database permissions**: Ensure data directory is writable
3. **Memory issues**: Adjust container memory limits if needed

### Debug Commands
```bash
# Check service status
docker-compose ps

# View detailed logs
docker-compose logs -f --tail=100 unsinfra-ui

# Access container shell
docker-compose exec unsinfra-ui bash

# Check network connectivity
docker-compose exec unsinfra-ui ping mosquitto
```

### Performance Tuning

For production deployments:

1. **Resource Limits**: Add memory and CPU limits
2. **Logging**: Configure log rotation
3. **Monitoring**: Add monitoring stack (Prometheus/Grafana)
4. **Load Balancing**: Use reverse proxy (nginx/traefik)

## Security Considerations

- **Non-root user**: Container runs as non-root user
- **Network isolation**: Services communicate via internal network
- **Volume permissions**: Data directories have appropriate permissions
- **MQTT security**: Configure authentication for production use

## Updates and Maintenance

```bash
# Update to latest images
docker-compose pull
docker-compose up -d

# Clean up unused resources
docker system prune -f

# Monitor resource usage
docker stats
```