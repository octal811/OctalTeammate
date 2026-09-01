using MediatR;
using Microsoft.AspNetCore.Identity;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Features.Command.Auth.Common;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Entities;

namespace OctalPulse.Application.Features.Command.Auth.Login;

public class LoginCommandHandler : AuthCommandHandlerBase, IRequestHandler<LoginCommand, AuthResponse>
{
    private readonly UserManager<User> _userManager;

    public LoginCommandHandler(
        UserManager<User> userManager,
        IJwtService jwtService,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork)
        : base(jwtService, refreshTokenRepository, unitOfWork)
    {
        _userManager = userManager;
    }

    public async Task<AuthResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || user.IsDeleted)
            throw new UnauthorizedException("Invalid email or password.");

        var validPassword = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!validPassword)
            throw new UnauthorizedException("Invalid email or password.");

        return await IssueTokensAsync(user, cancellationToken);
    }
}