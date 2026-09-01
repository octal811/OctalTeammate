using MediatR;
using Microsoft.AspNetCore.Identity;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Features.Command.Auth.Common;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Entities;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.Auth.Register;

public class RegisterCommandHandler : AuthCommandHandlerBase, IRequestHandler<RegisterCommand, AuthResponse>
{
    private readonly UserManager<User> _userManager;

    public RegisterCommandHandler(
        UserManager<User> userManager,
        IJwtService jwtService,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork)
        : base(jwtService, refreshTokenRepository, unitOfWork)
    {
        _userManager = userManager;
    }

    public async Task<AuthResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
            throw new ValidationException("Email is already registered.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            Name = request.Name,
            MainRole = request.MainRole,
            Rank = UserRank.Member,
            EmailConfirmed = false,
            IsDeleted = false
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            throw new ValidationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        return await IssueTokensAsync(user, cancellationToken);
    }
}