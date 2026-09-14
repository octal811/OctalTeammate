using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OctalPulse.Application.Contracts;

namespace OctalPulse.Infrastructure.Api;

/// <summary>
/// Ensures all DateTime values received from the API are treated as UTC.
/// Without this, System.Text.Json deserializes UTC datetime strings with Kind=Unspecified,
/// causing ToUniversalTime() to incorrectly add the local timezone offset.
/// </summary>
internal sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dt = reader.GetDateTime();
        return dt.Kind == DateTimeKind.Utc
            ? dt
            : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToUniversalTime().ToString("O"));
    }
}

public class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiClient(HttpClient httpClient, ILogger<ApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        _jsonOptions.Converters.Add(new JsonStringEnumConverter());
        _jsonOptions.Converters.Add(new UtcDateTimeJsonConverter());
    }

    public async Task<TResponse> SendAsync<TResponse>(
        HttpMethod method,
        string url,
        object? body = null,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequestMessage(method, url, body);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return default!;
        }

        var result = JsonSerializer.Deserialize<TResponse>(content, _jsonOptions);
        return result!;
    }

    public async Task SendAsync(
        HttpMethod method,
        string url,
        object? body = null,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequestMessage(method, url, body);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<TResponse> UploadAsync<TResponse>(
        string url,
        byte[] fileBytes,
        string fileName,
        string contentType,
        string? formFieldName = null,
        string? formFieldValue = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        using var requestContent = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        requestContent.Add(fileContent, "file", fileName);
        if (!string.IsNullOrEmpty(formFieldName))
        {
            requestContent.Add(new StringContent(formFieldValue ?? string.Empty), formFieldName);
        }
        request.Content = requestContent;

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return default!;
        }

        return JsonSerializer.Deserialize<TResponse>(content, _jsonOptions)!;
    }

    private HttpRequestMessage CreateRequestMessage(HttpMethod method, string url, object? body)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, _jsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }
        return request;
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var rawContent = string.Empty;
        try
        {
            rawContent = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            // Ignore error reading response body
        }

        _logger.LogWarning("API Error {StatusCode}: {Content}", response.StatusCode, rawContent);

        ApiErrorResponse? errorDto = null;
        if (!string.IsNullOrWhiteSpace(rawContent))
        {
            try
            {
                errorDto = JsonSerializer.Deserialize<ApiErrorResponse>(rawContent, _jsonOptions);
            }
            catch
            {
                // Raw content is not JSON
            }
        }

        var message = errorDto?.Message
                      ?? errorDto?.Detail
                      ?? (!string.IsNullOrWhiteSpace(rawContent) ? rawContent : response.ReasonPhrase)
                      ?? "An error occurred while contacting the server.";

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                throw new ApiValidationException(message, errorDto?.Error);

            case HttpStatusCode.Unauthorized:
                throw new ApiUnauthorizedException(message);

            case HttpStatusCode.Forbidden:
                throw new ApiForbiddenException(message);

            case HttpStatusCode.NotFound:
                throw new ApiNotFoundException(message);

            case HttpStatusCode.Conflict:
                throw new ApiConflictException(message);

            case HttpStatusCode.ServiceUnavailable when errorDto?.Code == "API_DISABLED":
                throw new ApiDisabledException(message);

            default:
                throw new ApiException(message, response.StatusCode, errorDto?.Error);
        }
    }
}
