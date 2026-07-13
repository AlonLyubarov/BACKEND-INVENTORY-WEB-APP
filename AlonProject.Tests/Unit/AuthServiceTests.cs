using System.Security.Cryptography;
using System.Text;
using AlonProject.Application.DTOs;
using AlonProject.Application.Services;
using AlonProject.Domain.Entities;
using AlonProject.Domain.Enums;
using AlonProject.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using BC = BCrypt.Net.BCrypt;

namespace AlonProject.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="AuthService"/> — registration, login, the
/// configurable email-verification gate, invitations, and email verification.
/// Repositories and the email sender are substituted; configuration is real
/// (built in-memory) so the JWT and Auth flags behave exactly as in production.
/// </summary>
public class AuthServiceTests
{
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IWarehouseRepository _whRepo = Substitute.For<IWarehouseRepository>();
    private readonly IProductCatalogRepository _catalogRepo = Substitute.For<IProductCatalogRepository>();
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();

    private static IConfiguration Config(bool requireVerification) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "unit-test-signing-key-of-at-least-32-chars!!",
            ["Jwt:Issuer"] = "TestIssuer",
            ["Jwt:Audience"] = "TestAudience",
            ["Auth:RequireEmailVerification"] = requireVerification ? "true" : "false",
        }).Build();

    private AuthService NewService(bool requireVerification = true) =>
        new(_userRepo, _whRepo, _catalogRepo, Config(requireVerification), _email,
            NullLogger<AuthService>.Instance);

    private static RegisterUserDto ValidRegistration() => new()
    {
        Username = "alice",
        Email = "alice@example.com",
        Password = "Str0ng_Pass!",
        WarehouseName = "Main",
        WarehouseLocation = "Tel Aviv",
        WarehouseLatitude = 32.08,
        WarehouseLongitude = 34.78
    };

    private User StoredUser(string username, string password, bool verified) => new()
    {
        Id = 1,
        Username = username,
        Email = $"{username}@example.com",
        PasswordHash = BC.HashPassword(password),
        Role = UserRole.Admin,
        EmailVerified = verified
    };

    // ── Registration ────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterAsync_CreatesAdminOwner_HashesPassword_AndSendsEmail()
    {
        User? captured = null;
        _userRepo.GetByUsernameAsync(Arg.Any<string>()).Returns((User?)null);
        _userRepo.GetByEmailAsync(Arg.Any<string>()).Returns((User?)null);
        _userRepo.CreateOwnerWithWarehouseAsync(Arg.Any<User>(), Arg.Any<Warehouse>())
            .Returns(ci =>
            {
                captured = ci.Arg<User>()!;
                captured.Id = 1;
                var w = ci.Arg<Warehouse>()!;
                w.Id = 2;
                return (captured, w);
            });

        var dto = await NewService().RegisterAsync(ValidRegistration());

        Assert.Equal(UserRole.Admin, dto.Role);
        Assert.Equal(2, dto.WarehouseId);
        // Password is never stored in the clear, but verifies against the hash
        Assert.NotNull(captured);
        Assert.NotEqual("Str0ng_Pass!", captured!.PasswordHash);
        Assert.True(BC.Verify("Str0ng_Pass!", captured.PasswordHash));
        // Self-registered owners start unverified and receive a verification email
        Assert.False(captured.EmailVerified);
        await _email.Received(1).SendAsync(dto.Email, Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task RegisterAsync_DuplicateUsername_Throws()
    {
        _userRepo.GetByUsernameAsync("alice").Returns(StoredUser("alice", "x", true));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewService().RegisterAsync(ValidRegistration()));
        await _userRepo.DidNotReceive().CreateOwnerWithWarehouseAsync(Arg.Any<User>(), Arg.Any<Warehouse>());
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_Throws()
    {
        _userRepo.GetByUsernameAsync(Arg.Any<string>()).Returns((User?)null);
        _userRepo.GetByEmailAsync("alice@example.com").Returns(StoredUser("bob", "x", true));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewService().RegisterAsync(ValidRegistration()));
    }

    // ── Login + the email-verification gate ──────────────────────────────────

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsUnauthorized()
    {
        _userRepo.GetByUsernameAsync("alice").Returns(StoredUser("alice", "correct", true));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            NewService().LoginAsync(new LoginDto { Username = "alice", Password = "wrong" }));
    }

    [Fact]
    public async Task LoginAsync_UnknownUser_ThrowsUnauthorized()
    {
        _userRepo.GetByUsernameAsync("ghost").Returns((User?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            NewService().LoginAsync(new LoginDto { Username = "ghost", Password = "x" }));
    }

    [Fact]
    public async Task LoginAsync_UnverifiedEmail_Blocked_WhenVerificationRequired()
    {
        _userRepo.GetByUsernameAsync("alice").Returns(StoredUser("alice", "secret", verified: false));

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            NewService(requireVerification: true)
                .LoginAsync(new LoginDto { Username = "alice", Password = "secret" }));
        Assert.Contains("not verified", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginAsync_UnverifiedEmail_Allowed_WhenVerificationDisabled()
    {
        _userRepo.GetByUsernameAsync("alice").Returns(StoredUser("alice", "secret", verified: false));

        // The config flag we added lets a log-only/demo environment skip the gate
        var result = await NewService(requireVerification: false)
            .LoginAsync(new LoginDto { Username = "alice", Password = "secret" });

        Assert.False(string.IsNullOrEmpty(result.Token));
        Assert.Equal("alice", result.Username);
    }

    [Fact]
    public async Task LoginAsync_VerifiedUser_ReturnsJwt()
    {
        _userRepo.GetByUsernameAsync("alice").Returns(StoredUser("alice", "secret", verified: true));

        var result = await NewService()
            .LoginAsync(new LoginDto { Username = "alice", Password = "secret" });

        Assert.False(string.IsNullOrEmpty(result.Token));
        Assert.Equal("Admin", result.Role);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
    }

    // ── Invitations ──────────────────────────────────────────────────────────

    [Fact]
    public async Task InviteUserAsync_AdminRole_IsRejected()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewService().InviteUserAsync(new InviteUserDto
            {
                Username = "eve", Email = "eve@example.com", Password = "P@ssw0rd1", Role = UserRole.Admin
            }, warehouseId: 2));
    }

    [Fact]
    public async Task InviteUserAsync_CreatesVerifiedEmployee_AssignedToWarehouse()
    {
        User? created = null;
        _userRepo.GetByUsernameAsync(Arg.Any<string>()).Returns((User?)null);
        _userRepo.GetByEmailAsync(Arg.Any<string>()).Returns((User?)null);
        _userRepo.CreateAsync(Arg.Any<User>()).Returns(ci =>
        {
            created = ci.Arg<User>()!;
            created.Id = 7;
            return created;
        });

        await NewService().InviteUserAsync(new InviteUserDto
        {
            Username = "eve", Email = "eve@example.com", Password = "P@ssw0rd1", Role = UserRole.Employee
        }, warehouseId: 2);

        Assert.NotNull(created);
        Assert.True(created!.EmailVerified);        // invited accounts start verified
        Assert.Equal(2, created.WarehouseId);
        Assert.Equal(UserRole.Employee, created.Role);
    }

    // ── Email verification ───────────────────────────────────────────────────

    [Fact]
    public async Task VerifyEmailAsync_ValidToken_MarksVerified_AndClearsToken()
    {
        var raw = "RAW-TOKEN-123";
        var user = StoredUser("alice", "x", verified: false);
        user.EmailVerificationToken = Hash(raw);
        user.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(1);
        _userRepo.GetByVerificationTokenAsync(Hash(raw)).Returns(user);

        await NewService().VerifyEmailAsync(raw);

        Assert.True(user.EmailVerified);
        Assert.Null(user.EmailVerificationToken);
        await _userRepo.Received(1).UpdateAsync(user);
    }

    [Fact]
    public async Task VerifyEmailAsync_ExpiredToken_Throws()
    {
        var raw = "RAW-TOKEN-123";
        var user = StoredUser("alice", "x", verified: false);
        user.EmailVerificationToken = Hash(raw);
        user.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(-1); // expired
        _userRepo.GetByVerificationTokenAsync(Hash(raw)).Returns(user);

        await Assert.ThrowsAsync<InvalidOperationException>(() => NewService().VerifyEmailAsync(raw));
        Assert.False(user.EmailVerified);
    }

    [Fact]
    public async Task VerifyEmailAsync_UnknownToken_Throws()
    {
        _userRepo.GetByVerificationTokenAsync(Arg.Any<string>()).Returns((User?)null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => NewService().VerifyEmailAsync("nope"));
    }

    // ── Account deletion guard ───────────────────────────────────────────────

    [Fact]
    public async Task DeleteAccountAsync_WrongPassword_Throws_AndDoesNotDelete()
    {
        _userRepo.GetByIdAsync(1).Returns(StoredUser("alice", "correct", true));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewService().DeleteAccountAsync(userId: 1, password: "wrong"));
        await _userRepo.DidNotReceive().DeleteAsync(Arg.Any<int>());
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
