#!/bin/bash

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
BINARY_NAME="signalbeam-agent"
BINARY_PATH="/usr/local/bin/${BINARY_NAME}"
SERVICE_NAME="signalbeam-agent"
SERVICE_FILE="${SERVICE_NAME}.service"
SYSTEMD_DIR="/etc/systemd/system"
SERVICE_PATH="${SYSTEMD_DIR}/${SERVICE_FILE}"
USER="signalbeam"
GROUP="signalbeam"
STATE_DIR="/var/lib/signalbeam"
LOG_DIR="/var/log/signalbeam"
CONFIG_DIR="/etc/signalbeam"

# Print functions
print_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    print_error "This script must be run as root"
    echo "Please run: sudo $0"
    exit 1
fi

print_info "Uninstalling SignalBeam Edge Agent systemd service..."

# Stop service if running
if systemctl is-active --quiet ${SERVICE_NAME}; then
    print_info "Stopping service..."
    systemctl stop ${SERVICE_NAME}
fi

# Disable service if enabled
if systemctl is-enabled --quiet ${SERVICE_NAME}; then
    print_info "Disabling service..."
    systemctl disable ${SERVICE_NAME}
fi

# Remove service file
if [ -f ${SERVICE_PATH} ]; then
    print_info "Removing service file..."
    rm -f ${SERVICE_PATH}
fi

# Reload systemd
print_info "Reloading systemd daemon..."
systemctl daemon-reload
systemctl reset-failed

# Remove binary
if [ -f ${BINARY_PATH} ]; then
    print_info "Removing binary..."
    rm -f ${BINARY_PATH}
fi

# Ask about removing data directories
echo ""
print_warn "The following directories contain configuration and data:"
print_warn "  - ${STATE_DIR}"
print_warn "  - ${LOG_DIR}"
print_warn "  - ${CONFIG_DIR}"
echo ""
read -p "Do you want to remove these directories? [y/N] " -n 1 -r
echo

if [[ $REPLY =~ ^[Yy]$ ]]; then
    print_info "Removing data directories..."
    rm -rf ${STATE_DIR}
    rm -rf ${LOG_DIR}
    rm -rf ${CONFIG_DIR}
else
    print_info "Keeping data directories"
fi

# Ask about removing user
echo ""
read -p "Do you want to remove the '${USER}' system user? [y/N] " -n 1 -r
echo

if [[ $REPLY =~ ^[Yy]$ ]]; then
    print_info "Removing system user '${USER}'..."
    userdel ${USER} 2>/dev/null || true
else
    print_info "Keeping system user '${USER}'"
fi

print_info ""
print_info "✅ Uninstallation complete!"
print_info ""
