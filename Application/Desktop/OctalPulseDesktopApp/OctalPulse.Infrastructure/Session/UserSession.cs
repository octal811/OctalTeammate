using CommunityToolkit.Mvvm.ComponentModel;
using OctalPulse.Application.Abstractions;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Infrastructure.Session;

public partial class UserSession : ObservableObject, IUserSession
{
    [ObservableProperty]
    private bool _isAuthenticated;

    [ObservableProperty]
    private Guid? _userId;

    [ObservableProperty]
    private string? _email;

    [ObservableProperty]
    private string? _name;

    [ObservableProperty]
    private UserRole? _mainRole;

    [ObservableProperty]
    private UserRank? _rank;

    public bool IsAdmin => Rank == UserRank.Admin;

    public void SetSession(Guid userId, string email, string name, UserRole mainRole, UserRank rank)
    {
        UserId = userId;
        Email = email;
        Name = name;
        MainRole = mainRole;
        Rank = rank;
        IsAuthenticated = true;
        OnPropertyChanged(nameof(IsAdmin));
    }

    public void ClearSession()
    {
        UserId = null;
        Email = null;
        Name = null;
        MainRole = null;
        Rank = null;
        IsAuthenticated = false;
        OnPropertyChanged(nameof(IsAdmin));
    }
}
