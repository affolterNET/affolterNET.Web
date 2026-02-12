---
name: security
description: Configure security headers, CORS, antiforgery, and the IConfigurableOptions pattern for affolterNET.Web.Bff. Use when setting up CSP, HSTS, CSRF protection, or custom options.
---

# Security Configuration

Configure security headers, CORS, antiforgery, and the options pattern.

For complete reference, see [Library Guide](../../LIBRARY_GUIDE.md).

## Security Headers

### appsettings.json

```json
{
  "affolterNET": {
    "Web": {
      "SecurityHeaders": {
        "EnableHsts": true,
        "AllowedConnectSources": ["https://api.example.com"],
        "AllowedImageSources": ["https://cdn.example.com"],
        "CustomCspDirectives": {
          "script-src": "'self'"
        }
      }
    }
  }
}
```

## CORS Configuration

```json
{
  "affolterNET": {
    "Web": {
      "Cors": {
        "AllowedOrigins": ["https://app.example.com"],
        "AllowedMethods": ["GET", "POST", "PUT", "DELETE"],
        "AllowedHeaders": ["Content-Type", "Authorization", "X-XSRF-TOKEN"],
        "AllowCredentials": true
      }
    }
  }
}
```

## Antiforgery (CSRF Protection)

### appsettings.json

```json
{
  "affolterNET": {
    "Web": {
      "Auth": {
        "AntiForgery": {
          "HeaderName": "X-XSRF-TOKEN",
          "CookieName": ".MyApp.Antiforgery"
        }
      }
    }
  }
}
```

### SPA Integration

```typescript
// Get the antiforgery token from cookie or meta tag
const token = document.querySelector('meta[name="csrf-token"]')?.getAttribute('content');

// Include in requests
fetch('/api/data', {
    method: 'POST',
    headers: {
        'Content-Type': 'application/json',
        'X-XSRF-TOKEN': token
    },
    body: JSON.stringify(data)
});
```

## IConfigurableOptions Pattern

All options follow a three-tier configuration pattern:

```csharp
var options = builder.Services.AddBffServices(isDev, config, opts => {
    // Lambda configuration (highest priority)
    opts.EnableSecurityHeaders = true;
});
```

## Configuration Sections

| Section | Options Class |
|---------|---------------|
| `affolterNET:Web:SecurityHeaders` | `SecurityHeadersOptions` |
| `affolterNET:Web:Cors` | `AffolterNetCorsOptions` |
| `affolterNET:Web:Auth:AntiForgery` | `BffAntiforgeryOptions` |

## CSP for SPAs

Use `CustomCspDirectives` to override individual CSP directives. Any directive provided here completely replaces the built-in default for that directive.

```json
{
  "affolterNET": {
    "Web": {
      "SecurityHeaders": {
        "CustomCspDirectives": {
          "script-src": "'self'"
        },
        "AllowedConnectSources": ["https://api.example.com"],
        "AllowedFontSources": ["https://fonts.googleapis.com"]
      }
    }
  }
}
```

**Note:** The default `script-src` uses `nonce` + `strict-dynamic`, which requires server-rendered nonce attributes on `<script>` tags. For static SPAs (Vite/Vue/React builds), override with `"script-src": "'self'"` via `CustomCspDirectives`.

## Troubleshooting

### CSRF validation fails
- Ensure antiforgery token is included in request header
- Check cookie name matches configuration
- Verify header name matches configuration

### CORS preflight fails
- Include `X-XSRF-TOKEN` in `AllowedHeaders`
- Ensure `AllowCredentials` is `true` for cookie auth
- Check origin exactly matches (including protocol/port)
