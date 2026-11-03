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
    /// <summary>Horloge figée facultative (seed et règles dépendant de la date du jour).</summary>
    public DateTimeOffset? Now { get; init; }

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"conges-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Conges", $"Data Source={_databasePath};Pooling=False");
        if (Now is { } fixedNow)
        {
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(new FixedTimeProvider(fixedNow)));
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
