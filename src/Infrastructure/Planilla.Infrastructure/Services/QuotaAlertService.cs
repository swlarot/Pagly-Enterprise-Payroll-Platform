using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Domain.Models;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Infrastructure.Services;

/// <summary>
/// Implementación del servicio de alertas de cuota.
/// Thresholds evaluados: 80% y 100%. Fácilmente extensible vía <see cref="ThresholdsAsc"/>.
/// </summary>
public class QuotaAlertService : IQuotaAlertService
{
    private readonly ApplicationDbContext _db;
    private readonly IBrevoEmailService _email;
    private readonly ILogger<QuotaAlertService> _logger;

    /// <summary>
    /// Umbrales a evaluar en orden ascendente. Se manda email UNA vez por umbral
    /// cruzado en el mes. Si quisiéramos agregar 50%, 90%, etc., aquí es el único lugar.
    /// </summary>
    private static readonly int[] ThresholdsAsc = [80, 100];

    public QuotaAlertService(
        ApplicationDbContext db,
        IBrevoEmailService email,
        ILogger<QuotaAlertService> logger)
    {
        _db = db;
        _email = email;
        _logger = logger;
    }

    public async Task EvaluateAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId <= 0) return;

        // 1. Subscription + plan limits. Si el tenant no existe o no tiene plan
        // con API, no hay nada que evaluar.
        var sub = await _db.Subscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        if (sub is null) return;

        var limits = PlanFeatures.GetLimits(sub.Plan);
        if (!limits.CanUseApi || limits.MonthlyApiRequests <= 0) return;
        if (limits.MonthlyApiRequests == int.MaxValue) return; // Enterprise sin tope

        // 2. Contar requests del mes actual (UTC). Indexado por
        // (TenantId, CreatedAt) — query O(log n) por rango.
        var now = DateTime.UtcNow;
        var periodStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1);

        var currentRequests = await _db.ApiUsageRecords
            .Where(r => r.TenantId == tenantId
                     && r.CreatedAt >= periodStart
                     && r.CreatedAt < periodEnd)
            .CountAsync(cancellationToken);

        // 3. Evaluar cada umbral (ascendente) y enviar email si cruzó sin haberlo reportado
        foreach (var threshold in ThresholdsAsc)
        {
            var triggerCount = (int)Math.Ceiling(limits.MonthlyApiRequests * (threshold / 100.0));
            if (currentRequests < triggerCount) continue; // no cruzó este umbral

            var alreadySent = await _db.QuotaAlertsSent
                .AnyAsync(
                    q => q.TenantId == tenantId
                      && q.PeriodYear == now.Year
                      && q.PeriodMonth == now.Month
                      && q.Threshold == threshold,
                    cancellationToken);

            if (alreadySent) continue;

            await TrySendAsync(
                tenantId, sub.Plan, limits, threshold,
                currentRequests, now, cancellationToken);
        }
    }

    /// <summary>
    /// Inserta el record de dedup ANTES de enviar el email. Si el insert falla por
    /// race condition (unique constraint), significa que otro request ya lo está
    /// enviando — salimos silenciosamente.
    /// Si el insert pasa pero el email falla, el tenant no recibirá otro hasta el
    /// próximo mes. Es una decisión consciente: preferible "ocasionalmente perder
    /// una alerta" a "spammear con reintentos".
    /// </summary>
    private async Task TrySendAsync(
        int tenantId,
        SubscriptionPlan plan,
        PlanLimits limits,
        int threshold,
        int currentRequests,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var dedupRecord = new QuotaAlertSent
        {
            TenantId = tenantId,
            PeriodYear = now.Year,
            PeriodMonth = now.Month,
            Threshold = threshold,
            RequestsAtAlert = currentRequests,
            LimitAtAlert = limits.MonthlyApiRequests,
            SentAt = now,
        };

        _db.QuotaAlertsSent.Add(dedupRecord);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Otro request ya lo disparó concurrentemente. Bien — no dup.
            _db.Entry(dedupRecord).State = EntityState.Detached;
            return;
        }

        // Buscar al Owner del tenant para el email. Join TenantUsers → AspNetUsers.
        var owner = await (
            from tu in _db.TenantUsers.AsNoTracking()
            join u in _db.Users on tu.UserId equals u.Id
            where tu.TenantId == tenantId
               && tu.Role == TenantRole.Owner
               && tu.IsActive
            select new { u.Email, u.UserName }
        ).FirstOrDefaultAsync(cancellationToken);

        if (owner is null || string.IsNullOrWhiteSpace(owner.Email))
        {
            _logger.LogWarning(
                "No se encontró Owner con email para tenant {TenantId} — alerta {Threshold}% no se envió",
                tenantId, threshold);
            return;
        }

        var tenant = await _db.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(cancellationToken);

        await _email.SendQuotaAlertEmailAsync(
            toEmail: owner.Email,
            toName: owner.UserName ?? owner.Email,
            tenantName: tenant ?? $"Tenant #{tenantId}",
            threshold: threshold,
            currentRequests: currentRequests,
            limit: limits.MonthlyApiRequests,
            planName: plan.ToString());

        _logger.LogInformation(
            "QuotaAlert {Threshold}% enviado a tenant {TenantId} ({Current}/{Limit})",
            threshold, tenantId, currentRequests, limits.MonthlyApiRequests);
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner == null) return false;

        var sqlState = inner.GetType()
            .GetProperty("SqlState")
            ?.GetValue(inner) as string;
        if (sqlState == "23505") return true;

        return inner.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
            || inner.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
    }
}
