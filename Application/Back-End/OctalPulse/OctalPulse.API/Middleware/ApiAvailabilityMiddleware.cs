using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OctalPulse.API.Attributes;
using OctalPulse.Application.Interface.Services;

namespace OctalPulse.API.Middleware;

public class ApiAvailabilityMiddleware
{
    private readonly RequestDelegate _next;

    public ApiAvailabilityMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IApiAvailabilityService availabilityService)
    {
        var endpoint = context.GetEndpoint();
        var attribute = endpoint?.Metadata.GetMetadata<ApiAvailabilityAttribute>();

        if (attribute is null)
        {
            await _next(context);
            return;
        }

        var isEnabled = await availabilityService.IsEnabledAsync(attribute.Key, context.RequestAborted);

        if (!isEnabled)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new
                {
                    code = "API_DISABLED",
                    message = "This operation is currently unavailable."
                }));
            return;
        }

        await _next(context);
    }
}