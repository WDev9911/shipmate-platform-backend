using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ShipMate.API.Filters;

/// <summary>
/// Runs FluentValidation automatically for any action argument that has a registered IValidator&lt;T&gt;,
/// so controllers don't need to call validators manually. Failures surface as a FluentValidation.ValidationException,
/// which GlobalExceptionHandler turns into a 400 ProblemDetails response.
/// </summary>
public class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is IValidator validator)
            {
                var validationContext = new ValidationContext<object>(argument);
                var result = await validator.ValidateAsync(validationContext);
                if (!result.IsValid)
                {
                    throw new ValidationException(result.Errors);
                }
            }
        }

        await next();
    }
}
