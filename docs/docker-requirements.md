# Docker Requirements for SignalBeam Edge Agent

This document outlines the Docker requirements for running the SignalBeam Edge Agent with the Docker Container Manager.

## Prerequisites

### Docker Engine

The Edge Agent requires Docker Engine to be installed and running on the edge device.

**Minimum Supported Versions:**
- Docker Engine: 20.10.0 or later
- Docker API: v1.41 or later

**Supported Platforms:**
- Linux (x86_64, ARM64, ARMv7)
- Windows (with Docker Desktop or Docker Engine)
- macOS (with Docker Desktop)

### Installation

#### Linux

```bash
# Ubuntu/Debian
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh
sudo usermod -aG docker $USER

# Start and enable Docker service
sudo systemctl start docker
sudo systemctl enable docker
```

#### Raspberry Pi (ARM)

```bash
# Same as Linux, Docker supports ARM architecture
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh
sudo usermod -aG docker pi

# Start and enable Docker service
sudo systemctl start docker
sudo systemctl enable docker
```

#### Windows

Download and install [Docker Desktop for Windows](https://www.docker.com/products/docker-desktop/)

#### macOS

Download and install [Docker Desktop for Mac](https://www.docker.com/products/docker-desktop/)

## Docker Socket Configuration

The Edge Agent communicates with Docker via the Docker socket.

### Unix/Linux

**Default Socket Location:** `unix:///var/run/docker.sock`

**Permissions:**
- The Edge Agent service user must have access to the Docker socket
- Either add the user to the `docker` group or run the Edge Agent with elevated privileges

```bash
# Add user to docker group
sudo usermod -aG docker <edge-agent-user>

# Verify docker socket permissions
ls -l /var/run/docker.sock
# Should show: srwxrwxrwx or similar with docker group
```

### Windows

**Default Socket Location:** `npipe://./pipe/docker_engine`

**Permissions:**
- The Edge Agent must run with appropriate permissions to access the named pipe
- Typically requires running as Administrator or a user in the docker-users group

### Custom Socket Location

If Docker is configured with a custom socket location, set the `DOCKER_HOST` environment variable:

```bash
export DOCKER_HOST=tcp://localhost:2375  # For TCP
export DOCKER_HOST=unix:///custom/path/docker.sock  # For custom Unix socket
```

## Resource Requirements

### Minimum Requirements per Edge Device

- **CPU:** 1 core (2+ cores recommended)
- **RAM:** 512 MB for Docker daemon + container memory
- **Disk Space:**
  - Docker: 2 GB
  - Container images: Varies by application bundles
  - Recommended: 10+ GB available space

### Docker Daemon Configuration

Create or edit `/etc/docker/daemon.json`:

```json
{
  "log-driver": "json-file",
  "log-opts": {
    "max-size": "10m",
    "max-file": "3"
  },
  "storage-driver": "overlay2",
  "live-restore": true
}
```

**Recommended Settings:**
- `log-driver`: Use `json-file` or `local` for log management
- `log-opts`: Limit log file size to prevent disk space issues
- `storage-driver`: `overlay2` (default and recommended)
- `live-restore`: `true` to keep containers running during Docker daemon updates

Apply changes:
```bash
sudo systemctl restart docker
```

## Network Requirements

### Outbound Connectivity

The Edge Agent requires outbound internet connectivity for:
- **Container Registry Access:** Pull container images from registries (Docker Hub, GitHub Container Registry, Azure Container Registry, etc.)
- **Cloud API Communication:** Connect to SignalBeam cloud services
- **Image Updates:** Download new bundle versions

**Required Ports (Outbound):**
- TCP 443 (HTTPS) - For registry access and API communication
- TCP 80 (HTTP) - For registry access (if not using HTTPS)

### Container Networking

Containers managed by the Edge Agent use Docker's default bridge network unless specified otherwise.

**Default Network Mode:** `bridge`

Custom networks can be configured via bundle specifications.

## Security Considerations

### Docker Socket Access

**Warning:** Access to the Docker socket is equivalent to root access on the host system.

**Best Practices:**
1. Run the Edge Agent with the minimum required permissions
2. Use Docker user namespaces when possible
3. Consider using Docker rootless mode for enhanced security
4. Implement network policies to restrict container access

### Container Image Security

1. **Use Trusted Registries:** Only pull images from trusted container registries
2. **Image Scanning:** Implement vulnerability scanning for container images
3. **Digest Pins:** Use image digests instead of tags for production deployments
4. **Minimal Base Images:** Prefer alpine or distroless base images

### Resource Limits

The Docker Container Manager does not currently set resource limits. Consider adding resource constraints via bundle specifications:

```json
{
  "name": "resource-limited-app",
  "image": "myapp:1.0",
  "resources": {
    "memory": "512M",
    "cpus": "0.5"
  }
}
```

## Troubleshooting

### Docker Socket Permission Denied

```bash
# Check socket permissions
ls -l /var/run/docker.sock

# Add user to docker group
sudo usermod -aG docker $USER

# Log out and back in for changes to take effect
```

### Docker Daemon Not Running

```bash
# Check Docker status
sudo systemctl status docker

# Start Docker
sudo systemctl start docker

# Enable Docker on boot
sudo systemctl enable docker
```

### Container Pull Failures

1. **Check Network Connectivity:**
   ```bash
   ping registry-1.docker.io
   ```

2. **Test Docker Registry Access:**
   ```bash
   docker pull hello-world
   ```

3. **Check Docker Login (for private registries):**
   ```bash
   docker login <registry-url>
   ```

4. **Inspect Edge Agent Logs:**
   - Look for Docker API errors
   - Check for authentication failures
   - Verify network connectivity

### Disk Space Issues

```bash
# Remove unused containers
docker container prune -f

# Remove unused images
docker image prune -a -f

# Remove unused volumes
docker volume prune -f

# Full system cleanup
docker system prune -a -f --volumes
```

## Monitoring

### Docker Health Checks

The Edge Agent monitors Docker daemon connectivity and will retry operations on transient failures.

**Retry Policy:**
- Retry Count: 3
- Backoff Strategy: Exponential (2^attempt seconds)
- Retryable Errors: Network timeouts, 5xx server errors

### Container Health

Monitor container health via:
- Docker Container Manager `GetContainerStatsAsync()` method
- Docker healthcheck directives in Dockerfiles

### Logging

Docker logs are accessible via:
```bash
docker logs <container-id>
```

Or programmatically via Edge Agent:
```csharp
var logs = await containerManager.GetContainerLogsAsync(containerId, tailLines: 100);
```

## Limitations

### Current Limitations (MVP)

1. **No Resource Limits:** Container resource limits must be set via bundle specifications
2. **No Multi-Host Orchestration:** Single-device management only (no clustering)
3. **No Kubernetes Support:** Docker-only in this version
4. **Basic Networking:** Uses default bridge network

### Future Enhancements

- Docker Compose support for multi-container bundles
- Custom network creation and management
- Volume management
- Container resource limit enforcement
- Health check integration
- Registry authentication management
- Multi-architecture image support (ARM/x86 auto-detection)

## References

- [Docker Engine Installation](https://docs.docker.com/engine/install/)
- [Docker Engine API](https://docs.docker.com/engine/api/)
- [Docker.DotNet Library](https://github.com/dotnet/Docker.DotNet)
- [Docker Security Best Practices](https://docs.docker.com/engine/security/)
- [Docker Resource Constraints](https://docs.docker.com/config/containers/resource_constraints/)
