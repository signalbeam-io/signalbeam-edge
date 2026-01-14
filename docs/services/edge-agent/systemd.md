# SignalBeam Edge Agent systemd Service

This directory contains systemd service configuration and installation scripts for running the SignalBeam Edge Agent as a system service on Linux.

## Features

- **Automatic startup**: Agent starts automatically on boot
- **Automatic restart**: Service restarts on failure with 10-second delay
- **Non-root execution**: Runs as dedicated `signalbeam` user for security
- **systemd integration**: Uses `Type=notify` for proper lifecycle management
- **Security hardening**: Includes systemd security directives
- **Resource limits**: Configurable resource constraints
- **Structured logging**: Logs to systemd journal

## Files

- `signalbeam-agent.service` - systemd unit file
- `install.sh` - Automated installation script
- `uninstall.sh` - Automated uninstallation script
- `README.md` - This file

## Prerequisites

- Linux system with systemd (tested on Ubuntu 20.04+, Raspberry Pi OS)
- Docker installed and running
- .NET 9.0 runtime (or self-contained build)
- Root/sudo access for installation

## Quick Start

### 1. Build the Agent

```bash
cd src/EdgeAgent/SignalBeam.EdgeAgent.Host
dotnet publish -c Release -r linux-x64 --self-contained false
```

For self-contained deployment (no .NET runtime required):

```bash
dotnet publish -c Release -r linux-x64 --self-contained true
```

For ARM devices (Raspberry Pi):

```bash
dotnet publish -c Release -r linux-arm64 --self-contained true
```

### 2. Install as systemd Service

```bash
cd src/EdgeAgent/systemd
sudo ./install.sh
```

The installation script will:
- Create `signalbeam` system user and group
- Add user to `docker` group
- Create `/var/lib/signalbeam` (state/config)
- Create `/var/log/signalbeam` (logs)
- Create `/etc/signalbeam` (configuration)
- Install binary to `/usr/local/bin/signalbeam-agent`
- Install systemd service
- Enable service for automatic startup

### 3. Register Device

Before starting the service, register your device:

```bash
signalbeam-agent register \
  --tenant-id <your-tenant-id> \
  --device-id <device-id> \
  --token <registration-token> \
  --cloud-url https://api.signalbeam.com
```

The registration state is stored in `/var/lib/signalbeam/device-state.json`.

### 4. Start Service

```bash
sudo systemctl start signalbeam-agent
```

### 5. Verify Service is Running

```bash
sudo systemctl status signalbeam-agent
```

Expected output:

```
● signalbeam-agent.service - SignalBeam Edge Agent - Manage edge devices from the cloud
     Loaded: loaded (/etc/systemd/system/signalbeam-agent.service; enabled; vendor preset: enabled)
     Active: active (running) since Mon 2024-12-23 10:30:00 UTC; 5s ago
   Main PID: 12345 (signalbeam-agen)
      Tasks: 10
     Memory: 45.2M
     CGroup: /system.slice/signalbeam-agent.service
             └─12345 /usr/local/bin/signalbeam-agent run
```

## Service Management

### View Logs

```bash
# View all logs
sudo journalctl -u signalbeam-agent

# Follow logs in real-time
sudo journalctl -u signalbeam-agent -f

# View last 100 lines
sudo journalctl -u signalbeam-agent -n 100

# View logs since boot
sudo journalctl -u signalbeam-agent -b
```

### Start Service

```bash
sudo systemctl start signalbeam-agent
```

### Stop Service

```bash
sudo systemctl stop signalbeam-agent
```

### Restart Service

```bash
sudo systemctl restart signalbeam-agent
```

### Enable (start on boot)

```bash
sudo systemctl enable signalbeam-agent
```

### Disable (don't start on boot)

```bash
sudo systemctl disable signalbeam-agent
```

### Check Service Status

```bash
sudo systemctl status signalbeam-agent
```

### View Service Configuration

```bash
systemctl cat signalbeam-agent
```

## Manual Installation

If you prefer manual installation instead of using the script:

### 1. Create User and Group

```bash
sudo useradd --system --no-create-home --shell /bin/false signalbeam
sudo usermod -aG docker signalbeam
```

### 2. Create Directories

