using FactoryFlow.Application.Interfaces;
using FactoryFlow.Infrastructure.Persistence;
using FactoryFlow.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=FactoryFlowDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, b => b.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IAccountingService, AccountingService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        return services;
    }
}

