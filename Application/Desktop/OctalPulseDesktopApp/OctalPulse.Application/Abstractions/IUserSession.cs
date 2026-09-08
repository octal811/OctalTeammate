using System.ComponentModel;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Abstractions;

public interface IUserSession : INotifyPropertyChanged
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    string? Email { get; }
    string? Name { get; }
    string? ProfileImageUrl { get; }
    UserRole? MainRole { get; }
    UserRank? Rank { get; }
    bool IsAdmin { get; }

    void SetSession(Guid userId, string email, string name, UserRole mainRole, UserRank rank, string? profileImageUrl = null);
    void ClearSession();
}
