# PRD: Zitadel Auto-Bootstrap for Aspire

> Generated: 2026-02-05
> Author: Claude Code + marjangjuroski
> Status: Draft

## 1. Executive Summary

Automate the complete Zitadel configuration when starting .NET Aspire for the first time. The system should create a Zitadel project, configure web and API applications, and propagate the generated client IDs and secrets to all services (frontend env file, IdentityManager, API Gateway, and all backend microservices) without any manual intervention.

## 2. Problem Statement

### Current State
- Zitadel setup requires **manual PAT token generation** before the auto-configuration can proceed
- Backend services have **hardcoded audience IDs** that don't match dynamically generated API client IDs
- The `.env.zitadel.local` file is generated but **not consumed** by backend services
- API Gateway has a **hardcoded Zitadel address** instead of using Aspire service discovery
- Developers must perform manual steps after `dotnet run` before authentication works

### Business Impact
- **Increased onboarding friction**: New developers spend 15-30 minutes configuring auth manually
- **Error-prone setup**: Manual steps lead to misconfiguration and debugging time
- **Inconsistent environments**: Different developers may have different configurations
- **CI/CD complexity**: Integration tests require manual auth setup or mocking

### User Impact
- Developers cannot run `dotnet run` and immediately have a working authenticated system
- First-time setup requires reading documentation and manual Zitadel UI interaction
- Authentication failures are difficult to debug due to configuration mismatches

## 3. Goals & Success Metrics

### Goals
- **Primary**: Zero-touch Zitadel configuration on first Aspire startup
- **Secondary**: Idempotent restarts that preserve existing configuration
- **Tertiary**: All services automatically receive correct auth credentials

### Success Metrics
| Metric | Current | Target | How to Measure |
|--------|---------|--------|----------------|
| Manual steps required | 4-5 steps | 0 steps | Count steps in setup guide |
| Time to working auth | 15-30 min | < 2 min | Time from `dotnet run` to successful login |
| Configuration errors | Common | Rare | Support tickets / GitHub issues |
| Services with correct audience | 0% | 100% | Automated test verifying JWT validation |

### Non-Goals
- Production Zitadel deployment (this is dev-only)
- Multi-tenant Zitadel configuration
- Zitadel high availability setup
- Custom Zitadel branding/theming

## 4. User Stories

### Developer (First-time Setup)
- As a developer, I want to run `dotnet run` in AppHost and have authentication work immediately so that I can start developing features without manual configuration
  - Acceptance: Running AppHost with empty Zitadel creates project, apps, and configures all services

### Developer (Returning)
- As a developer, I want Aspire restarts to preserve my existing Zitadel configuration so that I don't lose test users or have to reconfigure
  - Acceptance: Restarting AppHost with existing Zitadel project skips creation and only updates env files if needed

### Developer (Debugging Auth)
- As a developer, I want clear console output showing what Zitadel configuration was applied so that I can debug authentication issues
  - Acceptance: Console shows project ID, client IDs, and which files were updated

## 5. Functional Requirements

| ID | Requirement | Priority | Acceptance Criteria |
|----|-------------|----------|---------------------|
| FR-1 | Auto-generate machine user PAT on first startup | Must | No manual PAT creation needed; setup completes without user interaction |
| FR-2 | Create "SignalBeam Edge" project in Zitadel | Must | Project exists after first startup with correct name |
| FR-3 | Create "SignalBeam Web" OIDC application | Must | Web app configured with PKCE, correct redirect URIs |
| FR-4 | Create "SignalBeam API" application with client credentials | Must | API app exists with client ID and secret generated |
| FR-5 | Update `web/.env.development` with web client ID | Must | `VITE_ZITADEL_CLIENT_ID` contains correct value |
| FR-6 | Pass API client ID as JWT audience to all backend services | Must | All services validate JWT with correct audience |
| FR-7 | Pass API client secret to IdentityManager (and Gateway if needed) | Must | Services can call Zitadel APIs with credentials |
| FR-8 | Skip creation if project/apps already exist | Must | Idempotent; no duplicate projects or apps on restart |
| FR-9 | Update env files with existing IDs on restart | Should | Env files stay in sync even if manually edited |
| FR-10 | Log configuration details to console | Should | Developer can see what was configured |
| FR-11 | Validate configuration after setup | Could | Health check confirms auth endpoints work |

## 6. Technical Considerations

### Affected Services
- [x] SignalBeam.AppHost — Pass generated credentials to services via environment variables
- [x] SignalBeam.ZitadelSetup — Implement machine user PAT generation, remove manual PAT requirement
- [x] SignalBeam.ApiGateway — Receive API client credentials, use dynamic Zitadel address
- [x] SignalBeam.IdentityManager — Receive API client credentials for Zitadel API calls
- [x] SignalBeam.DeviceManager — Receive correct JWT audience
- [x] BundleOrchestrator — Receive correct JWT audience
- [x] TelemetryProcessor — Receive correct JWT audience
- [x] web (frontend) — Receive web client ID in `.env.development`

