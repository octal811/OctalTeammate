using System.Security.Claims;
using OctalPulse.Domain.Entities;

namespace OctalPulse.Application.Interface.Services;

public interface IJwtService
{
    (string Token, DateTime ExpiresAtUtc) GenerateAccessToken(User user);
    (string Token, DateTime ExpiresAtUtc, string Hash) GenerateRefreshToken();
    string HashRefreshToken(string refreshToken);
    ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
}