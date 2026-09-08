using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;

namespace OctalPulse.Infrastructure.Api;

public class AuthDelegatingHandler : DelegatingHandler
{
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthDelegatingHandler> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public event Action? SessionExpired;

    public AuthDelegatingHandler(ITokenService tokenService, ILogger<AuthDelegatingHandler> logger)
    {
        _tokenService = tokenService;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Attach access token if present
        var accessToken = _tokenService.GetAccessToken();
        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        var response = await base.SendAsync(request, cancellationToken);

        // If 401 Unauthorized and not an auth endpoint, attempt token refresh
        var requestPath = request.RequestUri?.AbsolutePath?.ToLowerInvariant() ?? string.Empty;
        var isAuthEndpoint = requestPath.Contains("/api/auth/");

        if (response.StatusCode == HttpStatusCode.Unauthorized && !isAuthEndpoint)
        {
            _logger.LogInformation("Received 401 Unauthorized for {Uri}. Attempting token refresh.", request.RequestUri);

            var refreshed = await TryRefreshTokenAsync(request.RequestUri?.GetLeftPart(UriPartial.Authority), cancellationToken);
            if (refreshed)
            {
                // Clone request and retry with new token
                var newAccessToken = _tokenService.GetAccessToken();
                var retryRequest = await CloneHttpRequestMessageAsync(request);
                retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newAccessToken);

                response.Dispose();
                return await base.SendAsync(retryRequest, cancellationToken);
            }
            else
            {
                _logger.LogWarning("Token refresh failed. Triggering SessionExpired.");
                _tokenService.ClearTokens();
                SessionExpired?.Invoke();
            }
        }

        return response;
    }

    private async Task<bool> TryRefreshTokenAsync(string? baseAuthority, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(baseAuthority))
            return false;

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            // Check if another thread already refreshed while we waited
            if (!_tokenService.IsAccessTokenExpired())
                return true;

            var refreshToken = _tokenService.GetRefreshToken();
            if (string.IsNullOrEmpty(refreshToken))
                return false;

            using var refreshClient = new HttpClient { BaseAddress = new Uri(baseAuthority) };
            var payload = JsonSerializer.Serialize(new RefreshTokenRequest(refreshToken));
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var refreshResponse = await refreshClient.PostAsync("/api/auth/refresh", content, cancellationToken);
            if (!refreshResponse.IsSuccessStatusCode)
            {
                return false;
            }

            var responseBody = await refreshResponse.Content.ReadAsStringAsync(cancellationToken);
            var tokenResult = JsonSerializer.Deserialize<RefreshTokenResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (tokenResult is null) return false;

            _tokenService.SetTokens(
                tokenResult.AccessToken,
                tokenResult.RefreshToken,
                tokenResult.AccessTokenExpiresAt,
                tokenResult.RefreshTokenExpiresAt);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during background token refresh.");
            return false;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage req)
    {
        var clone = new HttpRequestMessage(req.Method, req.RequestUri)
        {
            Version = req.Version
        };

        if (req.Content != null)
        {
            var ms = new MemoryStream();
            await req.Content.CopyToAsync(ms);
            ms.Position = 0;
            clone.Content = new StreamContent(ms);

            foreach (var h in req.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }
        }

        foreach (var header in req.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