### Data Model Changes
None — this is infrastructure/configuration only.

### API Changes
None — existing Zitadel Management API is used.

### Configuration Changes

**New Environment Variables (set by Aspire):**
| Variable | Target Services | Description |
|----------|-----------------|-------------|
| `ZITADEL_API_CLIENT_ID` | All backend services | JWT audience for token validation |
| `ZITADEL_API_CLIENT_SECRET` | IdentityManager, Gateway | Client secret for Zitadel API calls |
| `ZITADEL_WEB_CLIENT_ID` | ZitadelSetup (for env file) | Web app client ID |

**Modified appsettings.json:**
- Remove hardcoded `Authentication:Jwt:Audience` values
- Use `${ZITADEL_API_CLIENT_ID}` or Aspire binding

### External Integrations
- **Zitadel Management API v1**: Project and application CRUD
- **Zitadel Auth API**: Machine user authentication for PAT generation

### Zitadel Machine User Bootstrap Approach

```
1. Zitadel starts with init config that creates:
   - Admin user (existing)
   - Machine user "signalbeam-setup" with client credentials

2. ZitadelSetup service:
   a. Authenticates as machine user via client credentials grant
   b. Gets access token (no manual PAT needed)
   c. Uses access token for Management API calls
   d. Creates project and applications
   e. Writes configuration to env files and Aspire
```

**Zitadel Init Config Addition:**
```yaml
ZITADEL_FIRSTINSTANCE_ORG_MACHINE_USERS:
  - UserName: signalbeam-setup
    ClientId: signalbeam-setup-client
    ClientSecret: ${SETUP_CLIENT_SECRET}  # Generated or fixed for dev
```

### Performance Considerations
- Setup runs once on first startup (< 30 seconds)
- Idempotent checks add < 5 seconds on subsequent startups
- No runtime performance impact

### Security Considerations
- Machine user credentials are dev-only, not for production
- `.env.zitadel.local` must remain in `.gitignore`
- Client secrets should not be logged
- Production should use managed identity or external secret management

## 7. Dependencies

### Upstream
- Zitadel v2.66+ (already in use)
- .NET Aspire (already in use)

### Downstream
- All authentication flows depend on this working correctly
- Integration tests may depend on predictable client IDs

### External
- Zitadel Docker image: `ghcr.io/zitadel/zitadel:v2.66.3`

## 8. Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Zitadel API changes break setup | Low | High | Pin Zitadel version, add integration test |
| Machine user approach not supported | Low | High | Fallback: use admin OAuth login flow |
| Race condition on parallel startup | Medium | Medium | Add retry logic with backoff |
| Env file write fails (permissions) | Low | Medium | Clear error message, manual fallback instructions |
| Secrets accidentally committed | Medium | High | Pre-commit hook checking for secrets |

## 9. Out of Scope

- **Production Zitadel setup** — This is dev/local only; production uses external IdP or managed Zitadel
- **Zitadel UI customization** — Branding, themes, login page customization
- **Multi-organization setup** — Single org "ZITADEL" for dev
- **User provisioning** — Creating test users beyond admin
- **Role/permission configuration** — RBAC setup in Zitadel
- **Certificate/mTLS setup** — TLS disabled for local dev

## 10. Open Questions

- [x] ~~How to bootstrap without manual PAT?~~ → Machine user with client credentials
- [x] ~~Which services need API client secret?~~ → All backend services for JWT audience, IdentityManager + Gateway for API calls
- [ ] Should we support a "reset" flag to force recreation? (Deferred to future)
- [ ] Should client IDs be deterministic for test stability? (Nice to have)

## 11. Task Breakdown Hints

### Domain Layer
- None (infrastructure only)

### Application Layer
- None (infrastructure only)

### Infrastructure Layer
- Modify `SignalBeam.ZitadelSetup/Program.cs`:
  - Remove PAT requirement
  - Add machine user client credentials auth
  - Add retry logic for Zitadel startup race condition

### AppHost (Aspire Orchestration)
- Modify `SignalBeam.AppHost/Program.cs`:
  - Add Zitadel machine user environment variables
  - Create mechanism to pass generated client IDs to services
  - Use Aspire resource dependencies to ensure setup completes before services start

### Service Configuration
- Modify all Host `Program.cs` files:
  - Read `ZITADEL_API_CLIENT_ID` for JWT audience
  - Remove hardcoded audience values
- Modify IdentityManager and Gateway:
  - Read `ZITADEL_API_CLIENT_SECRET` for Zitadel API calls

### Frontend
- No code changes (env file updated by setup)

