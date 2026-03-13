---
name: run-tests
description: Run .NET tests with smart change detection — auto-detects changed files via git diff and runs only affected test projects. Use whenever the user wants to run tests, verify changes, check if tests pass, run unit tests, integration tests, or test a specific service. Supports service names (e.g., "device-manager", "edge-agent") and aliases (dm, ea, bo, tp, im).
allowed-tools: Bash
user-invocable: true
---

# Run Tests — Smart Change-Aware Test Selection

Run tests based on what changed. Auto-detects affected services from git diff and runs only their test projects instead of the full suite.

## 1. Parse Arguments

Determine from user input:
- **MODE**: `unit` (default), `integration`, or `all`
- **SERVICE**: optional service slug (e.g., `edge-agent`, `dm`)
- **Explicit `--all` flag**: runs both unit and integration for all projects

| User says | MODE | SERVICE |
|-----------|------|---------|
| `/run-tests` | unit | (auto-detect) |
| `/run-tests edge-agent` | unit | edge-agent |
| `/run-tests dm` | unit | device-manager |
| `/run-tests --integration` | integration | (auto-detect) |
| `/run-tests --integration device-manager` | integration | device-manager |
| `/run-tests --all` | all | (auto-detect) |
| `/run-tests --all bo` | all | bundle-orchestrator |

## 2. Resolve Test Projects

Run the resolver script:

```bash
bash .claude/skills/run-tests/resolve-test-projects.sh --mode $MODE [--service $SERVICE]
```

Handle exit codes:
- **Exit 0, output = `NO_TESTS_AFFECTED`**: Only non-source files changed. Report "No .NET tests affected by current changes" and offer to run full suite.
- **Exit 0, output = project paths**: Run only those targeted projects.
- **Exit 2**: Shared dependency changed or fallback — run full suite via `dotnet test src/SignalBeam.sln`.
- **Exit 1**: Error — report the error message.

## 3. Pre-flight: Docker Check (Integration Tests Only)

If MODE is `integration` or `all`:

```bash
docker info > /dev/null 2>&1 || echo "ERROR: Docker is not running. Integration tests require Docker for Testcontainers."
```

If Docker is not running, warn the user and stop. Do not attempt integration tests without Docker.

## 4. Build

Build once before running tests:

```bash
dotnet build src/SignalBeam.sln --no-restore -q
```

If build fails, report the errors and stop.

## 5. Run Tests

**Targeted projects** (exit 0 with paths):
```bash
# Run each project individually
for PROJECT in $PROJECTS; do
  dotnet test "$PROJECT" --no-restore --no-build
done
```

**Full suite** (exit 2 or explicit --all):
```bash
dotnet test src/SignalBeam.sln --no-restore --no-build
```

For debugging failures, add verbosity:
```bash
dotnet test "$PROJECT" --no-restore --no-build --verbosity normal --logger "console;verbosity=detailed"
```

## 6. Report

Use this output format:

```
## Test Results
- Mode: {Unit | Integration | All}
- Scope: {ServiceA, ServiceB (N of M test projects) | Full suite}
- Reason: {auto-detected from git changes | explicit service | shared dependency changed | full suite requested}
- Passed: {count}
- Failed: {count}
- Skipped: {count}
- Duration: {time}

### Failures (if any)
| Test | Expected | Actual | Location |
|------|----------|--------|----------|
| {test name} | {expected} | {actual} | {file:line} |

### Summary: {PASS | FAIL}
```

For failures, suggest fixes if they relate to recent changes.

## Error Handling

- **Docker not running (integration tests):** Warn and suggest starting Docker. Do not attempt integration tests without Docker.
- **Build errors:** Report compilation issues before testing.
- **No integration tests for service:** Report "No integration tests found for {service}" — this is normal for some services (e.g., identity-manager).
- **Test project not found:** The resolver script checks existence. If nothing is found, it falls back to full suite.

## Related Skills

- Use `/check-architecture` to verify architecture rules before running tests
- Use `/lint` to check formatting before creating a PR
