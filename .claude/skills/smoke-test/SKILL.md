---
name: smoke-test
description: Smoke test the running frontend and API by navigating key routes with Playwright, checking for console errors and broken pages. Use after /run-local or to verify the app works before creating a PR.
allowed-tools: Bash, Read, Glob, Grep, mcp__playwright__browser_navigate, mcp__playwright__browser_snapshot, mcp__playwright__browser_take_screenshot, mcp__playwright__browser_console_messages, mcp__playwright__browser_wait_for, mcp__playwright__browser_network_requests, mcp__playwright__browser_close
user-invocable: true
---

# Smoke Test

Navigate key application routes using Playwright and verify they render without errors.

## Arguments

- `--frontend-only` — Only test frontend routes (skip API health checks)
- `--api-only` — Only test API health endpoints
- `--screenshot` — Take screenshots of each page (saved to /tmp/smoke-test/)
- `{url}` — Test a specific URL instead of the default routes

## Prerequisites

The local environment must be running. If not, suggest running `/run-local` first.

## Process

### Step 1: Verify Services Are Up

Check that the expected ports are responding before running browser tests:

```bash
# Check API
curl -sf http://localhost:5000/health/live > /dev/null 2>&1 && echo "API: UP" || echo "API: DOWN"

# Check Frontend
curl -sf http://localhost:5173 > /dev/null 2>&1 && echo "Frontend: UP" || echo "Frontend: DOWN"

# Check Aspire Dashboard
curl -sf https://localhost:15888 -k > /dev/null 2>&1 && echo "Aspire: UP" || echo "Aspire: DOWN"
```

If services are down, STOP and suggest `/run-local`.

### Step 2: API Health Checks (unless --frontend-only)

```bash
# Hit each service health endpoint
curl -sf http://localhost:5000/health/ready | python3 -m json.tool 2>/dev/null || echo "Health check failed"
```

### Step 3: Frontend Smoke Test (unless --api-only)

Navigate to each key route and verify it loads:

**Routes to test:**
1. `/` — Landing/dashboard page
2. `/devices` — Device list
3. `/bundles` — Bundle list
4. `/groups` — Device groups (if exists)

For each route:

1. **Navigate** using `mcp__playwright__browser_navigate`
2. **Wait** for the page to settle using `mcp__playwright__browser_wait_for` (wait for network idle or a known element)
3. **Snapshot** the page using `mcp__playwright__browser_snapshot` to check the accessibility tree for content
4. **Check console** using `mcp__playwright__browser_console_messages` for errors
5. **Screenshot** (if `--screenshot`) using `mcp__playwright__browser_take_screenshot`

### Step 4: Check for Problems

Flag issues:
- **Console errors** — any `error` level messages in the browser console
- **Network failures** — check `mcp__playwright__browser_network_requests` for failed requests (4xx/5xx)
- **Empty pages** — snapshot shows no meaningful content
- **Auth redirects** — unexpected redirects to /login

### Step 5: Cleanup

Close the browser session:
```
mcp__playwright__browser_close
```

## Output Format

```markdown
## Smoke Test Results

### API Health
| Endpoint | Status |
|----------|--------|
| /health/live | PASS |
| /health/ready | PASS |

### Frontend Routes
| Route | Status | Console Errors | Notes |
|-------|--------|---------------|-------|
| / | PASS | 0 | Dashboard rendered |
| /devices | PASS | 0 | Device table loaded |
| /bundles | FAIL | 2 | "TypeError: Cannot read property..." |

### Console Errors
- `/bundles`: `TypeError: Cannot read property 'map' of undefined` at bundles-overview.tsx:42

### Network Failures
- None

### Summary: {PASS / FAIL}
{N routes tested, M passed, K failed}
```

## Guidelines

- This is a fast smoke test, not a comprehensive E2E suite
- Check for obvious breakage, not pixel-perfect rendering
- Console errors are the most valuable signal
- If auth is required and no credentials are available, note which routes redirected to login
- Don't fail on warnings, only on errors

## Error Handling

- **Services not running:** STOP and suggest `/run-local` before retrying.
- **Playwright not available:** Suggest installing with `npx playwright install` or skip browser tests and fall back to `curl`-based health checks only.
- **Auth required:** Note which routes redirected to login and report as "SKIPPED (auth required)" rather than "FAIL".

## Related Skills

- `/run-local` to start the environment first
- `/add-feature` to scaffold the feature, then smoke test it
- `/diagnose` if smoke test reveals issues
