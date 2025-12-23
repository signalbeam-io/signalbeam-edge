# SignalBeam Edge Agent

The SignalBeam Edge Agent is a lightweight .NET console application that runs on edge devices (e.g., Raspberry Pi, mini-PCs) and communicates with the SignalBeam cloud platform.

## Features

- **Device Registration**: Register edge devices with the SignalBeam cloud platform
- **Heartbeat Monitoring**: Automatically send device health metrics every 30 seconds
- **Container Reconciliation**: Keep containerized applications in sync with desired state from the cloud
- **Metrics Collection**: Collect and report CPU, memory, and disk usage
- **CLI Interface**: Easy-to-use command-line interface for all operations

## Prerequisites

- .NET 9.0 or later
- Docker (for container management)
- Linux, macOS, or Windows

## Installation

### Quick Install (Recommended)

For Debian-based systems (Ubuntu, Debian, Raspberry Pi OS):

```bash
# One-line installation
curl -fsSL https://install.signalbeam.io | sudo bash
```

Or using wget:

```bash
wget -qO- https://install.signalbeam.io | sudo bash
```

This will:
- Detect your OS and architecture
- Download the correct package
- Install Docker if needed
- Set up the systemd service
- Provide registration instructions

### Install .deb Package (Manual)

Download the appropriate package for your platform from the [releases page](https://github.com/signalbeam-io/signalbeam-edge/releases):

```bash
# For x86_64 (Intel/AMD)
wget https://github.com/signalbeam-io/signalbeam-edge/releases/download/v0.1.0/signalbeam-agent_0.1.0_amd64.deb
sudo dpkg -i signalbeam-agent_0.1.0_amd64.deb

# For ARM64 (Raspberry Pi 4, 5)
wget https://github.com/signalbeam-io/signalbeam-edge/releases/download/v0.1.0/signalbeam-agent_0.1.0_arm64.deb
sudo dpkg -i signalbeam-agent_0.1.0_arm64.deb

# For ARMv7 (Raspberry Pi 3)
wget https://github.com/signalbeam-io/signalbeam-edge/releases/download/v0.1.0/signalbeam-agent_0.1.0_armhf.deb
sudo dpkg -i signalbeam-agent_0.1.0_armhf.deb
```

### Docker Container

```bash
# Using docker-compose
cd src/EdgeAgent
docker-compose up -d

# Or using docker run
docker pull ghcr.io/signalbeam-io/edge-agent:latest
docker run -d \
  --name signalbeam-agent \
  --restart unless-stopped \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v signalbeam-data:/var/lib/signalbeam \
  ghcr.io/signalbeam-io/edge-agent:latest run
```

### From Source

```bash
# Clone the repository
git clone https://github.com/signalbeam-io/signalbeam-edge.git

# Build the agent
cd signalbeam-edge/src/EdgeAgent/SignalBeam.EdgeAgent.Host
dotnet build -c Release

# Run the agent
dotnet run -- --help
```

**For detailed installation instructions, see:**
- [Complete Installation Guide](package/INSTALL.md) - Platform-specific guides for Raspberry Pi, Ubuntu, Debian
- [Building Packages](package/README.md) - Build .deb packages from source

## Usage

### Available Commands

```bash
signalbeam-agent --help

Commands:
  register  Register this device with the SignalBeam cloud
  run       Run the SignalBeam agent (heartbeat + reconciliation loops)
  status    Show the current status of the SignalBeam agent
  version   Show the SignalBeam agent version
  logs      Show the SignalBeam agent logs
```

### Register a Device

Before running the agent, you must register your device with the SignalBeam cloud:

```bash
signalbeam-agent register \
  --tenant-id <your-tenant-id> \
  --device-id <unique-device-id> \
  --token <registration-token> \
  --cloud-url https://api.signalbeam.com
```

**Parameters:**
- `--tenant-id` (required): Your SignalBeam tenant ID
- `--device-id` (required): A unique identifier for this device (e.g., `warehouse-pi-01`)
- `--token` (required): Registration token from the SignalBeam dashboard
- `--cloud-url` (optional): SignalBeam cloud API URL (defaults to `https://api.signalbeam.com`)

**Example:**

```bash
signalbeam-agent register \
  --tenant-id acme-corp \
  --device-id warehouse-pi-01 \
  --token eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### Run the Agent

After registration, start the agent to begin sending heartbeats and syncing containers:

```bash
signalbeam-agent run
```

The agent will:
- Send heartbeat with device metrics every 30 seconds
- Check for desired state changes every 60 seconds
- Reconcile Docker containers to match the desired state
- Log all activities to console and file

**To run in the background:**

```bash
# Using nohup
nohup signalbeam-agent run > /dev/null 2>&1 &

# Or using screen
screen -dmS signalbeam signalbeam-agent run

# Or install as a systemd service (see below)
```

### Check Agent Status

```bash
signalbeam-agent status
```

Output example:

```
SignalBeam Edge Agent Status
============================

Status: ✅ Registered
Device ID: 12345678-1234-1234-1234-123456789012
Cloud Endpoint: https://api.signalbeam.com

Machine: warehouse-pi-01
Platform: Unix
OS Version: Unix 6.1.0.0

Agent: ✅ Running
```

### View Logs

```bash
# Show last 50 lines
signalbeam-agent logs

# Show last 100 lines
signalbeam-agent logs --lines 100

# Follow logs (like tail -f)
signalbeam-agent logs --follow
```

### Show Version

```bash
signalbeam-agent version
```

Output:

```
SignalBeam Edge Agent v1.0.0
Runtime: 9.0.11
Platform: Unix
```

## Running as a Service

### Systemd (Linux) - Recommended

The SignalBeam Edge Agent includes a complete systemd service configuration with automated installation scripts.

**Quick Install:**

```bash
# 1. Build the agent
cd src/EdgeAgent/SignalBeam.EdgeAgent.Host
dotnet publish -c Release -r linux-x64 --self-contained false

# 2. Run installation script
cd ../systemd
sudo ./install.sh

# 3. Register your device
signalbeam-agent register \
  --tenant-id <your-tenant-id> \
  --device-id <device-id> \
  --token <registration-token>

# 4. Start the service
sudo systemctl start signalbeam-agent

# 5. Check status
sudo systemctl status signalbeam-agent

# 6. View logs
sudo journalctl -u signalbeam-agent -f
```

**Features:**
- ✅ Automatic startup on boot
- ✅ Automatic restart on failure
- ✅ Runs as non-root user (`signalbeam`)
- ✅ Security hardening enabled
- ✅ systemd integration (`Type=notify`)
- ✅ Resource limits configured

**For detailed documentation, troubleshooting, and advanced configuration, see:**
- [systemd/README.md](systemd/README.md) - Complete systemd documentation
- [systemd/signalbeam-agent.service](systemd/signalbeam-agent.service) - Service unit file
- [systemd/install.sh](systemd/install.sh) - Installation script
- [systemd/uninstall.sh](systemd/uninstall.sh) - Uninstallation script

### Docker Container (Alternative)

You can also run the agent in a Docker container with Docker-in-Docker or by mounting the Docker socket:

```bash
docker run -d \
  --name signalbeam-agent \
  --restart unless-stopped \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v /etc/signalbeam:/etc/signalbeam \
  signalbeam/edge-agent:latest run
```

## Configuration

The agent uses `appsettings.json` for configuration. Default configuration:

```json
{
  "Agent": {
    "CloudUrl": "https://api.signalbeam.com",
    "HeartbeatIntervalSeconds": 30,
    "ReconciliationIntervalSeconds": 60,
    "MaxRetries": 3,
    "LogFilePath": "/var/log/signalbeam-agent/agent.log"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
    }
  }
}
```

### Environment Variables

You can override configuration via environment variables:

```bash
export DOTNET_ENVIRONMENT=Production
export Agent__CloudUrl=https://api.signalbeam.com
export Agent__HeartbeatIntervalSeconds=60
```

## Troubleshooting

### Agent Won't Register

1. Check network connectivity to the cloud URL:
   ```bash
   curl https://api.signalbeam.com/health
   ```

2. Verify registration token is valid
3. Check logs for detailed error messages:
   ```bash
   signalbeam-agent logs --follow
   ```

### Agent Not Sending Heartbeats

1. Check agent status:
   ```bash
   signalbeam-agent status
   ```

2. Verify device is registered
3. Check logs for errors:
   ```bash
   signalbeam-agent logs --lines 100
   ```

### Container Reconciliation Issues

1. Ensure Docker is running:
   ```bash
   docker ps
   ```

2. Check agent has permission to access Docker socket:
   ```bash
   ls -l /var/run/docker.sock
   ```

3. Verify desired state in SignalBeam dashboard matches agent logs

## Development

### Build

```bash
cd src/EdgeAgent/SignalBeam.EdgeAgent.Host
dotnet build
```

### Simulator (API Key)

Use the simulator to register a device and emit heartbeats/metrics against the local APIs:

```bash
dotnet run --project src/EdgeAgent/SignalBeam.EdgeAgent.Simulator -- \
  --device-manager-url http://localhost:5001 \
  --bundle-orchestrator-url http://localhost:5002 \
  --api-key dev-api-key-1 \
  --tenant-id 00000000-0000-0000-0000-000000000001
