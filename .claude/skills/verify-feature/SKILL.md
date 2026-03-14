---
name: verify-feature
description: Auto-detect changed frontend routes, navigate them with Playwright, take screenshots, and check for console/network errors. Use after implementing a frontend feature to verify it works visually — auto-detects routes from git changes or accepts a route argument. Supports --responsive for mobile testing and --interactive for click testing.
allowed-tools: Bash, Read, Glob, Grep, mcp__playwright__browser_navigate, mcp__playwright__browser_snapshot, mcp__playwright__browser_take_screenshot, mcp__playwright__browser_console_messages, mcp__playwright__browser_wait_for, mcp__playwright__browser_network_requests, mcp__playwright__browser_close, mcp__playwright__browser_click, mcp__playwright__browser_fill_form, mcp__playwright__browser_evaluate, mcp__playwright__browser_resize
user-invocable: true
---

# Verify Feature

Post-implementation browser verification for frontend features. Auto-detects changed routes from git history, navigates them, takes screenshots, and checks for console/network errors.

## Arguments

- `{route}` — Route to verify (e.g., `/bundles`, `/devices`). If omitted, auto-detects from git changes.
- `--responsive` — Also capture mobile viewport screenshots (375x667)
- `--interactive` — Click the first Create/Add button, verify dialog appears, then dismiss

## Process

### Step 1: Detect Routes

If a route was provided, use it directly. Otherwise, auto-detect from recent changes:

```bash
# Find changed page files
git diff --name-only HEAD~3 | grep "web/src/features/.*/pages/"
```

Map changed page files to routes by reading `web/src/routes/index.tsx` and matching feature names to route paths.

If no routes can be detected, ask the user which route to verify.

### Step 2: Pre-flight

```bash
curl -sf http://localhost:5173 > /dev/null 2>&1 && echo "Frontend: UP" || echo "Frontend: DOWN"
```

If frontend is down, STOP and suggest `/run-local`.

### Step 3: Authenticate

Navigate to `http://localhost:5173` to establish the origin, then inject auth via localStorage:

```javascript
localStorage.setItem('auth-storage', JSON.stringify({
  state: { isAuthenticated: true, apiKey: 'dev-api-key-1', tenantId: 'dev-tenant' },
  version: 0
}))
```

### Step 4: Verify Each Route

For each detected route:

1. **Navigate** — `browser_navigate` to `http://localhost:5173{route}`
2. **Wait** — `browser_wait_for` for content to load (network idle or 3 seconds)
3. **Snapshot** — `browser_snapshot` to check accessibility tree for meaningful content (not just a loading spinner or error boundary)
4. **Console** — `browser_console_messages` to flag any `error` level messages
5. **Network** — `browser_network_requests` to flag 4xx/5xx responses
6. **Screenshot** — `browser_take_screenshot` (always)

### Step 5: Responsive Check (if --responsive)

For each route:
1. `browser_resize` to `375, 667`
2. `browser_take_screenshot` — mobile viewport
3. `browser_resize` to `1280, 720` — restore desktop

### Step 6: Interactive Check (if --interactive)

For each route:
1. `browser_snapshot` to find clickable Create/Add/New buttons
2. If found: `browser_click` on the first one
3. `browser_wait_for` a dialog or form to appear (2 seconds)
4. `browser_snapshot` to verify the dialog rendered
5. `browser_click` on close/cancel or `browser_evaluate` to press Escape:
   ```javascript
   document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
   ```

### Step 7: Cleanup

`browser_close` to end the session.

## Output Format

```markdown
## Feature Verification: {route(s)}

### Route Checks
| Route | Content | Console | Network | Screenshot |
|-------|---------|---------|---------|------------|
| /bundles | PASS | PASS (0 errors) | PASS (all 2xx) | captured |
| /bundles/:id | FAIL | FAIL (1 error) | PASS | captured |

### Console Errors
- `/bundles/:id`: `TypeError: Cannot read property 'name' of undefined` at bundle-detail.tsx:28

### Network Issues
- None

### Responsive (if --responsive)
| Route | Mobile Screenshot | Issues |
|-------|-------------------|--------|
| /bundles | captured | None |

### Interactive (if --interactive)
| Route | Button Found | Dialog Opened | Dialog Closed |
|-------|-------------|---------------|---------------|
| /bundles | "Create Bundle" | PASS | PASS |

### Summary: {PASS / FAIL}
```

## Error Handling

- **Frontend not running:** STOP and suggest `/run-local`.
- **No routes detected:** Ask the user which route to verify.
- **Auth redirect:** If a page redirects to `/login` after auth injection, retry auth once with a full page reload.
- **Empty content:** If snapshot shows only a loading spinner after 5 seconds, report as FAIL with note.

## Related Skills

- `/screenshot` for a quick single-page capture
- `/smoke-test` for full app smoke testing across all routes
- `/add-feature` scaffolds the feature, then use this to verify it
- `/complete-task` runs this automatically when `web/` files changed
