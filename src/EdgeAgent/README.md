# SignalBeam Edge Agent

Lightweight agent that runs on edge devices (Raspberry Pi, mini-PCs) to reconcile desired state with running Docker containers.

## Overview

The Edge Agent polls the SignalBeam cloud API for its assigned bundle configuration, compares it against locally running containers, and pulls/starts/stops containers to match the desired state.

## Installation

Install the `.deb` package:

```bash
sudo dpkg -i signalbeam-edge-agent_<version>_<arch>.deb
```

## Configuration

Configuration is read from `/etc/signalbeam/agent.yaml` or environment variables.

| Variable | Description |
|----------|-------------|
| `SIGNALBEAM_API_URL` | Cloud API endpoint |
| `SIGNALBEAM_DEVICE_ID` | Unique device identifier |
| `SIGNALBEAM_API_KEY` | Authentication key |

## License

Proprietary — SignalBeam.io
