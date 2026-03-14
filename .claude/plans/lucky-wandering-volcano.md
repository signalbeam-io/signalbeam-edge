# Feature: Expand Playwright MCP for Autonomous Browser Testing

## Context

Playwright MCP is wired up but underused — only basic navigate-and-check-console in `/smoke-test` and an optional dashboard check in `/run-local`. The interactive tools (click, fill_form, evaluate, network_requests) and patterns like auth login, CRUD verification, responsive testing, and screenshot capture are unused. This plan adds practical browser automation across the development workflow so Claude Code can autonomously verify frontend changes work.

## Affected Services
- [x] web (frontend) — routes, auth flow
- [x] `.claude/` (skills, rules) — all changes are here

## Acceptance Criteria
- [x] AC1: `/smoke-test` logs in via API key and tests interactive CRUD flows when `--interactive` is passed
- [x] AC2: `/verify-feature` auto-detects changed routes, navigates, screenshots, checks console/network
- [x] AC3: `/screenshot` captures a URL with one command, supports `--mobile` and `--no-auth`
- [x] AC4: `/complete-task` runs browser verification when `web/` files changed and frontend is running
- [x] AC5: `mcp-tools.md` documents auth login, interactive testing, network inspection, and responsive patterns

## Implementation Tasks

### Task 1: Update `mcp-tools.md` — Playwright patterns (foundation)

**File:** `.claude/rules/mcp-tools.md`

Replace the current short Playwright section with expanded patterns:

- **Basic pattern** (keep existing 4-step)
- **Auth login pattern** — two methods:
  1. UI flow: navigate `/login` → `browser_fill_form` apiKey `dev-api-key-1` → click Continue → wait for redirect
  2. Fast injection: `browser_evaluate` to set `localStorage['auth-storage']` with Zustand auth state, then navigate
- **Interactive CRUD pattern** — snapshot to find elements → click trigger → wait for dialog → fill form → submit → check network → snapshot result
- **Network inspection pattern** — `browser_network_requests` to verify 2xx, catch 401/500
- **Responsive pattern** — `browser_resize(375, 667)` → screenshot → resize back

### Task 2: New `/screenshot` skill (simplest, build confidence)

**File (new):** `.claude/skills/screenshot/SKILL.md`

Dead simple skill — take a screenshot, nothing else.

- **Args:** `{url-or-route}` (default `/dashboard`), `--mobile`, `--no-auth`
- **Allowed tools:** `browser_navigate`, `browser_wait_for`, `browser_take_screenshot`, `browser_evaluate`, `browser_close`, `browser_resize`
- **Process:**
  1. Resolve URL — if starts with `/`, prepend `http://localhost:5173`
  2. Auth via localStorage injection (unless `--no-auth` or not localhost:5173)
  3. Resize if `--mobile` (375x667)
  4. Navigate, wait for network idle
  5. Screenshot
  6. Close
- **Output:** Just the screenshot. No tables, no analysis.

### Task 3: Enhance `/smoke-test` — auth + interactive CRUD

**File:** `.claude/skills/smoke-test/SKILL.md`

Changes:
- Add `browser_click`, `browser_fill_form`, `browser_evaluate` to `allowed-tools`
- Add auth login step (Step 2.5) before route testing — use API key form flow
- Add `--interactive` argument
- Add Step 3.5 "Interactive CRUD Flows" (only when `--interactive`):
  - **Create Bundle flow:** navigate `/bundles` → click "Create Bundle" → fill form (name: `smoke-test-{timestamp}`) → submit → verify via `browser_network_requests` (POST `/api/bundles` returns 2xx) → screenshot → verify bundle appears in list
  - **Device detail flow:** navigate `/devices` → if rows exist, click first row → verify detail page loads with expected tabs
- Update output format to include Interactive Flows section

### Task 4: New `/verify-feature` skill

**File (new):** `.claude/skills/verify-feature/SKILL.md`

Post-implementation browser verification for frontend features.

- **Args:** `{route}` (optional, auto-detect from git changes), `--responsive`, `--interactive`
- **Allowed tools:** Bash, Read, Glob, Grep + Playwright tools (navigate, snapshot, screenshot, console_messages, wait_for, network_requests, click, fill_form, evaluate, close, resize)
- **Process:**
  1. Auto-detect route if not provided: `git diff --name-only HEAD~3` → find changed page files in `web/src/features/*/pages/` → match to routes in `web/src/routes/index.tsx`
  2. Pre-flight: `curl -sf http://localhost:5173`
  3. Auth login via localStorage injection
  4. Navigate to route, wait for content
  5. Snapshot — verify meaningful content (not loading spinner or error boundary)
  6. Console check — flag errors
  7. Network check — flag 4xx/5xx
  8. Screenshot (always)
  9. If `--responsive`: resize to 375x667, screenshot, resize back
  10. If `--interactive`: snapshot for clickable elements, click first Create/Add button, verify dialog appears, press Escape to close
  11. Close browser
- **Output:** table with Pass/Fail per check + screenshots

Also update `.claude/skills/add-feature/SKILL.md` output section to suggest `/verify-feature {route}` instead of `/smoke-test`.

### Task 5: Integrate into `/complete-task` — Phase 2.5

**File:** `.claude/skills/complete-task/SKILL.md`

Add **Phase 2.5: Browser Verification** between Phase 2 (Tests) and Phase 3 (Quality Review).

- **Conditional:** only runs when `git diff --name-only origin/main...HEAD | grep -c "^web/"` > 0
- **Non-blocking:** if frontend isn't running (`curl -sf http://localhost:5173` fails), skip with advisory note
- **Implementation:** invoke `/smoke-test --frontend-only` (reuses existing skill with its Playwright tools — no need to add Playwright tools to complete-task's allowed-tools)
- **Report:** include results in output as advisory (WARNING, not FAIL)
- Update state machine diagram to show the optional browser verification step

## Sequencing

```
Task 1 (mcp-tools.md patterns) — foundation, all other tasks reference these
  ↓
Task 2 (/screenshot) — simplest new skill
  ↓
Task 3 (enhance /smoke-test) — adds auth + interactive
  ↓
Task 4 (/verify-feature) — new skill, references patterns from Task 1
  ↓
Task 5 (/complete-task integration) — depends on enhanced smoke-test from Task 3
```

## Verification

1. Run `/screenshot /login --no-auth` — should capture login page
2. Run `/screenshot /devices` — should auth and capture devices page
3. Run `/screenshot /bundles --mobile` — should capture mobile viewport
4. Run `/smoke-test` — should auth and test all routes
5. Run `/smoke-test --interactive` — should create a bundle and verify CRUD
6. Run `/verify-feature /bundles` — should run full verification suite
7. Modify a file in `web/` and run `/complete-task` — should trigger Phase 2.5

## Out of Scope

- Committed Playwright E2E test suite (this is about Claude Code skills, not CI tests)
- Visual regression diffing between screenshots
- PDF generation
- Multi-browser testing
- Scraping external sites
