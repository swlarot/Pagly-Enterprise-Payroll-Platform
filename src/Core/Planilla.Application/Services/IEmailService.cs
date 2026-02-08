namespace Planilla.Application.Services
{
    /// <summary>
    /// Servicio para envío de emails en el sistema Planilla
    /// </summary>
    public interface IEmailService
    {
        /// <summary>
        /// Envía email de invitación a un usuario para unirse a un tenant
        /// </summary>
        /// <param name="recipientEmail">Email del destinatario</param>
        /// <param name="recipientName">Nombre del destinatario</param>
        /// <param name="tenantName">Nombre del tenant/empresa</param>
        /// <param name="inviterName">Nombre de quien invita</param>
        /// <param name="inviteUrl">URL completa para aceptar la invitación</param>
        /// <param name="expiresAt">Fecha de expiración de la invitación</param>
        Task SendInvitationEmailAsync(
            string recipientEmail,
            string recipientName,
            string tenantName,
            string inviterName,
            string inviteUrl,
            DateTime expiresAt);

        /// <summary>
        /// Envía email de bienvenida a un empleado con usuario recién creado (contraseña temporal y enlace de login).
        /// </summary>
        /// <param name="recipientEmail">Email del destinatario</param>
        /// <param name="recipientName">Nombre del destinatario</param>
        /// <param name="tenantName">Nombre del tenant/empresa</param>
        /// <param name="temporaryPassword">Contraseña temporal generada</param>
        /// <param name="loginUrl">URL de la página de login</param>
        Task SendEmployeeWelcomeAsync(
            string recipientEmail,
            string recipientName,
            string tenantName,
            string temporaryPassword,
            string loginUrl);

        /// <summary>
        /// Envía un email genérico con HTML
        /// </summary>
        /// <param name="to">Destinatario</param>
        /// <param name="subject">Asunto del email</param>
        /// <param name="htmlBody">Cuerpo del mensaje en HTML</param>
        Task SendEmailAsync(string to, string subject, string htmlBody);
    }
}
