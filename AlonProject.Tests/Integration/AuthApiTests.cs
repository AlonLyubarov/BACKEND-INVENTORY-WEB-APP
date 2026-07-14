using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AlonProject.Tests.Integration;

/// <summary>
/// End-to-end HTTP tests against the real API pipeline (routing, model validation,
/// JWT auth, controllers, services) backed by an in-memory database. These exercise
/// the register → login → authenticated-request flow exactly as the browser would.
/// </summary>
public class AuthApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static object Registration(string suffix) => new
    {
        username = $"owner_{suffix}",
        email = $"owner_{suffix}@example.com",
        password = "Str0ng_Pass!",
        warehouseName = "Main WH",
        warehouseLocation = "Tel Aviv",
        warehouseLatitude = 32.0853,
        warehouseLongitude = 34.7818
    };

    [Fact]
    public async Task Register_ValidOwner_Returns201_WithAdminUser()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", Registration("reg1"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("owner_reg1", body.GetProperty("username").GetString());
        Assert.Equal("Admin", body.GetProperty("role").GetString());
        Assert.True(body.GetProperty("warehouseId").GetInt32() > 0);
    }

    [Fact]
    public async Task Register_DuplicateUsername_Returns400()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", Registration("dup"));

        var second = await client.PostAsJsonAsync("/api/auth/register", Registration("dup"));

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Register_MissingCoordinates_Returns400_FromModelValidation()
    {
        var client = _factory.CreateClient();

        // warehouseLatitude/Longitude are [Required] — omitting them trips model validation
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "owner_novalid",
            email = "owner_novalid@example.com",
            password = "Str0ng_Pass!",
            warehouseName = "Main",
            warehouseLocation = "Tel Aviv"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", Registration("wrongpw"));

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "owner_wrongpw",
            password = "not-the-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_AfterRegister_ReturnsJwtToken()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", Registration("login1"));

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "owner_login1",
            password = "Str0ng_Pass!"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("token").GetString()));
        Assert.Equal("Admin", body.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Refresh_WithValidRefreshToken_ReturnsNewTokens()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", Registration("refresh1"));
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "owner_refresh1",
            password = "Str0ng_Pass!"
        });
        var loginBody = await login.Content.ReadFromJsonAsync<JsonElement>();
        var refreshToken = loginBody.GetProperty("refreshToken").GetString();

        var response = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("token").GetString()));
        // Rotation: the new refresh token differs from the one we sent
        Assert.NotEqual(refreshToken, body.GetProperty("refreshToken").GetString());
    }

    [Fact]
    public async Task Refresh_WithRotatedToken_IsRejected()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", Registration("refresh2"));
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "owner_refresh2",
            password = "Str0ng_Pass!"
        });
        var refreshToken = (await login.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("refreshToken").GetString();

        // First refresh consumes (rotates) the token
        await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
        // Reusing the same (now-revoked) token must fail
        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/warehouse");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithToken_Returns200()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", Registration("flow"));
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "owner_flow",
            password = "Str0ng_Pass!"
        });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("token").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/warehouse");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
