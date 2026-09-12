using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Services;

namespace OctalPulse.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILog _log;
    private readonly IConfiguration _configuration;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILog log, IConfiguration configuration)
    {
        _next = next;
        _log = log;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (UnauthorizedException ex)
        {
            context.Items["OutcomeDetails"] = ex.Message;
            await WriteErrorAsync(context, HttpStatusCode.Unauthorized, "Unauthorized", ex.Message);
        }
        catch (ForbiddenException ex)
        {
            context.Items["OutcomeDetails"] = ex.Message;
            await WriteErrorAsync(context, HttpStatusCode.Forbidden, "Forbidden", ex.Message);
        }
        catch (ValidationException ex)
        {
            context.Items["OutcomeDetails"] = ex.Message;
            await WriteErrorAsync(context, HttpStatusCode.BadRequest, "Validation Error", ex.Message);
        }
        catch (NotFoundException ex)
        {
            context.Items["OutcomeDetails"] = ex.Message;
            await WriteErrorAsync(context, HttpStatusCode.NotFound, "Not Found", ex.Message);
        }
        catch (Exception ex)
        {
            _log.Error("Unhandled exception.", ex);
            context.Items["OutcomeDetails"] = ex.Message;

            var showErrors = _configuration["Diagnostics:ShowErrors"] == "true";
            var message = showErrors ? BuildDiagnosticMessage(ex) : "An unexpected error occurred.";
            await WriteErrorAsync(context, HttpStatusCode.InternalServerError, "Internal Server Error", message);
        }
    }

    private static string BuildDiagnosticMessage(Exception ex)
    {
        var lines = new List<string>
        {
            $"{ex.GetType().FullName}: {ex.Message}",
            ex.StackTrace ?? "(no stack trace)"
        };

        var inner = ex.InnerException;
        var depth = 0;
        while (inner != null && depth < 5)
        {
            lines.Add($"INNER ({depth + 1}) {inner.GetType().FullName}: {inner.Message}");
            lines.Add(inner.StackTrace ?? "(no stack trace)");
            inner = inner.InnerException;
            depth++;
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static async Task WriteErrorAsync(
        HttpContext context,
        HttpStatusCode statusCode,
        string error,
        string message)
    {
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var payload = JsonSerializer.Serialize(new
        {
            error,
            message,
            statusCode = (int)statusCode
        });

        await context.Response.WriteAsync(payload);
    }
}