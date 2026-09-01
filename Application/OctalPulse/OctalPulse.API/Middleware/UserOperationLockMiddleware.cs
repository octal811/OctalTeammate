using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using OctalPulse.API.Attributes;
using OctalPulse.Application.Interface.Services;

namespace OctalPulse.API.Middleware;

public class UserOperationLockMiddleware
{
    private readonly RequestDelegate _next;

    public UserOperationLockMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IUserOperationLock userOperationLock)
    {
        var endpoint = context.GetEndpoint();
        var attribute = endpoint?.Metadata.GetMetadata<UserOperationLockAttribute>();

        if (attribute is null || context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdValue) || !Guid.TryParse(userIdValue, out var userId))
        {
            await _next(context);
            return;
        }

        var operation = !string.IsNullOrWhiteSpace(attribute.Operation)
                        ? attribute.Operation
                        : $"{context.Request.Method}:{context.Request.Path}";

        using var lease = userOperationLock.TryAcquire(userId, operation);
        if (lease is null)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = "A request for this operation is already in progress. Please wait for it to finish and try again."
            });
            return;
        }

        await _next(context);
    }
}