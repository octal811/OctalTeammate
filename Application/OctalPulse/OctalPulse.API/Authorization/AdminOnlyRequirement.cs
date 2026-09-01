using Microsoft.AspNetCore.Authorization;

namespace OctalPulse.API.Authorization;

public sealed class AdminOnlyRequirement : IAuthorizationRequirement
{
}