### Tests
- Add integration test: Start Aspire, verify auth works end-to-end
- Add unit test: ZitadelSetup idempotency (mock Zitadel API)

---

## Appendix: Codebase Analysis

### Current Zitadel Setup Flow

The existing `SignalBeam.ZitadelSetup` service (360 lines) handles:
1. Health check polling (60 attempts, 2s intervals)
2. PAT-based authentication (**requires manual step**)
3. Idempotent project creation ("SignalBeam Edge")
4. Web app creation (OIDC with PKCE)
5. API app creation (client credentials)
6. Env file updates (`web/.env.development`, `.env.zitadel.local`)

### Key Files

| File | Lines | Purpose |
|------|-------|---------|
| `src/SignalBeam.AppHost/Program.cs` | 27-114 | Aspire orchestration, Zitadel container setup |
| `src/SignalBeam.ZitadelSetup/Program.cs` | 1-360 | Auto-configuration service |
| `src/SignalBeam.ApiGateway/appsettings.json` | 154 | Hardcoded Zitadel address (needs fix) |
| `web/.env.development` | - | Frontend Zitadel config |

### Hardcoded Values to Replace

| Location | Current Value | Should Be |
|----------|---------------|-----------|
| DeviceManager appsettings | `354924471519401733` | `${ZITADEL_API_CLIENT_ID}` |
| BundleOrchestrator appsettings | `354924471519401733` | `${ZITADEL_API_CLIENT_ID}` |
| TelemetryProcessor appsettings | `354924471519401733` | `${ZITADEL_API_CLIENT_ID}` |
| IdentityManager appsettings | `354924471519401733` | `${ZITADEL_API_CLIENT_ID}` |
| ApiGateway Zitadel cluster | `http://localhost:9080` | Aspire service discovery |

---

## PRD Validation

- Completeness: **PASS** — All sections filled
- Measurability: **PASS** — Success metrics have numbers
- Testability: **PASS** — Acceptance criteria are specific
- Language: **PASS** — No vague terms without metrics
- Scope: **PASS** — Out of scope clearly defined

**Overall: PASS**

---

## Next Steps

1. Review and refine the PRD with stakeholders
2. Resolve open questions (reset flag, deterministic IDs)
3. Run `/create-issue` to track in GitHub
4. Run `/start-work {issue-number}` to create feature branch

---

# Implementation Plan

## Affected Services

- [x] SignalBeam.AppHost — Orchestrate machine user config, pass credentials to services
- [x] SignalBeam.ZitadelSetup — Replace PAT auth with client credentials flow
- [x] SignalBeam.ApiGateway — Receive API client credentials
- [x] SignalBeam.IdentityManager — Receive API client credentials
- [x] SignalBeam.DeviceManager — Receive dynamic JWT audience
- [x] BundleOrchestrator — Receive dynamic JWT audience
- [x] TelemetryProcessor — Receive dynamic JWT audience
- [ ] SignalBeam.EdgeAgent — No changes needed
- [ ] SignalBeam.Domain (shared) — No changes needed
- [ ] web (frontend) — No code changes (env file updated by setup)

## Acceptance Criteria

- [ ] AC1: Running `dotnet run` in AppHost with empty Zitadel completes setup without manual steps
- [ ] AC2: "SignalBeam Edge" project is created in Zitadel with correct name
- [ ] AC3: "SignalBeam Web" OIDC app is created with PKCE and correct redirect URIs
- [ ] AC4: "SignalBeam API" app is created with client credentials
- [ ] AC5: `web/.env.development` is updated with `VITE_ZITADEL_CLIENT_ID`
- [ ] AC6: All backend services receive correct `Authentication__Jwt__Audience` from generated API client ID
- [ ] AC7: IdentityManager receives `ZITADEL_API_CLIENT_SECRET` for API calls
- [ ] AC8: Restarting Aspire skips creation if project/apps exist (idempotent)
- [ ] AC9: Console output shows project ID, client IDs, and which files were updated

## Implementation Tasks

### Infrastructure (Zitadel Configuration)

- [ ] **Add machine user to Zitadel init config** — `src/SignalBeam.AppHost/Program.cs`
  - Add environment variables for machine user creation on Zitadel init
  - Configure machine user `signalbeam-setup` with client ID and secret
  - Use fixed dev credentials (not for production)

### ZitadelSetup Service

- [ ] **Remove PAT authentication requirement** — `src/SignalBeam.ZitadelSetup/Program.cs`
  - Remove lines 52-67 (PAT file reading and validation)
  - Remove `ZITADEL_PAT` and `ZITADEL_PAT_FILE` environment variable handling

