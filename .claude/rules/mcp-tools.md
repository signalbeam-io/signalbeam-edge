# MCP Tool Usage

Prefer MCP tools over CLI equivalents when available. MCP tools return structured data, handle authentication automatically, and are more reliable than parsing CLI output.

## context7 — Library Documentation

Before scaffolding code that uses a library, look up the current API to avoid outdated patterns.

**When to use:**
- Scaffolding commands/queries/entities that use EF Core, WolverineFx, FluentValidation
- Debugging library-specific errors
- Checking if a library has a built-in feature before writing custom code
- Researching library capabilities during feature planning

**How to use:**
1. Resolve the library ID: `mcp__context7__resolve-library-id` with the library name
2. Query docs: `mcp__context7__query-docs` with the library ID and your question

**Key libraries in this project:**
- `efcore` / `microsoft/efcore` — Entity Framework Core (migrations, configurations, queries)
- `wolverine` / `wolverinefx` — WolverineFx (message handlers, middleware)
- `fluentvalidation` — FluentValidation (validator rules, custom validators)
- `tanstack/react-query` — TanStack Query (useQuery, useMutation, cache invalidation)
- `zustand` — Zustand (store creation, middleware, persist)
- `polly` — Polly (retry, circuit breaker, timeout policies)
- `nats.net` — NATS .NET client (pub/sub, JetStream)

Don't look up docs for every trivial operation — use it when the pattern is non-obvious, when hitting an error, or when a library feature might already exist for what you're building.

## github-mcp-server — GitHub Operations

Use structured MCP tools instead of `gh` CLI for GitHub operations. MCP tools return typed JSON directly without needing to parse CLI output.

**Prefer MCP for:**
- Reading issues: `mcp__github-mcp-server__get_issue` over `gh issue view`
- Creating issues: `mcp__github-mcp-server__create_issue` over `gh issue create`
- Creating PRs: `mcp__github-mcp-server__create_pull_request` over `gh pr create`
- Searching: `mcp__github-mcp-server__search_issues`, `search_code`

**Still use `gh` CLI for:**
- `gh auth` operations
- Complex queries with `gh api` that don't have an MCP equivalent
- Quick one-liners in pre-flight checks where the overhead isn't worth it

## playwright — Browser Automation

Use for frontend verification, smoke testing, interactive CRUD verification, and visual confirmation.

**When to use:**
- After `/run-local` — navigate to the Aspire dashboard and verify it loaded
- After `/add-feature` — screenshot the new page with `/screenshot` or `/verify-feature`
- During `/smoke-test` — verify routes render, auth works, and CRUD flows succeed
- During `/verify-feature` — auto-detect changed routes, screenshot, check console/network
- During `/complete-task` — browser verification when `web/` files changed
- Debugging frontend issues — take screenshots, check console errors, inspect network

### Basic pattern (navigate + check)

1. Navigate: `mcp__playwright__browser_navigate` to the URL
2. Wait: `mcp__playwright__browser_wait_for` for content to load
3. Snapshot: `mcp__playwright__browser_snapshot` for accessibility tree, or `browser_take_screenshot` for visual
4. Check: `mcp__playwright__browser_console_messages` for errors

### Auth login pattern

Two methods depending on context:

**Method 1 — UI form flow** (use when testing the login experience itself):
1. `browser_navigate` to `http://localhost:5173/login`
2. `browser_fill_form` with `apiKey: "dev-api-key-1"`
3. `browser_click` on the Continue/Sign In button
4. `browser_wait_for` redirect away from `/login`

**Method 2 — localStorage injection** (use for fast auth before testing other pages):
1. `browser_navigate` to `http://localhost:5173` (any page to establish origin)
2. `browser_evaluate` to inject auth state:
   ```javascript
   localStorage.setItem('auth-storage', JSON.stringify({
     state: { isAuthenticated: true, apiKey: 'dev-api-key-1', tenantId: 'dev-tenant' },
     version: 0
   }))
   ```
3. `browser_navigate` to the target route (reload picks up the auth state)

### Interactive CRUD pattern

For verifying create/edit/delete flows work end-to-end:

1. `browser_snapshot` to find interactive elements (buttons, links)
2. `browser_click` on the trigger (e.g., "Create Bundle" button)
3. `browser_wait_for` the dialog/form to appear
4. `browser_fill_form` with test data
5. `browser_click` on submit
6. `browser_network_requests` to verify the API call returned 2xx
7. `browser_snapshot` to confirm the new item appears in the list

### Network inspection pattern

Use `browser_network_requests` to verify API health:
- All API calls should return 2xx
- Flag any 401 (auth issue), 403 (permissions), 4xx (client error), or 5xx (server error)
- Check after page load and after interactive actions

### Responsive pattern

For mobile viewport testing:
1. `browser_resize` to `375, 667` (iPhone SE)
2. `browser_take_screenshot` for mobile view
3. `browser_resize` to `1280, 720` to restore desktop viewport

## Mermaid Chart — Diagram Validation

Use to validate mermaid diagrams before writing them to documentation files.

**When to use:**
- During `/docs` — validate any mermaid syntax in generated docs
- During `/prd` — validate architecture diagrams

**How:** `mcp__claude_ai_Mermaid_Chart__validate_and_render_mermaid_diagram` with the diagram source
