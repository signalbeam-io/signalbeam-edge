# SignalBeam Edge Agent Installation Guide

Complete installation guide for deploying the SignalBeam Edge Agent on various platforms.

## Table of Contents

- [Quick Start](#quick-start)
- [Platform-Specific Installation](#platform-specific-installation)
  - [Raspberry Pi](#raspberry-pi)
  - [Ubuntu Server](#ubuntu-server)
  - [Debian](#debian)
- [Installation Methods](#installation-methods)
  - [Method 1: One-Line Install (Recommended)](#method-1-one-line-install-recommended)
  - [Method 2: Manual .deb Package](#method-2-manual-deb-package)
  - [Method 3: Docker Container](#method-3-docker-container)
  - [Method 4: Build from Source](#method-4-build-from-source)
- [Post-Installation](#post-installation)
- [Uninstallation](#uninstallation)
- [Troubleshooting](#troubleshooting)

---

## Quick Start

For most users on Debian-based systems (Ubuntu, Debian, Raspberry Pi OS):

```bash
# One-line installation
curl -fsSL https://install.signalbeam.io | sudo bash

# Register device
signalbeam-agent register \
  --tenant-id <your-tenant-id> \
  --device-id <unique-device-id> \
  --token <registration-token>

# Start service
sudo systemctl start signalbeam-agent

# Check status
sudo systemctl status signalbeam-agent
```

---

## Platform-Specific Installation

### Raspberry Pi

The SignalBeam Edge Agent works great on Raspberry Pi devices (3B+, 4, 5, Zero 2 W).

#### Raspberry Pi OS (64-bit) - Recommended

```bash
# Update system
sudo apt update && sudo apt upgrade -y

# Install using one-line installer
curl -fsSL https://install.signalbeam.io | sudo bash

# Or download specific package
wget https://github.com/signalbeam-io/signalbeam-edge/releases/download/v0.1.0/signalbeam-agent_0.1.0_arm64.deb
sudo dpkg -i signalbeam-agent_0.1.0_arm64.deb
```

#### Raspberry Pi OS (32-bit)

```bash
# For older 32-bit Raspberry Pi OS
wget https://github.com/signalbeam-io/signalbeam-edge/releases/download/v0.1.0/signalbeam-agent_0.1.0_armhf.deb
sudo dpkg -i signalbeam-agent_0.1.0_armhf.deb
```

#### Performance Tips for Raspberry Pi

- Use a Class 10 SD card or better yet, boot from USB SSD
- Ensure adequate cooling (heat sink or fan) for stable operation
- Allocate at least 1GB RAM for the agent and containers
- For Raspberry Pi 3, consider reducing heartbeat frequency:
  ```bash
  sudo systemctl edit signalbeam-agent
  # Add:
  # [Service]
  # Environment="Agent__HeartbeatIntervalSeconds=60"
  ```

### Ubuntu Server

#### Ubuntu 22.04 LTS / 24.04 LTS (Recommended)

```bash
# Update system
sudo apt update && sudo apt upgrade -y

# One-line installation
curl -fsSL https://install.signalbeam.io | sudo bash

# Or manual installation
wget https://github.com/signalbeam-io/signalbeam-edge/releases/download/v0.1.0/signalbeam-agent_0.1.0_amd64.deb
sudo dpkg -i signalbeam-agent_0.1.0_amd64.deb
```

#### Ubuntu 20.04 LTS

Fully supported. Follow the same steps as Ubuntu 22.04.

### Debian

#### Debian 12 (Bookworm) / Debian 11 (Bullseye)

```bash
# Update system
sudo apt update && sudo apt upgrade -y

# Install dependencies
sudo apt install -y curl ca-certificates

# One-line installation
curl -fsSL https://install.signalbeam.io | sudo bash

# Or manual installation
wget https://github.com/signalbeam-io/signalbeam-edge/releases/download/v0.1.0/signalbeam-agent_0.1.0_amd64.deb
sudo dpkg -i signalbeam-agent_0.1.0_amd64.deb
```

---

## Installation Methods

### Method 1: One-Line Install (Recommended)

The easiest way to install on any supported Debian-based system:

```bash
curl -fsSL https://install.signalbeam.io | sudo bash
```

Or using wget:

```bash
wget -qO- https://install.signalbeam.io | sudo bash
```

**What it does:**
- Detects your OS and architecture automatically
- Downloads the correct .deb package
- Installs Docker if not present
- Creates `signalbeam` user and directories
- Installs and enables the systemd service
- Provides next-step instructions

**Environment Variables:**

```bash
# Specify version
export SIGNALBEAM_VERSION=0.1.0
curl -fsSL https://install.signalbeam.io | sudo bash

# Use custom download URL
export SIGNALBEAM_BASE_URL=https://my-mirror.example.com
curl -fsSL https://install.signalbeam.io | sudo bash
```

### Method 2: Manual .deb Package

Download and install the package manually:

#### 1. Download Package

```bash
# For x86_64 (Intel/AMD)
wget https://github.com/signalbeam-io/signalbeam-edge/releases/download/v0.1.0/signalbeam-agent_0.1.0_amd64.deb

# For ARM64 (Raspberry Pi 4, 5)
wget https://github.com/signalbeam-io/signalbeam-edge/releases/download/v0.1.0/signalbeam-agent_0.1.0_arm64.deb

# For ARMv7 (Raspberry Pi 3, older models)
wget https://github.com/signalbeam-io/signalbeam-edge/releases/download/v0.1.0/signalbeam-agent_0.1.0_armhf.deb
```

#### 2. Install Package

```bash
sudo dpkg -i signalbeam-agent_*.deb

# If you get dependency errors, fix them with:
sudo apt-get install -f
```

#### 3. Verify Installation

```bash
signalbeam-agent version
systemctl status signalbeam-agent
```

### Method 3: Docker Container

Run the agent as a Docker container (useful for testing):

#### Using docker-compose (Recommended)

```bash
# Clone repository
git clone https://github.com/signalbeam-io/signalbeam-edge.git
cd signalbeam-edge/src/EdgeAgent

# Start container
docker-compose up -d

# View logs
docker-compose logs -f

# Stop container
docker-compose down
```

#### Using docker run

```bash
# Pull image (when published)
docker pull ghcr.io/signalbeam-io/edge-agent:latest

# Or build locally
cd signalbeam-edge/src/EdgeAgent
docker build -t signalbeam/edge-agent:latest .

# Run container
docker run -d \
  --name signalbeam-agent \
  --restart unless-stopped \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v signalbeam-data:/var/lib/signalbeam \
  -v signalbeam-logs:/var/log/signalbeam \
  -e Agent__CloudUrl=https://api.signalbeam.com \
  signalbeam/edge-agent:latest run

# Register device (exec into container)
docker exec -it signalbeam-agent \
  dotnet SignalBeam.EdgeAgent.Host.dll register \
  --tenant-id <your-tenant-id> \
  --device-id <device-id> \
  --token <token>

# View logs
docker logs -f signalbeam-agent
```

#### Docker Compose Configuration

Create `docker-compose.yml`:

```yaml
version: '3.8'

services:
  signalbeam-agent:
    image: ghcr.io/signalbeam-io/edge-agent:latest
    container_name: signalbeam-agent
    restart: unless-stopped
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - signalbeam-data:/var/lib/signalbeam
      - signalbeam-logs:/var/log/signalbeam
    environment:
      - Agent__CloudUrl=https://api.signalbeam.com
      - Agent__HeartbeatIntervalSeconds=30
      - Agent__ReconciliationIntervalSeconds=60

volumes:
  signalbeam-data:
  signalbeam-logs:
```

### Method 4: Build from Source

For developers or custom deployments:

#### Prerequisites

- .NET 9.0 SDK
- Docker (optional, for containerized apps)

#### Build Steps

```bash
# Clone repository
git clone https://github.com/signalbeam-io/signalbeam-edge.git
cd signalbeam-edge/src/EdgeAgent/SignalBeam.EdgeAgent.Host

# Build
dotnet build -c Release

# Run directly
dotnet run -- --help

# Publish for deployment
dotnet publish -c Release -r linux-x64 --self-contained false -o ./publish

# Copy binary
sudo cp ./publish/SignalBeam.EdgeAgent.Host /usr/local/bin/signalbeam-agent
sudo chmod 755 /usr/local/bin/signalbeam-agent

# Install systemd service manually (see systemd/README.md)
```

#### Build .deb Package

```bash
cd signalbeam-edge/src/EdgeAgent/package

# Build packages for all architectures
sudo ./build-deb.sh

# Packages will be in: build/
ls -lh build/
```

---

## Post-Installation

### 1. Register Device

Before running the agent, register your device with SignalBeam:

```bash
signalbeam-agent register \
  --tenant-id <your-tenant-id> \
  --device-id <unique-device-id> \
  --token <registration-token> \
  --cloud-url https://api.signalbeam.com
```

**Parameters:**
- `--tenant-id`: Your SignalBeam tenant/organization ID
- `--device-id`: Unique identifier for this device (e.g., `warehouse-pi-01`)
- `--token`: Registration token from the SignalBeam dashboard
- `--cloud-url`: SignalBeam API endpoint (optional, defaults to production)

**Example:**

```bash
signalbeam-agent register \
  --tenant-id acme-corp \
  --device-id warehouse-rpi-01 \
  --token eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### 2. Start Service

```bash
sudo systemctl start signalbeam-agent
```

### 3. Enable Auto-Start on Boot

```bash
sudo systemctl enable signalbeam-agent
```

### 4. Verify Service is Running

```bash
# Check service status
sudo systemctl status signalbeam-agent

# View logs
sudo journalctl -u signalbeam-agent -f

# Check agent status
signalbeam-agent status
```

### 5. Configuration

Edit configuration via environment variables:

```bash
sudo systemctl edit signalbeam-agent
```

Add overrides:

```ini
[Service]
Environment="Agent__CloudUrl=https://api.signalbeam.com"
Environment="Agent__HeartbeatIntervalSeconds=60"
Environment="Agent__ReconciliationIntervalSeconds=120"
```

Reload and restart:

```bash
sudo systemctl daemon-reload
sudo systemctl restart signalbeam-agent
```

---

## Uninstallation

### Using systemd Script

```bash
cd /path/to/signalbeam-edge/src/EdgeAgent/systemd
sudo ./uninstall.sh
```

### Using apt/dpkg

```bash
# Remove package (keeps configuration)
sudo apt remove signalbeam-agent

# Remove package and configuration
sudo apt purge signalbeam-agent

# Or using dpkg
sudo dpkg -r signalbeam-agent
sudo dpkg --purge signalbeam-agent
```

### Manual Uninstallation

```bash
# Stop and disable service
sudo systemctl stop signalbeam-agent
sudo systemctl disable signalbeam-agent

# Remove package
sudo apt remove signalbeam-agent

# Remove data (optional)
sudo rm -rf /var/lib/signalbeam
sudo rm -rf /var/log/signalbeam
sudo rm -rf /etc/signalbeam

# Remove user (optional)
sudo userdel signalbeam
```

---

## Troubleshooting

### Installation Issues

#### Package Not Found

```bash
# Verify package exists
wget --spider https://github.com/signalbeam-io/signalbeam-edge/releases/download/v0.1.0/signalbeam-agent_0.1.0_amd64.deb

# Check your architecture
uname -m
# x86_64 → use amd64
# aarch64 → use arm64
# armv7l → use armhf
```

#### Dependency Errors

```bash
# Fix missing dependencies
sudo apt-get install -f

# Update package lists
sudo apt update
sudo apt upgrade
```

#### Docker Not Found

```bash
# Install Docker manually
curl -fsSL https://get.docker.com | sudo bash

# Or follow official Docker installation:
# https://docs.docker.com/engine/install/
```

### Runtime Issues

#### Service Won't Start

```bash
# Check logs
sudo journalctl -u signalbeam-agent -n 100 --no-pager

# Verify device is registered
ls -la /var/lib/signalbeam/device-state.json

# Check Docker is running
sudo systemctl status docker
```

#### Permission Denied with Docker

```bash
# Ensure signalbeam user is in docker group
sudo usermod -aG docker signalbeam

# Restart service
sudo systemctl restart signalbeam-agent
```

#### Can't Connect to Cloud

```bash
# Test connectivity
curl -v https://api.signalbeam.com/health

# Check firewall
sudo ufw status

# Check DNS resolution
nslookup api.signalbeam.com
```

### Platform-Specific Issues

#### Raspberry Pi: Low Memory

```bash
# Increase swap space
sudo dphys-swapfile swapoff
sudo sed -i 's/CONF_SWAPSIZE=100/CONF_SWAPSIZE=1024/' /etc/dphys-swapfile
sudo dphys-swapfile setup
sudo dphys-swapfile swapon
```

#### Raspberry Pi: SD Card Performance

- Use a high-quality SD card (Class 10, A2 rated)
- Or boot from USB SSD for better performance
- Enable trim for SSDs:
  ```bash
  sudo fstrim -v /
  ```

## Support

For additional help:
- Documentation: https://docs.signalbeam.io
- GitHub Issues: https://github.com/signalbeam-io/signalbeam-edge/issues
- Community Forum: https://community.signalbeam.io
