using DynamicUpdater.Host.Extensions;

var builder = WebApplication
    .CreateBuilder(args);

builder.Services
    .AddServices(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();

app.UseDynamicModuleMiddleware();

app.MapGet("/", () =>
{
    return "The Host is working!";
});

app.MapHealthChecks("/health");

await app.RunAsync();
