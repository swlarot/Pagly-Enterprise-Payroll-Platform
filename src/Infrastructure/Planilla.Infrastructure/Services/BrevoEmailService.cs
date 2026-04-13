using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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

    /// <summary>
    /// Envía una alerta al Owner de un tenant cuando el uso del API Platform
    /// cruza un umbral (80% o 100% de su cuota mensual).
    /// </summary>
    /// <param name="toEmail">Email del Owner.</param>
    /// <param name="toName">Nombre del Owner.</param>
    /// <param name="tenantName">Nombre del tenant.</param>
    /// <param name="threshold">80 o 100 (porcentaje).</param>
    /// <param name="currentRequests">Requests consumidos este mes.</param>
    /// <param name="limit">Límite mensual del plan actual.</param>
    /// <param name="planName">Nombre del plan ("Professional", "Enterprise").</param>
    Task<bool> SendQuotaAlertEmailAsync(
        string toEmail,
        string toName,
        string tenantName,
        int threshold,
        int currentRequests,
        int limit,
        string planName);
}

/// <summary>
/// Implementación del servicio de envío de emails mediante Brevo API
/// </summary>
public class BrevoEmailService : IBrevoEmailService
{
    private readonly string _apiKey;
    private readonly string _senderEmail;
    private readonly string _senderName;
    private readonly ILogger<BrevoEmailService> _logger;

