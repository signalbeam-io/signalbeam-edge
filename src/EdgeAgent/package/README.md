# SignalBeam Edge Agent Packaging

This directory contains the packaging configuration and build scripts for distributing the SignalBeam Edge Agent.

## Contents

- `DEBIAN/` - Debian package control files
  - `control` - Package metadata and dependencies
  - `postinst` - Post-installation script
  - `prerm` - Pre-removal script
  - `postrm` - Post-removal script
- `build-deb.sh` - Script to build .deb packages for all architectures
- `install.sh` - Universal installation script for end users
- `INSTALL.md` - Comprehensive installation guide
- `README.md` - This file

## Building Packages

### Prerequisites

- .NET 9.0 SDK
- dpkg tools (`dpkg-deb`)
- Linux system (or WSL on Windows)

### Build All Packages

```bash
cd src/EdgeAgent/package
sudo ./build-deb.sh
```

This builds .deb packages for:
- `amd64` (x86_64)
- `arm64` (ARM 64-bit)
- `armhf` (ARM 32-bit)

Built packages will be in `build/` directory.

### Build Specific Version

```bash
VERSION=1.0.0 ./build-deb.sh
```

## Package Structure

The .deb package installs the following:

```
/usr/local/bin/signalbeam-agent         # Main binary
/etc/systemd/system/signalbeam-agent.service  # systemd service
/var/lib/signalbeam/                    # State directory (created)
/var/log/signalbeam/                    # Log directory (created)
/etc/signalbeam/                        # Config directory (created)
/usr/share/doc/signalbeam-agent/        # Documentation
```

System user `signalbeam:signalbeam` is created during installation.

## Installation Scripts

### Universal Installer (`install.sh`)

The universal installer provides a one-line installation experience:

```bash
curl -fsSL https://install.signalbeam.io | sudo bash
```

**Features:**
- Automatic OS and architecture detection
- Downloads correct package from GitHub releases
- Optional Docker installation
- Provides post-install guidance

**Usage:**

```bash
# Default installation
curl -fsSL https://install.signalbeam.io | sudo bash

# Specify version
curl -fsSL https://install.signalbeam.io | SIGNALBEAM_VERSION=1.0.0 sudo bash

# Custom download URL
curl -fsSL https://install.signalbeam.io | SIGNALBEAM_BASE_URL=https://custom.url sudo bash
```

## Package Maintainer Scripts

### postinst

Runs after package installation:
- Creates `signalbeam` user and group
- Adds user to `docker` group
- Creates required directories with proper permissions
- Enables systemd service

### prerm

Runs before package removal:
- Stops the service
- Disables the service

### postrm

Runs after package removal:
- On purge: removes data directories and user
- Reloads systemd daemon

## GitHub Actions Workflow

The `.github/workflows/build-edge-agent.yml` workflow automatically:

1. **On every push to main/develop:**
   - Builds .deb packages for all architectures
   - Builds multi-arch Docker image
   - Uploads artifacts

2. **On version tags (v*):**
   - Builds release packages
   - Creates GitHub Release
   - Publishes Docker images to GHCR
   - Attaches .deb files to release

## Testing Packages

### Local Testing

```bash
# Build package
./build-deb.sh

# Install locally
sudo dpkg -i build/signalbeam-agent_*_amd64.deb

# Test
signalbeam-agent version
systemctl status signalbeam-agent

# Uninstall
sudo apt remove signalbeam-agent
# Or purge (removes data)
sudo apt purge signalbeam-agent
```

### Test in Docker Container

```bash
# Ubuntu
docker run -it --rm ubuntu:22.04 bash
# Inside container:
apt update && apt install -y curl
curl -fsSL https://raw.githubusercontent.com/signalbeam-io/signalbeam-edge/main/src/EdgeAgent/package/install.sh | bash

# Debian
docker run -it --rm debian:12 bash

# Raspberry Pi OS (ARM64)
docker run -it --rm --platform linux/arm64 ubuntu:22.04 bash
```

## Distribution

### GitHub Releases

Packages are automatically attached to GitHub releases:

```
https://github.com/signalbeam-io/signalbeam-edge/releases/download/v1.0.0/signalbeam-agent_1.0.0_amd64.deb
https://github.com/signalbeam-io/signalbeam-edge/releases/download/v1.0.0/signalbeam-agent_1.0.0_arm64.deb
https://github.com/signalbeam-io/signalbeam-edge/releases/download/v1.0.0/signalbeam-agent_1.0.0_armhf.deb
```

### Docker Images

Multi-arch images are published to GitHub Container Registry:

```bash
docker pull ghcr.io/signalbeam-io/edge-agent:latest
docker pull ghcr.io/signalbeam-io/edge-agent:1.0.0
```

## Creating a Release

1. **Tag the release:**
   ```bash
   git tag -a v1.0.0 -m "Release v1.0.0"
   git push origin v1.0.0
   ```

2. **GitHub Actions will automatically:**
   - Build packages
   - Create release
   - Upload assets

3. **Update install.signalbeam.io:**
   - Point to `src/EdgeAgent/package/install.sh`
   - Set up CDN/redirect

## Troubleshooting

### Build Fails on ARM

If building ARM packages on x86 fails, you may need QEMU:

```bash
sudo apt install qemu-user-static
```

Or use GitHub Actions which has multi-arch support.

### Package Size

The packages are self-contained .NET applications (no runtime required on target).

**Note:** Trimming is disabled to ensure compatibility with JSON serialization and reflection-based features used by the agent. This results in larger binaries but guarantees reliability.

Current sizes (approximate):
- amd64: ~80-90 MB
- arm64: ~75-85 MB
- armhf: ~70-80 MB

To reduce size, you can use framework-dependent deployment (requires .NET 9.0 runtime on target):
```bash
dotnet publish -c Release -r linux-x64 --self-contained false
```

### Permission Issues

Ensure the `signalbeam` user has access to Docker:

```bash
sudo usermod -aG docker signalbeam
```

## Development

### Local Development Without Package

For development, you don't need to build packages:

```bash
cd src/EdgeAgent/SignalBeam.EdgeAgent.Host
dotnet run -- --help
```

### Testing Package Scripts

Test individual scripts:

```bash
# Test postinst
sudo bash -x DEBIAN/postinst

# Test prerm
sudo bash -x DEBIAN/prerm

# Test postrm
sudo bash -x DEBIAN/postrm
```

## Future Enhancements

- [ ] APT repository for easier updates
- [ ] RPM packages for RHEL/CentOS
- [ ] Snap/Flatpak packages
- [ ] Homebrew formula for macOS
- [ ] Windows installer (.msi)
- [ ] Auto-update mechanism

## License

Copyright © 2025 SignalBeam. All rights reserved.
