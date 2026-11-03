using System.Net.Http.Json;
using Conges.Application.Employees;
using Conges.Application.LeaveRequests;

namespace Conges.Tests;

/// <summary>
/// Base des tests d'API qui modifient l'état : une fabrique (donc une base fraîche) par test,
/// avec une horloge fixée par défaut au lundi 2027-03-01 à 09:00 UTC.
/// </summary>
public abstract class ApiTestBase : IDisposable
{
    private CongesApiFactory? _factory;

    protected TestClock Clock { get; } = TestClock.At("2027-03-01");

    protected CongesApiFactory Factory => _factory ??= new CongesApiFactory { Clock = Clock };

    protected HttpClient As(int employeeId) => Factory.CreateClientAs(employeeId);

    protected HttpClient Anonymous() => Factory.CreateClient();

    protected static async Task<LeaveRequestDto> ReadLeaveAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<LeaveRequestDto>(Json.Options))!;

    /// <summary>Solde CP courant d'un collaborateur, lu via l'API.</summary>
    protected async Task<int> BalanceOfAsync(int employeeId) =>
        (await Anonymous().GetFromJsonAsync<EmployeeDto>($"/api/employees/{employeeId}", Json.Options))!.PaidLeaveBalance;

    public void Dispose()
    {
        _factory?.Dispose();
        GC.SuppressFinalize(this);
    }
}
