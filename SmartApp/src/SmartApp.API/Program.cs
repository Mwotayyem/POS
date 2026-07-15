using SmartApp.API.Extensions;
using SmartApp.API.Middleware;
using SmartApp.Application;
using SmartApp.Infrastructure;
using SmartApp.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuration loading
// appsettings.json + appsettings.{Environment}.json + environment variables +
// (Development) user-secrets are loaded by the default host builder.
// Secrets (JWT key, connection string) come from user-secrets / env, never source.
// JwtSettings is bound inside AddInfrastructure (the layer that owns the JWT service).
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Composition Root — wire each layer's DI in dependency order.
// (See SmartApp-Architecture/02-Solution-Architecture.md §3 and 03-Project-Structure.md §7)
// ---------------------------------------------------------------------------
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddPersistence(builder.Configuration)
    .AddApiServices();

var app = builder.Build();

// ---------------------------------------------------------------------------
// Seed the global permission catalog (idempotent). Non-fatal: an unavailable/unmigrated database
// is logged and does not prevent startup (handled inside the extension).
// ---------------------------------------------------------------------------
await app.SeedPermissionCatalogAsync();

// ---------------------------------------------------------------------------
// Middleware pipeline — order is load-bearing.
// (See SmartApp-Architecture/02-Solution-Architecture.md §5 and 03-Project-Structure.md §7)
// ---------------------------------------------------------------------------

// 1) Global exception handling — first, so it wraps everything below.
app.UseGlobalExceptionHandling();

// 2) Swagger — non-production only (A05: no API surface exposure in prod).
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// 3) AuthN -> TenantResolution -> AuthZ.
// TenantResolution runs after authentication so it can read the tenant claim, and before
// authorization so an inactive tenant is rejected early.
app.UseAuthentication();
app.UseTenantResolution();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory-based integration tests (SmartApp.IntegrationTests).
public partial class Program { }
