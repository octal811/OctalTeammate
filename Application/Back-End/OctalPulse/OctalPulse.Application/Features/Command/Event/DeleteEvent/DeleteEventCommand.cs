using MediatR;

namespace OctalPulse.Application.Features.Command.Event.DeleteEvent;

public record DeleteEventCommand(Guid Id, Guid UserId) : IRequest<Unit>;
