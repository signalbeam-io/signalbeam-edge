# Device Registration Guide

This guide explains how to register edge devices with SignalBeam Edge platform.

## Overview

SignalBeam Edge uses a **registration token** system to securely onboard new devices. This approach allows administrators to generate time-limited tokens that devices use to register themselves with the platform.

## Registration Flow

```
┌─────────────┐         ┌──────────────────┐         ┌─────────────┐
│  Admin UI   │         │  SignalBeam API  │         │ Edge Device │
└──────┬──────┘         └────────┬─────────┘         └──────┬──────┘
       │                         │                          │
       │ 1. Generate Token       │                          │
       ├────────────────────────>│                          │
       │                         │                          │
       │ 2. Return Token         │                          │
       │<────────────────────────┤                          │
       │                         │                          │
       │ 3. Copy token to device │                          │
       ├──────────────────────────────────────────────────>│
       │                         │                          │
       │                         │ 4. Register with token   │
       │                         │<─────────────────────────┤
       │                         │                          │
       │                         │ 5. Return device ID      │
       │                         │─────────────────────────>│
       │                         │                          │
```

## Step-by-Step Instructions

### Step 1: Generate a Registration Token

1. Log in to the **SignalBeam Web UI**
2. Navigate to **Registration Tokens** page
3. Click **Generate Token**
4. Configure token settings:
   - **Maximum Uses** (optional): Limit how many devices can use this token
     - Leave empty for unlimited uses
     - Example: Set to `10` to register 10 devices
   - **Expires In (days)** (optional): Set token expiration
     - Leave empty for tokens that never expire
     - Example: Set to `7` for a 7-day token
5. Click **Generate Token**
6. **Copy the token immediately** - it will only be shown once!

**Example Token:**
```
sbt_a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0
```

### Step 2: Install the Edge Agent

On your edge device (Raspberry Pi, mini-PC, etc.):

#### Option A: Using Docker (Recommended)

```bash
# Pull the Edge Agent image
docker pull signalbeam/edge-agent:latest

# Run the Edge Agent
docker run -d \
  --name signalbeam-agent \
  --restart unless-stopped \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -e SIGNALBEAM_API_URL=https://api.signalbeam.io \
  -e SIGNALBEAM_TENANT_ID=your-tenant-id \
  -e SIGNALBEAM_REGISTRATION_TOKEN=sbt_a1b2c3d4e5f6g7h8... \
  -e SIGNALBEAM_DEVICE_NAME=my-device-01 \
  signalbeam/edge-agent:latest
```

#### Option B: Using .NET Runtime

```bash
# Download the Edge Agent
wget https://releases.signalbeam.io/edge-agent/latest/edge-agent-linux-arm64.tar.gz

# Extract
tar -xzf edge-agent-linux-arm64.tar.gz
cd edge-agent

# Run the agent
./SignalBeam.EdgeAgent.Host \
  --api-url https://api.signalbeam.io \
  --tenant-id your-tenant-id \
  --registration-token sbt_a1b2c3d4e5f6g7h8... \
  --device-name my-device-01
```

#### Option C: Using systemd Service

Create a systemd service file at `/etc/systemd/system/signalbeam-agent.service`:

```ini
[Unit]
Description=SignalBeam Edge Agent
After=network.target docker.service
Requires=docker.service

[Service]
Type=simple
User=root
Environment="SIGNALBEAM_API_URL=https://api.signalbeam.io"
Environment="SIGNALBEAM_TENANT_ID=your-tenant-id"
Environment="SIGNALBEAM_REGISTRATION_TOKEN=sbt_a1b2c3d4e5f6g7h8..."
Environment="SIGNALBEAM_DEVICE_NAME=my-device-01"
ExecStart=/usr/local/bin/signalbeam-agent
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

Enable and start the service:

```bash
sudo systemctl daemon-reload
sudo systemctl enable signalbeam-agent
sudo systemctl start signalbeam-agent
sudo systemctl status signalbeam-agent
```

### Step 3: Verify Registration

Once the Edge Agent starts, it will:

1. **Register with the platform** using the registration token
2. **Receive a device ID** and API key
3. **Store credentials** locally for future communication
4. **Start sending heartbeats** every 30 seconds

**Check registration in the UI:**

1. Go to **Devices** page in the Web UI
2. You should see your new device listed
3. Device status should show as **Online** (green indicator)

**Check agent logs:**

```bash
# If using Docker
docker logs signalbeam-agent

# If using systemd
sudo journalctl -u signalbeam-agent -f
```

**Expected output:**
```
[INFO] SignalBeam Edge Agent starting...
[INFO] Registering device with platform...
[INFO] Device registered successfully! Device ID: 550e8400-e29b-41d4-a716-446655440000
[INFO] Starting heartbeat service...
[INFO] Heartbeat sent successfully
```

## Configuration Options

### Environment Variables

| Variable | Description | Required | Default | Example |
|----------|-------------|----------|---------|---------|
| `SIGNALBEAM_API_URL` | Platform API endpoint | Yes | - | `https://api.signalbeam.io` |
| `SIGNALBEAM_TENANT_ID` | Your tenant ID | Yes | - | `123e4567-e89b-12d3-a456-426614174000` |
| `SIGNALBEAM_REGISTRATION_TOKEN` | Registration token | Yes (first run) | - | `sbt_a1b2c3d4...` |
| `SIGNALBEAM_DEVICE_NAME` | Friendly device name | No | Hostname | `warehouse-pi-01` |
| `SIGNALBEAM_DEVICE_ID` | Device ID (auto-assigned) | No | Auto-generated | `550e8400-e29b-41d4...` |
| `SIGNALBEAM_HEARTBEAT_INTERVAL` | Heartbeat interval (seconds) | No | `30` | `60` |
| `SIGNALBEAM_LOG_LEVEL` | Logging level | No | `Information` | `Debug` |

