# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

This is a .NET 9.0 library collection that provides authentication and authorization components for ASP.NET Core applications. The library supports three authentication modes (None, Authenticate, Authorize) with progressive enhancement, YARP reverse proxy integration, and Keycloak-based permission management.

## Project Structure

The repository contains three NuGet packages with dependency hierarchy:

- **affolterNET.Web.Core** - Foundation layer with base classes, interfaces, middleware, and services
- **affolterNET.Web.Api** - API package (depends on Core) for stateless JWT Bearer authentication
- **affolterNET.Web.Bff** - BFF package (depends on Core) for stateful cookie-based auth with YARP reverse proxy

Test projects are in `Tests/`:
- `affolterNET.Web.Core.Test`, `affolterNET.Web.Api.Test`, `affolterNET.Web.Bff.Test`

## Development Commands

### Building and Testing

```bash
# Restore dependencies
dotnet restore

# Build all projects (in dependency order)
dotnet build affolterNET.Web.Core/affolterNET.Web.Core.csproj --configuration Release --no-restore
dotnet build affolterNET.Web.Api/affolterNET.Web.Api.csproj --configuration Release --no-restore
dotnet build affolterNET.Web.Bff/affolterNET.Web.Bff.csproj --configuration Release --no-restore

# Build entire solution
dotnet build affolterNET.Web.sln --configuration Release

# Run tests
dotnet test --configuration Release --no-build --verbosity normal
```

### Package Management

The repository includes helper scripts for managing NuGet packages:

```bash
# Build, test, and pack all packages (auto-increments patch version)
./manage-packages.sh

# Build only
./manage-packages.sh build

# Pack packages only
./manage-packages.sh pack

# Publish to local NuGet source for testing
./manage-packages.sh publish-local
```

### Reference Switching

Use `switch-references.sh` to toggle between local project references (for development) and NuGet package references (for distribution):

```bash
# Switch to local project references for development
./switch-references.sh local

# Switch to NuGet package references
./switch-references.sh nuget

# Show current reference status
./switch-references.sh status

# Validate configuration
./switch-references.sh validate
```

**Important:** The CI/CD pipeline automatically switches to NuGet references before packing to ensure proper package dependencies.

## Architecture

### Options Configuration Pattern

All configuration uses the `IConfigurableOptions<T>` interface with a three-tier pattern:

1. **Create Defaults** - Constructor with `AppSettings` provides sensible defaults
2. **Bind from appsettings.json** - Via `config.CreateFromConfig<T>()` extension
3. **Manual Configuration** - Via lambda actions in service registration
4. **Register with DI** - Via `ConfigureDi()` using `services.Configure<T>()`

Key option classes:
- `AuthProviderOptions` - Keycloak connection settings
- `OidcOptions` - OIDC protocol configuration
- `BffOptions` / `ApiJwtBearerOptions` - Package-specific behavior
- `CookieAuthOptions`, `BffAuthOptions`, `BffAntiforgeryOptions`

### Service Registration Pattern

Both API and BFF follow a two-step pattern:

```csharp
// Step 1: Register services and get configuration
var options = builder.Services.AddBffServices(isDev, builder.Configuration, opts => {
    opts.EnableSecurityHeaders = true;
    opts.ConfigureBff = bff => {
        bff.AuthMode = AuthenticationMode.Authorize;
        bff.EnableSessionManagement = true;
    };
});

// Step 2: Configure middleware pipeline
app.ConfigureBffApp(options);
```

### Authentication Modes

Three progressive modes control authentication behavior:

- **None**: No authentication, anonymous access
- **Authenticate**: Login required but no permission checks
- **Authorize**: Full permission-based authorization with Keycloak RPT tokens

### Permission-Based Authorization

Uses dynamic policy provider pattern:
- Policy names can be comma-separated permissions: `"resource1,resource2"`
- Permissions extracted from Keycloak RPT (Resource Party Token) tokens
- Permission structure: `{rsname: "resource", scopes: ["action1", "action2"]}`
- Claims added as `Claim("permission", "resourceName:action")`

Key services:
- `PermissionAuthorizationPolicyProvider` - Creates policies dynamically at runtime
- `PermissionAuthorizationHandler` - Validates permission requirements
- `RptTokenService` - Exchanges access tokens for RPT tokens
- `RptCacheService` - Caches decoded RPT tokens per user
- `PermissionService` - Extracts permissions from JWT authorization claims

### YARP Reverse Proxy (BFF Only)

