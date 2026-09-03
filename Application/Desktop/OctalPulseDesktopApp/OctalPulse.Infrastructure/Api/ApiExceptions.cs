using System.Net;

namespace OctalPulse.Infrastructure.Api;

public class ApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string? ServerError { get; }

    public ApiException(string message, HttpStatusCode statusCode, string? serverError = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ServerError = serverError;
    }
}

public class ApiValidationException : ApiException
{
    public ApiValidationException(string message, string? serverError = null)
        : base(message, HttpStatusCode.BadRequest, serverError) { }
}

public class ApiUnauthorizedException : ApiException
{
    public ApiUnauthorizedException(string message = "Unauthorized access.")
        : base(message, HttpStatusCode.Unauthorized) { }
}

public class ApiForbiddenException : ApiException
{
    public ApiForbiddenException(string message = "Forbidden access.")
        : base(message, HttpStatusCode.Forbidden) { }
}

public class ApiNotFoundException : ApiException
{
    public ApiNotFoundException(string message = "Resource not found.")
        : base(message, HttpStatusCode.NotFound) { }
}

public class ApiConflictException : ApiException
{
    public ApiConflictException(string message = "A duplicate operation is in progress or resource is in conflict.")
        : base(message, HttpStatusCode.Conflict) { }
}

public class ApiDisabledException : ApiException
{
    public ApiDisabledException(string message = "This operation is currently disabled by the administrator.")
        : base(message, HttpStatusCode.ServiceUnavailable) { }
}
