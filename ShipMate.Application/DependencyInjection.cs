using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;

namespace ShipMate.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(cfg => { }, typeof(DependencyInjection).Assembly);
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
