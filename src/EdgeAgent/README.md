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

### From Binary

Download the latest release from the releases page and extract it to your preferred location.

```bash
# Extract the archive
tar -xzf signalbeam-agent-linux-x64.tar.gz

# Make it executable
chmod +x signalbeam-agent

# Optionally, move to a system path
sudo mv signalbeam-agent /usr/local/bin/
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

### Systemd (Linux)

Create a systemd service file:

```bash
sudo nano /etc/systemd/system/signalbeam-agent.service
```

Add the following content:

```ini
[Unit]
Description=SignalBeam Edge Agent
After=network.target docker.service
Requires=docker.service

[Service]
Type=simple
User=root
ExecStart=/usr/local/bin/signalbeam-agent run
Restart=always
RestartSec=10
Environment=DOTNET_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

Enable and start the service:

```bash
sudo systemctl daemon-reload
sudo systemctl enable signalbeam-agent
sudo systemctl start signalbeam-agent

# Check status
sudo systemctl status signalbeam-agent

# View logs
sudo journalctl -u signalbeam-agent -f
```

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
