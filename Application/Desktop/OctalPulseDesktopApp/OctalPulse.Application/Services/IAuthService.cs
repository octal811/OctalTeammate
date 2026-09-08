using OctalPulse.Application.Contracts;

namespace OctalPulse.Application.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<RefreshTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task RevokeTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<MessageResponse> RequestEmailVerificationAsync(string email, CancellationToken cancellationToken = default);
    Task<MessageResponse> VerifyEmailAsync(string email, string otp, CancellationToken cancellationToken = default);
    Task<MessageResponse> RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default);
    Task<MessageResponse> ResetPasswordAsync(string email, string otp, string newPassword, CancellationToken cancellationToken = default);
    Task<UserProfileResponse> GetProfileAsync(CancellationToken cancellationToken = default);
    Task<UserProfileResponse> UpdateProfileAsync(UpdateUserProfileRequest request, CancellationToken cancellationToken = default);
    Task<UserProfileResponse> UploadProfileImageAsync(string imageType, byte[] fileBytes, string fileName, string contentType, CancellationToken cancellationToken = default);
}
