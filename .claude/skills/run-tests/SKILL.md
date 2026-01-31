---
name: run-tests
description: Run .NET tests for the project - unit tests, integration tests, or all
allowed-tools: Bash
---

# Run Tests

Run tests based on what the user asks for. Default to unit tests only.

## Commands

**All unit tests (excludes integration):**
```bash
dotnet test /Users/marjangjuroski/Projects/signalbeam-edge/src/SignalBeam.sln --filter "Category!=Integration" --no-restore
```

**All integration tests (requires Docker running):**
```bash
dotnet test /Users/marjangjuroski/Projects/signalbeam-edge/src/SignalBeam.sln --filter "Category=Integration" --no-restore
```

**All tests:**
```bash
dotnet test /Users/marjangjuroski/Projects/signalbeam-edge/src/SignalBeam.sln --no-restore
```

**Specific test project:**
```bash
# Domain unit tests
dotnet test /Users/marjangjuroski/Projects/signalbeam-edge/tests/SignalBeam.Domain.Tests/

# DeviceManager integration tests
dotnet test /Users/marjangjuroski/Projects/signalbeam-edge/tests/DeviceManager.Tests.Integration/

# EdgeAgent integration tests
dotnet test /Users/marjangjuroski/Projects/signalbeam-edge/tests/EdgeAgent.Tests.Integration/
```

**Tests with verbosity (for debugging failures):**
```bash
dotnet test <path> --verbosity normal --logger "console;verbosity=detailed"
```

## After Running
- Report pass/fail counts
- For failures, show the test name, expected vs actual, and the relevant source location
- Suggest fixes for failing tests if the failures are related to recent changes
