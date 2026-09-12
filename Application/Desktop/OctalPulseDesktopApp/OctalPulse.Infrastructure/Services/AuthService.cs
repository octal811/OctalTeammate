using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Infrastructure.Api;

namespace OctalPulse.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly ApiClient _apiClient;
    private readonly ITokenService _tokenService;

    public AuthService(ApiClient apiClient, ITokenService tokenService)
    {
        _apiClient = apiClient;
        _tokenService = tokenService;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.SendAsync<LoginResponse>(
            HttpMethod.Post,
            "/api/auth/login",
            request,
            cancellationToken);

        _tokenService.SetTokens(
            response.AccessToken,
            response.RefreshToken,
            response.AccessTokenExpiresAt,
            response.RefreshTokenExpiresAt);

        return response;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.SendAsync<RegisterResponse>(
            HttpMethod.Post,
            "/api/auth/register",
            request,
            cancellationToken);

        _tokenService.SetTokens(
            response.AccessToken,
            response.RefreshToken,
            response.AccessTokenExpiresAt,
            response.RefreshTokenExpiresAt);

        return response;
    }

    public async Task<RefreshTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.SendAsync<RefreshTokenResponse>(
            HttpMethod.Post,
            "/api/auth/refresh",
            new RefreshTokenRequest(refreshToken),
            cancellationToken);

        _tokenService.SetTokens(
            response.AccessToken,
            response.RefreshToken,
            response.AccessTokenExpiresAt,
            response.RefreshTokenExpiresAt);

        return response;
    }

    public async Task RevokeTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        try
        {
            await _apiClient.SendAsync(
                HttpMethod.Post,
                "/api/auth/revoke",
                new RevokeTokenRequest(refreshToken),
                cancellationToken);
        }
        finally
        {
            _tokenService.ClearTokens();
        }
    }

    public Task<MessageResponse> RequestEmailVerificationAsync(string email, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<MessageResponse>(
            HttpMethod.Post,
            "/api/auth/verify-email/request",
            new RequestEmailVerificationRequest(email),
            cancellationToken);
    }

    public Task<MessageResponse> VerifyEmailAsync(string email, string otp, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<MessageResponse>(
            HttpMethod.Post,
            "/api/auth/verify-email",
            new VerifyEmailRequest(email, otp),
            cancellationToken);
    }

    public Task<MessageResponse> RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<MessageResponse>(
            HttpMethod.Post,
            "/api/auth/password-reset/request",
            new RequestPasswordResetRequest(email),
            cancellationToken);
    }

    public Task<MessageResponse> ResetPasswordAsync(string email, string otp, string newPassword, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<MessageResponse>(
            HttpMethod.Post,
            "/api/auth/password-reset",
            new ResetPasswordRequest(email, otp, newPassword),
            cancellationToken);
    }

    public Task<UserProfileResponse> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<UserProfileResponse>(
            HttpMethod.Get,
            "/api/users/me",
            cancellationToken: cancellationToken);
    }

    public Task<UserProfileResponse> GetProfileByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<UserProfileResponse>(
            HttpMethod.Get,
            $"/api/users/{userId}",
            cancellationToken: cancellationToken);
    }

    public Task<SearchUsersResponse> SearchUsersAsync(string query, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<SearchUsersResponse>(
            HttpMethod.Get,
            $"/api/users/search?query={Uri.EscapeDataString(query)}",
            cancellationToken: cancellationToken);
    }

    public Task<UserProfileResponse> UpdateProfileAsync(UpdateUserProfileRequest request, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<UserProfileResponse>(
            HttpMethod.Put,
            "/api/users/profile",
            request,
            cancellationToken);
    }

    public Task<UserProfileResponse> UploadProfileImageAsync(
        string imageType,
        byte[] fileBytes,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        return _apiClient.UploadAsync<UserProfileResponse>(
            "/api/users/profile/image",
            fileBytes,
            fileName,
            contentType,
            formFieldName: "type",
            formFieldValue: imageType,
            cancellationToken);
    }
}
