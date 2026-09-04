using System.Net;
using System.Text.Json;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Services;

namespace OctalPulse.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILog _log;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILog log)
    {
        _next = next;
        _log = log;
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
            await WriteErrorAsync(context, HttpStatusCode.InternalServerError, "Internal Server Error", "An unexpected error occurred.");
        }
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