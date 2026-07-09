using AlonProject.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AlonProject.Infrastructure;

/// <summary>
/// Design-time DbContext factory for EF Core tooling (migrations, scaffolding).
/// Provides a way for dotnet ef commands to create AppDbContext without running the full application.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=AlonInventoryDb;Trusted_Connection=True;TrustServerCertificate=True;";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
