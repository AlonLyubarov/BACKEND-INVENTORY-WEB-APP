using AlonProject.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace AlonProject.Tests.Integration;

/// <summary>
/// Boots the real API in-process (all middleware, routing, auth, model validation)
/// but swaps SQL Server for the EF in-memory provider and supplies test configuration.
/// The "Testing" environment turns off the rate limiter (see Program.cs) so a test
/// run's rapid auth calls are not throttled. Email stays in log-only mode.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"IntegrationTests-{Guid.NewGuid()}";

    static CustomWebApplicationFactory()
    {
        // Injected as environment variables (":" → "__") so they override the empty
        // Jwt:Key / missing keys in appsettings.json. Environment variables sit ABOVE
        // appsettings in the default configuration order, so the fail-fast startup
        // checks (connection string, JWT key length, CORS origin) all pass. The real
        // DbContext is replaced below, so the connection string is never used to connect.
        Environment.SetEnvironmentVariable("ConnectionStrings__SqlServer", "Server=(inmemory);Database=Test;");
        Environment.SetEnvironmentVariable("Jwt__Key", "integration-test-signing-key-of-at-least-32-chars!!");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "TestIssuer");
        Environment.SetEnvironmentVariable("Jwt__Audience", "TestAudience");
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", "http://localhost");
        Environment.SetEnvironmentVariable("Auth__RequireEmailVerification", "false");
        Environment.SetEnvironmentVariable("Email__Host", ""); // log-only, never sends
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Drop every SQL Server DbContext registration. EF Core 10 also registers an
            // IDbContextOptionsConfiguration<AppDbContext> that carries the UseSqlServer
            // call; leaving it in place would register TWO providers on the same context
            // and EF throws a provider-conflict InvalidOperationException at startup.
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(AppDbContext) ||
                (d.ServiceType.FullName?.StartsWith(
                    "Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration") ?? false)).ToList();
            foreach (var descriptor in toRemove)
                services.Remove(descriptor);

            // One shared in-memory database per factory instance (isolated across test classes).
            // The repositories wrap owner creation / deletion in explicit transactions, which
            // the in-memory store cannot honor — ignore the warning so those calls no-op cleanly.
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName)
                       .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        });
    }
}
