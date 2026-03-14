---
name: screenshot
description: Capture a screenshot of a URL or route with one command. Use whenever the user wants to take a screenshot, capture a page, see what a route looks like, or visually verify a page. Supports mobile viewport with --mobile and unauthenticated mode with --no-auth.
allowed-tools: mcp__playwright__browser_navigate, mcp__playwright__browser_wait_for, mcp__playwright__browser_take_screenshot, mcp__playwright__browser_evaluate, mcp__playwright__browser_close, mcp__playwright__browser_resize
user-invocable: true
---

# Screenshot

Capture a screenshot of a URL or route. Nothing else — no tables, no analysis, just the image.

## Arguments

- `{url-or-route}` — URL or route path (default: `/dashboard`). If starts with `/`, prepends `http://localhost:5173`.
- `--mobile` — Resize viewport to 375x667 (iPhone SE) before capturing
- `--no-auth` — Skip authentication (use for login page or public pages)

## Process

### Step 1: Resolve URL

If the argument starts with `/`, prepend `http://localhost:5173`. Otherwise use the full URL as-is.

### Step 2: Authenticate (unless --no-auth)

Skip this step if `--no-auth` is passed or the URL is not on `localhost:5173`.

Navigate to `http://localhost:5173` first to establish the origin, then inject auth via localStorage:

```javascript
localStorage.setItem('auth-storage', JSON.stringify({
  state: { isAuthenticated: true, apiKey: 'dev-api-key-1', tenantId: 'dev-tenant' },
  version: 0
}))
```

### Step 3: Resize (if --mobile)

If `--mobile` is passed, resize the browser to `375, 667`.

### Step 4: Navigate and Capture

1. `browser_navigate` to the resolved URL
2. `browser_wait_for` network idle or 3 seconds
3. `browser_take_screenshot`

### Step 5: Cleanup

Close the browser session with `browser_close`.

## Output

Just show the screenshot. No markdown tables, no analysis, no status reports.

If the page shows an error or blank content, mention it briefly alongside the screenshot.

## Error Handling

- **Frontend not running:** Report that `localhost:5173` isn't responding and suggest `/run-local`.
- **Auth redirect:** If not using `--no-auth` and the page redirects to `/login`, the auth injection may have failed. Retry once with a page reload.

## Related Skills

- `/smoke-test` for testing multiple routes with console/network checks
- `/verify-feature` for post-implementation browser verification
- `/run-local` to start the frontend first
