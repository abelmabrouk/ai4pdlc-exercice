using Conges.Application.Common;
using Conges.Infrastructure.Calendar;
using Conges.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Conges.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>((sp, options) =>
            options.UseSqlite(sp.GetRequiredService<IConfiguration>().GetConnectionString("Conges")));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.Configure<HolidayCalendarOptions>(configuration.GetSection(HolidayCalendarOptions.SectionName));
        services.AddSingleton<IHolidayCalendarService, HolidayCalendarService>();

        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
