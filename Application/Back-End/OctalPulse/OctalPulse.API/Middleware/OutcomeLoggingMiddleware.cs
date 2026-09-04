using System.Diagnostics;
using System.Security.Claims;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Enums;

namespace OctalPulse.API.Middleware;

public class OutcomeLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public OutcomeLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ILog log)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (IsSkipped(path))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var start = DateTime.Now;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            var statusCode = context.Response.StatusCode;
            var state = Classify(statusCode);

            var apiRequested = $"{context.Request.Method} {path}";
            var userId = ResolveUserId(context);

            var details = BuildDetails(context, state, statusCode, stopwatch.Elapsed);

            log.WriteOutcome(new Outcome(
                Id: Guid.NewGuid(),
                UserId: userId,
                Priority: DerivePriority(state),
                APIRequested: apiRequested,
                State: state,
                Details: details,
                Date: start));
        }
    }

    private static bool IsSkipped(string path)
    {
        if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/hubs", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var extension = System.IO.Path.GetExtension(path);
        return extension is ".js" or ".css" or ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" or ".ico" or ".html" or ".map";
    }

    private static OutcomeState Classify(int statusCode) => statusCode switch
    {
        >= 200 and < 400 => OutcomeState.Success,
        >= 400 and < 500 => OutcomeState.Warning,
        >= 500 => OutcomeState.Failed,
        _ => OutcomeState.Warning
    };

    private static Priority DerivePriority(OutcomeState state) => state switch
    {
        OutcomeState.Success => Priority.Low,
        OutcomeState.Warning => Priority.Medium,
        OutcomeState.Failed => Priority.High,
        _ => Priority.Low
    };

    private static Guid? ResolveUserId(HttpContext context)
    {
        var value = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private static string BuildDetails(
        HttpContext context,
        OutcomeState state,
        int statusCode,
        TimeSpan elapsed)
    {
        var parts = new List<string>
        {
            $"HTTP {statusCode}",
            $"elapsed={elapsed.TotalMilliseconds:0}ms"
        };

        if (context.Items.TryGetValue("OutcomeDetails", out var stored) && stored is string detail)
        {
            parts.Add(detail);
        }

        return string.Join(" | ", parts);
    }
}