```

You can also set environment variables instead of CLI flags:

```bash
export SIM_DEVICE_MANAGER_URL=http://localhost:5001
export SIM_BUNDLE_ORCHESTRATOR_URL=http://localhost:5002
export SIM_API_KEY=dev-api-key-1
export SIM_TENANT_ID=00000000-0000-0000-0000-000000000001
```

### Run Locally

```bash
# Development environment
export DOTNET_ENVIRONMENT=Development
dotnet run -- --help
```

### Run Tests

```bash
cd src/EdgeAgent
dotnet test
```

## Architecture

The Edge Agent follows hexagonal architecture:

```
SignalBeam.EdgeAgent.Host/          # CLI and host application
├── Commands/                        # CLI commands
│   ├── RegisterCommand.cs
│   ├── RunCommand.cs
│   ├── StatusCommand.cs
│   ├── VersionCommand.cs
│   └── LogsCommand.cs
├── Services/                        # Background services
│   ├── HeartbeatService.cs         # Sends heartbeats every 30s
│   ├── ReconciliationService.cs    # Reconciles containers every 60s
│   └── DeviceStateManager.cs       # Manages device registration state
├── Configuration/
│   └── AgentOptions.cs
└── Program.cs                       # Entry point

SignalBeam.EdgeAgent.Application/    # Business logic
├── Commands/                        # CQRS command handlers
├── Services/                        # Service interfaces
└── ...

SignalBeam.EdgeAgent.Infrastructure/ # External integrations
├── Cloud/                           # Cloud API client
│   └── HttpCloudClient.cs
├── Container/                       # Docker management
│   └── DockerContainerManager.cs
└── Metrics/                         # System metrics
    └── SystemMetricsCollector.cs
```

## License

Copyright © 2025 SignalBeam. All rights reserved.
