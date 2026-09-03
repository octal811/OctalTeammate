namespace OctalPulse.Application.Events;

public record SessionExpiredEvent();
public record UserLoggedInEvent(Guid UserId, string Email, string Name);
public record UserLoggedOutEvent();
