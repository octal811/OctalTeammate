using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using OctalPulse.Domain.Entities;
using OctalPulse.Domain.Enums;

namespace OctalPulse.API.Authorization;

public sealed class AdminOnlyAuthorizationHandler : AuthorizationHandler<AdminOnlyRequirement>
{
    private readonly UserManager<User> _userManager;

    public AdminOnlyAuthorizationHandler(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminOnlyRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Fail();
            return;
        }

        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdValue is null || !Guid.TryParse(userIdValue, out var userId))
        {
            context.Fail();
            return;
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.IsDeleted)
        {
            context.Fail();
            return;
        }

        if (user.Rank == UserRank.Admin)
        {
            context.Succeed(requirement);
            return;
        }

        context.Fail();
    }
}