using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using affolterNET.Web.Core.Configuration;

namespace affolterNET.Web.Core.Middleware;

public class RequestLoggingMiddleware(
    RequestDelegate next,
    IOptionsMonitor<RequestLoggingOptions> options,
    ILogger<RequestLoggingMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        if (IsExcluded(path))
        {
            await next(context);
            return;
        }

        logger.LogInformation("Request: {Method} {Path}", context.Request.Method, path);
        await next(context);
        var endpoint = context.GetEndpoint();
        logger.LogInformation("Matched Endpoint: {Endpoint}", endpoint?.DisplayName ?? "None");
    }

    private bool IsExcluded(string path)
    {
        var excludePaths = options.CurrentValue.ExcludePaths;
        foreach (var prefix in excludePaths)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