### Configuration File (appsettings.json)

Alternatively, create `/etc/signalbeam/appsettings.json`:

```json
{
  "SignalBeam": {
    "ApiUrl": "https://api.signalbeam.io",
    "TenantId": "123e4567-e89b-12d3-a456-426614174000",
    "RegistrationToken": "sbt_a1b2c3d4e5f6g7h8...",
    "DeviceName": "my-device-01",
    "HeartbeatIntervalSeconds": 30,
    "ReconciliationIntervalSeconds": 60
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

## Bulk Device Registration

For registering multiple devices, you can use a single token with unlimited or high max uses:

### Example: Register 100 Devices

1. **Generate a token** with:
   - Max Uses: `100`
   - Expires In: `7` days

2. **Create a deployment script** (`deploy.sh`):

```bash
#!/bin/bash

TOKEN="sbt_a1b2c3d4e5f6g7h8..."
TENANT_ID="123e4567-e89b-12d3-a456-426614174000"
API_URL="https://api.signalbeam.io"

# Get hostname for device name
DEVICE_NAME=$(hostname)

# Deploy Edge Agent
docker run -d \
  --name signalbeam-agent \
  --restart unless-stopped \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -e SIGNALBEAM_API_URL=$API_URL \
  -e SIGNALBEAM_TENANT_ID=$TENANT_ID \
  -e SIGNALBEAM_REGISTRATION_TOKEN=$TOKEN \
  -e SIGNALBEAM_DEVICE_NAME=$DEVICE_NAME \
  signalbeam/edge-agent:latest

echo "Device $DEVICE_NAME registered successfully!"
```

3. **Deploy to all devices**:

```bash
# Using Ansible
ansible all -i inventory.ini -m script -a "deploy.sh"

# Or SSH loop
for host in $(cat hosts.txt); do
  scp deploy.sh $host:/tmp/
  ssh $host "bash /tmp/deploy.sh"
done
```

## Token Management Best Practices

### Security

1. **Never commit tokens to version control** - Use environment variables or secrets management
2. **Use short-lived tokens** for production deployments (7-30 days)
3. **Revoke tokens** immediately after bulk deployments are complete
4. **Use limited-use tokens** when possible (e.g., max uses = number of devices)

### Token Lifecycle

```
Generate Token → Deploy Devices → Verify All Registered → Revoke Token
     (Day 0)         (Day 0-7)          (Day 7)            (Day 7)
```

### Revoking Tokens

To revoke a token (prevents further registrations):

1. Go to **Registration Tokens** page
2. Find the token to revoke
3. Click the **Trash** icon
4. Confirm revocation

**Note:** Revoking a token does NOT affect devices already registered with it.

## Troubleshooting

### Token Already Used / Expired

**Error:**
```
Failed to register: Registration token is invalid or has expired
```

**Solution:**
- Generate a new token
- Check token hasn't reached max uses
- Verify token hasn't expired

### Cannot Connect to API

**Error:**
```
Failed to connect to https://api.signalbeam.io: Connection refused
```

**Solution:**
- Check network connectivity: `ping api.signalbeam.io`
- Verify API URL is correct
- Check firewall rules allow outbound HTTPS (port 443)

### Device Not Appearing in UI

**Possible causes:**

1. **Registration failed** - Check agent logs for errors
2. **Wrong tenant ID** - Verify `SIGNALBEAM_TENANT_ID` matches your account
3. **Heartbeat not sent** - Wait 30 seconds for first heartbeat
4. **UI not refreshed** - Refresh the Devices page

### Permission Denied (Docker Socket)

**Error:**
```
Permission denied while trying to connect to Docker daemon socket
```

**Solution:**
```bash
# Add user to docker group
sudo usermod -aG docker $USER
newgrp docker

# Or run with sudo
sudo docker run ...
```

## Advanced: Pre-provisioning Devices

For environments where devices don't have internet access during initial setup:

### 1. Pre-generate Device ID and API Key

Use the API to pre-provision devices:

```bash
curl -X POST https://api.signalbeam.io/api/devices \
  -H "Content-Type: application/json" \
  -H "X-Tenant-ID: your-tenant-id" \
  -H "Authorization: Bearer your-admin-token" \
  -d '{
    "deviceId": "550e8400-e29b-41d4-a716-446655440000",
    "name": "warehouse-pi-01",
    "registrationToken": "sbt_a1b2c3d4..."
  }'
```

### 2. Deploy with Pre-configured Credentials

Create `appsettings.json` with device-specific credentials:

```json
{
  "SignalBeam": {
    "ApiUrl": "https://api.signalbeam.io",
    "TenantId": "123e4567-e89b-12d3-a456-426614174000",
    "DeviceId": "550e8400-e29b-41d4-a716-446655440000",
    "ApiKey": "sk_device_a1b2c3d4...",
    "DeviceName": "warehouse-pi-01"
  }
}
```

The agent will skip registration and use the provided credentials directly.

## Next Steps

After registration:

1. **Assign an App Bundle** - Deploy containerized applications to your device
2. **Configure Device Groups** - Organize devices by location, function, or environment
3. **Add Tags** - Label devices for easier filtering and management
4. **Monitor Health** - View device metrics (CPU, memory, disk usage)
5. **View Activity Logs** - Track device events and deployments

See [App Bundle Management](./app-bundles.md) for next steps.