- [ ] **Add client credentials authentication** — `src/SignalBeam.ZitadelSetup/Program.cs`
  - Add new method `AuthenticateWithClientCredentials()`
  - Read `ZITADEL_SETUP_CLIENT_ID` and `ZITADEL_SETUP_CLIENT_SECRET` from environment
  - POST to `/oauth/v2/token` with `grant_type=client_credentials`
  - Extract access token from response
  - Set `Authorization: Bearer {token}` header

- [ ] **Add retry logic with exponential backoff** — `src/SignalBeam.ZitadelSetup/Program.cs`
  - Wrap API calls in retry policy (3 attempts, 1s/2s/4s delays)
  - Handle transient errors during Zitadel startup race condition

- [ ] **Add JSON records for OAuth token response** — `src/SignalBeam.ZitadelSetup/Program.cs`
  - Add `TokenResponse` record with `access_token`, `token_type`, `expires_in`

### AppHost (Aspire Orchestration)

- [ ] **Generate setup client secret parameter** — `src/SignalBeam.AppHost/Program.cs`
  - Add `builder.AddParameter("zitadel-setup-secret", secret: true)` or use fixed dev secret

- [ ] **Configure Zitadel machine user via env vars** — `src/SignalBeam.AppHost/Program.cs`
  - Add machine user environment variables to Zitadel container

- [ ] **Pass setup credentials to ZitadelSetup service** — `src/SignalBeam.AppHost/Program.cs`
  - Add `.WithEnvironment("ZITADEL_SETUP_CLIENT_ID", "...")`
  - Add `.WithEnvironment("ZITADEL_SETUP_CLIENT_SECRET", "...")`

- [ ] **Update service references to use dynamic audience** — `src/SignalBeam.AppHost/Program.cs`
  - Replace hardcoded `"354924471519401733"` with config file reference
  - Read from `zitadelConfigPath` JSON file after setup
  - Add `.WaitFor(zitadelSetup)` to all services needing auth config

- [ ] **Pass API client credentials to services** — `src/SignalBeam.AppHost/Program.cs`
  - Add Zitadel client ID/secret environment variables to IdentityManager
  - Add to ApiGateway if needed for token introspection

### Backend Services Configuration

- [ ] **Remove hardcoded audience from DeviceManager** — `src/DeviceManager/SignalBeam.DeviceManager.Host/appsettings.json`
  - Remove or comment out `Authentication:Jwt:Audience`
  - Rely on environment variable from Aspire

- [ ] **Remove hardcoded audience from BundleOrchestrator** — `src/BundleOrchestrator/SignalBeam.BundleOrchestrator.Host/appsettings.json`
  - Remove or comment out `Authentication:Jwt:Audience`

- [ ] **Remove hardcoded audience from TelemetryProcessor** — `src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Host/appsettings.json`
  - Remove or comment out `Authentication:Jwt:Audience`

- [ ] **Remove hardcoded audience from IdentityManager** — `src/IdentityManager/SignalBeam.IdentityManager.Host/appsettings.json`
  - Remove or comment out `Authentication:Jwt:Audience`

- [ ] **Add Zitadel API client config to IdentityManager** — `src/IdentityManager/SignalBeam.IdentityManager.Host/appsettings.json`
  - Add `Zitadel` section with `Authority`, `ClientId`, `ClientSecret` placeholders

### Tests

- [ ] **Add ZitadelSetup unit tests** — `tests/SignalBeam.ZitadelSetup.Tests/`
  - Test client credentials authentication flow (mock HTTP)
  - Test idempotency (existing project/app detection)
  - Test env file writing

- [ ] **Add AppHost integration test** — `tests/SignalBeam.AppHost.Tests/`
  - Test that starting Aspire creates Zitadel config
  - Verify generated client IDs are passed to services

## Technical Details

### Machine User Authentication Flow

```
1. Zitadel starts with init config:
   - Creates admin user (existing)
   - Creates machine user "signalbeam-setup" with client credentials

2. ZitadelSetup service:
   POST /oauth/v2/token
   Content-Type: application/x-www-form-urlencoded

   grant_type=client_credentials
   &client_id=signalbeam-setup-client
   &client_secret={secret}
   &scope=openid urn:zitadel:iam:org:project:id:zitadel:aud

3. Response:
   {
     "access_token": "...",
     "token_type": "Bearer",
     "expires_in": 3600
   }

4. Use access_token for Management API calls
```

### Credential Propagation Strategy

**File-based approach (extending current implementation):**
```
ZitadelSetup writes → /tmp/signalbeam-zitadel-config.json
AppHost reads → passes to services via WithEnvironment
Services → .WaitFor(zitadelSetup) ensures config exists
```

## Estimated Effort

| Task Category | Files | Complexity |
|---------------|-------|------------|
| ZitadelSetup changes | 1 | Medium |
| AppHost changes | 1 | Medium |
| Service config removal | 4 | Low |
| Tests | 2 | Medium |
| **Total** | **8 files** | **Medium** |
