using System.Security.Cryptography;
using OctalPulse.Application.Interface.Services;

namespace OctalPulse.Infrastructure.Services;

public class OtpService : IOtpService
{
    private const int DefaultLength = 8;
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(3);
    private const int MaxAttempts = 5;

    private readonly ICacheService _cache;

    public OtpService(ICacheService cache)
    {
        _cache = cache;
    }

    public string GenerateOtp(int length = DefaultLength)
    {
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length), "OTP length must be greater than zero.");

        var buffer = new char[length];
        for (var i = 0; i < length; i++)
            buffer[i] = (char)('0' + RandomNumberGenerator.GetInt32(0, 10));

        return new string(buffer);
    }

    public Task<string> IssueOtpAsync(
        string subject,
        OtpPurpose purpose,
        int length = DefaultLength,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        var code = GenerateOtp(length);
        var effectiveExpiry = expiry ?? DefaultExpiry;

        _cache.Set(
            GetKey(subject, purpose),
            new OtpRecord(code, DateTime.UtcNow.Add(effectiveExpiry)),
            effectiveExpiry);

        return Task.FromResult(code);
    }

    public Task<OtpValidationResult> ValidateOtpAsync(
        string subject,
        OtpPurpose purpose,
        string otp,
        CancellationToken cancellationToken = default)
    {
        var key = GetKey(subject, purpose);

        if (!_cache.TryGet<OtpRecord>(key, out var record) || record is null)
            return Task.FromResult(OtpValidationResult.Expired);

        if (record.ExpiresAt <= DateTime.UtcNow)
        {
            _cache.Remove(key);
            return Task.FromResult(OtpValidationResult.Expired);
        }

        if (!string.Equals(record.Code, otp, StringComparison.Ordinal))
        {
            record.Attempts++;
            if (record.Attempts >= MaxAttempts)
            {
                _cache.Remove(key);
                return Task.FromResult(OtpValidationResult.TooManyAttempts);
            }

            _cache.Set(key, record, record.ExpiresAt - DateTime.UtcNow);
            return Task.FromResult(OtpValidationResult.Invalid);
        }

        _cache.Remove(key);
        return Task.FromResult(OtpValidationResult.Valid);
    }

    public Task<bool> HasActiveOtpAsync(
        string subject,
        OtpPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        var key = GetKey(subject, purpose);

        if (!_cache.TryGet<OtpRecord>(key, out var record) || record is null)
            return Task.FromResult(false);

        return Task.FromResult(record.ExpiresAt > DateTime.UtcNow);
    }

    public Task RemoveOtpAsync(
        string subject,
        OtpPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        _cache.Remove(GetKey(subject, purpose));
        return Task.CompletedTask;
    }

    private static string GetKey(string subject, OtpPurpose purpose)
        => $"otp:{purpose.ToString().ToLowerInvariant()}:{subject.Trim().ToLowerInvariant()}";

    private sealed class OtpRecord
    {
        public OtpRecord(string code, DateTime expiresAt)
        {
            Code = code;
            ExpiresAt = expiresAt;
        }

        public string Code { get; }
        public DateTime ExpiresAt { get; }
        public int Attempts { get; set; }
    }
}