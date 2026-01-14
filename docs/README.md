# SignalBeam Edge Documentation

Welcome to the SignalBeam Edge documentation! This directory contains guides, architecture documentation, and operational procedures for the SignalBeam Edge platform.

## Getting Started

- **[Project Overview](./project-overview.md)** - Learn what SignalBeam Edge is and how it works
- **[Device Registration Guide](./device-registration.md)** - Step-by-step guide to register edge devices ⭐ **Start here!**

## Features

- **[Authentication & Multi-Tenancy](./features/authentication.md)** - Complete guide to Zitadel authentication, workspaces, and subscriptions ⭐
- **[App Bundles](./features/)** - Deploy containerized applications to edge devices
- **[Device Groups](./features/)** - Organize and manage devices at scale
- **[Tags](./features/)** - Label and filter devices

## Architecture

- **[Main Architecture](./architecture.md)** - Complete system architecture overview ⭐
- **[Authentication Architecture](./architecture/authentication-architecture.md)** - Deep dive into authentication flows, JWT validation, and multi-tenancy
- **[Docker Requirements](./docker-requirements.md)** - Container runtime requirements

## Development

- **[Development Setup](./development/)** - Local development environment setup
- **[Zitadel Aspire Setup](./zitadel-aspire-setup.md)** - Running Zitadel with .NET Aspire for local development ⭐
- **[Zitadel Production Setup](./zitadel-setup.md)** - Production Zitadel deployment and configuration
- **[Frontend Authentication Guide](./web/authentication.md)** - Frontend-specific authentication configuration
- **[Business Plan](./business-plan.md)** - Product vision and roadmap

## Quick Links

### For Device Operators

1. [Register a new device](./device-registration.md#step-by-step-instructions)
2. [Deploy an app bundle](./features/)
3. [Monitor device health](./features/)
4. [Troubleshoot common issues](./device-registration.md#troubleshooting)

### For Platform Administrators

1. [Generate registration tokens](./device-registration.md#step-1-generate-a-registration-token)
2. [Manage device groups](./features/)
3. [Monitor fleet status](./features/)
4. [Configure rollouts](./features/)

### For Developers

1. [Set up development environment](./development/)
2. [Architecture documentation](./architecture/)
3. [API reference](../src/DeviceManager/SignalBeam.DeviceManager.Host/) (OpenAPI/Scalar)

## Support

For issues and questions:
- Check the [Troubleshooting Guide](./device-registration.md#troubleshooting)
- Review [GitHub Issues](https://github.com/signalbeam-io/signalbeam-edge/issues)
- Contact support: support@signalbeam.io
