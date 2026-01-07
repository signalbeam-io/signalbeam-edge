# Zitadel Local Setup Guide

Quick guide to set up Zitadel for local SignalBeam development.

## Quick Start

### 1. Start Infrastructure Services

```bash
# Start PostgreSQL, Zitadel, NATS, and Valkey
docker-compose -f docker-compose.dev.yml up -d

# Wait for services to start (Zitadel takes ~30 seconds)
docker-compose -f docker-compose.dev.yml logs -f zitadel
```

Once you see "Zitadel is running", press Ctrl+C and continue.

### 2. Access Zitadel Console

Open http://localhost:9080 in your browser.

> **Note**: Zitadel runs on port **9080** to avoid conflicts with the API Gateway (port 8080).

**Default Admin Credentials:**
- Username: `zitadel-admin@zitadel.localhost`
- Password: `Password1!`

### 3. Create Organization (First Time Only)

1. Click "Organizations" in the sidebar
2. Click "Create Organization"
3. Name: `SignalBeam` (or your company name)
4. Click "Create"

### 4. Create Application

1. Navigate to your organization
2. Click "Projects" → "Create New Project"
3. Project Name: `SignalBeam Platform`
4. Click "Continue"
5. Click "New Application"
6. Application Configuration:
   - **Name**: `SignalBeam Web`
   - **Type**: `Web`
   - Click "Continue"
7. Authentication Configuration:
   - **Authentication Method**: `PKCE`
   - Click "Continue"
8. Redirect URIs:
   - Add `http://localhost:5173/callback`
   - Add `http://localhost:3000/callback` (if using alternative port)
   - Click "Continue"
9. Post Logout Redirect URIs:
   - Add `http://localhost:5173`
   - Add `http://localhost:3000`
   - Click "Continue"
10. Click "Create"

### 5. Copy Client ID

After creating the application:

1. You'll see your **Client ID** - copy this!
2. Update your frontend `.env.development`:

```env
VITE_ZITADEL_AUTHORITY=http://localhost:8080
VITE_ZITADEL_CLIENT_ID=<paste-your-client-id-here>
VITE_ZITADEL_REDIRECT_URI=http://localhost:5173/callback
VITE_ZITADEL_POST_LOGOUT_REDIRECT_URI=http://localhost:5173
```

### 6. Test Authentication

1. Start the frontend:
   ```bash
   cd web
   npm run dev
   ```

2. Open http://localhost:5173

3. Click "Sign in with Zitadel"

4. You'll be redirected to Zitadel login

5. Create a new user account or use test credentials

## Creating Test Users

### Option 1: Self-Registration (Recommended)

1. On the login page, click "Register"
2. Fill in your details
3. Verify email (check console logs if using local SMTP)
4. Complete registration

### Option 2: Admin-Created Users

1. In Zitadel Console, go to "Users"
2. Click "Create User"
3. Fill in user details
4. Set initial password
5. Assign to your organization

## Troubleshooting

### Zitadel not starting

**Issue**: Container exits immediately

**Solution**: Check logs
```bash
docker-compose -f docker-compose.dev.yml logs zitadel
```

**Common causes**:
- PostgreSQL not ready (wait 10 seconds and retry)
- Port 8080 already in use (change port in docker-compose.dev.yml)

### Can't access Zitadel Console

**Issue**: http://localhost:9080 shows connection refused

**Solution**:
```bash
# Check if Zitadel is running
docker ps | grep zitadel

# If not running, start it
docker-compose -f docker-compose.dev.yml up -d zitadel

# View startup logs
docker-compose -f docker-compose.dev.yml logs -f zitadel
```

### Authentication redirects to wrong URL

**Issue**: After login, redirected to wrong URL

**Solution**: Verify redirect URIs match exactly in:
1. Zitadel Application settings
2. Frontend `.env.development` file

### "Invalid Client" error

**Issue**: Login fails with "invalid_client"

**Solution**:
1. Verify `VITE_ZITADEL_CLIENT_ID` matches the Client ID in Zitadel Console
2. Ensure PKCE is enabled for the application
3. Clear browser cache and try again

## Advanced Configuration

### Enable Email Verification

1. In Zitadel Console → Settings → SMTP
2. Configure SMTP server (or use Mailhog for testing)
3. Enable email verification

### Custom Branding

1. In Zitadel Console → Settings → Branding
2. Upload logo
3. Customize colors and text

### Enable Multi-Factor Authentication

1. Go to Settings → Login Policy
2. Enable "Multi-Factor Authentication"
3. Configure allowed factors (TOTP, WebAuthn, etc.)

## Production Deployment

For production, **DO NOT** use this docker-compose setup. Instead:

1. Deploy Zitadel to Kubernetes (Helm chart available)
2. Use managed PostgreSQL (Azure, AWS RDS, etc.)
3. Enable TLS/HTTPS
4. Configure proper backup and monitoring
5. Use strong masterkey (not the example one!)

See: https://zitadel.com/docs/self-hosting/deploy/kubernetes

## Stopping Services

```bash
# Stop all services
docker-compose -f docker-compose.dev.yml down

# Stop and remove volumes (clean slate)
docker-compose -f docker-compose.dev.yml down -v
```

## Useful Links

- **Zitadel Console**: http://localhost:9080
- **API Gateway**: http://localhost:8080
- **Zitadel Docs**: https://zitadel.com/docs
- **NATS Monitoring**: http://localhost:8222
- **PostgreSQL**: localhost:5432
- **Valkey**: localhost:6379

## Next Steps

After setting up Zitadel:

1. ✅ Configure frontend `.env.development` with Client ID
2. ✅ Start backend services (Aspire or individual microservices)
3. ✅ Test full authentication flow
4. ✅ Create test user account
5. ✅ Register workspace in SignalBeam
6. ✅ Start managing devices!
