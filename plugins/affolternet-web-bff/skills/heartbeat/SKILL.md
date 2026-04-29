---
name: heartbeat
description: Configure the periodic heartbeat log line for affolterNET.Web.Bff. Use when wiring an external liveness alert (e.g. Azure Log Search alert), changing the log substring, or matching a legacy alert pattern after a library upgrade.
---

# Heartbeat (Liveness Log Line)

A `HeartbeatBackgroundService` (registered automatically by `AddBffServices`) emits one log line at a fixed interval. An external monitor (Azure Log Search alert, Loki rule, etc.) greps for the configured substring and fires when no match is seen within its window — catching silently-stuck containers that still pass HTTP health probes.

The service is in `affolterNET.Web.Core` and is wired from both `AddBffServices` and `AddApiServices`.

## Default behavior

| Property | Default | Meaning |
|----------|---------|---------|
| `Enabled` | `true` | Emit heartbeats. Set `false` to disable entirely. |
| `Pattern` | `Heartbeat` | Substring included in every heartbeat line. The external monitor greps for this. |
| `IntervalSeconds` | `300` (5 min) | Time between heartbeats. Pick comfortably below the alert window so a single missed tick can't false-positive. |
| `LogLevel` | `Information` | Log level of the heartbeat line. |

Default output:

```
[INF] Heartbeat: alive at 2026-04-29T07:35:00.0000000+00:00
```

## Configure via environment variables (recommended for containers)

Standard ASP.NET Core config binding uses `__` as the section separator. The four overrides are:

```bash
affolterNET__Web__Heartbeat__Enabled=true
affolterNET__Web__Heartbeat__Pattern=SyncHeartbeat
affolterNET__Web__Heartbeat__IntervalSeconds=600
affolterNET__Web__Heartbeat__LogLevel=Information
```

Terraform example for an Azure Container App:

```hcl
template {
  container {
    env { name = "affolterNET__Web__Heartbeat__Pattern", value = "SyncHeartbeat" }
    env { name = "affolterNET__Web__Heartbeat__IntervalSeconds", value = "300" }
  }
}
```

## Configure via appsettings.json

```json
{
  "affolterNET": {
    "Web": {
      "Heartbeat": {
        "Enabled": true,
        "Pattern": "SyncHeartbeat",
        "IntervalSeconds": 300,
        "LogLevel": "Information"
      }
    }
  }
}
```

## Configure programmatically

```csharp
var options = builder.Services.AddBffServices(appSettings, config, opts =>
{
    opts.ConfigureHeartbeat = hb =>
    {
        hb.Pattern = "SyncHeartbeat";
        hb.IntervalSeconds = 300;
    };
});
```

The programmatic action runs *after* config binding, so values set here override env vars / `appsettings.json`.

## When to override `Pattern`

The default `"Heartbeat"` substring is fine for new deployments. Override it when:

- **Migrating a legacy app** that already has an alert keyed on a custom substring (e.g. GP-DataFlow's pre-library inline log line used `"SyncHeartbeat"`). Set `Pattern=SyncHeartbeat` to keep the existing alert working without touching the alert rule.
- **Multiple apps share one Log Analytics workspace** and you need per-app patterns to disambiguate.
- **`"Heartbeat"` collides** with another log line in your stack.

## Wiring an Azure Log Search alert

```kql
ContainerAppConsoleLogs_CL
| where ContainerAppName_s == "<your-app>"
| where Log_s contains "<your Pattern value>"
```

Trigger when `Count == 0` over your chosen window. Keep the window at least 2× `IntervalSeconds` so one missed tick never alerts.

## Disable heartbeat

For dev environments that scale to zero (cold container ⇒ no heartbeat ⇒ false positive), either:

- Set `affolterNET__Web__Heartbeat__Enabled=false` on dev, or
- Keep heartbeat on and disable the *alert* (the workbook still shows heartbeat data when the container is awake).

The second is preferred: production behavior matches dev, only the alert rule differs per-env.

## Troubleshooting

### No heartbeat lines in Log Analytics
- Confirm `Enabled` is `true` (default). Check the rendered config at startup.
- Confirm the container is actually running — scale-to-zero apps emit nothing while asleep.
- Wait at least `IntervalSeconds` after startup; the first heartbeat fires after one interval, not immediately.

### Alert keeps firing despite container being healthy
- Substring mismatch: the alert query's `contains` value must equal `Pattern` exactly (case-sensitive in KQL when using `contains_cs`; case-insensitive with `contains`).
- Window too tight relative to `IntervalSeconds` — give yourself 2× margin.
- Cold-start window: add `business_hours` scoping or raise the window for scale-to-zero apps.

### After library upgrade, alert went silent
- The default `Pattern` changed (or didn't) between versions; pin it explicitly via env var instead of relying on the default. This makes the alert stable across library bumps.
