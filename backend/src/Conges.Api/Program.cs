using System.Text.Json.Serialization;
using Conges.Api.Http;
using Conges.Application;
using Conges.Application.Common;
using Conges.Infrastructure;
using Conges.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    // Les erreurs de liaison du modèle (JSON invalide, valeur mal formée) passent par le gestionnaire global, comme
    // les erreurs FluentValidation : sinon [Produces("application/json")] réécrit leur type de contenu
    // (application/json au lieu de application/problem+json, ADR-003).
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = context =>
        throw new ValidationException(context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .SelectMany(entry => entry.Value!.Errors.Select(error => new ValidationFailure(
                entry.Key,
                string.IsNullOrEmpty(error.ErrorMessage) ? "Valeur invalide." : error.ErrorMessage)))));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HeaderCurrentUser>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

await app.Services.InitializeDatabaseAsync();

app.Run();

public partial class Program;