```bash
sudo mkdir -p /var/lib/signalbeam
sudo mkdir -p /var/log/signalbeam
sudo mkdir -p /etc/signalbeam

sudo chown -R signalbeam:signalbeam /var/lib/signalbeam
sudo chown -R signalbeam:signalbeam /var/log/signalbeam
sudo chown -R signalbeam:signalbeam /etc/signalbeam

sudo chmod 755 /var/lib/signalbeam
sudo chmod 755 /var/log/signalbeam
sudo chmod 755 /etc/signalbeam
```

### 3. Copy Binary

```bash
sudo cp signalbeam-agent /usr/local/bin/
sudo chmod 755 /usr/local/bin/signalbeam-agent
```

### 4. Install Service File

```bash
sudo cp signalbeam-agent.service /etc/systemd/system/
sudo chmod 644 /etc/systemd/system/signalbeam-agent.service
```

### 5. Reload systemd and Enable Service

```bash
sudo systemctl daemon-reload
sudo systemctl enable signalbeam-agent
```

## Uninstallation

### Using Script

```bash
cd src/EdgeAgent/systemd
sudo ./uninstall.sh
```

The script will prompt you about:
- Removing data directories (`/var/lib/signalbeam`, `/var/log/signalbeam`, `/etc/signalbeam`)
- Removing the `signalbeam` system user

### Manual Uninstallation

```bash
# Stop and disable service
sudo systemctl stop signalbeam-agent
sudo systemctl disable signalbeam-agent

# Remove service file
sudo rm /etc/systemd/system/signalbeam-agent.service

# Reload systemd
sudo systemctl daemon-reload
sudo systemctl reset-failed

# Remove binary
sudo rm /usr/local/bin/signalbeam-agent

# Optional: Remove data directories
sudo rm -rf /var/lib/signalbeam
sudo rm -rf /var/log/signalbeam
sudo rm -rf /etc/signalbeam

# Optional: Remove user
sudo userdel signalbeam
```

## Configuration

### Environment Variables

You can override configuration by editing the service file:

```bash
sudo systemctl edit signalbeam-agent
```

Add environment variables in the override file:

```ini
[Service]
Environment="Agent__CloudUrl=https://api.signalbeam.com"
Environment="Agent__HeartbeatIntervalSeconds=60"
Environment="Agent__ReconciliationIntervalSeconds=120"
```

Then reload and restart:

```bash
sudo systemctl daemon-reload
sudo systemctl restart signalbeam-agent
```

### Configuration File

You can also place a custom `appsettings.json` in `/etc/signalbeam/`:

```bash
sudo nano /etc/signalbeam/appsettings.json
```

```json
{
  "Agent": {
    "CloudUrl": "https://api.signalbeam.com",
    "HeartbeatIntervalSeconds": 30,
    "ReconciliationIntervalSeconds": 60
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
    }
  }
}
```

## Troubleshooting

### Service Won't Start

**Check service status for errors:**

```bash
sudo systemctl status signalbeam-agent
```

**View detailed logs:**

```bash
sudo journalctl -u signalbeam-agent -n 100 --no-pager
```

**Common issues:**

1. **Device not registered**: Register the device first using `signalbeam-agent register`
2. **Docker not running**: Ensure Docker service is running: `sudo systemctl status docker`
3. **Permission denied**: Check that `signalbeam` user is in `docker` group: `groups signalbeam`
4. **Binary not found**: Verify binary exists and is executable: `ls -l /usr/local/bin/signalbeam-agent`

### Service Crashes Immediately

Check if the device is registered:

```bash
sudo ls -la /var/lib/signalbeam/device-state.json
```

If file doesn't exist, register the device:

```bash
signalbeam-agent register \
  --tenant-id <your-tenant-id> \
  --device-id <device-id> \
  --token <registration-token>
```

### Permission Errors with Docker

Ensure the `signalbeam` user is in the `docker` group:

```bash
sudo usermod -aG docker signalbeam
sudo systemctl restart signalbeam-agent
```

Verify group membership:

```bash
groups signalbeam
```

### Service Restarts Repeatedly

View logs to identify the issue:

```bash
sudo journalctl -u signalbeam-agent -f
```

Check for:
- Network connectivity issues
- Invalid registration token
- Cloud API endpoint unreachable
- Docker daemon errors

### High Memory Usage

Check current resource usage:

```bash
systemctl status signalbeam-agent
```

To limit memory usage, edit the service file:

```bash
sudo systemctl edit signalbeam-agent
```

Add memory limit:

```ini
[Service]
MemoryMax=512M
MemoryHigh=384M
```

### Logs Not Appearing

Ensure the service is configured to use systemd journal:

```bash
systemctl cat signalbeam-agent | grep -A2 "\[Service\]"
```

Should show:

```ini
StandardOutput=journal
StandardError=journal
```

If logs still don't appear, check journald is running:

```bash
sudo systemctl status systemd-journald
```

### Service Not Starting on Boot

Verify service is enabled:

```bash
sudo systemctl is-enabled signalbeam-agent
```

If disabled, enable it:

```bash
sudo systemctl enable signalbeam-agent
```

Check for dependency issues:

```bash
systemctl list-dependencies signalbeam-agent
```

## Security Hardening

The included service file has security hardening enabled. Review and adjust based on your needs:

```ini
# Security directives
NoNewPrivileges=true           # Prevent privilege escalation
PrivateTmp=true                # Isolated /tmp directory
ProtectSystem=strict           # Make system directories read-only
ProtectHome=true               # Make /home directories inaccessible
ReadWritePaths=/var/lib/signalbeam /var/log/signalbeam  # Only these paths are writable
```

Additional hardening options you can add:

```ini
# Network restrictions
PrivateNetwork=false           # Set to true if no network needed
RestrictAddressFamilies=AF_INET AF_INET6  # Only allow IPv4/IPv6

# Filesystem restrictions
ProtectKernelTunables=true     # Protect /proc/sys
ProtectKernelModules=true      # Prevent loading kernel modules
ProtectControlGroups=true      # Make cgroup filesystem read-only

# Process restrictions
RestrictRealtime=true          # Prevent realtime scheduling
LockPersonality=true           # Prevent personality changes
```

## Testing on Different Platforms

### Ubuntu 20.04 / 22.04

```bash
# Install dependencies
sudo apt update
sudo apt install -y docker.io

# Follow standard installation
cd src/EdgeAgent/systemd
sudo ./install.sh
```

### Raspberry Pi OS (Bullseye/Bookworm)

```bash
# Build for ARM64
dotnet publish -c Release -r linux-arm64 --self-contained true

# Install
cd src/EdgeAgent/systemd
sudo ./install.sh
```

### Debian 11/12

```bash
# Install Docker
sudo apt update
sudo apt install -y docker.io
sudo systemctl start docker
sudo systemctl enable docker

# Follow standard installation
cd src/EdgeAgent/systemd
sudo ./install.sh
```

## Performance Tuning

### Heartbeat Interval

Adjust how often the agent sends heartbeats (default: 30 seconds):

```bash
sudo systemctl edit signalbeam-agent
```

```ini
[Service]
Environment="Agent__HeartbeatIntervalSeconds=60"
```

### Reconciliation Interval

Adjust how often the agent checks for desired state changes (default: 60 seconds):

```bash
sudo systemctl edit signalbeam-agent
```

```ini
[Service]
Environment="Agent__ReconciliationIntervalSeconds=120"
```

### Resource Limits

Limit CPU and memory usage:

```bash
sudo systemctl edit signalbeam-agent
```

```ini
[Service]
CPUQuota=50%
MemoryMax=512M
TasksMax=256
```

## Monitoring

### Systemd Status

```bash
# Quick status check
sudo systemctl is-active signalbeam-agent

# Detailed status
sudo systemctl status signalbeam-agent

# Check if enabled
sudo systemctl is-enabled signalbeam-agent
```

### Resource Usage

```bash
# Current resource usage
systemctl status signalbeam-agent

# Or use systemd-cgtop
sudo systemd-cgtop
```

### Log Analysis

```bash
# Count error messages
sudo journalctl -u signalbeam-agent | grep -i error | wc -l

# Show errors only
sudo journalctl -u signalbeam-agent -p err

# Show warnings and errors
sudo journalctl -u signalbeam-agent -p warning
```

## Integration with Monitoring Tools

### Prometheus

The agent exposes metrics for Prometheus scraping. Configure your Prometheus instance to scrape the agent endpoint.

### Grafana

Use the logs stored in systemd journal with Grafana Loki:

```bash
# Install promtail to ship logs to Loki
# Configure promtail to read from journal
```

## License

Copyright © 2025 SignalBeam. All rights reserved.
