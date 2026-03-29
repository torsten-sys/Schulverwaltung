using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Schulverwaltung.Application.Services;
using Schulverwaltung.Domain.Interfaces;
using Schulverwaltung.Infrastructure.Repositories;
using Schulverwaltung.Infrastructure.Persistence;
using Schulverwaltung.Infrastructure.Services;

namespace Schulverwaltung.Infrastructure;

/// <summary>
/// Extension-Methode für IServiceCollection – registriert alle
/// Infrastructure- und Application-Dienste.
/// In Program.cs aufrufen: builder.Services.AddSchuleverwaltung(builder.Configuration);
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddSchuleverwaltung(
        this IServiceCollection services,
        IConfiguration          configuration)
    {
        // ── EF Core ─────────────────────────────────────────────────────────
        services.AddDbContext<SchulverwaltungDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("Schulverwaltung"),
                sql => sql.MigrationsAssembly(
                    typeof(SchulverwaltungDbContext).Assembly.FullName)
            )
        );

        // ── Unit of Work & Repositories ──────────────────────────────────────
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ── Application Services ─────────────────────────────────────────────
        services.AddScoped<IPersonRolleService, PersonRolleService>();
        services.AddScoped<MeisterkursService>();
        services.AddScoped<InternatService>();
        services.AddScoped<AppBenutzerService>();
        services.AddScoped<InventarService>();

        return services;
    }
}
