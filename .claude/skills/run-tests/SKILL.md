---
name: run-tests
description: Run .NET tests for the project — unit tests, integration tests, or all. Use to verify changes haven't broken anything, or to run specific test projects for a service.
allowed-tools: Bash
user-invocable: true
---

# Run Tests

Run tests based on what the user asks for. Default to unit tests only.

## Pre-flight: Docker Check (Integration Tests Only)

Before running integration tests, verify Docker is available:

```bash
docker info > /dev/null 2>&1 || echo "ERROR: Docker is not running. Integration tests require Docker for Testcontainers."
```

If Docker is not running, warn the user and suggest starting Docker first. Do not attempt to run integration tests without Docker.

## Commands

**All unit tests (excludes integration):**
```bash
dotnet test src/SignalBeam.sln --filter "Category!=Integration" --no-restore
```

**All integration tests (requires Docker running):**
```bash
dotnet test src/SignalBeam.sln --filter "Category=Integration" --no-restore
```

**All tests:**
```bash
dotnet test src/SignalBeam.sln --no-restore
```

**Specific test project:**
```bash
# Domain unit tests
dotnet test src/tests/SignalBeam.Domain.Tests/

# DeviceManager unit tests
dotnet test src/tests/SignalBeam.DeviceManager.Tests.Unit/

# DeviceManager integration tests
dotnet test src/tests/SignalBeam.DeviceManager.Tests.Integration/

# EdgeAgent integration tests
dotnet test src/tests/SignalBeam.EdgeAgent.Tests.Integration/
```

**Tests with verbosity (for debugging failures):**
```bash
dotnet test <path> --verbosity normal --logger "console;verbosity=detailed"
```

## After Running

- Report pass/fail counts
- For failures, show the test name, expected vs actual, and the relevant source location
- Suggest fixes for failing tests if the failures are related to recent changes

## Related Skills

- Use `/check-architecture` to verify architecture rules before running tests
- Use `/lint` to check formatting before creating a PR
