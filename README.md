# affolterNET.Web - Authentication & Authorization Libraries

[![Build and Publish NuGet Packages](https://github.com/affolterNET/affolterNET.Web/actions/workflows/nuget-packages.yml/badge.svg)](https://github.com/affolterNET/affolterNET.Web/actions/workflows/nuget-packages.yml)

This library collection provides flexible authentication and authorization modes for ASP.NET Core applications with YARP reverse proxy integration.

## NuGet Packages

| Package | NuGet | Description |
|---------|-------|-------------|
| **affolterNET.Web.Core** | [![NuGet](https://img.shields.io/nuget/v/affolterNET.Web.Core.svg)](https://www.nuget.org/packages/affolterNET.Web.Core/) | Core authentication and authorization components |
| **affolterNET.Web.Api** | [![NuGet](https://img.shields.io/nuget/v/affolterNET.Web.Api.svg)](https://www.nuget.org/packages/affolterNET.Web.Api/) | API authentication components |
| **affolterNET.Web.Bff** | [![NuGet](https://img.shields.io/nuget/v/affolterNET.Web.Bff.svg)](https://www.nuget.org/packages/affolterNET.Web.Bff/) | BFF authentication with YARP reverse proxy integration |
| **affolterNET.Web.Mcp** | [![NuGet](https://img.shields.io/nuget/v/affolterNET.Web.Mcp.svg)](https://www.nuget.org/packages/affolterNET.Web.Mcp/) | OAuth 2.1 protection for MCP endpoints |

### Installation

```bash
# Core package (required)
dotnet add package affolterNET.Web.Core

# For API authentication
dotnet add package affolterNET.Web.Api

# For BFF (Backend-for-Frontend) with YARP
dotnet add package affolterNET.Web.Bff

# For an OAuth-protected MCP endpoint
dotnet add package affolterNET.Web.Mcp
```

## Claude Code Integration

This library includes Claude Code plugins for AI-assisted configuration. The plugins provide contextual guidance for service registration, authentication setup, YARP configuration, and more.

### Plugin Installation

```bash
# Add the marketplace
/plugin marketplace add https://github.com/affolterNET/affolterNET.Web

# Install for BFF applications
/plugin install affolternet-web-bff@affolterNET.Web

# Install for API applications
/plugin install affolternet-web-api@affolterNET.Web
```

### Available Skills

**BFF Plugin:**
- `bff-setup` - Service registration and middleware pipeline
- `keycloak-auth` - Cookie-based OIDC authentication
- `yarp-proxy` - YARP reverse proxy configuration
- `spa-integration` - SPA fallback and 401 handling
- `rpt-permissions` - Permission-based authorization
- `security` - Headers, CORS, and antiforgery
- `swagger` - OpenAPI documentation
- `customization` - Middleware extension hooks

**API Plugin:**
- `api-setup` - Service registration and middleware
- `jwt-auth` - JWT Bearer authentication
- `security` - Headers and CORS
- `swagger` - OpenAPI documentation
- `health-checks` - Health check endpoints

## Development

### Building Packages Locally

Use the provided script to build and test packages locally:

```bash
# Build, test, and pack version 1.0.0
./manage-packages.sh 1.0.0

# Only build
./manage-packages.sh 1.0.0 build

# Only pack packages
./manage-packages.sh 1.0.0 pack

# Publish to local NuGet source for testing
./manage-packages.sh 1.0.0 publish-local
```

### CI/CD Pipeline

The repository includes GitHub Actions workflows for:

- **Continuous Integration**: Build and test on every push/PR
- **Package Publishing**: Automatic NuGet publishing on releases
- **Version Management**: Automatic versioning with preview builds

Publishing uses [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing), matching the setup used by affolterNET.CRM / AzureStorage. No long-lived NuGet API key is required.

One-time setup:
1. Sign in to [NuGet Trusted Publishing](https://www.nuget.org/account/trustedpublishing) using the account used for AzureStorage. Select the owner of the Web packages and add a GitHub policy with repository owner `affolterNET`, repository `affolterNET.Web`, and workflow file `nuget-packages.yml` (filename only). Leave environment empty: this workflow does not use a GitHub environment. If scope selection is shown, allow new versions for `affolterNET.Web.*`.
2. Add the GitHub Actions repository secret `NUGET_USER` with the same NuGet profile username used by CRM (not an email address or API key).
3. After the first successful trusted publication, remove the obsolete `NUGET_API_KEY` repository secret and revoke the old key on NuGet if still active.

Pushes to `develop` increment the package patch version and run the build and tests. Merge `develop` into `main` through a pull request to publish all four packages and create the release tag. To retry publishing after account setup, run **Build and Publish NuGet Packages** on `main` with **Publish to NuGet.org** enabled; rerunning an old workflow run uses its original workflow revision.

This library provides flexible authentication and authorization modes for ASP.NET Core applications with YARP reverse proxy integration.

## MCP Endpoints (affolterNET.Web.Mcp)

Protects a Model Context Protocol endpoint as an OAuth 2.1 resource server, so
MCP clients (Claude Code, Codex) sign in with Keycloak instead of carrying a
static bearer token through a proxy.

The package registers its **own** authentication schemes (`McpBearer`, `McpOAuth`),
so the endpoint lives inside a cookie-based BFF without disturbing it: browser
requests keep using the BFF session, MCP requests use the access token.

```csharp
// The MCP server itself stays with the application
builder.Services.AddMcpServer().WithHttpTransport().WithTools<MyTools>();

// …this only puts OAuth in front of it
var mcpOptions = builder.Services.AddMcpAuthentication(appSettings, builder.Configuration);

var app = builder.Build();
app.ConfigureBffApp(bffOptions);   // or ConfigureApiApp
app.MapMcpSecured();               // no-op when the endpoint is disabled
```

```json
{
  "affolterNET": {
    "Web": {
      "Mcp": {
        "Enabled": true,
        "Path": "/mcp",
        "Audience": "my-mcp",
        "RequiredScopes": [ "my.read" ],
        "ResourceName": "My MCP server"
      }
    }
  }
}
```

`AuthorityBase` and `Realm` are inherited from `affolterNET:Web:Auth:Provider`
unless the `Mcp` section overrides them.

### What a client sees

1. `POST /mcp` without a token → `401` with
   `WWW-Authenticate: Bearer resource_metadata="https://host/.well-known/oauth-protected-resource/mcp"`
2. The client fetches that document (RFC 9728) and finds the Keycloak realm.
3. It runs an authorization-code flow with PKCE and retries with the token.

### Keycloak notes

- **`Audience` is mandatory.** Keycloak does not implement RFC 8707 resource
  indicators, so a token is bound to this MCP server through an audience mapper
  on the requested client scope — not through the `resource` parameter.
- Clients can register themselves (Dynamic Client Registration / Client ID
  Metadata Document, needs `--features=cimd`), or you pre-register one public
  PKCE client with redirect URI `http://localhost:PORT/callback` and point the
  CLI at it:

```bash
claude mcp add --transport http --client-id my-mcp-cli --callback-port 8080 \
  my-server https://host/mcp
codex mcp login my-server
```


## Authentication Modes

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                               AUTHENTICATION MODES                              │
└─────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────┐  ┌─────────────────┐  ┌─────────────────────────────────────┐
│      NONE       │  │   AUTHENTICATE  │  │             AUTHORIZE               │
│                 │  │     (LOGIN)     │  │                                     │
│ Anonymous       │  │ Login Required  │  │ Login + Permission Claims Required  │
│ Access          │  │ No Permissions  │  │ Fine-grained Access Control         │
└─────────────────┘  └─────────────────┘  └─────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────┐
│                            ALWAYS ENABLED SERVICES                              │
│                        (Security & Infrastructure)                              │
├─────────────────────────────────────────────────────────────────────────────────┤
│ ✅ SecurityHeadersMiddleware     │ CSP, HSTS, X-Frame-Options, etc.             │
│ ✅ AntiforgeryTokenMiddleware    │ CSRF protection                              │
│ ✅ HTTP Context Accessor         │ Core infrastructure                          │
│ ✅ Memory Cache                  │ Performance & caching                        │
│ ✅ YARP Reverse Proxy            │ Frontend/API proxying                        │
└─────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────┐
│                           MODE-SPECIFIC SERVICES                                │
└─────────────────────────────────────────────────────────────────────────────────┘

MODE: NONE                    MODE: AUTHENTICATE             MODE: AUTHORIZE
├─────────────────────┐      ├─────────────────────────┐   ├──────────────────────────┐
│ Services:           │      │ Services:               │   │ Services:                │
│ • Basic Routing     │      │ • Cookie Authentication │   │ • Cookie Authentication  │
│ • Static Files      │      │ • OIDC Integration      │   │ • OIDC Integration       │
│                     │      │ • Token Refresh         │   │ • Token Refresh          │
│ Middleware:         │      │ • Claims Enrichment     │   │ • Claims Enrichment      │
│ • No Auth Pipeline  │      │ • Basic Authorization   │   │ • Permission Policies    │
│                     │      │                         │   │ • RPT Token Service      │
│ Use Cases:          │      │ Middleware:             │   │ • Keycloak Integration   │
│ • Public websites   │      │ • UseAuthentication()   │   │                          │
│ • Static content    │      │ • UseAuthorization()    │   │ Middleware:              │
│ • Development       │      │ • RefreshTokenMware     │   │ • UseAuthentication()    │
└─────────────────────┘      │ • RptMiddleware         │   │ • UseAuthorization()     │
                             │                         │   │ • RefreshTokenMware      │
                             │ Use Cases:              │   │ • RptMiddleware          │
                             │ • Internal tools        │   │                          │
                             │ • Simple apps           │   │ Use Cases:               │
                             │ • Prototyping           │   │ • Enterprise apps        │
                             └─────────────────────────┘   │ • Multi-tenant systems   │
                                                           │ • Fine-grained access    │
                                                           └──────────────────────────┘

┌────────────────────────────────────────────────────────────────────────────────┐
│                               SERVICE MATRIX                                   │
├─────────────────────────────────┬───────┬────────────────┬─────────────────────┤
│ Service/Middleware              │ NONE  │ AUTHENTICATE   │ AUTHORIZE           │
├─────────────────────────────────┼───────┼────────────────┼─────────────────────┤
│ SecurityHeadersMiddleware       │   ✅   │      ✅        │         ✅          │
│ AntiforgeryTokenMiddleware      │   ✅   │      ✅        │         ✅          │
│ HTTP Context Accessor           │   ✅   │      ✅        │         ✅          │
│ Memory Cache                    │   ✅   │      ✅        │         ✅          │
│ YARP Reverse Proxy              │   ✅   │      ✅        │         ✅          │
│ Static Files                    │   ✅   │      ✅        │         ✅          │
│ API NotFound Handling           │   ✅   │      ✅        │         ✅          │
├─────────────────────────────────┼───────┼────────────────┼─────────────────────┤
│ Cookie Authentication           │   ❌   │      ✅        │         ✅          │
│ OIDC Integration                │   ❌   │      ✅        │         ✅          │
│ UseAuthentication()             │   ❌   │      ✅        │         ✅          │
│ UseAuthorization()              │   ❌   │      ✅        │         ✅          │
│ Session Management              │   ❌   │      ✅        │         ✅          │
│ Token Refresh Middleware        │   ❌   │      ✅        │         ✅          │
│ No Unauthorized Redirect        │   ❌   │      ✅        │         ✅          │
├─────────────────────────────────┼───────┼────────────────┼─────────────────────┤
│ RPT Token Service               │   ❌   │      ❌        │         ✅          │
│ Permission Policies             │   ❌   │      ❌        │         ✅          │
│ Permission Claims Service       │   ❌   │      ❌        │         ✅          │
└─────────────────────────────────┴───────┴────────────────┴─────────────────────┘
```

## Configuration

Configure the authorization mode in your `appsettings.json`:

```json
{
  "Auth": {
    "AuthenticationMode": "Authenticate",
    "RequireHttpsMetadata": true,
    "RedirectUri": "/signin-oidc",
    "PostLogoutRedirectUri": "/",
    "Cookie": { 
      "Secure": true 
    }
  }
}
```

### Available Authentication Modes

- **`None`**: Anonymous access, no authentication required
- **`Authenticate`**: Login required, no permission checks
- **`Authorize`**: Login + fine-grained permission validation

## Usage

### 1. Register Services

```csharp
var bffOptions = builder.Services.AddBffServices(isDev, builder.Configuration, options =>
{
    options.EnableSecurityHeaders = true;
    options.ConfigureBff = bffOptions =>
    {
        bffOptions.AuthMode = AuthenticationMode.Authenticate;
        bffOptions.EnableSessionManagement = true;
        bffOptions.EnableTokenRefresh = true;
    };
});
```

### 2. Configure Middleware Pipeline

```csharp
app.ConfigureBffApp(bffOptions);
```

## Key Features

- **Progressive Enhancement**: Each mode builds upon the previous one
- **YARP Integration**: Reverse proxy works seamlessly in all modes
- **Security First**: CSP, Antiforgery, and Security Headers always enabled
- **Flexible Configuration**: Easy mode switching via configuration
- **Clean Service Registration**: Only required services are registered per mode
- **Swagger Integration**: Built-in OpenAPI documentation support
- **Multi-Section Configuration**: Separate configuration sections for different concerns

## Usage Pattern

The library follows a two-step configuration pattern:

1. **Service Registration**: `AddBffServices()` returns configuration object
2. **Pipeline Configuration**: `ConfigureBffApp()` accepts the configuration object

```csharp
// Step 1: Register services and get configuration
var bffOptions = builder.Services.AddBffServices(isDev, builder.Configuration, options => { /* configure */ });

// Step 2: Configure middleware pipeline
app.ConfigureBffApp(bffOptions);
```

## Technical Configuration Switches

The BFF library provides fine-grained control over features through configuration switches. These can be set in `appsettings.json` or programmatically:

### Core Application Switches (All Modes)
- **`EnableSecurityHeaders`**: Security headers middleware at application level (default: `true`)

### BFF-Specific Switches
- **`EnableApiNotFound`**: API 404 handling for unmatched routes (default: `true`)
- **`EnableAntiforgery`**: CSRF protection with antiforgery tokens (default: `true`) 
- **`EnableHttpsRedirection`**: HTTPS enforcement middleware (default: `true`)
- **`EnableStaticFiles`**: Static file serving capability (default: `true`)
- **`EnableYarp`**: Reverse proxy functionality (default: `true`)

### Authentication Switches (Authenticate + Authorize Modes)
- **`EnableSessionManagement`**: Session handling and management (default: `true`)
- **`EnableTokenRefresh`**: Automatic token renewal middleware (default: `true`)
- **`EnableNoUnauthorizedRedirect`**: Prevent API route redirects on 401 (default: `true`)
- **`RevokeRefreshTokenOnLogout`**: Cleanup tokens on logout (default: `true`)

### Authorization Switches (Authorize Mode Only)  
- **`EnableRptTokens`**: Resource Permission Token support (default: `true`)

### Configuration Example

```json
{
  "affolterNET.Web": {
    "Bff": {
      "Options": {
        "AuthMode": "Authorize",
        "EnableSessionManagement": true,
        "EnableTokenRefresh": true,
        "EnableRptTokens": true,
        "EnableAntiforgery": true,
        "EnableApiNotFound": true,
        "EnableStaticFiles": true,
        "EnableYarp": true,
        "EnableHttpsRedirection": false,
        "RevokeRefreshTokenOnLogout": true
      }
    }
  }
}
```

### Programmatic Configuration

```csharp
var bffOptions = builder.Services.AddBffServices(isDev, builder.Configuration, options =>
{
    // Core application options
    options.EnableSecurityHeaders = true;
    
    // BFF-specific configuration
    options.ConfigureBff = bffOptions =>
    {
        bffOptions.AuthMode = AuthenticationMode.Authorize;
        bffOptions.EnableSessionManagement = true;
        bffOptions.EnableTokenRefresh = true;
        bffOptions.EnableRptTokens = true;
        bffOptions.EnableAntiforgery = false; // Disable for APIs
        bffOptions.EnableHttpsRedirection = false; // For development
    };
    
    // Swagger/OpenAPI configuration (optional)
    options.ConfigureSwagger = swaggerOptions =>
    {
        swaggerOptions.Title = "My API";
        swaggerOptions.Version = "v1";
        swaggerOptions.ConfigureApiDocumentation = app =>
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        };
    };
});
```

## Architecture

### Core Components

- **affolterNET.Auth.Core**: Base authorization policies, middleware, and services
- **affolterNET.Auth.Bff**: Backend-for-Frontend pattern with YARP integration
- **affolterNET.Auth.Api**: API-specific authentication (if needed)

### Security Services (Always Active)

- **SecurityHeadersMiddleware**: Applies CSP, HSTS, X-Frame-Options
- **AntiforgeryTokenMiddleware**: CSRF protection
- **YARP Reverse Proxy**: Frontend/API gateway functionality

### Authentication Services (AuthenticatedOnly + PermissionBased)

- **Cookie Authentication**: Secure session management
- **OIDC Integration**: Keycloak/OAuth2 authentication
- **Token Refresh**: Automatic token renewal
- **Claims Enrichment**: User information processing

### Authorization Services (PermissionBased Only)

- **PermissionAuthorizationPolicyProvider**: Dynamic policy creation
- **PermissionAuthorizationHandler**: Permission validation
- **RPT Token Service**: Resource Permission Token handling
- **Keycloak Integration**: Permission claim processing

## Examples

### Development Mode (No Authentication)
```json
{
  "affolterNET.Web": {
    "Bff": {
      "Options": {
        "AuthMode": "None",
        "EnableHttpsRedirection": false
      }
    }
  }
}
```

### Internal Tools (Simple Authentication)
```json
{
  "affolterNET.Web": {
    "Bff": {
      "Options": {
        "AuthMode": "Authenticate",
        "EnableSessionManagement": true,
        "EnableTokenRefresh": true
      }
    }
  }
}
```

### Enterprise Applications (Full Authorization)
```json
{
  "affolterNET.Web": {
    "Bff": {
      "Options": {
        "AuthMode": "Authorize",
        "EnableSessionManagement": true,
        "EnableTokenRefresh": true,
        "EnableRptTokens": true,
        "RevokeRefreshTokenOnLogout": true
      }
    }
  }
}
```