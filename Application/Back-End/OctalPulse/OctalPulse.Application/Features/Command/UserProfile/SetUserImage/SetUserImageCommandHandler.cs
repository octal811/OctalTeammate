using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Features.Query.UserProfile.GetProfile;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.UserProfile.SetUserImage;

public class SetUserImageCommandHandler : IRequestHandler<SetUserImageCommand, UserProfileResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public SetUserImageCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<UserProfileResponse> Handle(SetUserImageCommand request, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            throw new NotFoundException("User not found.");

        switch (request.ImageType)
        {
            case UserImageType.Profile:
                user.ProfilePictureUrl = request.ImageUrl;
                break;
            case UserImageType.Background:
                user.BackgroundImageUrl = request.ImageUrl;
                break;
        }

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UserProfileResponse(
            user.Id,
            user.Name,
            user.Email ?? string.Empty,
            user.MainRole,
            user.Rank,
            user.ProfilePictureUrl,
            user.BackgroundImageUrl,
            user.Bio);
    }
}