    public BrevoEmailService(IConfiguration configuration, ILogger<BrevoEmailService> logger)
    {
        var rawApiKey = configuration["Brevo:ApiKey"]
            ?? throw new InvalidOperationException("Brevo:ApiKey no está configurado en appsettings.json");
        
        _senderEmail = configuration["Brevo:SenderEmail"] ?? "noreply@planilla.cloud";
        _senderName = configuration["Brevo:SenderName"] ?? "Planilla";
        _logger = logger;

        // Validar configuración al inicializar
        if (string.IsNullOrWhiteSpace(rawApiKey))
        {
            _logger.LogError("Brevo:ApiKey está vacío o no configurado");
            throw new InvalidOperationException("Brevo:ApiKey no está configurado correctamente");
        }

        // Decodificar API Key si está en Base64 (formato JSON)
        _apiKey = DecodeApiKey(rawApiKey);

        // Validar formato de API Key de Brevo (debe empezar con "xkeysib-")
        if (!_apiKey.StartsWith("xkeysib-", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "La API Key de Brevo no tiene el formato esperado (debe empezar con 'xkeysib-'). " +
                "Valor recibido: {ApiKeyPrefix}...",
                _apiKey.Length > 20 ? _apiKey.Substring(0, 20) : _apiKey);
        }

        if (string.IsNullOrWhiteSpace(_senderEmail))
        {
            _logger.LogWarning("Brevo:SenderEmail no está configurado, usando valor por defecto");
        }

        // Validar formato de email
        if (!System.Text.RegularExpressions.Regex.IsMatch(_senderEmail, 
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            _logger.LogWarning("Brevo:SenderEmail no tiene un formato de email válido: {SenderEmail}", _senderEmail);
        }

        _logger.LogInformation(
            "BrevoEmailService inicializado. Sender: {SenderEmail} ({SenderName}), API Key: {ApiKeyPrefix}...",
            _senderEmail,
            _senderName,
            _apiKey.Length > 15 ? _apiKey.Substring(0, 15) : "***");
    }

    /// <summary>
    /// Decodifica la API Key si está en Base64 (formato JSON) o retorna la clave directamente
    /// </summary>
    private string DecodeApiKey(string rawApiKey)
    {
        try
        {
            // Intentar decodificar como Base64
            if (rawApiKey.Length > 50 && !rawApiKey.StartsWith("xkeysib-", StringComparison.OrdinalIgnoreCase))
            {
                var decodedBytes = System.Convert.FromBase64String(rawApiKey);
                var decodedJson = System.Text.Encoding.UTF8.GetString(decodedBytes);
                
                // Intentar parsear como JSON
                using var doc = System.Text.Json.JsonDocument.Parse(decodedJson);
                if (doc.RootElement.TryGetProperty("api_key", out var apiKeyElement))
                {
                    var extractedKey = apiKeyElement.GetString();
                    if (!string.IsNullOrWhiteSpace(extractedKey))
                    {
                        _logger.LogInformation("API Key decodificada desde Base64 JSON");
                        return extractedKey;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo decodificar la API Key como Base64, usando valor directo");
        }

        // Si no es Base64 o falla la decodificación, usar el valor directo
        return rawApiKey;
    }

    /// <inheritdoc />
    public async Task<bool> SendInvitationEmailAsync(
        string toEmail,
        string toName,
        string invitationLink,
        string tenantName)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogError("No se puede enviar email: el destinatario está vacío");
            return false;
        }

        try
        {
            _logger.LogInformation(
                "Enviando email de invitación a {ToEmail} para tenant {TenantName}",
                toEmail,
                tenantName);

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

            if (result != null)
            {
                _logger.LogInformation(
                    "Email enviado exitosamente a {ToEmail}. MessageId: {MessageId}",
                    toEmail,
                    result.MessageId);
                return true;
            }
            else
            {
                _logger.LogWarning("Brevo retornó null al enviar email a {ToEmail}", toEmail);
                return false;
            }
        }
        catch (sib_api_v3_sdk.Client.ApiException apiEx)
        {
            // Convertir todos los valores a string explícitamente para evitar problemas con tipos dinámicos
            string errorContentStr = "N/A";
            string errorMessageStr = apiEx.Message ?? "Sin mensaje";
            string statusCodeStr = apiEx.ErrorCode.ToString();
            
            if (apiEx.ErrorContent != null)
            {
                try
                {
                    errorContentStr = apiEx.ErrorContent.ToString() ?? "N/A";
                }
                catch
                {
                    errorContentStr = "Error al convertir ErrorContent a string";
                }
            }
            
            // Intentar parsear el error como JSON para obtener más detalles
            string detailedErrorStr = errorContentStr;
            if (!string.IsNullOrWhiteSpace(errorContentStr) && errorContentStr.StartsWith("{"))
            {
                try
                {
                    using var errorDoc = System.Text.Json.JsonDocument.Parse(errorContentStr);
                    if (errorDoc.RootElement.TryGetProperty("message", out System.Text.Json.JsonElement messageElement))
                    {
                        var msg = messageElement.GetString();
                        if (!string.IsNullOrWhiteSpace(msg))
                            detailedErrorStr = msg;
                    }
                    else if (errorDoc.RootElement.TryGetProperty("error", out System.Text.Json.JsonElement errorElement))
                    {
                        var err = errorElement.GetString();
                        if (!string.IsNullOrWhiteSpace(err))
                            detailedErrorStr = err;
                    }
                }
                catch
                {
                    // Si no se puede parsear, usar el contenido directo
                }
            }

            _logger.LogError(
                "Error de API de Brevo al enviar email a {ToEmail}. StatusCode: {StatusCode}, Error: {ErrorMessage}, Details: {ErrorDetails}, FullContent: {ErrorContent}",
                toEmail,
                statusCodeStr,
                errorMessageStr,
                detailedErrorStr,
                errorContentStr);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error inesperado al enviar email con Brevo a {ToEmail}. Error: {ErrorMessage}",
                toEmail,
                ex.Message);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SendQuotaAlertEmailAsync(
        string toEmail,
        string toName,
        string tenantName,
        int threshold,
        int currentRequests,
        int limit,
        string planName)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogError("No se puede enviar alerta de cuota: email destinatario vacío");
            return false;
        }

        try
        {
            sib_api_v3_sdk.Client.Configuration.Default.ApiKey["api-key"] = _apiKey;
            var apiInstance = new TransactionalEmailsApi();

            var subject = threshold >= 100
                ? $"[Pagly API] {tenantName} alcanzó 100% de su cuota mensual"
                : $"[Pagly API] {tenantName} está al {threshold}% de su cuota mensual";

            var sendSmtpEmail = new SendSmtpEmail
            {
                Sender = new SendSmtpEmailSender(_senderName, _senderEmail),
                To = new List<SendSmtpEmailTo> { new SendSmtpEmailTo(toEmail, toName) },
                Subject = subject,
                HtmlContent = GenerateQuotaAlertEmailHtml(
                    toName, tenantName, threshold, currentRequests, limit, planName)
            };

            var result = await apiInstance.SendTransacEmailAsync(sendSmtpEmail);
            if (result != null)
            {
                _logger.LogInformation(
                    "Email de alerta {Threshold}% enviado a {ToEmail} para tenant {Tenant}. MessageId: {MessageId}",
                    threshold, toEmail, tenantName, result.MessageId);
                return true;
            }

            _logger.LogWarning("Brevo retornó null al enviar alerta de cuota a {ToEmail}", toEmail);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error enviando alerta de cuota {Threshold}% a {ToEmail}: {Error}",
                threshold, toEmail, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// HTML del email de alerta de cuota. Diferente tono entre 80% (warning)
    /// y 100% (crítico — se bloquearán requests adicionales del rate limiter).
    /// </summary>
    private string GenerateQuotaAlertEmailHtml(
        string toName,
        string tenantName,
        int threshold,
        int currentRequests,
        int limit,
        string planName)
    {
        var isAt100 = threshold >= 100;
        var accentColor = isAt100 ? "#dc2626" : "#f59e0b"; // red-600 vs amber-500
        var headline = isAt100
            ? $"Alcanzaste el 100% de tu cuota de API"
            : $"Estás al {threshold}% de tu cuota de API";

        var message = isAt100
            ? @"<p>Tu tenant consumió <b>toda</b> la cuota mensual del plan. Los siguientes
                 requests al API podrían ser rechazados hasta el próximo ciclo de facturación
                 o hasta que subas de plan.</p>"
            : @"<p>Llevas consumido más del 80% de tu cuota mensual. Si mantienes el ritmo
                 actual, alcanzarás el límite antes del cierre del mes.</p>";

        var percentUsed = limit > 0
            ? Math.Min(100, (int)Math.Round(currentRequests * 100.0 / limit))
            : 0;

        return $@"
<!DOCTYPE html>
<html lang='es'>
<head><meta charset='UTF-8'><title>Alerta de cuota API</title></head>
<body style='font-family: -apple-system, BlinkMacSystemFont, sans-serif; margin: 0; background: #f9fafb; padding: 24px;'>
  <div style='max-width: 560px; margin: 0 auto; background: white; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 12px rgba(0,0,0,0.05);'>
    <div style='background: {accentColor}; padding: 24px;'>
      <h1 style='color: white; margin: 0; font-size: 20px;'>{headline}</h1>
      <p style='color: rgba(255,255,255,0.85); margin: 4px 0 0; font-size: 14px;'>Tenant: {tenantName}</p>
    </div>
    <div style='padding: 24px;'>
      <p>Hola {toName},</p>
      {message}
      <div style='background: #f3f4f6; padding: 16px; border-radius: 8px; margin: 16px 0;'>
        <p style='margin: 0 0 8px; font-size: 14px; color: #6b7280;'>Uso del mes</p>
        <p style='margin: 0; font-size: 22px; font-weight: 700; color: #111827;'>
          {currentRequests:N0} / {limit:N0} requests ({percentUsed}%)
        </p>
        <p style='margin: 8px 0 0; font-size: 13px; color: #6b7280;'>Plan actual: {planName}</p>
      </div>
      <p style='color: #6b7280; font-size: 13px;'>
        Puedes ver el detalle completo en tu dashboard de API en Pagly → Settings → API Usage.
        Si quieres aumentar tu cuota, considera upgradear al plan Enterprise.
      </p>
    </div>
    <div style='padding: 16px 24px; background: #f9fafb; color: #9ca3af; font-size: 12px; text-align: center;'>
      Este email es automático. Si recibes este aviso y no esperas uso del API, contacta al equipo de Pagly.
    </div>
  </div>
</body>
</html>";
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
