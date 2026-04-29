# affolterNET.Web.Api Library Guide

## Overview

affolterNET.Web.Api provides JWT Bearer authentication components for ASP.NET Core APIs with Keycloak integration. It supports three authentication modes with progressive enhancement and permission-based authorization.

**NuGet Package:** `affolterNET.Web.Api`

**Dependencies:**
- `affolterNET.Web.Core` (included automatically)
- `Microsoft.AspNetCore.Authentication.JwtBearer`

## Quick Start

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Step 1: Register services
var options = builder.Services.AddApiServices(
    builder.Environment.IsDevelopment(),
    builder.Configuration,
    opts => {
        opts.EnableSecurityHeaders = true;
        opts.ConfigureApi = api => {
            api.AuthMode = AuthenticationMode.Authorize;
        };
    });

var app = builder.Build();

// Step 2: Configure middleware
app.ConfigureApiApp(options);

app.Run();
```

## Authentication Modes

Three progressive modes control authentication behavior:

| Mode | Description |
|------|-------------|
| `None` | No authentication, anonymous access |
| `Authenticate` | Login required but no permission checks |
| `Authorize` | Full permission-based authorization with Keycloak RPT tokens |

## Service Registration Pattern

All configuration uses the `IConfigurableOptions<T>` interface with a three-tier pattern:

1. **Create Defaults** - Constructor provides sensible defaults
2. **Bind from appsettings.json** - Via `config.CreateFromConfig<T>()`
3. **Manual Configuration** - Via lambda actions in service registration
4. **Register with DI** - Via `ConfigureDi()` using `services.Configure<T>()`

## Configuration Sections

```
affolterNET:Web:Auth:Provider     → AuthProviderOptions (Keycloak settings)
affolterNET:Web:Oidc              → OidcOptions
affolterNET:Web:OidcClaimTypes    → OidcClaimTypeOptions
affolterNET:Web:PermissionCache   → PermissionCacheOptions
affolterNET:Web:SecurityHeaders   → SecurityHeadersOptions
affolterNET:Web:Swagger           → SwaggerOptions
affolterNET:Web:Cors              → AffolterNetCorsOptions
affolterNET:Web:Heartbeat         → HeartbeatOptions (liveness log line — see heartbeat skill)
```

## Middleware Pipeline Order

The `ConfigureApiApp` method configures middleware in this order:

1. Security Headers Middleware
2. Swagger/OpenAPI
3. Routing
4. Custom Middleware (after routing hook)
5. CORS
6. Authentication & Authorization + RPT Middleware
7. Custom Middleware (before endpoints hook)
8. Endpoint Mapping (with Health Checks)

## appsettings.json Example

```json
{
  "affolterNET": {
    "Web": {
      "Auth": {
        "Provider": {
          "Authority": "https://keycloak.example.com/realms/myrealm",
          "ClientId": "my-api-client",
          "ClientSecret": "your-secret"
        }
      },
      "SecurityHeaders": {
        "EnableHsts": true,
        "EnableXFrameOptions": true
      },
      "Cors": {
        "AllowedOrigins": ["https://myapp.example.com"],
        "AllowCredentials": true
      },
      "Swagger": {
        "Enabled": true,
        "Title": "My API",
        "Version": "v1"
      }
    }
  }
}
```

## Permission-Based Authorization

Uses dynamic policy provider pattern with Keycloak RPT tokens.

### Using RequirePermission Attribute (Recommended)

```csharp
[RequirePermission("admin-resource:view")]
[HttpGet("admin")]
public IActionResult AdminEndpoint() { ... }

// Multiple permissions (any match)
[RequirePermission("admin-resource:manage", "user-resource:delete")]
[HttpGet("multi")]
public IActionResult MultiPermissionEndpoint() { ... }
```

### Using Authorize Policy (Alternative)

```csharp
[Authorize(Policy = "admin-resource:view")]
[HttpGet("admin")]
public IActionResult AdminEndpoint() { ... }

// Multiple permissions (comma-separated)
[Authorize(Policy = "admin-resource:view,user-resource:read")]
[HttpGet("multi")]
public IActionResult MultiPermissionEndpoint() { ... }
```

### Permission Claims Format

Permissions extracted from Keycloak RPT tokens are added as claims:
- Claim type: `"permission"`
- Claim value: `"{resource}:{scope}"` (e.g., `"admin-resource:view"`)

## Health Check Endpoints

Built-in health checks are available at:
- `/health` - All checks
- `/health/startup` - Startup checks only
- `/health/ready` - Readiness checks

Includes:
- `StartupHealthCheck` - Verifies application startup
- `KeycloakHealthCheck` - Checks Keycloak availability (if configured)

## Extension Hooks

`ApiAppOptions` provides middleware customization:

```csharp
var options = builder.Services.AddApiServices(isDev, config, opts => {
    opts.ConfigureAfterRoutingCustomMiddleware = app => {
        app.UseMiddleware<MyCustomMiddleware>();
    };
    opts.ConfigureBeforeEndpointsCustomMiddleware = app => {
        app.UseMiddleware<AnotherMiddleware>();
    };
});
```
