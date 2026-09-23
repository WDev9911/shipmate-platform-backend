using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ShipMate.Application.Exceptions;

namespace ShipMate.API.Middlewares;

public class GlobalExceptionHandler : IExceptionHandler
{
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

        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions["errors"] = validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }

        if (exception is AppException appException)
        {
            problemDetails.Extensions["errorCode"] = appException.ErrorCode;
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
        ValidationException e => (StatusCodes.Status400BadRequest, "Invalid data", e.Message),
        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred",
              "Something went wrong on our end. Please try again later.")
    };
}
