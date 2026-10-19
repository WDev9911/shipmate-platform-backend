using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ShipMate.Application.Exceptions;

namespace ShipMate.API.Middlewares;

public class GlobalExceptionHandler : IExceptionHandler
{
    // Shared with the invalid-JSON response in Program.cs, so every 400 for bad input looks the same.
    public const string ValidationFailedCode = "VALIDATION_FAILED";
    public const string ValidationTitle = "Invalid data";
    public const string ValidationDetail = "One or more fields are invalid.";

    private const string NotFoundCode = "NOT_FOUND";

    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = MapException(exception);

        _logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        if (exception is NotFoundException)
        {
            problemDetails.Extensions["errorCode"] = NotFoundCode;
        }

        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions["errorCode"] = ValidationFailedCode;
            problemDetails.Extensions["errors"] = validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }

        if (exception is AppException appException)
        {
            problemDetails.Extensions["errorCode"] = appException.ErrorCode;

            if (appException.Details is not null)
            {
                problemDetails.Extensions["details"] = appException.Details;
            }
        }

        httpContext.Response.StatusCode = statusCode;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }

    // For known/expected exception types, the message is safe (and useful) to return as-is —
    // it was written for the client. For anything else, the real message might leak internal
    // details (which downstream service failed, connection strings, stack info...), so only
    // a generic detail goes to the client; the real exception.Message is still logged above.
    private static (int StatusCode, string Title, string Detail) MapException(Exception exception) => exception switch
    {
        NotFoundException e => (StatusCodes.Status404NotFound, "Resource not found", e.Message),
        AppException e => (e.StatusCode, "Bad request", e.Message),
        // Per-field messages are in "errors"; e.Message repeats them in one hard-to-read string.
        ValidationException => (StatusCodes.Status400BadRequest, ValidationTitle, ValidationDetail),
        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred",
              "Something went wrong on our end. Please try again later.")
    };
}
