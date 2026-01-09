using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vorluno.Planilla.Application.Common;
using Vorluno.Planilla.Application.DTOs.Tenant;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Infrastructure.Services;

/// <summary>
/// Servicio para registrar y consultar audit logs del sistema
/// CRÍTICO: Todos los logs están filtrados por TenantId para aislamiento multi-tenant
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(
        ApplicationDbContext context,
        ITenantContext tenantContext,
        ICurrentUserService currentUserService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditLogService> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _currentUserService = currentUserService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Registra una acción en el audit log del tenant actual
    /// </summary>
    public async Task LogAsync(string action, string entityType, string? entityId = null, Dictionary<string, string>? metadata = null)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;
            if (tenantId == 0)
            {
                _logger.LogWarning("Attempted to log audit entry without tenant context. Action: {Action}", action);
                return;
            }

            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Attempted to log audit entry without user context. Action: {Action}", action);
                return;
            }

            var entry = new AuditLogEntry
            {
                TenantId = tenantId,
                ActorUserId = userId,
                ActorEmail = _currentUserService.Email ?? "unknown",
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                CreatedAt = DateTime.UtcNow,
                IpAddress = GetClientIpAddress(),
                UserAgent = GetUserAgent(),
                MetadataJson = metadata != null ? JsonSerializer.Serialize(metadata) : null,
                IsActive = true
            };

            _context.AuditLogEntries.Add(entry);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Audit log created: Action={Action}, EntityType={EntityType}, EntityId={EntityId}, TenantId={TenantId}, UserId={UserId}",
                action, entityType, entityId, tenantId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating audit log entry: Action={Action}, EntityType={EntityType}", action, entityType);
            // No lanzamos la excepción para no afectar la operación principal
        }
    }

    /// <summary>
    /// Obtiene el audit log del tenant actual con filtros y paginación
    /// </summary>
    public async Task<Result<PagedResultDto<AuditLogDto>>> GetAuditLogAsync(AuditLogFilterDto filter)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;
            if (tenantId == 0)
            {
                return Result<PagedResultDto<AuditLogDto>>.Fail("Tenant context no encontrado");
            }

            // Validar y ajustar filtros
            filter.ValidatePageSize();
            filter.ValidatePage();

            // Query base filtrada por tenant (global query filter se aplica automáticamente)
            var query = _context.AuditLogEntries
                .AsNoTracking()
                .AsQueryable();

            // Aplicar filtros opcionales
            if (filter.From.HasValue)
            {
                query = query.Where(a => a.CreatedAt >= filter.From.Value);
            }

            if (filter.To.HasValue)
            {
                query = query.Where(a => a.CreatedAt <= filter.To.Value);
            }

            if (!string.IsNullOrEmpty(filter.Action))
            {
                query = query.Where(a => a.Action == filter.Action);
            }

            if (!string.IsNullOrEmpty(filter.EntityType))
            {
                query = query.Where(a => a.EntityType == filter.EntityType);
            }

            if (!string.IsNullOrEmpty(filter.ActorEmail))
            {
                query = query.Where(a => a.ActorEmail.Contains(filter.ActorEmail));
            }

            // Contar total de registros
            var totalCount = await query.CountAsync();

            // Aplicar paginación y ordenar por fecha descendente
            var items = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(a => new AuditLogDto
                {
                    Id = a.Id,
                    TenantId = a.TenantId,
                    ActorUserId = a.ActorUserId,
                    ActorEmail = a.ActorEmail,
                    Action = a.Action,
                    EntityType = a.EntityType,
                    EntityId = a.EntityId,
                    CreatedAt = a.CreatedAt,
                    IpAddress = a.IpAddress,
                    UserAgent = a.UserAgent,
                    MetadataJson = a.MetadataJson
                })
                .ToListAsync();

            var result = new PagedResultDto<AuditLogDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            };

            return Result<PagedResultDto<AuditLogDto>>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit log");
            return Result<PagedResultDto<AuditLogDto>>.Fail("Error al obtener el audit log");
        }
    }

    /// <summary>
    /// Obtiene el audit log de una entidad específica
    /// </summary>
    public async Task<Result<List<AuditLogDto>>> GetEntityAuditLogAsync(string entityType, string entityId)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;
            if (tenantId == 0)
            {
                return Result<List<AuditLogDto>>.Fail("Tenant context no encontrado");
            }

            var entries = await _context.AuditLogEntries
                .Where(a => a.EntityType == entityType && a.EntityId == entityId)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new AuditLogDto
                {
                    Id = a.Id,
                    TenantId = a.TenantId,
                    ActorUserId = a.ActorUserId,
                    ActorEmail = a.ActorEmail,
                    Action = a.Action,
                    EntityType = a.EntityType,
                    EntityId = a.EntityId,
                    CreatedAt = a.CreatedAt,
                    IpAddress = a.IpAddress,
                    UserAgent = a.UserAgent,
                    MetadataJson = a.MetadataJson
                })
                .ToListAsync();

            return Result<List<AuditLogDto>>.Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving entity audit log: EntityType={EntityType}, EntityId={EntityId}", entityType, entityId);
            return Result<List<AuditLogDto>>.Fail("Error al obtener el audit log de la entidad");
        }
    }

    private string? GetClientIpAddress()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return null;

            // Check for X-Forwarded-For header (proxies/load balancers)
            var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                return forwardedFor.Split(',').First().Trim();
            }

            // Check for X-Real-IP header
            var realIp = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp))
            {
                return realIp;
            }

            // Fallback to RemoteIpAddress
            return httpContext.Connection.RemoteIpAddress?.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting client IP address");
            return null;
        }
    }

    private string? GetUserAgent()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return null;

            return httpContext.Request.Headers["User-Agent"].FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting user agent");
            return null;
        }
    }
}
