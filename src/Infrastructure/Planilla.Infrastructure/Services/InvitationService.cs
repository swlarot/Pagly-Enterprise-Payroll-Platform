using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Vorluno.Planilla.Application.Common;
using Vorluno.Planilla.Application.DTOs.Auth;
using Vorluno.Planilla.Application.DTOs.Tenant;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Domain.Models;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Infrastructure.Services;

/// <summary>
/// Servicio para gestión de invitaciones de usuarios al tenant
/// CRÍTICO: Verificar límites del plan ANTES de crear invitaciones
/// </summary>
public class InvitationService : IInvitationService
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditLogService _auditLogService;
    private readonly IPlanLimitService _planLimitService;
    private readonly UserManager<AppUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<InvitationService> _logger;

    public InvitationService(
        ApplicationDbContext context,
        ITenantContext tenantContext,
        IAuditLogService auditLogService,
        IPlanLimitService planLimitService,
        UserManager<AppUser> userManager,
        IConfiguration configuration,
        IJwtTokenService jwtTokenService,
        ILogger<InvitationService> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _auditLogService = auditLogService;
        _planLimitService = planLimitService;
        _userManager = userManager;
        _configuration = configuration;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    /// <summary>
    /// Crea una invitación para un nuevo usuario, verificando límites del plan
    /// </summary>
    public async Task<Result<InvitationResponseDto>> CreateInvitationAsync(CreateInvitationDto dto)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;
            if (tenantId == 0)
            {
                return Result<InvitationResponseDto>.Fail("Tenant context no encontrado");
            }

            // Verificar permisos
            if (!_tenantContext.IsAdminOrOwner())
            {
                return Result<InvitationResponseDto>.Fail("No tiene permisos para invitar usuarios");
            }

            // CRÍTICO: Verificar límites del plan
            var (canInvite, reason) = await _planLimitService.CanInviteUserAsync(tenantId);
            if (!canInvite)
            {
                return Result<InvitationResponseDto>.Fail(reason ?? "No puedes invitar más usuarios en tu plan actual");
            }

            // Verificar que el email no esté ya asociado al tenant
            var existingUser = await _context.TenantUsers
                .AnyAsync(tu => tu.TenantId == tenantId && tu.InvitedEmail == dto.Email);

            if (existingUser)
            {
                return Result<InvitationResponseDto>.Fail("Este usuario ya está en el tenant");
            }

            // Verificar que no haya invitaciones pendientes para este email
            var pendingInvitation = await _context.TenantInvitations
                .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Email == dto.Email && i.IsActive && !i.AcceptedAt.HasValue && !i.IsRevoked && i.ExpiresAt > DateTime.UtcNow);

            if (pendingInvitation != null)
            {
                return Result<InvitationResponseDto>.Fail("Ya existe una invitación pendiente para este email");
            }

            // Crear invitación
            var invitation = new TenantInvitation
            {
                TenantId = tenantId,
                Email = dto.Email.ToLowerInvariant(),
                Role = dto.Role,
                Token = Guid.NewGuid().ToString("N"), // Token sin guiones
                CreatedByUserId = _tenantContext.UserId!,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7), // 7 días para aceptar
                IsActive = true
            };

            _context.TenantInvitations.Add(invitation);
            await _context.SaveChangesAsync();

            // Audit log
            await _auditLogService.LogAsync("InviteCreated", "TenantInvitation", invitation.Id.ToString(), new Dictionary<string, string>
            {
                ["InvitedEmail"] = dto.Email,
                ["Role"] = dto.Role.ToString()
            });

            _logger.LogInformation("Invitation created for {Email} in tenant {TenantId}", dto.Email, tenantId);

            // Construir URL de invitación (asumiendo frontend en /accept-invite)
            var baseUrl = _configuration["App:FrontendUrl"] ?? "https://localhost:5173";
            var inviteUrl = $"{baseUrl}/accept-invite?token={invitation.Token}";

            var response = new InvitationResponseDto
            {
                InvitationId = invitation.Id,
                Email = invitation.Email,
                Token = invitation.Token,
                ExpiresAt = invitation.ExpiresAt,
                InviteUrl = inviteUrl
            };

            return Result<InvitationResponseDto>.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating invitation");
            return Result<InvitationResponseDto>.Fail("Error al crear la invitación");
        }
    }

    /// <summary>
    /// Acepta una invitación y crea/asocia el usuario al tenant
    /// </summary>
    public async Task<Result<AuthResponseDto>> AcceptInvitationAsync(AcceptInvitationDto dto)
    {
        try
        {
            // Buscar invitación válida
            var invitation = await _context.TenantInvitations
                .Include(i => i.Tenant)
                    .ThenInclude(t => t.Subscription)
                .FirstOrDefaultAsync(i => i.Token == dto.Token && i.IsActive);

            if (invitation == null)
            {
                return Result<AuthResponseDto>.Fail("Invitación no encontrada");
            }

            // Validar invitación
            if (!invitation.IsValid())
            {
                if (invitation.IsExpired())
                {
                    return Result<AuthResponseDto>.Fail("La invitación ha expirado");
                }
                if (invitation.IsRevoked)
                {
                    return Result<AuthResponseDto>.Fail("La invitación ha sido revocada");
                }
                if (invitation.IsAccepted())
                {
                    return Result<AuthResponseDto>.Fail("La invitación ya fue aceptada");
                }
                return Result<AuthResponseDto>.Fail("Invitación inválida");
            }

            // Verificar si el usuario ya existe
            var existingUser = await _userManager.FindByEmailAsync(invitation.Email);
            AppUser user;

            if (existingUser == null)
            {
                // Crear nuevo usuario
                if (string.IsNullOrEmpty(dto.Password))
                {
                    return Result<AuthResponseDto>.Fail("La contraseña es requerida para crear una cuenta nueva");
                }

                user = new AppUser
                {
                    Email = invitation.Email,
                    UserName = invitation.Email,
                    NombreCompleto = dto.FullName,
                    EmailConfirmed = true // Auto-confirmar email por invitación
                };

                var createResult = await _userManager.CreateAsync(user, dto.Password);
                if (!createResult.Succeeded)
                {
                    var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                    return Result<AuthResponseDto>.Fail($"Error al crear usuario: {errors}");
                }

                _logger.LogInformation("New user {Email} created via invitation", invitation.Email);
            }
            else
            {
                user = existingUser;
                _logger.LogInformation("Existing user {Email} accepting invitation", invitation.Email);
            }

            // Verificar que el usuario no esté ya en el tenant
            var existingTenantUser = await _context.TenantUsers
                .FirstOrDefaultAsync(tu => tu.TenantId == invitation.TenantId && tu.UserId == user.Id);

            if (existingTenantUser != null)
            {
                // Usuario ya está en el tenant, solo actualizar rol si es diferente
                if (existingTenantUser.Role != invitation.Role)
                {
                    existingTenantUser.Role = invitation.Role;
                    existingTenantUser.UpdatedAt = DateTime.UtcNow;
                }
                existingTenantUser.IsActive = true;
            }
            else
            {
                // Crear relación TenantUser
                var tenantUser = new TenantUser
                {
                    TenantId = invitation.TenantId,
                    UserId = user.Id,
                    Role = invitation.Role,
                    JoinedAt = DateTime.UtcNow,
                    IsActive = true,
                    IsPendingInvitation = false
                };

                _context.TenantUsers.Add(tenantUser);
            }

            // Marcar invitación como aceptada
            invitation.AcceptedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Audit log (cambiamos temporalmente el contexto del tenant)
            var originalTenantId = _tenantContext.TenantId;
            await _tenantContext.SetTenantAsync(invitation.TenantId);
            await _auditLogService.LogAsync("InviteAccepted", "TenantInvitation", invitation.Id.ToString(), new Dictionary<string, string>
            {
                ["AcceptedByEmail"] = invitation.Email,
                ["Role"] = invitation.Role.ToString()
            });
            if (originalTenantId != 0)
            {
                await _tenantContext.SetTenantAsync(originalTenantId);
            }

            _logger.LogInformation("Invitation {InvitationId} accepted by user {UserId}", invitation.Id, user.Id);

            // Generar JWT token
            var token = await GenerateJwtTokenAsync(user, invitation.Tenant);

            var response = new AuthResponseDto
            {
                Token = token,
                User = new UserInfoDto
                {
                    UserId = user.Id,
                    Email = user.Email!,
                    Role = invitation.Role,
                    RoleName = invitation.Role.ToString()
                },
                Tenant = new TenantInfoDto
                {
                    Id = invitation.Tenant.Id,
                    Name = invitation.Tenant.Name,
                    Subdomain = invitation.Tenant.Subdomain,
                    RUC = invitation.Tenant.RUC,
                    DV = invitation.Tenant.DV
                },
                Subscription = invitation.Tenant.Subscription != null ? new SubscriptionInfoDto
                {
                    Plan = invitation.Tenant.Subscription.Plan,
                    PlanName = invitation.Tenant.Subscription.Plan.ToString(),
                    Status = invitation.Tenant.Subscription.Status,
                    StatusName = invitation.Tenant.Subscription.Status.ToString(),
                    TrialEndsAt = invitation.Tenant.Subscription.TrialEndsAt,
                    MaxEmployees = PlanFeatures.GetLimits(invitation.Tenant.Subscription.Plan).MaxEmployees,
                    MaxUsers = PlanFeatures.GetLimits(invitation.Tenant.Subscription.Plan).MaxUsers,
                    MaxCompanies = PlanFeatures.GetLimits(invitation.Tenant.Subscription.Plan).MaxCompanies,
                    CanExportExcel = PlanFeatures.GetLimits(invitation.Tenant.Subscription.Plan).CanExportExcel,
                    CanExportPdf = PlanFeatures.GetLimits(invitation.Tenant.Subscription.Plan).CanExportPdf,
                    CanUseApi = PlanFeatures.GetLimits(invitation.Tenant.Subscription.Plan).CanUseApi,
                    MonthlyPrice = PlanFeatures.GetLimits(invitation.Tenant.Subscription.Plan).PricePerMonth
                } : null
            };

            return Result<AuthResponseDto>.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting invitation");
            return Result<AuthResponseDto>.Fail("Error al aceptar la invitación");
        }
    }

    /// <summary>
    /// Obtiene las invitaciones pendientes del tenant actual
    /// </summary>
    public async Task<Result<List<InvitationDto>>> GetPendingInvitationsAsync()
    {
        try
        {
            var tenantId = _tenantContext.TenantId;
            if (tenantId == 0)
            {
                return Result<List<InvitationDto>>.Fail("Tenant context no encontrado");
            }

            var invitations = await _context.TenantInvitations
                .Include(i => i.CreatedBy)
                .Where(i => i.TenantId == tenantId && i.IsActive && !i.AcceptedAt.HasValue)
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => new InvitationDto
                {
                    Id = i.Id,
                    TenantId = i.TenantId,
                    Email = i.Email,
                    Role = i.Role,
                    RoleName = i.Role.ToString(),
                    Token = i.Token,
                    CreatedAt = i.CreatedAt,
                    ExpiresAt = i.ExpiresAt,
                    CreatedByUserId = i.CreatedByUserId,
                    CreatedByEmail = i.CreatedBy.Email!,
                    AcceptedAt = i.AcceptedAt,
                    IsRevoked = i.IsRevoked,
                    IsValid = i.IsValid(),
                    IsExpired = i.IsExpired()
                })
                .ToListAsync();

            return Result<List<InvitationDto>>.Ok(invitations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending invitations");
            return Result<List<InvitationDto>>.Fail("Error al obtener las invitaciones pendientes");
        }
    }

    /// <summary>
    /// Revoca una invitación
    /// </summary>
    public async Task<Result<bool>> RevokeInvitationAsync(int invitationId)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;
            if (tenantId == 0)
            {
                return Result<bool>.Fail("Tenant context no encontrado");
            }

            // Verificar permisos
            if (!_tenantContext.IsAdminOrOwner())
            {
                return Result<bool>.Fail("No tiene permisos para revocar invitaciones");
            }

            var invitation = await _context.TenantInvitations
                .FirstOrDefaultAsync(i => i.Id == invitationId && i.TenantId == tenantId);

            if (invitation == null)
            {
                return Result<bool>.Fail("Invitación no encontrada");
            }

            if (invitation.IsRevoked)
            {
                return Result<bool>.Fail("La invitación ya fue revocada");
            }

            if (invitation.IsAccepted())
            {
                return Result<bool>.Fail("No se puede revocar una invitación que ya fue aceptada");
            }

            invitation.IsRevoked = true;
            invitation.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Audit log
            await _auditLogService.LogAsync("InviteRevoked", "TenantInvitation", invitationId.ToString(), new Dictionary<string, string>
            {
                ["InvitedEmail"] = invitation.Email,
                ["Role"] = invitation.Role.ToString()
            });

            _logger.LogInformation("Invitation {InvitationId} revoked by user {UserId}", invitationId, _tenantContext.UserId);

            return Result<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking invitation");
            return Result<bool>.Fail("Error al revocar la invitación");
        }
    }

    /// <summary>
    /// Valida un token de invitación sin aceptarlo
    /// </summary>
    public async Task<Result<InvitationDto>> ValidateInvitationTokenAsync(string token)
    {
        try
        {
            var invitation = await _context.TenantInvitations
                .Include(i => i.Tenant)
                .Include(i => i.CreatedBy)
                .FirstOrDefaultAsync(i => i.Token == token && i.IsActive);

            if (invitation == null)
            {
                return Result<InvitationDto>.Fail("Invitación no encontrada");
            }

            var dto = new InvitationDto
            {
                Id = invitation.Id,
                TenantId = invitation.TenantId,
                Email = invitation.Email,
                Role = invitation.Role,
                RoleName = invitation.Role.ToString(),
                Token = invitation.Token,
                CreatedAt = invitation.CreatedAt,
                ExpiresAt = invitation.ExpiresAt,
                CreatedByUserId = invitation.CreatedByUserId,
                CreatedByEmail = invitation.CreatedBy.Email!,
                AcceptedAt = invitation.AcceptedAt,
                IsRevoked = invitation.IsRevoked,
                IsValid = invitation.IsValid(),
                IsExpired = invitation.IsExpired()
            };

            return Result<InvitationDto>.Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating invitation token");
            return Result<InvitationDto>.Fail("Error al validar el token de invitación");
        }
    }

    private async Task<string> GenerateJwtTokenAsync(AppUser user, Tenant tenant)
    {
        // Obtener rol del usuario en el tenant
        var tenantUser = await _context.TenantUsers
            .FirstOrDefaultAsync(tu => tu.UserId == user.Id && tu.TenantId == tenant.Id);

        var role = tenantUser?.Role ?? TenantRole.Employee;
        var plan = tenant.Subscription?.Plan.ToString() ?? "Free";

        return _jwtTokenService.GenerateToken(user.Id, user.Email!, tenant.Id, role, plan);
    }
}
