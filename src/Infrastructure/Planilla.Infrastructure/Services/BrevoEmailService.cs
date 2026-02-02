using Microsoft.Extensions.Configuration;
using sib_api_v3_sdk.Api;
using sib_api_v3_sdk.Client;
using sib_api_v3_sdk.Model;

namespace Vorluno.Planilla.Infrastructure.Services;

/// <summary>
/// Interfaz para el servicio de envío de emails mediante Brevo (anteriormente Sendinblue)
/// </summary>
public interface IBrevoEmailService
{
    /// <summary>
    /// Envía un email de invitación a un usuario
    /// </summary>
    /// <param name="toEmail">Email del destinatario</param>
    /// <param name="toName">Nombre del destinatario</param>
    /// <param name="invitationLink">Link de invitación o login</param>
    /// <param name="tenantName">Nombre de la empresa/tenant</param>
    /// <returns>True si el email se envió correctamente</returns>
    Task<bool> SendInvitationEmailAsync(string toEmail, string toName, string invitationLink, string tenantName);
}

/// <summary>
/// Implementación del servicio de envío de emails mediante Brevo API
/// </summary>
public class BrevoEmailService : IBrevoEmailService
{
    private readonly string _apiKey;
    private readonly string _senderEmail;
    private readonly string _senderName;

    public BrevoEmailService(IConfiguration configuration)
    {
        _apiKey = configuration["Brevo:ApiKey"]
            ?? throw new InvalidOperationException("Brevo:ApiKey no está configurado en appsettings.json");
        _senderEmail = configuration["Brevo:SenderEmail"] ?? "noreply@planilla.cloud";
        _senderName = configuration["Brevo:SenderName"] ?? "Planilla";
    }

    /// <inheritdoc />
    public async Task<bool> SendInvitationEmailAsync(
        string toEmail,
        string toName,
        string invitationLink,
        string tenantName)
    {
        try
        {
            // Configurar API key de Brevo
            sib_api_v3_sdk.Client.Configuration.Default.ApiKey["api-key"] = _apiKey;

            var apiInstance = new TransactionalEmailsApi();

            var sendSmtpEmail = new SendSmtpEmail
            {
                Sender = new SendSmtpEmailSender(_senderName, _senderEmail),
                To = new List<SendSmtpEmailTo>
                {
                    new SendSmtpEmailTo(toEmail, toName)
                },
                Subject = $"Invitación a {tenantName} - Planilla",
                HtmlContent = GenerateInvitationEmailHtml(toName, tenantName, invitationLink)
            };

            var result = await apiInstance.SendTransacEmailAsync(sendSmtpEmail);

            return result != null;
        }
        catch (Exception ex)
        {
            // Log error (en una implementación real se debe inyectar ILogger)
            Console.Error.WriteLine($"Error enviando email con Brevo: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Genera el HTML del email de invitación
    /// </summary>
    private string GenerateInvitationEmailHtml(string toName, string tenantName, string invitationLink)
    {
        return $@"
<!DOCTYPE html>
<html lang='es'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Invitación a {tenantName}</title>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background-color: #4F46E5; color: white; padding: 20px; border-radius: 8px 8px 0 0; text-align: center;'>
        <h1 style='margin: 0; font-size: 24px;'>Bienvenido a Planilla</h1>
    </div>

    <div style='background-color: #f9fafb; padding: 30px; border-radius: 0 0 8px 8px; border: 1px solid #e5e7eb;'>
        <h2 style='color: #1f2937; margin-top: 0;'>Hola {toName},</h2>

        <p style='font-size: 16px; color: #4b5563;'>
            Has sido invitado a unirte a <strong>{tenantName}</strong> en Planilla,
            el sistema de gestión de nómina para empresas en Panamá.
        </p>

        <div style='background-color: #fef3c7; border-left: 4px solid #f59e0b; padding: 15px; margin: 20px 0;'>
            <p style='margin: 0; font-size: 14px;'>
                <strong>📌 Contraseña temporal:</strong>
                <code style='background-color: white; padding: 4px 8px; border-radius: 4px; font-family: monospace;'>Planilla2024!Temp</code>
            </p>
        </div>

        <p style='font-size: 14px; color: #6b7280;'>
            Por favor, cambia tu contraseña después del primer inicio de sesión por seguridad.
        </p>

        <div style='text-align: center; margin: 30px 0;'>
            <a href='{invitationLink}'
               style='background-color: #4F46E5;
                      color: white;
                      padding: 14px 28px;
                      text-decoration: none;
                      border-radius: 6px;
                      display: inline-block;
                      font-weight: bold;
                      font-size: 16px;'>
                Acceder al Sistema
            </a>
        </div>

        <hr style='border: none; border-top: 1px solid #e5e7eb; margin: 30px 0;'>

        <p style='font-size: 13px; color: #9ca3af; text-align: center; margin: 0;'>
            Este es un email automático de Planilla.
            <br>
            Si no solicitaste este acceso, puedes ignorar este mensaje.
        </p>
    </div>

    <div style='text-align: center; padding: 20px; color: #9ca3af; font-size: 12px;'>
        <p>© 2026 Planilla - Sistema de Gestión de Nómina</p>
        <p>Panamá</p>
    </div>
</body>
</html>";
    }
}
