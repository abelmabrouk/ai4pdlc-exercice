using Conges.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Conges.Tests;

/// <summary>
/// Démarre l'API en mémoire avec une base SQLite dédiée, recréée et seedée.
/// </summary>
public sealed class CongesApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"conges-tests-{Guid.NewGuid():N}.db");

    /// <summary>Horloge à substituer au <see cref="TimeProvider"/> de l'application (facultatif).</summary>
    public TimeProvider? Clock { get; init; }

    /// <summary>Intercepteurs EF Core à ajouter au contexte de l'application (facultatif).</summary>
    public IInterceptor[] DbInterceptors { get; init; } = [];

    private string ConnectionString => $"Data Source={_databasePath};Pooling=False";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Conges", ConnectionString);

        builder.ConfigureTestServices(services =>
        {
            if (Clock is not null)
            {
                services.AddSingleton(Clock);
            }

            if (DbInterceptors.Length > 0)
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(o => o.UseSqlite(ConnectionString).AddInterceptors(DbInterceptors));
            }
        });
    }

    public HttpClient CreateClientAs(int employeeId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", employeeId.ToString());
        return client;
    }

    /// <summary>Prépare des données directement en base, dans un scope dédié.</summary>
    public async Task ArrangeAsync(Func<AppDbContext, Task> arrange)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await arrange(db);
        await db.SaveChangesAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        File.Delete(_databasePath);
    }
}
