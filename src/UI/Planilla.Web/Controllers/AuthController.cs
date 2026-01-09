using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Vorluno.Planilla.Application.DTOs.Auth;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Domain.Models;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Web.Controllers;

/// <summary>
/// Controller de autenticación con JWT y gestión de multi-tenancy
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Registra un nuevo usuario y crea su tenant con suscripción de prueba
    /// POST /api/auth/register
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Verificar que el email no exista
        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser != null)
        {
            return BadRequest(new { message = "El email ya está registrado" });
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // 1. Crear usuario en Identity
            var user = new AppUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                EmailConfirmed = false,  // TODO: Implementar confirmación por email
                NombreCompleto = dto.CompanyName
            };

            var createResult = await _userManager.CreateAsync(user, dto.Password);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                return BadRequest(new { message = "Error al crear usuario", errors });
            }

            // 2. Generar subdomain único basado en el nombre de la empresa
            var subdomain = GenerateUniqueSubdomain(dto.CompanyName);

            // 3. Crear Tenant
            var tenant = new Tenant
            {
                Name = dto.CompanyName,
                Subdomain = subdomain,
                RUC = dto.RUC,
                DV = dto.DV,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Tenants.Add(tenant);
            await _context.SaveChangesAsync();

            // 4. Crear Subscription (Professional con 14 días de prueba)
            var trialEndsAt = DateTime.UtcNow.AddDays(14);
            var limits = PlanFeatures.GetLimits(SubscriptionPlan.Professional);

            var subscription = new Subscription
            {
                TenantId = tenant.Id,
                Plan = SubscriptionPlan.Professional,
                Status = SubscriptionStatus.Trialing,
                StartDate = DateTime.UtcNow,
                TrialEndsAt = trialEndsAt,
                MonthlyPrice = limits.PricePerMonth,
                CreatedAt = DateTime.UtcNow
            };

            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            // Asociar subscription al tenant
            tenant.SubscriptionId = subscription.Id;
            _context.Tenants.Update(tenant);
            await _context.SaveChangesAsync();

            // 5. Crear TenantUser con rol Owner
            var tenantUser = new TenantUser
            {
                TenantId = tenant.Id,
                UserId = user.Id,
                Role = TenantRole.Owner,
                IsActive = true,
                JoinedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.TenantUsers.Add(tenantUser);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            _logger.LogInformation("New tenant registered: {TenantId} ({TenantName}) by user {UserId}",
                tenant.Id, tenant.Name, user.Id);

            // 6. Generar JWT
            var token = GenerateJwtToken(user, tenant, tenantUser);

            // 7. Construir respuesta
            var response = new AuthResponseDto
            {
                Token = token.Token,
                ExpiresAt = token.ExpiresAt,
                User = new UserInfoDto
                {
                    UserId = user.Id,
                    Email = user.Email!,
                    Role = TenantRole.Owner,
                    RoleName = "Owner"
                },
                Tenant = new TenantInfoDto
                {
                    Id = tenant.Id,
                    Name = tenant.Name,
                    Subdomain = tenant.Subdomain,
                    RUC = tenant.RUC,
                    DV = tenant.DV
                },
                Subscription = new SubscriptionInfoDto
                {
                    Plan = subscription.Plan,
                    PlanName = subscription.Plan.ToString(),
                    Status = subscription.Status,
                    StatusName = subscription.Status.ToString(),
                    TrialEndsAt = subscription.TrialEndsAt,
                    MaxEmployees = subscription.GetEffectiveMaxEmployees(),
                    MaxUsers = subscription.GetEffectiveMaxUsers(),
                    MaxCompanies = limits.MaxCompanies,
                    CanExportExcel = limits.CanExportExcel,
                    CanExportPdf = limits.CanExportPdf,
                    CanUseApi = limits.CanUseApi,
                    MonthlyPrice = subscription.MonthlyPrice
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error registering new tenant: {Email}", dto.Email);
            return StatusCode(500, new { message = "Error al registrar. Por favor, intente nuevamente." });
        }
    }

    /// <summary>
    /// Inicia sesión y devuelve JWT con información del tenant
    /// POST /api/auth/login
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            // 1. Buscar usuario
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
            {
                return Unauthorized(new { message = "Credenciales inválidas" });
            }

            // 2. Verificar contraseña
            var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                if (result.IsLockedOut)
                {
                    return Unauthorized(new { message = "Cuenta bloqueada. Intente más tarde." });
                }
                return Unauthorized(new { message = "Credenciales inválidas" });
            }

            // 3. Obtener primer tenant activo del usuario
            var tenantUser = await _context.TenantUsers
                .Include(tu => tu.Tenant)
                    .ThenInclude(t => t.Subscription)
                .Where(tu => tu.UserId == user.Id && tu.IsActive && tu.Tenant.IsActive)
                .OrderBy(tu => tu.JoinedAt)  // Primer tenant que se unió
                .FirstOrDefaultAsync();

            if (tenantUser == null)
            {
                return Unauthorized(new { message = "No tienes acceso a ninguna empresa activa" });
            }

            // 4. Actualizar último login
            tenantUser.LastLoginAt = DateTime.UtcNow;
            _context.TenantUsers.Update(tenantUser);
            await _context.SaveChangesAsync();

            // 5. Generar JWT
            var token = GenerateJwtToken(user, tenantUser.Tenant, tenantUser);

            // 6. Obtener límites del plan
            var limits = PlanFeatures.GetLimits(tenantUser.Tenant.Subscription.Plan);

            // 7. Construir respuesta
            var response = new AuthResponseDto
            {
                Token = token.Token,
                ExpiresAt = token.ExpiresAt,
                User = new UserInfoDto
                {
                    UserId = user.Id,
                    Email = user.Email!,
                    Role = tenantUser.Role,
                    RoleName = tenantUser.Role.ToString()
                },
                Tenant = new TenantInfoDto
                {
                    Id = tenantUser.Tenant.Id,
                    Name = tenantUser.Tenant.Name,
                    Subdomain = tenantUser.Tenant.Subdomain,
                    RUC = tenantUser.Tenant.RUC,
                    DV = tenantUser.Tenant.DV
                },
                Subscription = new SubscriptionInfoDto
                {
                    Plan = tenantUser.Tenant.Subscription.Plan,
                    PlanName = tenantUser.Tenant.Subscription.Plan.ToString(),
                    Status = tenantUser.Tenant.Subscription.Status,
                    StatusName = tenantUser.Tenant.Subscription.Status.ToString(),
                    TrialEndsAt = tenantUser.Tenant.Subscription.TrialEndsAt,
                    MaxEmployees = tenantUser.Tenant.Subscription.GetEffectiveMaxEmployees(),
                    MaxUsers = tenantUser.Tenant.Subscription.GetEffectiveMaxUsers(),
                    MaxCompanies = limits.MaxCompanies,
                    CanExportExcel = limits.CanExportExcel,
                    CanExportPdf = limits.CanExportPdf,
                    CanUseApi = limits.CanUseApi,
                    MonthlyPrice = tenantUser.Tenant.Subscription.MonthlyPrice
                }
            };

            _logger.LogInformation("User {UserId} logged in to tenant {TenantId}",
                user.Id, tenantUser.TenantId);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login: {Email}", dto.Email);
            return StatusCode(500, new { message = "Error al iniciar sesión. Por favor, intente nuevamente." });
        }
    }

    /// <summary>
    /// Obtiene información del usuario autenticado y su tenant
    /// GET /api/auth/me
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var tenantIdClaim = User.FindFirst("tenant_id")?.Value;
            if (string.IsNullOrEmpty(tenantIdClaim) || !int.TryParse(tenantIdClaim, out var tenantId))
            {
                return Unauthorized(new { message = "Tenant no identificado" });
            }

            // Obtener usuario completo con tenant y suscripción
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "Usuario no encontrado" });
            }

            var tenantUser = await _context.TenantUsers
                .Include(tu => tu.Tenant)
                    .ThenInclude(t => t.Subscription)
                .FirstOrDefaultAsync(tu => tu.UserId == userId && tu.TenantId == tenantId);

            if (tenantUser == null)
            {
                return NotFound(new { message = "Acceso al tenant no encontrado" });
            }

            var limits = PlanFeatures.GetLimits(tenantUser.Tenant.Subscription.Plan);

            var response = new AuthResponseDto
            {
                Token = string.Empty,  // No devolver token en /me
                ExpiresAt = DateTime.MinValue,
                User = new UserInfoDto
                {
                    UserId = user.Id,
                    Email = user.Email!,
                    Role = tenantUser.Role,
                    RoleName = tenantUser.Role.ToString()
                },
                Tenant = new TenantInfoDto
                {
                    Id = tenantUser.Tenant.Id,
                    Name = tenantUser.Tenant.Name,
                    Subdomain = tenantUser.Tenant.Subdomain,
                    RUC = tenantUser.Tenant.RUC,
                    DV = tenantUser.Tenant.DV
                },
                Subscription = new SubscriptionInfoDto
                {
                    Plan = tenantUser.Tenant.Subscription.Plan,
                    PlanName = tenantUser.Tenant.Subscription.Plan.ToString(),
                    Status = tenantUser.Tenant.Subscription.Status,
                    StatusName = tenantUser.Tenant.Subscription.Status.ToString(),
                    TrialEndsAt = tenantUser.Tenant.Subscription.TrialEndsAt,
                    MaxEmployees = tenantUser.Tenant.Subscription.GetEffectiveMaxEmployees(),
                    MaxUsers = tenantUser.Tenant.Subscription.GetEffectiveMaxUsers(),
                    MaxCompanies = limits.MaxCompanies,
                    CanExportExcel = limits.CanExportExcel,
                    CanExportPdf = limits.CanExportPdf,
                    CanUseApi = limits.CanUseApi,
                    MonthlyPrice = tenantUser.Tenant.Subscription.MonthlyPrice
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user info");
            return StatusCode(500, new { message = "Error al obtener información del usuario" });
        }
    }

    // ========================================================================
    // HELPER METHODS
    // ========================================================================

    private (string Token, DateTime ExpiresAt) GenerateJwtToken(AppUser user, Tenant tenant, TenantUser tenantUser)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
        var jwtIssuer = _configuration["Jwt:Issuer"] ?? "Planilla";
        var jwtAudience = _configuration["Jwt:Audience"] ?? "Planilla";
        var jwtExpireHours = int.Parse(_configuration["Jwt:ExpireHours"] ?? "24");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var expiresAt = DateTime.UtcNow.AddHours(jwtExpireHours);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("tenant_id", tenant.Id.ToString()),
            new Claim("tenant_role", tenantUser.Role.ToString()),
            new Claim("plan", tenant.Subscription.Plan.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private string GenerateUniqueSubdomain(string companyName)
    {
        // Generar subdomain base limpiando el nombre de la empresa
        var baseSubdomain = new string(companyName
            .ToLower()
            .Where(c => char.IsLetterOrDigit(c) || c == '-')
            .Take(20)
            .ToArray())
            .Replace(' ', '-');

        // Verificar si existe
        var subdomain = baseSubdomain;
        var counter = 1;

        while (_context.Tenants.Any(t => t.Subdomain == subdomain))
        {
            subdomain = $"{baseSubdomain}-{counter}";
            counter++;
        }

        return subdomain;
    }
}
