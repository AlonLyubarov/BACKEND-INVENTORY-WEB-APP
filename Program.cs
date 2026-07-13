using Scalar.AspNetCore;
using AlonProject.Infrastructure.Data;
using AlonProject.Infrastructure.Email;
using AlonProject.Infrastructure.Repositories;
using AlonProject.Domain.Interfaces;
using AlonProject.Application.Interfaces;
using AlonProject.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

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

// Bootstrap logger for the pre-builder phase only; the real logger is
// configured from appsettings ("Serilog" section) once the host exists.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("=== Application startup ===");

    var builder = WebApplication.CreateBuilder(args);

    // Add Serilog to the host builder — levels and sinks come from configuration
    builder.Host.UseSerilog((context, services, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration));

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
    builder.Services.AddScoped<IReminderRepository, ReminderRepository>();
    builder.Services.AddScoped<IPersonalTaskRepository, PersonalTaskRepository>();
    builder.Services.AddScoped<IShiftRepository, ShiftRepository>();
    Log.Information("Repository dependencies registered");

    // Service dependency injection - Register business logic layer
    builder.Services.AddScoped<IProductCatalogService, ProductCatalogService>();
    builder.Services.AddScoped<IItemService, ItemService>();
    builder.Services.AddScoped<ITransactionService, TransactionService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IWarehouseService, WarehouseService>();
    builder.Services.AddScoped<IWarehouseAccessService, WarehouseAccessService>();
    builder.Services.AddScoped<IReminderService, ReminderService>();
    builder.Services.AddScoped<IPersonalTaskService, PersonalTaskService>();
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
    builder.Services.AddScoped<IRouteOptimizerService, RouteOptimizerService>();
    builder.Services.AddScoped<IShiftService, ShiftService>();
    Log.Information("Application services registered");

    // Geocoding proxy: named HttpClient with the User-Agent Nominatim's usage
    // policy requires, plus a bounded in-memory cache for repeated lookups.
    var geoContactEmail = builder.Configuration["Geo:ContactEmail"] ?? "contact@example.com";
    var geoUserAgent = $"AlonProject-Inventory/1.0 ({geoContactEmail})";
    builder.Services.AddMemoryCache(options => options.SizeLimit = 1024);
    builder.Services.AddHttpClient("nominatim", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(8);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(geoUserAgent);
    });
    builder.Services.AddHttpClient("osrm", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(geoUserAgent);
    });
    Log.Information("Geocoding and routing HTTP clients registered");

    // CORS configuration — allowed origins come from configuration
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
    if (allowedOrigins == null || allowedOrigins.Length == 0)
    {
        Log.Fatal("CRITICAL: Cors:AllowedOrigins is not configured");
        throw new InvalidOperationException(
            "Cors:AllowedOrigins must contain at least one origin (appsettings.json or Cors__AllowedOrigins__0 env var).");
    }

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAngularApp", policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    });
    Log.Information("CORS policy configured for origins: {Origins}", string.Join(", ", allowedOrigins));

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

    // Rate limiting — fixed windows partitioned by client IP.
    // "auth": login/register/verify-email; "email": resend-verification; "geo": geocode/route.
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsync(
                "{\"error\":\"Too many requests. Try again later.\"}", cancellationToken);
        };

        static string ClientIp(HttpContext httpContext) =>
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
            ClientIp(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

        options.AddPolicy("email", httpContext => RateLimitPartition.GetFixedWindowLimiter(
            ClientIp(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0
            }));

        options.AddPolicy("geo", httpContext => RateLimitPartition.GetFixedWindowLimiter(
            ClientIp(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    });
    Log.Information("Rate limiting configured (auth: 10/min, email: 3/10min, geo: 30/min per IP)");

    var app = builder.Build();

    // Apply EF migrations at startup so a fresh database (e.g. an empty SQL
    // Server container) is created and up to date. Retries because SQL Server
    // may take a few seconds to accept connections after the container starts.
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!db.Database.IsRelational())
        {
            // Integration tests swap in the EF in-memory provider, which has no
            // migrations — build the schema directly. Never used in production.
            db.Database.EnsureCreated();
            Log.Information("Non-relational database schema created (in-memory provider)");
        }
        else
        {
            for (var attempt = 1; attempt <= 12; attempt++)
            {
                try
                {
                    db.Database.Migrate();
                    Log.Information("Database migrations applied");
                    break;
                }
                catch (Exception ex) when (attempt < 12)
                {
                    Log.Warning("Database not ready (attempt {Attempt}/12): {Message}. Retrying in 5s...",
                        attempt, ex.Message);
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }
        }
    }

    Log.Information("=== Configuring HTTP request pipeline ===");

    // Behind a reverse proxy (nginx): trust its forwarded headers so the real
    // client IP (used by the rate limiter) and scheme are honored.
    var forwardedOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    };
    // Trust the reverse proxy inside the private compose network
    forwardedOptions.KnownIPNetworks.Clear();
    forwardedOptions.KnownProxies.Clear();
    app.UseForwardedHeaders(forwardedOptions);

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        Log.Information("Development environment detected - enabling OpenAPI documentation");
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    // In a container the app serves plain HTTP behind nginx (which terminates
    // TLS), so an HTTPS redirect here would break requests. The official .NET
    // images set DOTNET_RUNNING_IN_CONTAINER=true.
    var runningInContainer = builder.Configuration.GetValue<bool>("DOTNET_RUNNING_IN_CONTAINER");
    if (!app.Environment.IsDevelopment() && !runningInContainer)
    {
        app.UseHttpsRedirection();
    }

    // Enable CORS middleware - must come before UseAuthentication and UseAuthorization
    app.UseCors("AllowAngularApp");
    Log.Information("CORS middleware enabled");

    // Rate limiting middleware — policies applied per-endpoint via [EnableRateLimiting].
    // Skipped under the integration-test host so a test run's rapid auth calls are
    // not throttled; the [EnableRateLimiting] attributes are inert without this.
    if (!app.Environment.IsEnvironment("Testing"))
    {
        app.UseRateLimiter();
        Log.Information("Rate limiting middleware enabled");
    }

    // Authentication middleware - must come before UseAuthorization
    app.UseAuthentication();
    Log.Information("Authentication middleware enabled");

    app.UseAuthorization();
    app.MapControllers();

    Log.Information("=== Application fully configured and starting ===");
    await app.RunAsync();
}
// Let the host-control sentinels (thrown by WebApplicationFactory's HostFactoryResolver
// to stop the app right after the host is built, and by graceful shutdown) propagate —
// swallowing them here would break integration tests with "never built an IHost".
catch (Exception ex) when (ex.GetType().Name is not "StopTheHostException" and not "HostAbortedException")
{
    Log.Fatal(ex, "=== Application terminated unexpectedly ===");
}
finally
{
    Log.Information("=== Application shutdown ===");
    await Log.CloseAndFlushAsync();
}

// Exposes the implicit top-level Program class to the integration test project
// (WebApplicationFactory<Program>). Has no effect on normal execution.
public partial class Program { }