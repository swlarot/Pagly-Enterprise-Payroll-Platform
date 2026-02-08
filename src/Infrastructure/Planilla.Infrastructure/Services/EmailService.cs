using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Planilla.Application.Services;

namespace Vorluno.Planilla.Infrastructure.Services
{
    /// <summary>
    /// Servicio de envío de emails usando SMTP
    /// Soporta Gmail, Outlook, SendGrid y cualquier servidor SMTP
    /// </summary>
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly SmtpClient _smtpClient;
        private readonly string _fromEmail;
        private readonly string _fromName;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            // Configurar SMTP desde appsettings
            var smtpHost = _configuration["Email:SmtpHost"] ?? "smtp.gmail.com";
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            var smtpUser = _configuration["Email:SmtpUser"] ?? "";
            var smtpPass = _configuration["Email:SmtpPassword"] ?? "";

            _fromEmail = _configuration["Email:FromEmail"] ?? "noreply@planilla.com";
            _fromName = _configuration["Email:FromName"] ?? "Planilla";

            _smtpClient = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = true,
                Timeout = 30000 // 30 segundos
            };

            _logger.LogInformation($"EmailService initialized with SMTP: {smtpHost}:{smtpPort}");
        }

        public async Task SendInvitationEmailAsync(
            string recipientEmail,
            string recipientName,
            string tenantName,
            string inviterName,
            string inviteUrl,
            DateTime expiresAt)
        {
            _logger.LogInformation($"Sending invitation email to {recipientEmail} for tenant {tenantName}");

            var subject = $"Invitación a {tenantName} - Planilla";
            var htmlBody = GenerateInvitationTemplate(
                recipientName,
                tenantName,
                inviterName,
                inviteUrl,
                expiresAt);

            await SendEmailAsync(recipientEmail, subject, htmlBody);

            _logger.LogInformation($"Invitation email sent successfully to {recipientEmail}");
        }

        public async Task SendEmployeeWelcomeAsync(
            string recipientEmail,
            string recipientName,
            string tenantName,
            string temporaryPassword,
            string loginUrl)
        {
            _logger.LogInformation("Sending employee welcome email to {RecipientEmail} for tenant {TenantName}", recipientEmail, tenantName);

            var subject = $"Bienvenido a {tenantName} - Planilla";
            var htmlBody = $@"<!DOCTYPE html>
<html lang=""es"">
<head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0""></head>
<body style=""font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background: linear-gradient(135deg, #10b981 0%, #059669 100%); color: white; padding: 24px; text-align: center; border-radius: 8px 8px 0 0;"">
        <h1 style=""margin: 0; font-size: 24px;"">Bienvenido a Planilla</h1>
        <p style=""margin: 8px 0 0 0; opacity: 0.9;"">{tenantName}</p>
    </div>
    <div style=""background: #fff; padding: 24px; border: 1px solid #e5e7eb; border-top: none; border-radius: 0 0 8px 8px;"">
        <p>Hola <strong>{recipientName}</strong>,</p>
        <p>Se ha creado tu acceso al sistema de planilla de <strong>{tenantName}</strong>.</p>
        <p><strong>Contraseña temporal:</strong></p>
        <p style=""background: #f3f4f6; padding: 12px; border-radius: 6px; font-family: monospace;"">{temporaryPassword}</p>
        <p><strong>Recomendamos cambiar tu contraseña después del primer inicio de sesión.</strong></p>
        <p style=""margin-top: 24px;"">
            <a href=""{loginUrl}"" style=""display: inline-block; background: #10b981; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: 600;"">Iniciar sesión</a>
        </p>
        <p style=""margin-top: 24px; font-size: 13px; color: #6b7280;"">Si no esperabas este correo, puedes ignorarlo de forma segura.</p>
    </div>
</body>
</html>";

            await SendEmailAsync(recipientEmail, subject, htmlBody);
            _logger.LogInformation("Employee welcome email sent to {RecipientEmail}", recipientEmail);
        }

        public async Task SendEmailAsync(string to, string subject, string htmlBody)
        {
            try
            {
                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_fromEmail, _fromName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true,
                    Priority = MailPriority.Normal
                };

                mailMessage.To.Add(to);

                await _smtpClient.SendMailAsync(mailMessage);

                _logger.LogInformation($"Email sent successfully to {to}");
            }
            catch (SmtpException smtpEx)
            {
                _logger.LogError(smtpEx, $"SMTP error sending email to {to}: {smtpEx.Message}");
                throw new InvalidOperationException($"Error al enviar email: {smtpEx.Message}", smtpEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error sending email to {to}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Genera el template HTML profesional para email de invitación
        /// </summary>
        private string GenerateInvitationTemplate(
            string recipientName,
            string tenantName,
            string inviterName,
            string inviteUrl,
            DateTime expiresAt)
        {
            var expiresInDays = Math.Max(1, (expiresAt - DateTime.UtcNow).Days);

            return $@"<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Invitación a {tenantName}</title>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            line-height: 1.6;
            color: #333;
            background-color: #f5f5f5;
            margin: 0;
            padding: 0;
        }}
        .email-wrapper {{
            max-width: 600px;
            margin: 0 auto;
            background-color: #ffffff;
        }}
        .header {{
            background: linear-gradient(135deg, #3b82f6 0%, #2563eb 100%);
            color: white;
            padding: 40px 30px;
            text-align: center;
        }}
        .header h1 {{
            margin: 0;
            font-size: 28px;
            font-weight: 700;
        }}
        .header p {{
            margin: 10px 0 0 0;
            font-size: 14px;
            opacity: 0.9;
        }}
        .content {{
            padding: 40px 30px;
        }}
        .greeting {{
            font-size: 18px;
            margin-bottom: 20px;
            color: #1f2937;
        }}
        .message {{
            font-size: 16px;
            color: #4b5563;
            margin-bottom: 15px;
        }}
        .highlight {{
            background-color: #eff6ff;
            border-left: 4px solid #3b82f6;
            padding: 20px;
            margin: 30px 0;
            border-radius: 4px;
        }}
        .highlight strong {{
            color: #1e40af;
            font-size: 18px;
        }}
        .button-container {{
            text-align: center;
            margin: 35px 0;
        }}
        .button {{
            display: inline-block;
            background: linear-gradient(135deg, #3b82f6 0%, #2563eb 100%);
            color: white !important;
            padding: 16px 40px;
            text-decoration: none;
            border-radius: 8px;
            font-weight: 600;
            font-size: 16px;
            box-shadow: 0 4px 6px rgba(59, 130, 246, 0.3);
            transition: all 0.3s ease;
        }}
        .button:hover {{
            box-shadow: 0 6px 12px rgba(59, 130, 246, 0.4);
            transform: translateY(-2px);
        }}
        .warning {{
            background-color: #fef3c7;
            border-left: 4px solid #f59e0b;
            padding: 20px;
            margin: 25px 0;
            border-radius: 4px;
        }}
        .warning p {{
            margin: 0;
            color: #92400e;
        }}
        .warning strong {{
            color: #78350f;
            font-size: 16px;
        }}
        .features {{
            background-color: #f9fafb;
            padding: 25px;
            border-radius: 8px;
            margin: 25px 0;
        }}
        .features h3 {{
            color: #1f2937;
            margin-top: 0;
            margin-bottom: 15px;
            font-size: 18px;
        }}
        .features ul {{
            margin: 0;
            padding-left: 20px;
            color: #4b5563;
        }}
        .features li {{
            margin: 8px 0;
        }}
        .footer {{
            background-color: #f9fafb;
            padding: 30px;
            text-align: center;
            border-top: 1px solid #e5e7eb;
        }}
        .footer p {{
            margin: 5px 0;
            font-size: 13px;
            color: #6b7280;
        }}
        .footer .url {{
            color: #3b82f6;
            word-break: break-all;
            font-size: 12px;
            margin-top: 15px;
            padding: 10px;
            background-color: #ffffff;
            border-radius: 4px;
            border: 1px solid #e5e7eb;
        }}
        @media only screen and (max-width: 600px) {{
            .content {{
                padding: 30px 20px;
            }}
            .header {{
                padding: 30px 20px;
            }}
            .button {{
                padding: 14px 30px;
                font-size: 15px;
            }}
        }}
    </style>
</head>
<body>
    <div class=""email-wrapper"">
        <div class=""header"">
            <h1>📋 Planilla</h1>
            <p>Sistema de Gestión de Planilla Empresarial</p>
        </div>

        <div class=""content"">
            <p class=""greeting"">Hola <strong>{recipientName}</strong>,</p>

            <p class=""message"">
                <strong>{inviterName}</strong> te ha invitado a unirte a
                <strong>{tenantName}</strong> en Planilla.
            </p>

            <div class=""features"">
                <h3>¿Qué es Planilla?</h3>
                <ul>
                    <li>Gestión completa de empleados y departamentos</li>
                    <li>Cálculo automático de planillas con CSS, SE e ISR</li>
                    <li>Control de préstamos, anticipos y deducciones</li>
                    <li>Reportes y exportación a Excel/PDF</li>
                    <li>Gestión de vacaciones y ausencias</li>
                </ul>
            </div>

            <div class=""button-container"">
                <a href=""{inviteUrl}"" class=""button"">✓ Aceptar Invitación</a>
            </div>

            <div class=""warning"">
                <p><strong>⏰ Esta invitación expira en {expiresInDays} día{(expiresInDays != 1 ? "s" : "")}</strong></p>
                <p style=""margin-top: 8px;"">Asegúrate de aceptarla antes del <strong>{expiresAt:dd/MM/yyyy 'a las' HH:mm}</strong>.</p>
            </div>

            <p class=""message"" style=""margin-top: 30px; font-size: 14px; color: #6b7280;"">
                Si no esperabas esta invitación o no conoces a {inviterName},
                puedes ignorar este email de forma segura.
            </p>
        </div>

        <div class=""footer"">
            <p><strong>Este es un mensaje automático de Planilla</strong></p>
            <p>Por favor, no respondas a este email.</p>
            <p style=""margin-top: 20px;"">Si tienes problemas con el botón, copia y pega esta URL en tu navegador:</p>
            <div class=""url"">{inviteUrl}</div>
            <p style=""margin-top: 20px; font-size: 12px; color: #9ca3af;"">
                © {DateTime.UtcNow.Year} Planilla. Todos los derechos reservados.
            </p>
        </div>
    </div>
</body>
</html>";
        }
    }
}
