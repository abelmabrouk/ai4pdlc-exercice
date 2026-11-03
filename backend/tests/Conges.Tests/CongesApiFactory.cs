using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Conges.Tests;

/// <summary>
/// Démarre l'API en mémoire avec une base SQLite dédiée, recréée et seedée.
/// </summary>
public sealed class CongesApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"conges-tests-{Guid.NewGuid():N}.db");

    /// <summary>
    /// Date du jour imposée à l'API (09:00 UTC). Les données de démonstration se calent sur cette date.
    /// Sans valeur, l'horloge système est utilisée.
    /// </summary>
    public DateOnly? Today { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Conges", $"Data Source={_databasePath};Pooling=False");

        if (Today is { } today)
        {
            var now = new DateTimeOffset(today.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero);
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(new FixedTimeProvider(now)));
        }
    }

    public HttpClient CreateClientAs(int employeeId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", employeeId.ToString());
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        File.Delete(_databasePath);
    }
}
