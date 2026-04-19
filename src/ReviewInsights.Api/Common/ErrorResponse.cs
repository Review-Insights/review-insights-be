namespace ReviewInsights.Api.Common;

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;

    public static ErrorResponse From(string message) => new() { Error = message };
}

public class ApiException : Exception
{
    public int StatusCode { get; }

    public ApiException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}

public class ValidationException(string message) : ApiException(StatusCodes.Status400BadRequest, message);
public class NotFoundException(string message) : ApiException(StatusCodes.Status404NotFound, message);
public class UnprocessableEntityException(string message) : ApiException(StatusCodes.Status422UnprocessableEntity, message);
public class PayloadTooLargeException(string message) : ApiException(StatusCodes.Status413PayloadTooLarge, message);
public class UnsupportedMediaTypeException(string message) : ApiException(StatusCodes.Status415UnsupportedMediaType, message);