- Configuration loaded from `affolterNET:ReverseProxy` section
- `AuthTransform` automatically adds bearer tokens to proxied backend requests
- Extracts access token via `context.GetTokenAsync("access_token")`
- Registered after controllers/razor pages in middleware pipeline

### Claims Enrichment

Both API and BFF implement `IClaimsEnrichmentService` with different strategies:

- **API**: Extracts standard JWT claims, aggregates roles from `ClaimTypes.Role` and `"roles"` claims
- **BFF**: Uses configurable `OidcClaimTypeOptions` for claim name mapping, supports multi-valued claims

Both enrich with permissions via `IPermissionService.GetUserPermissionsAsync()`.

### Middleware Pipeline Order

**BFF Pipeline** (from `ConfigureBffApp`):
1. Exception Handling
2. Security Headers Middleware
3. HTTPS Redirection
4. Static Files
5. Swagger/OpenAPI
6. Routing
7. Custom Middleware (after routing hook)
8. CORS
9. Antiforgery (CSRF protection)
10. Authentication & Authorization
11. Token Refresh Middleware
12. RPT Middleware
13. NoUnauthorizedRedirect Middleware (API routes)
14. Antiforgery Token Middleware
15. Custom Middleware (before endpoints hook)
16. API 404 Handling
17. Endpoint Mapping (Razor Pages, Controllers, YARP, Fallback)

**API Pipeline** (from `ConfigureApiApp`):
1. Security Headers Middleware
2. Swagger/OpenAPI
3. Routing
4. Custom Middleware (after routing hook)
5. CORS
6. Authentication & Authorization + RPT Middleware
7. Custom Middleware (before endpoints hook)
8. Endpoint Mapping (with Health Checks)

### Configuration Section Names

```
affolterNET:Web:Auth:Provider     → AuthProviderOptions (Keycloak settings)
affolterNET:Web:Oidc              → OidcOptions
affolterNET:Web:OidcClaimTypes    → OidcClaimTypeOptions
affolterNET:Web:PermissionCache   → PermissionCacheOptions
affolterNET:Web:SecurityHeaders   → SecurityHeadersOptions
affolterNET:Web:Swagger           → SwaggerOptions
affolterNET:Web:Cors              → AffolterNetCorsOptions
affolterNET:Web:BffOptions        → BffOptions
affolterNET:Web:Auth:BffAuth      → BffAuthOptions
affolterNET:Web:Auth:CookieAuth   → CookieAuthOptions
affolterNET:Web:Auth:AntiForgery  → BffAntiforgeryOptions
affolterNET:Web:Heartbeat         → HeartbeatOptions
affolterNET:ReverseProxy          → YARP configuration
```

## CI/CD Pipeline

The repository uses GitHub Actions with automated version management:

### Workflow Behavior

- **develop branch**: Automatic patch version bump on push (e.g., 0.3.12 → 0.3.13)
- **main branch**: Publishes packages to NuGet.org with the current version
- **Manual trigger**: Workflow dispatch with optional NuGet publishing

### Build Process

1. **build-and-test** job:
   - Extracts current version from `affolterNET.Web.Core.csproj`
   - On develop: auto-increments patch version and commits back
   - Builds all projects with project references (Core → Api → Bff order)
   - Runs tests if test projects exist
   - Determines if publishing should occur

2. **pack-and-publish** job (only on main branch or manual trigger):
   - Builds with project references first
   - Switches to NuGet references using `./switch-references.sh nuget`
   - Packs all packages in dependency order
   - Publishes to NuGet.org (requires `NUGET_API_KEY` secret)
   - Creates and pushes git tag (e.g., `v0.3.13`)

### Important Notes

- The switch to NuGet references before packing ensures proper package dependencies
- Symbol packages (.snupkg) are generated with `--include-symbols --include-source`
- Packages are built with `--no-build` flag after initial compilation

## Key Implementation Details

### Token Refresh (BFF Only)

`RefreshTokenMiddleware` proactively refreshes tokens:
- Checks token expiration before each request
- Refreshes when < 10 seconds until expiration
- Uses semaphore lock to prevent concurrent refreshes
- Signs out user on refresh failure

### Security Features

- **Security Headers**: CSP, HSTS, X-Frame-Options (always enabled in production, disabled in development for Vite compatibility)
- **Secure Cookies (BFF)**: All cookies use strict security settings:
  - `__Host-` prefix (browser-enforced: `Secure=true`, `Path=/`, no `Domain`)
  - `SameSite=Strict` (CSRF protection)
  - `HttpOnly=true` for auth cookies (no JavaScript access)
  - HTTPS required for all environments (use mkcert for local development)
