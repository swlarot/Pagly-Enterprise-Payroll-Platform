using System.Collections.Concurrent;
using Vorluno.Planilla.Infrastructure.Services;

namespace Planilla.Web.IntegrationTests;

/// <summary>
/// Sustituye a Brevo en las pruebas: no envía nada, solo anota lo que se
/// habría enviado para poder comprobarlo.
/// </summary>
public class CorreoDePrueba : IBrevoEmailService
{
    public record Enviado(string Para, string Asunto, string Enlace, IReadOnlyList<string> Parrafos);

    public ConcurrentQueue<Enviado> Avisos { get; } = new();

    public Task<bool> SendInvitationEmailAsync(string toEmail, string toName, string invitationLink, string tenantName) =>
        Task.FromResult(true);

    public Task<bool> SendQuotaAlertEmailAsync(string toEmail, string toName, string tenantName, int threshold, int currentRequests, int limit, string planName) =>
        Task.FromResult(true);

    public Task<bool> SendAvisoEmailAsync(string toEmail, string toName, string asunto, string titulo, IReadOnlyList<string> parrafos, string textoBoton, string enlace)
    {
        Avisos.Enqueue(new Enviado(toEmail, asunto, enlace, parrafos));
        return Task.FromResult(true);
    }
}
