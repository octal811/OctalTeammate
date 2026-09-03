using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Features.Query.UserProfile.GetProfile;
using OctalPulse.Application.Interface.Repositories;

namespace OctalPulse.Application.Features.Command.UserProfile.UpdateProfile;

public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, UserProfileResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateProfileCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<UserProfileResponse> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            throw new NotFoundException("User not found.");

        user.Name = request.Name.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        user.MainRole = request.MainRole;
        if (request.ProfilePictureUrl != null)
        {
            user.ProfilePictureUrl = request.ProfilePictureUrl.Trim();
        }

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UserProfileResponse(
            user.Id,
            user.Name,
            user.Email ?? string.Empty,
            user.PhoneNumber,
            user.MainRole,
            user.Rank,
            user.ProfilePictureUrl);
    }
}