- **Antiforgery (BFF)**: Dual-cookie CSRF protection:
  - `__Host-X-XSRF-TOKEN` (HttpOnly server cookie)
  - `X-XSRF-TOKEN` (client-readable cookie for JavaScript)
  - Header validation on state-changing requests
- **CORS**: Configurable origins, methods, headers, credentials
- **API 404 Handling (BFF)**: Returns JSON for API routes, HTML for SPA routes

### BFF Pattern for SPAs

The BFF implements a specific pattern for single-page applications:
- `OnRedirectToIdentityProvider` event intercepts unauthorized responses
- Returns 401 instead of redirecting to Keycloak login
- SPA handles login UI and explicitly calls `/bff/account/login`
- Prevents unwanted redirects from SPA API calls

### Health Checks

Endpoints:
- `/health` - All checks
- `/health/startup` - Startup checks only
- `/health/ready` - Readiness checks

Includes:
- `StartupHealthCheck` - Verifies application startup
- `KeycloakHealthCheck` - Checks Keycloak availability (if configured)
- Self health check

### Extension Hooks

Both `ApiAppOptions` and `BffAppOptions` provide middleware customization hooks:
- `ConfigureAfterRoutingCustomMiddleware` - Inject middleware after routing
- `ConfigureBeforeEndpointsCustomMiddleware` - Inject middleware before endpoints

## Development Certificates

See `.claude/skills/development-certificates/SKILL.md` for SSL/TLS certificate configuration. Key points:

- Use `mkcert` for locally-trusted development certificates
- ASP.NET Kestrel requires PFX format (convert with `openssl pkcs12`)
- Keycloak uses PEM files directly
- Firefox/Zen may need manual CA import from `$(mkcert -CAROOT)/rootCA.pem`
- Docker: use stop/start (not restart) when updating mounted certificates

## Keycloak Configuration

See `.claude/skills/keycloak-configuration/SKILL.md` for Keycloak identity provider setup. Key points:

- Standard OIDC client scopes must be explicitly defined in JSON realm imports
- Roles mapper must use flat `claim.name: "roles"`, not nested `realm_access.roles`
- Default client scopes should NOT be requested explicitly in OIDC scope parameter
- Authorization services (RPT-based permissions) are auto-configured by `keycloak-init` container via `fix-permissions.sh`
- Docker containers need custom CA installed via entrypoint script for HTTPS communication

## Cypress E2E Tests

See `.claude/skills/cypress-e2e-tests/SKILL.md` for end-to-end testing with Cypress. Key points:

- Tests located in `examples/e2e-tests/` with Cypress 15+
- Custom commands for Keycloak authentication: `cy.login()`, `cy.loginAsAdmin()`, `cy.logout()`
- Uses `cy.origin()` for cross-origin Keycloak interactions
- Three test users: admin, user, viewer with different permission levels
- Run with `npm run cy:open` (interactive) or `npm run test` (headless)
- Requires Docker containers running (Keycloak, API, BFF)

## Development Workflow

See `.claude/skills/development-workflow/SKILL.md` for local development setup. Key points:

- **Development mode**: BFF proxies to Vite dev server for HMR (hot module replacement)
- **Production/Docker mode**: BFF serves pre-built static files from `wwwroot/`
- YARP routes in `appsettings.Development.json` proxy Vite paths (`@vite`, `src`, `assets`, etc.)
- CSP disabled in development (Vite injects inline styles)

## Working with This Codebase

### Adding New Features

1. Determine which package(s) need changes (Core, Api, or Bff)
2. Follow the existing options configuration pattern if adding configuration
3. Use appropriate middleware registration pattern
4. Update version in `.csproj` files if needed (or let CI handle it)
5. Test locally using `./manage-packages.sh publish-local`

### Testing Package Dependencies

1. Switch to local references: `./switch-references.sh local`
2. Make changes across packages
3. Test with consuming applications
4. Switch back to NuGet: `./switch-references.sh nuget` (or let CI handle it)

### Versioning

- CI automatically bumps patch version on develop branch
- Manual version changes: update `<Version>` in all `.csproj` files
- Version format: `MAJOR.MINOR.PATCH` (no prerelease suffixes in production)

### Configuration Serialization

- Sensitive values (e.g., `ClientSecret`) use `SensibleAttribute`
- Logs show only first/last char and length: `"K...[HIDDEN]...y (len=40)"`
- Never commit sensitive configuration values
