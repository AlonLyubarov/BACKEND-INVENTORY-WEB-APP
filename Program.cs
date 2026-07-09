using Scalar.AspNetCore;
using AlonProject.Infrastructure.Data;
using AlonProject.Infrastructure.Repositories;
using AlonProject.Domain.Interfaces;
using AlonProject.Application.Interfaces;
using AlonProject.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using System.Text;
using System.Text.Json.Serialization;

// SECURITY: Load environment variables from .env file (if present)
// For development: copy .env.example to .env and fill in values
// For production: set environment variables via deployment platform
if (File.Exists(".env"))
{
    Console.WriteLine("[STARTUP] Found .env file, loading environment variables...");
    var lines = File.ReadAllLines(".env");
    int loadedCount = 0;
    foreach (var line in lines)
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
            continue;

        var parts = line.Split('=', 2);
        if (parts.Length == 2)
        {
            var key = parts[0].Trim();
            var value = parts[1].Trim();
            // Set environment variable if not already set (don't override existing vars)
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
                loadedCount++;
                Console.WriteLine($"[STARTUP] Set {key}");
            }
        }
    }
    Console.WriteLine($"[STARTUP] Loaded {loadedCount} environment variables from .env");
}
else
{
    Console.WriteLine("[STARTUP] No .env file found");
}

// Configure Serilog for comprehensive application logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/alonproject-.txt", rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("=== Application startup ===");

    var builder = WebApplication.CreateBuilder(args);

    // Add Serilog to the host builder
    builder.Host.UseSerilog();

    // Add services to the container
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
    builder.Services.AddOpenApi();
    Log.Information("Web API services registered");

    // Database configuration - Establish SQL Server connection
    var connectionString = builder.Configuration.GetConnectionString("SqlServer");

    // SECURITY: Fail fast if connection string is not configured
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        Log.Fatal("CRITICAL: Database connection string 'SqlServer' is not configured");
        throw new InvalidOperationException(
            "Database connection string 'SqlServer' must be configured in appsettings.json or environment variables. " +
            "Use: appsettings.json → ConnectionStrings.SqlServer or environment variable ConnectionStrings__SqlServer");
    }

    Log.Information("Database connection string configured: {ConnectionString}", 
        connectionString.Substring(0, Math.Min(30, connectionString.Length)) + "***");

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(connectionString));
    Log.Information("DbContext registered with SQL Server provider");

    // Repository dependency injection - Register data access layer
    builder.Services.AddScoped<IProductCatalogRepository, ProductCatalogRepository>();
    builder.Services.AddScoped<IItemRepository, ItemRepository>();
    builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<IWarehouseRepository, WarehouseRepository>();
    Log.Information("Repository dependencies registered");

    // Service dependency injection - Register business logic layer
    builder.Services.AddScoped<IProductCatalogService, ProductCatalogService>();
    builder.Services.AddScoped<IItemService, ItemService>();
    builder.Services.AddScoped<ITransactionService, TransactionService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IWarehouseService, WarehouseService>();
    builder.Services.AddScoped<IWarehouseAccessService, WarehouseAccessService>();
    Log.Information("Application services registered");

    // CORS configuration - Allow Angular frontend on port 4200
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAngularApp", policy =>
        {
            policy.WithOrigins("http://localhost:4200")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    });
    Log.Information("CORS policy configured for Angular frontend");

    // JWT Authentication configuration
    var jwtSettings = builder.Configuration.GetSection("Jwt");
    var jwtKey = jwtSettings["Key"];

    // SECURITY: Fail fast if JWT Key is not configured or too short
    if (string.IsNullOrWhiteSpace(jwtKey))
    {
        Log.Fatal("SECURITY CRITICAL: JWT Key is not configured. Use 'dotnet user-secrets set Jwt:Key <long-secret-key>'");
        throw new InvalidOperationException(
            "JWT Key must be configured. In development, use: dotnet user-secrets set Jwt:Key \"<key-minimum-32-chars>\"");
    }

    if (jwtKey.Length < 32)
    {
        Log.Fatal("SECURITY CRITICAL: JWT Key is too short. Must be at least 32 characters, got {KeyLength}", jwtKey.Length);
        throw new InvalidOperationException(
            "JWT Key must be at least 32 characters long for secure HMAC-SHA256 signing. Use: dotnet user-secrets set Jwt:Key \"<key-minimum-32-chars>\"");
    }

    var key = Encoding.UTF8.GetBytes(jwtKey);
    Log.Information("JWT Key configured successfully ({KeyLength} chars)", jwtKey.Length);

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });
    Log.Information("JWT Bearer authentication configured");

    var app = builder.Build();

    Log.Information("=== Configuring HTTP request pipeline ===");

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        Log.Information("Development environment detected - enabling OpenAPI documentation");
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    // Enable CORS middleware - must come before UseAuthentication and UseAuthorization
    app.UseCors("AllowAngularApp");
    Log.Information("CORS middleware enabled");

    // Authentication middleware - must come before UseAuthorization
    app.UseAuthentication();
    Log.Information("Authentication middleware enabled");

    app.UseAuthorization();
    app.MapControllers();

    Log.Information("=== Application fully configured and starting ===");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "=== Application terminated unexpectedly ===");
}
finally
{
    Log.Information("=== Application shutdown ===");
    await Log.CloseAndFlushAsync();
}