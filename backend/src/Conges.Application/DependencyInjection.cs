using Conges.Application.Employees;
using Conges.Application.LeaveRequests;
using Conges.Application.Teams;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Conges.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<IEmployeeQueries, EmployeeQueries>();
        services.AddScoped<ITeamQueries, TeamQueries>();
        services.AddScoped<ILeaveRequestQueries, LeaveRequestQueries>();

        return services;
    }
}
