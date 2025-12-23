#!/bin/bash

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

print_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

print_step() {
    echo -e "${BLUE}==>${NC} $1"
}

# Configuration
VERSION="${VERSION:-0.1.0}"
PACKAGE_NAME="signalbeam-agent"
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_DIR="${SCRIPT_DIR}/../SignalBeam.EdgeAgent.Host"
BUILD_DIR="${SCRIPT_DIR}/build"

# Architectures to build
ARCHITECTURES=("linux-x64:amd64" "linux-arm64:arm64" "linux-arm:armhf")

print_info "Building SignalBeam Edge Agent .deb packages"
print_info "Version: ${VERSION}"
echo ""

# Clean previous builds
if [ -d "${BUILD_DIR}" ]; then
    print_step "Cleaning previous builds..."
    rm -rf "${BUILD_DIR}"
fi

# Build for each architecture
for ARCH_PAIR in "${ARCHITECTURES[@]}"; do
    IFS=':' read -r DOTNET_ARCH DEB_ARCH <<< "$ARCH_PAIR"

    print_step "Building for ${DOTNET_ARCH} (${DEB_ARCH})..."

    PACKAGE_DIR="${BUILD_DIR}/${PACKAGE_NAME}_${VERSION}_${DEB_ARCH}"

    # Create package directory structure
    mkdir -p "${PACKAGE_DIR}/DEBIAN"
    mkdir -p "${PACKAGE_DIR}/usr/local/bin"
    mkdir -p "${PACKAGE_DIR}/etc/systemd/system"
    mkdir -p "${PACKAGE_DIR}/usr/share/doc/${PACKAGE_NAME}"

    # Build the .NET application
    print_info "  Publishing .NET application for ${DOTNET_ARCH}..."
    dotnet publish "${PROJECT_DIR}" \
        -c Release \
        -r "${DOTNET_ARCH}" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:PublishTrimmed=true \
        -p:TrimMode=link \
        -o "${PACKAGE_DIR}/usr/local/bin/" \
        > /dev/null

    # Rename binary
    if [ -f "${PACKAGE_DIR}/usr/local/bin/SignalBeam.EdgeAgent.Host" ]; then
        mv "${PACKAGE_DIR}/usr/local/bin/SignalBeam.EdgeAgent.Host" \
           "${PACKAGE_DIR}/usr/local/bin/${PACKAGE_NAME}"
    fi

    # Make binary executable
    chmod 755 "${PACKAGE_DIR}/usr/local/bin/${PACKAGE_NAME}"

    # Copy systemd service file
    print_info "  Copying systemd service file..."
    cp "${SCRIPT_DIR}/../systemd/signalbeam-agent.service" \
       "${PACKAGE_DIR}/etc/systemd/system/"

    # Copy documentation
    if [ -f "${SCRIPT_DIR}/../README.md" ]; then
        cp "${SCRIPT_DIR}/../README.md" \
           "${PACKAGE_DIR}/usr/share/doc/${PACKAGE_NAME}/"
    fi

    # Create copyright file
    cat > "${PACKAGE_DIR}/usr/share/doc/${PACKAGE_NAME}/copyright" <<EOF
Format: https://www.debian.org/doc/packaging-manuals/copyright-format/1.0/
Upstream-Name: signalbeam-agent
Source: https://github.com/signalbeam-io/signalbeam-edge

Files: *
Copyright: 2025 SignalBeam
License: Proprietary
 Copyright © 2025 SignalBeam. All rights reserved.
EOF

    # Create changelog
    cat > "${PACKAGE_DIR}/usr/share/doc/${PACKAGE_NAME}/changelog.Debian" <<EOF
signalbeam-agent (${VERSION}) stable; urgency=medium

  * Initial release
  * Device registration and authentication
  * Heartbeat monitoring
  * Container reconciliation
  * Metrics collection
  * systemd integration

 -- SignalBeam <support@signalbeam.io>  $(date -R)
EOF

    gzip -9 "${PACKAGE_DIR}/usr/share/doc/${PACKAGE_NAME}/changelog.Debian"

    # Copy DEBIAN control files
    print_info "  Creating DEBIAN control files..."
    cp "${SCRIPT_DIR}/DEBIAN/control" "${PACKAGE_DIR}/DEBIAN/"
    cp "${SCRIPT_DIR}/DEBIAN/postinst" "${PACKAGE_DIR}/DEBIAN/"
    cp "${SCRIPT_DIR}/DEBIAN/prerm" "${PACKAGE_DIR}/DEBIAN/"
    cp "${SCRIPT_DIR}/DEBIAN/postrm" "${PACKAGE_DIR}/DEBIAN/"

    # Update architecture in control file
    sed -i.bak "s/ARCH_PLACEHOLDER/${DEB_ARCH}/g" "${PACKAGE_DIR}/DEBIAN/control"
    rm -f "${PACKAGE_DIR}/DEBIAN/control.bak"

    # Update version in control file
    sed -i.bak "s/Version: .*/Version: ${VERSION}/g" "${PACKAGE_DIR}/DEBIAN/control"
    rm -f "${PACKAGE_DIR}/DEBIAN/control.bak"

    # Make maintainer scripts executable
    chmod 755 "${PACKAGE_DIR}/DEBIAN/postinst"
    chmod 755 "${PACKAGE_DIR}/DEBIAN/prerm"
    chmod 755 "${PACKAGE_DIR}/DEBIAN/postrm"

    # Calculate installed size
    INSTALLED_SIZE=$(du -sk "${PACKAGE_DIR}" | cut -f1)
    echo "Installed-Size: ${INSTALLED_SIZE}" >> "${PACKAGE_DIR}/DEBIAN/control"

    # Build the .deb package
    print_info "  Building .deb package..."
    dpkg-deb --build "${PACKAGE_DIR}" > /dev/null

    print_info "  ✅ Created: ${PACKAGE_NAME}_${VERSION}_${DEB_ARCH}.deb"

    # Clean up package directory
    rm -rf "${PACKAGE_DIR}"

    echo ""
done

print_step "Package build complete!"
echo ""
print_info "Built packages:"
ls -lh "${BUILD_DIR}"/*.deb
echo ""
print_info "To install a package:"
print_info "  sudo dpkg -i ${BUILD_DIR}/${PACKAGE_NAME}_${VERSION}_<arch>.deb"
echo ""
