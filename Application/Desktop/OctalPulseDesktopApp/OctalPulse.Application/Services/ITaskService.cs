using OctalPulse.Application.Contracts;

namespace OctalPulse.Application.Services;

public interface ITaskService
{
    // Major Tasks
    Task<CreateMajorTaskResponse> CreateMajorTaskAsync(CreateMajorTaskRequest request, CancellationToken cancellationToken = default);
    Task<GetMajorTasksByTrackResponse> GetMajorTasksByTrackAsync(Guid trackId, CancellationToken cancellationToken = default);
    Task<UpdateMajorTaskResponse> UpdateMajorTaskAsync(UpdateMajorTaskRequest request, CancellationToken cancellationToken = default);
    Task DeleteMajorTaskAsync(Guid id, CancellationToken cancellationToken = default);

    // Minor Tasks
    Task<CreateMinorTaskResponse> CreateMinorTaskAsync(CreateMinorTaskRequest request, CancellationToken cancellationToken = default);
    Task<GetMinorTasksByMajorTaskResponse> GetMinorTasksByMajorTaskAsync(Guid majorTaskId, CancellationToken cancellationToken = default);
    Task<UpdateMinorTaskResponse> UpdateMinorTaskAsync(UpdateMinorTaskRequest request, CancellationToken cancellationToken = default);
    Task<AddMinorTaskWorkTimeResponse> AddMinorTaskWorkTimeAsync(AddMinorTaskWorkTimeRequest request, CancellationToken cancellationToken = default);
    Task DeleteMinorTaskAsync(Guid id, CancellationToken cancellationToken = default);
}
