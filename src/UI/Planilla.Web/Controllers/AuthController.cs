using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Vorluno.Planilla.Application.DTOs.Auth;
using Vorluno.Planilla.Application.Interfaces;
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
    private readonly Vorluno.Planilla.Application.Interfaces.IInvitationService? _invitationService;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<AuthController> logger,
        IJwtTokenService jwtTokenService,
        Vorluno.Planilla.Application.Interfaces.IInvitationService? invitationService = null)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenService = jwtTokenService;
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _invitationService = invitationService;
    }

    /// <summary>
    /// PAGLY: Registro deshabilitado para usuarios públicos.
    /// Los tenants y usuarios se crean exclusivamente desde el Admin Panel interno.
    /// Este endpoint retorna 403 Forbidden para cualquier intento de registro público.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public IActionResult Register([FromBody] RegisterDto dto)
    {
        // PAGLY: Auto-registro deshabilitado - solo Admin Panel puede crear usuarios
        return StatusCode(403, new {
            message = "El registro de usuarios no está disponible. Contacte a su administrador para obtener acceso."
        });
    }

    /// <summary>
    /// [INTERNAL - ADMIN PANEL ONLY]
    /// Registra un nuevo tenant con usuario owner - solo para uso interno del Admin Panel
    /// POST /api/auth/admin/create-tenant
    /// Requiere autenticación de super-admin (implementar en Admin Panel separado)
    /// </summary>
    [HttpPost("admin/create-tenant")]
    [Authorize(Roles = "Owner,Admin")]  // Solo admins existentes pueden crear tenants
    public async Task<IActionResult> AdminCreateTenant([FromBody] RegisterDto dto)
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

        // NOTE: BeginTransactionAsync doesn't work with InMemory database in tests
        // For production (PostgreSQL), this will work correctly
        var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // 1. Crear usuario en Identity
            var user = new AppUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                EmailConfirmed = true,  // TODO: Implementar confirmación por email en producción
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

            // 6. Generar JWT (access token)
            var token = GenerateJwtToken(user, tenant, tenantUser);

            // 7. Generar refresh token
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var refreshToken = await _jwtTokenService.GenerateRefreshTokenAsync(user.Id, tenant.Id, ipAddress);

            // 8. Construir respuesta
            var response = new AuthResponseDto
            {
                Token = token.Token,
                RefreshToken = refreshToken.Token,
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

            // 1.1. Verificar que el usuario no esté eliminado (soft delete)
            if (user.IsDeleted)
            {
                _logger.LogWarning("Intento de login de usuario eliminado: {Email}", dto.Email);
                return Unauthorized(new { message = "Esta cuenta ha sido desactivada. Contacte al administrador." });
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

            // 3. Si es SystemAdmin, generar token especial sin Tenant
            if (user.IsSystemAdmin)
            {
                var systemAdminToken = GenerateSystemAdminJwtToken(user);

                var systemAdminResponse = new AuthResponseDto
                {
                    Token = systemAdminToken.Token,
                    RefreshToken = "", // SystemAdmins no usan refresh tokens por ahora
                    ExpiresAt = systemAdminToken.ExpiresAt,
                    User = new UserInfoDto
                    {
                        UserId = user.Id,
                        Email = user.Email!,
                        Role = TenantRole.Owner, // Dummy role para SystemAdmins
                        RoleName = "SystemAdmin",
                        IsSystemAdmin = true
                    }
                };

                return Ok(systemAdminResponse);
            }

            // 4. Obtener TODOS los tenants activos del usuario
            var userTenants = await _context.TenantUsers
                .Include(tu => tu.Tenant)
                    .ThenInclude(t => t.Subscription)
                .Where(tu => tu.UserId == user.Id && tu.IsActive && tu.Tenant.IsActive)
                .OrderBy(tu => tu.JoinedAt)
                .ToListAsync();

            if (userTenants.Count == 0)
            {
                return Unauthorized(new { message = "No tienes acceso a ninguna empresa activa" });
            }

            // 4.1. Si el usuario tiene múltiples tenants, retornar la lista para selección
            if (userTenants.Count > 1)
            {
                var availableTenants = userTenants.Select(tu => new TenantSummaryDto
                {
                    Id = tu.TenantId,
                    Name = tu.Tenant.Name,
                    Role = tu.Role,
                    RoleName = tu.Role.ToString(),
                    Subdomain = tu.Tenant.Subdomain
                }).ToList();

                // Generar token temporal (5 min) que solo permite seleccionar tenant
                var tempToken = GenerateTenantSelectionToken(user);

                // Retornar respuesta con lista de tenants, sin seleccionar ninguno aún
                var multiTenantResponse = new AuthResponseDto
                {
                    Token = tempToken.Token, // Token temporal para selección
                    RefreshToken = string.Empty,
                    ExpiresAt = tempToken.ExpiresAt,
                    User = new UserInfoDto
                    {
                        UserId = user.Id,
                        Email = user.Email!,
                        Role = TenantRole.Owner, // Dummy, se actualizará al seleccionar
                        RoleName = "Multiple"
                    },
                    Tenant = null, // No hay tenant seleccionado aún
                    Subscription = null,
                    AvailableTenants = availableTenants,
                    RequiresTenantSelection = true
                };

                _logger.LogInformation("User {UserId} has {Count} tenants, requiring selection",
                    user.Id, userTenants.Count);

                return Ok(multiTenantResponse);
            }

            // 4.2. Si solo tiene un tenant, continuar con el flujo normal
            var tenantUser = userTenants.First();

            // 5. Actualizar último login
            tenantUser.LastLoginAt = DateTime.UtcNow;
            _context.TenantUsers.Update(tenantUser);
            await _context.SaveChangesAsync();

            // 6. Generar JWT (access token)
            var token = GenerateJwtToken(user, tenantUser.Tenant, tenantUser);

            // 7. Generar refresh token
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var refreshToken = await _jwtTokenService.GenerateRefreshTokenAsync(user.Id, tenantUser.TenantId, ipAddress);

            // 8. Obtener límites del plan
            var subscription = tenantUser.Tenant?.Subscription;
            if (subscription == null)
                return StatusCode(500, new { message = "Suscripción del tenant no encontrada" });
            var limits = PlanFeatures.GetLimits(subscription.Plan);

            // 9. Construir respuesta (con un solo tenant seleccionado)
            var tenant = tenantUser.Tenant!;
            var singleTenantList = new List<TenantSummaryDto>
            {
                new TenantSummaryDto
                {
                    Id = tenantUser.TenantId,
                    Name = tenant.Name,
                    Role = tenantUser.Role,
                    RoleName = tenantUser.Role.ToString(),
                    Subdomain = tenant.Subdomain
                }
            };

            var response = new AuthResponseDto
            {
                Token = token.Token,
                RefreshToken = refreshToken.Token,
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
                },
                AvailableTenants = singleTenantList,
                RequiresTenantSelection = false // Solo un tenant, no requiere selección
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

            // Obtener usuario
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "Usuario no encontrado" });
            }

            // CASO 1: Usuario es SystemAdmin (no tiene tenant)
            var isSystemAdminClaim = User.FindFirst("is_system_admin")?.Value;
            if (isSystemAdminClaim == "true" || user.IsSystemAdmin)
            {
                var systemAdminResponse = new AuthResponseDto
                {
                    Token = string.Empty,  // No devolver token en /me
                    ExpiresAt = DateTime.MinValue,
                    User = new UserInfoDto
                    {
                        UserId = user.Id,
                        Email = user.Email!,
                        Role = TenantRole.Owner, // Dummy role para SystemAdmins
                        RoleName = "SystemAdmin",
                        IsSystemAdmin = true
                    },
                    Tenant = null,  // SystemAdmins no tienen tenant
                    Subscription = null  // SystemAdmins no tienen suscripción
                };

                _logger.LogInformation("SystemAdmin {UserId} accessed /me endpoint", user.Id);
                return Ok(systemAdminResponse);
            }

            // CASO 2: Usuario regular con tenant
            var tenantIdClaim = User.FindFirst("tenant_id")?.Value;
            if (string.IsNullOrEmpty(tenantIdClaim) || !int.TryParse(tenantIdClaim, out var tenantId) || tenantId <= 0)
            {
                return Unauthorized(new { message = "Tenant no identificado" });
            }

            var tenantUser = await _context.TenantUsers
                .Include(tu => tu.Tenant)
                    .ThenInclude(t => t.Subscription)
                .FirstOrDefaultAsync(tu => tu.UserId == userId && tu.TenantId == tenantId);

            if (tenantUser == null)
            {
                return NotFound(new { message = "Acceso al tenant no encontrado" });
            }

            var subscriptionMe = tenantUser.Tenant?.Subscription;
            if (subscriptionMe == null)
                return StatusCode(500, new { message = "Suscripción del tenant no encontrada" });
            var limits = PlanFeatures.GetLimits(subscriptionMe.Plan);

            var response = new AuthResponseDto
            {
                Token = string.Empty,  // No devolver token en /me
                ExpiresAt = DateTime.MinValue,
                User = new UserInfoDto
                {
                    UserId = user.Id,
                    Email = user.Email!,
                    Role = tenantUser.Role,
                    RoleName = tenantUser.Role.ToString(),
                    IsSystemAdmin = false
                },
                Tenant = new TenantInfoDto
                {
                    Id = tenantUser.Tenant!.Id,
                    Name = tenantUser.Tenant.Name,
                    Subdomain = tenantUser.Tenant.Subdomain,
                    RUC = tenantUser.Tenant.RUC,
                    DV = tenantUser.Tenant.DV
                },
                Subscription = new SubscriptionInfoDto
                {
                    Plan = subscriptionMe.Plan,
                    PlanName = subscriptionMe.Plan.ToString(),
                    Status = subscriptionMe.Status,
                    StatusName = subscriptionMe.Status.ToString(),
                    TrialEndsAt = subscriptionMe.TrialEndsAt,
                    MaxEmployees = subscriptionMe.GetEffectiveMaxEmployees(),
                    MaxUsers = subscriptionMe.GetEffectiveMaxUsers(),
                    MaxCompanies = limits.MaxCompanies,
                    CanExportExcel = limits.CanExportExcel,
                    CanExportPdf = limits.CanExportPdf,
                    CanUseApi = limits.CanUseApi,
                    MonthlyPrice = subscriptionMe.MonthlyPrice
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

        // Solo los tokens de SystemAdmin (generados en GenerateSystemAdminJwtToken) llevan is_system_admin = true.
        // Los usuarios de tenant (Owner, User) siempre reciben false; no se usa user.IsSystemAdmin aquí
        // para evitar que un mismo usuario con acceso sistema y tenant redirija al admin del sistema al entrar.
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("tenant_id", tenant.Id.ToString()),
            new Claim("tenant_role", tenantUser.Role.ToString()),
            new Claim(ClaimTypes.Role, tenantUser.Role.ToString()), // ✅ CRITICAL: Para [Authorize(Roles = "Owner,Admin")]
            new Claim("plan", (tenant.Subscription?.Plan ?? Vorluno.Planilla.Domain.Enums.SubscriptionPlan.Free).ToString()),
            new Claim("is_system_admin", "false")
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

    /// <summary>
    /// Genera un JWT temporal (5 min) para permitir solo la selección de tenant
    /// </summary>
    private (string Token, DateTime ExpiresAt) GenerateTenantSelectionToken(AppUser user)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
        var jwtIssuer = _configuration["Jwt:Issuer"] ?? "Planilla";
        var jwtAudience = _configuration["Jwt:Audience"] ?? "Planilla";

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var expiresAt = DateTime.UtcNow.AddMinutes(5); // Solo 5 minutos para seleccionar

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("requires_tenant_selection", "true"), // Marca especial
            new Claim("is_system_admin", "false")
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

    private (string Token, DateTime ExpiresAt) GenerateSystemAdminJwtToken(AppUser user)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
        var jwtIssuer = _configuration["Jwt:Issuer"] ?? "Planilla";
        var jwtAudience = _configuration["Jwt:Audience"] ?? "Planilla";
        var jwtExpireHours = int.Parse(_configuration["Jwt:ExpireHours"] ?? "24");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var expiresAt = DateTime.UtcNow.AddHours(jwtExpireHours);

        // Para SystemAdmins: sin tenant_id, sin tenant_role, sin plan
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("is_system_admin", "true"),
            new Claim("tenant_id", "0"), // 0 = sin tenant
            new Claim("tenant_role", "SystemAdmin"), // Dummy para compatibilidad
            new Claim("plan", "Enterprise") // Dummy para compatibilidad
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

    /// <summary>
    /// Acepta una invitación de usuario al tenant
    /// POST /api/auth/accept-invite
    /// Público (no requiere autenticación)
    /// </summary>
    [HttpPost("accept-invite")]
    [AllowAnonymous]
    public async Task<IActionResult> AcceptInvite([FromBody] Vorluno.Planilla.Application.DTOs.Tenant.AcceptInvitationDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (_invitationService == null)
        {
            return StatusCode(500, new { message = "Invitation service not configured" });
        }

        var result = await _invitationService.AcceptInvitationAsync(dto);

        if (!result.Success)
        {
            return BadRequest(new { message = result.ErrorMessage });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Valida un token de invitación sin aceptarlo
    /// GET /api/auth/validate-invite?token={token}
    /// Público (no requiere autenticación)
    /// </summary>
    [HttpGet("validate-invite")]
    [AllowAnonymous]
    public async Task<IActionResult> ValidateInvite([FromQuery] string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return BadRequest(new { message = "Token requerido" });
        }

        if (_invitationService == null)
        {
            return StatusCode(500, new { message = "Invitation service not configured" });
        }

        var result = await _invitationService.ValidateInvitationTokenAsync(token);

        if (!result.Success)
        {
            return BadRequest(new { message = result.ErrorMessage });
        }

        return Ok(result.Value);
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

    /// <summary>
    /// DEVELOPMENT ONLY: Confirma todos los emails no confirmados
    /// </summary>
    [HttpPost("dev/confirm-all-emails")]
    public async Task<IActionResult> DevConfirmAllEmails()
    {
        // Solo permitir en Development
        if (!_configuration.GetValue<bool>("IsDevelopment", false) &&
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Development")
        {
            return NotFound();
        }

        var unconfirmedUsers = await _userManager.Users
            .Where(u => !u.EmailConfirmed)
            .ToListAsync();

        var count = 0;
        foreach (var user in unconfirmedUsers)
        {
            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user);
            count++;
        }

        return Ok(new {
            message = $"Se confirmaron {count} emails",
            emails = unconfirmedUsers.Select(u => u.Email).ToList()
        });
    }

    /// <summary>
    /// POST /api/auth/refresh - Renueva el access token usando un refresh token
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto dto)
    {
        if (string.IsNullOrEmpty(dto.RefreshToken))
        {
            return BadRequest(new { message = "Refresh token es requerido" });
        }

        try
        {
            // 1. Validar refresh token
            var refreshToken = await _jwtTokenService.ValidateRefreshTokenAsync(dto.RefreshToken);
            if (refreshToken == null)
            {
                return Unauthorized(new { message = "Refresh token inválido o expirado" });
            }

            // 2. Obtener usuario y tenant
            var user = await _userManager.FindByIdAsync(refreshToken.UserId);
            if (user == null)
            {
                return Unauthorized(new { message = "Usuario no encontrado" });
            }

            var tenantUser = await _context.TenantUsers
                .Include(tu => tu.Tenant)
                    .ThenInclude(t => t.Subscription)
                .FirstOrDefaultAsync(tu => tu.UserId == user.Id && tu.TenantId == refreshToken.TenantId && tu.IsActive);

            if (tenantUser == null || !tenantUser.Tenant.IsActive)
            {
                return Unauthorized(new { message = "Tenant no encontrado o inactivo" });
            }

            // 3. Generar nuevo access token
            var newToken = GenerateJwtToken(user, tenantUser.Tenant, tenantUser);

            // 4. Generar nuevo refresh token (rotación de tokens)
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var newRefreshToken = await _jwtTokenService.GenerateRefreshTokenAsync(user.Id, tenantUser.TenantId, ipAddress);

            // 5. Revocar el refresh token anterior (rotación)
            await _jwtTokenService.RevokeRefreshTokenAsync(dto.RefreshToken, "token_rotated", newRefreshToken.Id);

            // 6. Obtener límites del plan
            var subscriptionRefresh = tenantUser.Tenant?.Subscription;
            if (subscriptionRefresh == null)
                return StatusCode(500, new { message = "Suscripción del tenant no encontrada" });
            var limits = PlanFeatures.GetLimits(subscriptionRefresh.Plan);

            // 7. Construir respuesta
            var response = new AuthResponseDto
            {
                Token = newToken.Token,
                RefreshToken = newRefreshToken.Token,
                ExpiresAt = newToken.ExpiresAt,
                User = new UserInfoDto
                {
                    UserId = user.Id,
                    Email = user.Email!,
                    Role = tenantUser.Role,
                    RoleName = tenantUser.Role.ToString()
                },
                Tenant = new TenantInfoDto
                {
                    Id = tenantUser.Tenant!.Id,
                    Name = tenantUser.Tenant.Name,
                    Subdomain = tenantUser.Tenant.Subdomain,
                    RUC = tenantUser.Tenant.RUC,
                    DV = tenantUser.Tenant.DV
                },
                Subscription = new SubscriptionInfoDto
                {
                    Plan = subscriptionRefresh.Plan,
                    PlanName = subscriptionRefresh.Plan.ToString(),
                    Status = subscriptionRefresh.Status,
                    StatusName = subscriptionRefresh.Status.ToString(),
                    TrialEndsAt = subscriptionRefresh.TrialEndsAt,
                    MaxEmployees = subscriptionRefresh.GetEffectiveMaxEmployees(),
                    MaxUsers = subscriptionRefresh.GetEffectiveMaxUsers(),
                    MaxCompanies = limits.MaxCompanies,
                    CanExportExcel = limits.CanExportExcel,
                    CanExportPdf = limits.CanExportPdf,
                    CanUseApi = limits.CanUseApi,
                    MonthlyPrice = subscriptionRefresh.MonthlyPrice
                }
            };

            _logger.LogInformation("User {UserId} refreshed token for tenant {TenantId}",
                user.Id, tenantUser.TenantId);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            return StatusCode(500, new { message = "Error al renovar el token" });
        }
    }

    /// <summary>
    /// POST /api/auth/revoke - Revoca un refresh token (logout)
    /// </summary>
    [HttpPost("revoke")]
    [Authorize]
    public async Task<IActionResult> RevokeToken([FromBody] RefreshTokenRequestDto dto)
    {
        if (string.IsNullOrEmpty(dto.RefreshToken))
        {
            return BadRequest(new { message = "Refresh token es requerido" });
        }

        try
        {
            var success = await _jwtTokenService.RevokeRefreshTokenAsync(dto.RefreshToken, "logout");

            if (!success)
            {
                return NotFound(new { message = "Refresh token no encontrado" });
            }

            _logger.LogInformation("Refresh token revoked by user");

            return Ok(new { message = "Sesión cerrada exitosamente" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking token");
            return StatusCode(500, new { message = "Error al cerrar sesión" });
        }
    }

    /// <summary>
    /// POST /api/auth/select-tenant - Selecciona un tenant cuando el usuario pertenece a múltiples empresas
    /// Requiere autenticación pero acepta token sin tenant_id válido (primer login)
    /// </summary>
    [HttpPost("select-tenant")]
    [Authorize]
    public async Task<IActionResult> SelectTenant([FromBody] SelectTenantDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            // 1. Obtener userId del token actual
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "Usuario no identificado" });
            }

            // 2. Verificar que el usuario existe
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "Usuario no encontrado" });
            }

            // 3. Verificar que el usuario tiene acceso al tenant solicitado
            var tenantUser = await _context.TenantUsers
                .Include(tu => tu.Tenant)
                    .ThenInclude(t => t.Subscription)
                .FirstOrDefaultAsync(tu => tu.UserId == userId
                    && tu.TenantId == dto.TenantId
                    && tu.IsActive
                    && tu.Tenant.IsActive);

            if (tenantUser == null)
            {
                return Forbidden(new { message = "No tienes acceso a esta empresa o está inactiva" });
            }

            // 4. Actualizar último login
            tenantUser.LastLoginAt = DateTime.UtcNow;
            _context.TenantUsers.Update(tenantUser);
            await _context.SaveChangesAsync();

            // 5. Generar nuevo JWT con el tenant seleccionado
            var token = GenerateJwtToken(user, tenantUser.Tenant, tenantUser);

            // 6. Generar refresh token
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var refreshToken = await _jwtTokenService.GenerateRefreshTokenAsync(user.Id, tenantUser.TenantId, ipAddress);

            // 7. Obtener límites del plan
            var subscriptionSelect = tenantUser.Tenant?.Subscription;
            if (subscriptionSelect == null)
                return StatusCode(500, new { message = "Suscripción del tenant no encontrada" });
            var limits = PlanFeatures.GetLimits(subscriptionSelect.Plan);

            // 8. Obtener lista de todos los tenants del usuario (para mantener disponibilidad)
            var allUserTenants = await _context.TenantUsers
                .Where(tu => tu.UserId == userId && tu.IsActive)
                .Include(tu => tu.Tenant)
                .OrderBy(tu => tu.JoinedAt)
                .ToListAsync();

            var availableTenants = allUserTenants.Select(tu => new TenantSummaryDto
            {
                Id = tu.TenantId,
                Name = tu.Tenant.Name,
                Role = tu.Role,
                RoleName = tu.Role.ToString(),
                Subdomain = tu.Tenant.Subdomain
            }).ToList();

            // 9. Construir respuesta
            var response = new AuthResponseDto
            {
                Token = token.Token,
                RefreshToken = refreshToken.Token,
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
                    Id = tenantUser.Tenant!.Id,
                    Name = tenantUser.Tenant.Name,
                    Subdomain = tenantUser.Tenant.Subdomain,
                    RUC = tenantUser.Tenant.RUC,
                    DV = tenantUser.Tenant.DV
                },
                Subscription = new SubscriptionInfoDto
                {
                    Plan = subscriptionSelect.Plan,
                    PlanName = subscriptionSelect.Plan.ToString(),
                    Status = subscriptionSelect.Status,
                    StatusName = subscriptionSelect.Status.ToString(),
                    TrialEndsAt = subscriptionSelect.TrialEndsAt,
                    MaxEmployees = subscriptionSelect.GetEffectiveMaxEmployees(),
                    MaxUsers = subscriptionSelect.GetEffectiveMaxUsers(),
                    MaxCompanies = limits.MaxCompanies,
                    CanExportExcel = limits.CanExportExcel,
                    CanExportPdf = limits.CanExportPdf,
                    CanUseApi = limits.CanUseApi,
                    MonthlyPrice = subscriptionSelect.MonthlyPrice
                },
                AvailableTenants = availableTenants,
                RequiresTenantSelection = false // Ya seleccionó
            };

            _logger.LogInformation("User {UserId} selected tenant {TenantId} ({TenantName})",
                user.Id, tenantUser.TenantId, tenantUser.Tenant.Name);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting tenant");
            return StatusCode(500, new { message = "Error al seleccionar empresa" });
        }
    }

    private IActionResult Forbidden(object value)
    {
        return StatusCode(403, value);
    }
}
