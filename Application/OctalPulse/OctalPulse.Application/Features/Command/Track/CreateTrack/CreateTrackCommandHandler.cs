using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;

namespace OctalPulse.Application.Features.Command.Track.CreateTrack;

public class CreateTrackCommandHandler : IRequestHandler<CreateTrackCommand, CreateTrackResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProgressCalculator _progressCalculator;

    public CreateTrackCommandHandler(IUnitOfWork unitOfWork, IProgressCalculator progressCalculator)
    {
        _unitOfWork = unitOfWork;
        _progressCalculator = progressCalculator;
    }

    public async Task<CreateTrackResponse> Handle(CreateTrackCommand request, CancellationToken cancellationToken)
    {
        var project = await _unitOfWork.Projects.GetByIdAsync(request.ProjectId, cancellationToken);
        if (project is null)
            throw new NotFoundException("Project not found.");

        var now = DateTime.UtcNow;

        var track = new Domain.Entities.Track
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Progress = 0,
            ProjectId = request.ProjectId,
            IsDeleted = false,
            CreatedDate = now
        };

        await _unitOfWork.Tracks.AddAsync(track, cancellationToken);
        await _unitOfWork.CompleteAsync(cancellationToken);

        return new CreateTrackResponse(
            track.Id,
            track.ProjectId,
            track.Name,
            track.Description,
            track.Progress);
    }
}