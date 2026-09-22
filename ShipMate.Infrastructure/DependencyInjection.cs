using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipMate.Infrastructure.Persistence;

namespace ShipMate.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // TODO: thay UseInMemoryDatabase bằng UseSqlServer/UseNpgsql/... khi đã chọn DB provider,
        // và cài package Microsoft.EntityFrameworkCore.<Provider> tương ứng.
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(configuration.GetConnectionString("DefaultConnection") ?? "ShipMateDb"));

        return services;
    }
}
