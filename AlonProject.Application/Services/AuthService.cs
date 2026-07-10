using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AlonProject.Application.DTOs;
using AlonProject.Application.Interfaces;
using AlonProject.Domain.Entities;
using AlonProject.Domain.Enums;
using AlonProject.Domain.Interfaces;
using BC = BCrypt.Net.BCrypt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace AlonProject.Application.Services;

/// <summary>
/// Authentication service implementing JWT token generation and user management.
/// Handles registration, login, password hashing with BCrypt, and JWT token creation.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Registers a new warehouse OWNER account.
    /// Validates username uniqueness, hashes password using BCrypt, then atomically creates:
    /// 1. The User with Role = Admin (owner)
    /// 2. Their main Warehouse (OwnerId = user.Id, ParentWarehouseId = null)
    /// Both inserts run in a single database transaction.
    /// SECURITY: Role is decided by the server — the client cannot influence it.
    /// </summary>
    /// <param name="dto">Registration details (username, email, password, warehouse name/location)</param>
    /// <returns>UserDto including the created warehouse id/name</returns>
    /// <exception cref="InvalidOperationException">Username already exists</exception>
    public async Task<UserDto> RegisterAsync(RegisterUserDto dto)
    {
        _logger.LogInformation("Auth Request: Register new owner with username: {Username}, email: {Email}, warehouse: {WarehouseName}",
            dto.Username, dto.Email, dto.WarehouseName);

        // Check if username already exists
        var existingUser = await _userRepository.GetByUsernameAsync(dto.Username);
        if (existingUser != null)
        {
            _logger.LogWarning("Auth Register Failed: Username already exists - {Username}", dto.Username);
            throw new InvalidOperationException($"Username '{dto.Username}' is already taken.");
        }

        try
        {
            // Hash password using BCrypt (never store plain text)
            var passwordHash = BC.HashPassword(dto.Password);

            // Create the owner user entity
            // SECURITY: Registration always creates an Admin OWNER (server-decided).
            // Employees are added later via the owner's invitation endpoint.
            var user = new User
            {
                Username = dto.Username,
                Email = dto.Email,
                PasswordHash = passwordHash,
                Role = UserRole.Admin,
                WarehouseId = null  // Owners are linked via Warehouse.OwnerId, not User.WarehouseId
            };

            // The owner's main warehouse (OwnerId is set inside the atomic create).
            // Coordinates are [Required] on the DTO — main warehouses always carry
            // a real map location so routes between them can be computed.
            var warehouse = new Warehouse
            {
                Name = dto.WarehouseName,
                Location = dto.WarehouseLocation,
                Latitude = dto.WarehouseLatitude,
                Longitude = dto.WarehouseLongitude,
                ParentWarehouseId = null
            };

            // Persist both atomically (single DB transaction)
            var (createdUser, createdWarehouse) = await _userRepository.CreateOwnerWithWarehouseAsync(user, warehouse);

            _logger.LogInformation("Auth Register Success: Owner created - ID: {UserId}, Username: {Username}, Role: Admin, Main warehouse: {WarehouseId} ({WarehouseName})",
                createdUser.Id, createdUser.Username, createdWarehouse.Id, createdWarehouse.Name);

            // Return as UserDto (never include PasswordHash in response)
            return new UserDto
            {
                Id = createdUser.Id,
                Username = createdUser.Username,
                Email = createdUser.Email,
                Role = createdUser.Role,
                WarehouseId = createdWarehouse.Id,
                WarehouseName = createdWarehouse.Name
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auth Register Error: Unexpected error during registration for username: {Username}",
                dto.Username);
            throw;
        }
    }

    /// <summary>
    /// Invites (creates) a new Employee/ShiftManager user into a main warehouse.
    /// Called by the warehouse owner via the invite endpoint.
    /// SECURITY: Role is restricted to Employee/ShiftManager — Admin invitations are rejected.
    /// </summary>
    public async Task<UserDto> InviteUserAsync(InviteUserDto dto, int warehouseId)
    {
        _logger.LogInformation("Auth Request: Invite user {Username} as {Role} to warehouse {WarehouseId}",
            dto.Username, dto.Role, warehouseId);

        // SECURITY: An owner cannot create another Admin via invitation
        if (dto.Role == UserRole.Admin)
        {
            _logger.LogWarning("Auth Invite Failed: Attempt to invite user with Admin role - {Username}", dto.Username);
            throw new InvalidOperationException("Cannot invite a user with the Admin role. Only Employee or ShiftManager roles are allowed.");
        }

        // Check if username already exists
        var existingUser = await _userRepository.GetByUsernameAsync(dto.Username);
        if (existingUser != null)
        {
            _logger.LogWarning("Auth Invite Failed: Username already exists - {Username}", dto.Username);
            throw new InvalidOperationException($"Username '{dto.Username}' is already taken.");
        }

        var passwordHash = BC.HashPassword(dto.Password);

        var user = new User
        {
            Username = dto.Username,
            Email = dto.Email,
            PasswordHash = passwordHash,
            Role = dto.Role,
            WarehouseId = warehouseId  // Assigned directly to the owner's main warehouse
        };

        var created = await _userRepository.CreateAsync(user);

        _logger.LogInformation("Auth Invite Success: User created - ID: {UserId}, Username: {Username}, Role: {Role}, Warehouse: {WarehouseId}",
            created.Id, created.Username, created.Role, warehouseId);

        return new UserDto
        {
            Id = created.Id,
            Username = created.Username,
            Email = created.Email,
            Role = created.Role,
            WarehouseId = created.WarehouseId
        };
    }

    /// <summary>
    /// Authenticates a user and generates a JWT Bearer token.
    /// Verifies username and password, creates JWT with 1-hour expiration.
    /// </summary>
    /// <param name="dto">Login credentials (username, password)</param>
    /// <returns>AuthResponseDto with JWT token and user information</returns>
    /// <exception cref="UnauthorizedAccessException">Invalid username or password</exception>
    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        _logger.LogInformation("Auth Request: Login attempt for username: {Username}", dto.Username);

        // Look up user by username
        var user = await _userRepository.GetByUsernameAsync(dto.Username);
        if (user == null)
        {
            _logger.LogWarning("Auth Login Failed: User not found - {Username}", dto.Username);
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        // Verify password using BCrypt
        var isPasswordValid = BC.Verify(dto.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            _logger.LogWarning("Auth Login Failed: Invalid password for username: {Username}", dto.Username);
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        try
        {
            // Generate JWT token
            var token = GenerateJwtToken(user);

            _logger.LogInformation("Auth Login Success: User authenticated - ID: {UserId}, Username: {Username}, Role: {Role}",
                user.Id, user.Username, user.Role);

            // Return authentication response
            var expiresAt = DateTime.UtcNow.AddHours(1);
            return new AuthResponseDto
            {
                Token = token,
                Username = user.Username,
                Role = user.Role.ToString(),
                ExpiresAt = expiresAt,
                UserId = user.Id,
                WarehouseId = user.WarehouseId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auth Login Error: Unexpected error during authentication for username: {Username}",
                dto.Username);
            throw;
        }
    }

    /// <summary>
    /// Generates a JWT Bearer token with user claims.
    /// Token expires in 1 hour.
    /// Claims include: NameIdentifier (user ID), Name (username), Role
    /// </summary>
    /// <param name="user">Authenticated user</param>
    /// <returns>JWT token string</returns>
    private string GenerateJwtToken(User user)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = jwtSettings["Key"];
        var issuer = jwtSettings["Issuer"];
        var audience = jwtSettings["Audience"];

        // SECURITY: Validate JWT configuration at token generation time (defense in depth)
        if (string.IsNullOrEmpty(key))
        {
            _logger.LogError("SECURITY: JWT Key is missing at token generation time");
            throw new InvalidOperationException("JWT Key is not configured. Cannot generate tokens.");
        }

        if (key.Length < 32)
        {
            _logger.LogError("SECURITY: JWT Key is too short ({KeyLength} chars), minimum required is 32 chars", key.Length);
            throw new InvalidOperationException("JWT Key is too short. Cannot generate secure tokens.");
        }

        if (string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(audience))
        {
            _logger.LogError("JWT configuration is missing Issuer or Audience");
            throw new InvalidOperationException("JWT configuration is incomplete (missing Issuer or Audience).");
        }

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        // Create claims with user information
        // WarehouseId can be null at registration; only include in claim if assigned
        var claimsList = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        if (user.WarehouseId.HasValue)
        {
            claimsList.Add(new Claim("WarehouseId", user.WarehouseId.Value.ToString()));
        }

        var claims = claimsList.ToArray();

        // Token expires in 1 hour
        var expiresAt = DateTime.UtcNow.AddHours(1);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        _logger.LogDebug("JWT token generated for user {UserId} with {ClaimCount} claims, expires at {ExpiresAt}", user.Id, claims.Length, expiresAt);

        var tokenHandler = new JwtSecurityTokenHandler();
        return tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Public method to generate JWT token for an existing user.
    /// Used when role or warehouse assignment changes to refresh the token with new claims.
    /// SECURITY: This prevents stale claims that would block user access to newly assigned resources.
    /// </summary>
    public async Task<string> GenerateJwtTokenAsync(User user)
    {
        // Generate token synchronously (the private GenerateJwtToken does not require async)
        var token = GenerateJwtToken(user);
        _logger.LogInformation("Refreshed JWT token for user {UserId} after privilege/warehouse change", user.Id);
        return await Task.FromResult(token);
    }
}
