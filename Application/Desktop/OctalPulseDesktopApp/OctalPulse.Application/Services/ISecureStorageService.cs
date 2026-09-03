namespace OctalPulse.Application.Services;

public interface ISecureStorageService
{
    void SaveSecret(string key, string secret);
    string? GetSecret(string key);
    void RemoveSecret(string key);
}
