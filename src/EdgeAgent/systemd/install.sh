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

print_info "Installing SignalBeam Edge Agent as systemd service..."

# Determine the script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

# Check if binary exists in common locations
BINARY_SOURCE=""
if [ -f "${SCRIPT_DIR}/../bin/Release/net9.0/linux-x64/publish/${BINARY_NAME}" ]; then
    BINARY_SOURCE="${SCRIPT_DIR}/../bin/Release/net9.0/linux-x64/publish/${BINARY_NAME}"
elif [ -f "${SCRIPT_DIR}/${BINARY_NAME}" ]; then
    BINARY_SOURCE="${SCRIPT_DIR}/${BINARY_NAME}"
elif [ -f "./${BINARY_NAME}" ]; then
    BINARY_SOURCE="./${BINARY_NAME}"
else
    print_error "Binary '${BINARY_NAME}' not found!"
    echo "Please build the agent first:"
    echo "  cd src/EdgeAgent/SignalBeam.EdgeAgent.Host"
    echo "  dotnet publish -c Release -r linux-x64 --self-contained false"
    exit 1
fi

print_info "Found binary at: ${BINARY_SOURCE}"

# Check if service file exists
if [ ! -f "${SCRIPT_DIR}/${SERVICE_FILE}" ]; then
    print_error "Service file '${SERVICE_FILE}' not found in ${SCRIPT_DIR}"
    exit 1
fi

# Create user and group if they don't exist
if ! id -u ${USER} >/dev/null 2>&1; then
    print_info "Creating system user '${USER}'..."
    useradd --system --no-create-home --shell /bin/false ${USER}
else
    print_info "User '${USER}' already exists"
fi

# Add user to docker group for container management
if getent group docker >/dev/null 2>&1; then
    print_info "Adding user '${USER}' to docker group..."
    usermod -aG docker ${USER}
else
    print_warn "Docker group not found. Make sure Docker is installed."
fi

# Create directories
print_info "Creating directories..."
mkdir -p ${STATE_DIR}
mkdir -p ${LOG_DIR}
mkdir -p ${CONFIG_DIR}

# Set ownership
print_info "Setting directory permissions..."
chown -R ${USER}:${GROUP} ${STATE_DIR}
chown -R ${USER}:${GROUP} ${LOG_DIR}
chown -R ${USER}:${GROUP} ${CONFIG_DIR}

chmod 755 ${STATE_DIR}
chmod 755 ${LOG_DIR}
chmod 755 ${CONFIG_DIR}

# Copy binary
print_info "Installing binary to ${BINARY_PATH}..."
cp -f ${BINARY_SOURCE} ${BINARY_PATH}
chmod 755 ${BINARY_PATH}

# Copy service file
print_info "Installing systemd service to ${SERVICE_PATH}..."
cp -f ${SCRIPT_DIR}/${SERVICE_FILE} ${SERVICE_PATH}
chmod 644 ${SERVICE_PATH}

# Reload systemd
print_info "Reloading systemd daemon..."
systemctl daemon-reload

# Enable service
print_info "Enabling service to start on boot..."
systemctl enable ${SERVICE_NAME}

print_info ""
print_info "✅ Installation complete!"
print_info ""
print_info "Next steps:"
print_info "  1. Register your device:"
print_info "     ${BINARY_PATH} register --tenant-id <your-tenant-id> --device-id <device-id> --token <token>"
print_info ""
print_info "  2. Start the service:"
print_info "     sudo systemctl start ${SERVICE_NAME}"
print_info ""
print_info "  3. Check service status:"
print_info "     sudo systemctl status ${SERVICE_NAME}"
print_info ""
print_info "  4. View logs:"
print_info "     sudo journalctl -u ${SERVICE_NAME} -f"
print_info ""
