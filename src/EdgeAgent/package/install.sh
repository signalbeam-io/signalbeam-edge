#!/bin/bash

# SignalBeam Edge Agent Universal Installer
# Usage: curl -fsSL https://install.signalbeam.io | bash
# Or: wget -qO- https://install.signalbeam.io | bash

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
PACKAGE_NAME="signalbeam-agent"
VERSION="${SIGNALBEAM_VERSION:-0.1.0}"
GITHUB_REPO="signalbeam-io/signalbeam-edge"
BASE_URL="${SIGNALBEAM_BASE_URL:-https://github.com/${GITHUB_REPO}/releases/download/v${VERSION}}"

print_step "SignalBeam Edge Agent Installer"
echo ""
print_info "Version: ${VERSION}"
echo ""

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    print_error "This script must be run as root"
    echo "Please run: sudo $0"
    exit 1
fi

# Detect OS
detect_os() {
    if [ -f /etc/os-release ]; then
        . /etc/os-release
        OS=$ID
        OS_VERSION=$VERSION_ID
    elif [ -f /etc/lsb-release ]; then
        . /etc/lsb-release
        OS=$DISTRIB_ID
        OS_VERSION=$DISTRIB_RELEASE
    else
        print_error "Unable to detect OS"
        exit 1
    fi

    # Convert to lowercase
    OS=$(echo "$OS" | tr '[:upper:]' '[:lower:]')
}

# Detect architecture
detect_arch() {
    ARCH=$(uname -m)
    case $ARCH in
        x86_64)
            DEB_ARCH="amd64"
            ;;
        aarch64)
            DEB_ARCH="arm64"
            ;;
        armv7l)
            DEB_ARCH="armhf"
            ;;
        *)
            print_error "Unsupported architecture: $ARCH"
            exit 1
            ;;
    esac
}

# Check if Docker is installed
check_docker() {
    if ! command -v docker &> /dev/null; then
        print_warn "Docker is not installed"
        return 1
    fi

    if ! systemctl is-active --quiet docker; then
        print_warn "Docker is not running"
        return 1
    fi

    return 0
}

# Install Docker
install_docker() {
    print_step "Installing Docker..."

    case $OS in
        ubuntu|debian|raspbian)
            apt-get update
            apt-get install -y ca-certificates curl gnupg

            # Add Docker's official GPG key
            install -m 0755 -d /etc/apt/keyrings
            curl -fsSL https://download.docker.com/linux/${OS}/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg
            chmod a+r /etc/apt/keyrings/docker.gpg

            # Add Docker repository
            echo \
              "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/${OS} \
              $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
              tee /etc/apt/sources.list.d/docker.list > /dev/null

            apt-get update
            apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

            systemctl enable docker
            systemctl start docker

            print_info "Docker installed successfully"
            ;;
        *)
            print_error "Automatic Docker installation not supported for $OS"
            echo "Please install Docker manually: https://docs.docker.com/engine/install/"
            exit 1
            ;;
    esac
}

# Install .deb package
install_deb() {
    local package_file="$1"

    print_step "Installing ${PACKAGE_NAME} package..."

    dpkg -i "$package_file" || {
        print_warn "Fixing dependencies..."
        apt-get install -f -y
    }

    print_info "Package installed successfully"
}

# Download and install
install_signalbeam() {
    local package_file="${PACKAGE_NAME}_${VERSION}_${DEB_ARCH}.deb"
    local download_url="${BASE_URL}/${package_file}"
    local temp_file="/tmp/${package_file}"

    print_step "Downloading ${PACKAGE_NAME} for ${DEB_ARCH}..."
    print_info "URL: ${download_url}"

    if command -v curl &> /dev/null; then
        curl -fsSL -o "$temp_file" "$download_url" || {
            print_error "Failed to download package"
            exit 1
        }
    elif command -v wget &> /dev/null; then
        wget -q -O "$temp_file" "$download_url" || {
            print_error "Failed to download package"
            exit 1
        }
    else
        print_error "Neither curl nor wget is available"
        exit 1
    fi

    print_info "Download complete"

    # Install the package
    install_deb "$temp_file"

    # Clean up
    rm -f "$temp_file"
}

# Main installation flow
main() {
    # Detect OS and architecture
    detect_os
    detect_arch

    print_info "Detected OS: ${OS} ${OS_VERSION}"
    print_info "Detected Architecture: ${ARCH} (${DEB_ARCH})"
    echo ""

    # Check for Debian-based OS
    case $OS in
        ubuntu|debian|raspbian)
            print_info "OS supported: ${OS}"
            ;;
        *)
            print_error "Unsupported OS: ${OS}"
            print_error "This installer only supports Ubuntu, Debian, and Raspberry Pi OS"
            echo ""
            echo "For manual installation, please visit:"
            echo "https://github.com/${GITHUB_REPO}/releases"
            exit 1
            ;;
    esac

    # Check Docker installation
    if ! check_docker; then
        echo ""
        read -p "Docker is required but not installed. Install Docker now? [Y/n] " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Nn]$ ]]; then
            install_docker
        else
            print_warn "Skipping Docker installation"
            print_warn "You will need to install Docker manually before running the agent"
        fi
    else
        print_info "Docker is already installed and running"
    fi

    echo ""

    # Download and install SignalBeam
    install_signalbeam

    echo ""
    print_step "Installation complete!"
    echo ""
    print_info "Next steps:"
    echo ""
    echo "  1. Register your device:"
    echo "     signalbeam-agent register \\"
    echo "       --tenant-id <your-tenant-id> \\"
    echo "       --device-id <unique-device-id> \\"
    echo "       --token <registration-token> \\"
    echo "       --cloud-url https://api.signalbeam.com"
    echo ""
    echo "  2. Start the service:"
    echo "     sudo systemctl start signalbeam-agent"
    echo ""
    echo "  3. Check service status:"
    echo "     sudo systemctl status signalbeam-agent"
    echo ""
    echo "  4. View logs:"
    echo "     sudo journalctl -u signalbeam-agent -f"
    echo ""
    echo "For more information, visit: https://docs.signalbeam.io"
    echo ""
}

# Run main installation
main
