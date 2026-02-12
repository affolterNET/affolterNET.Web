# affolterNET.Web.Bff Library Guide

## Overview

affolterNET.Web.Bff provides Backend-for-Frontend authentication components for ASP.NET Core applications with:
- Cookie-based OIDC authentication with Keycloak
- YARP reverse proxy for backend API calls
- SPA (Single Page Application) integration
- Token refresh and RPT-based permissions

**NuGet Package:** `affolterNET.Web.Bff`

**Dependencies:**
- `affolterNET.Web.Core` (included automatically)
- `Microsoft.AspNetCore.Authentication.OpenIdConnect`
- `Yarp.ReverseProxy`

## Quick Start

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Step 1: Register services
var options = builder.Services.AddBffServices(
    builder.Environment.IsDevelopment(),
    builder.Configuration,
    opts => {
        opts.EnableSecurityHeaders = true;
        opts.ConfigureBff = bff => {
            bff.AuthMode = AuthenticationMode.Authorize;
            bff.EnableSessionManagement = true;
        };
    });

var app = builder.Build();

// Step 2: Configure middleware
app.ConfigureBffApp(options);

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
affolterNET:Web:BffOptions        → BffOptions
affolterNET:Web:Auth:BffAuth      → BffAuthOptions
affolterNET:Web:Auth:CookieAuth   → CookieAuthOptions
affolterNET:Web:Auth:AntiForgery  → BffAntiforgeryOptions
affolterNET:ReverseProxy          → YARP configuration
```

## Middleware Pipeline Order

The `ConfigureBffApp` method configures middleware in this order:

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

## YARP Reverse Proxy Configuration

Configure YARP to proxy requests to your backend API:

```json
{
  "affolterNET": {
    "ReverseProxy": {
      "Routes": {
        "api-route": {
          "ClusterId": "api-cluster",
          "Match": {
            "Path": "/api/{**catch-all}"
          }
        }
      },
      "Clusters": {
        "api-cluster": {
          "Destinations": {
            "api": {
              "Address": "https://api.example.com"
            }
          }
        }
      }
    }
  }
}
```

The `AuthTransform` automatically adds bearer tokens to proxied backend requests.

## SPA Integration

The BFF implements a specific pattern for single-page applications:

1. **No Redirect on 401**: `OnRedirectToIdentityProvider` returns 401 instead of redirecting
2. **Explicit Login**: SPA calls `/bff/account/login` to initiate login
3. **Logout Endpoint**: `/bff/account/logout` for logging out

```typescript
// SPA login handling
if (response.status === 401) {
    window.location.href = '/bff/account/login?returnUrl=' + encodeURIComponent(window.location.pathname);
}
```

## appsettings.json Example

```json
{
  "affolterNET": {
    "Web": {
      "Auth": {
        "Provider": {
          "Authority": "https://keycloak.example.com/realms/myrealm",
          "ClientId": "my-bff-client",
          "ClientSecret": "your-secret"
        },
        "CookieAuth": {
          "CookieName": ".MyApp.Auth",
          "ExpireTimeSpan": "01:00:00"
        },
        "AntiForgery": {
          "HeaderName": "X-XSRF-TOKEN",
          "CookieName": ".MyApp.Antiforgery"
        }
      },
      "BffOptions": {
        "AuthMode": "Authorize",
        "EnableSessionManagement": true
      },
      "SecurityHeaders": {
        "EnableHsts": true,
        "CustomCspDirectives": {
          "script-src": "'self'"
        }
      },
      "Cors": {
        "AllowedOrigins": ["https://myapp.example.com"],
        "AllowCredentials": true
      }
    },
    "ReverseProxy": {
      "Routes": {
        "api": {
          "ClusterId": "api",
          "Match": { "Path": "/api/{**catch-all}" }
        }
      },
      "Clusters": {
        "api": {
          "Destinations": {
            "default": { "Address": "https://api.example.com" }
          }
        }
      }
    }
  }
}
```

## Token Refresh

`RefreshTokenMiddleware` proactively refreshes tokens:
- Checks token expiration before each request
- Refreshes when < 10 seconds until expiration
- Uses semaphore lock to prevent concurrent refreshes
- Signs out user on refresh failure

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

## Antiforgery (CSRF Protection)

CSRF protection is automatically enabled for state-changing requests:

```typescript
// Include antiforgery token in requests
const token = document.querySelector('meta[name="csrf-token"]')?.getAttribute('content');
fetch('/api/data', {
    method: 'POST',
    headers: {
        'X-XSRF-TOKEN': token
    }
});
```

## Extension Hooks

`BffAppOptions` provides middleware customization:

```csharp
var options = builder.Services.AddBffServices(isDev, config, opts => {
    opts.ConfigureAfterRoutingCustomMiddleware = app => {
        app.UseMiddleware<MyCustomMiddleware>();
    };
    opts.ConfigureBeforeEndpointsCustomMiddleware = app => {
        app.UseMiddleware<AnotherMiddleware>();
    };
});
```

## Health Check Endpoints

Built-in health checks are available at:
- `/health` - All checks
- `/health/startup` - Startup checks only
- `/health/ready` - Readiness checks
