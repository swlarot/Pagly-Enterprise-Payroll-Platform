# Configuración del Sistema de Emails - Planilla

## Resumen

El sistema de emails de Planilla permite enviar invitaciones profesionales a usuarios cuando se les invita a un tenant. El servicio está implementado con:

- **IEmailService**: Interfaz en `Planilla.Application/Services/`
- **EmailService**: Implementación SMTP en `Planilla.Infrastructure/Services/`
- **Template HTML**: Email responsive y profesional con diseño moderno

## Configuración de SMTP

### 1. Configurar `appsettings.json`

Editar `src/UI/Planilla.Web/appsettings.json`:

```json
{
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUser": "your-email@gmail.com",
    "SmtpPassword": "your-app-password",
    "FromEmail": "noreply@planilla.com",
    "FromName": "Planilla - Sistema de Planilla"
  },
  "App": {
    "BaseUrl": "https://localhost:5001",
    "Name": "Planilla"
  }
}
```

### 2. Configuración para Gmail

#### Opción A: App Password (Recomendado)

1. Ir a [Google Account Security](https://myaccount.google.com/security)
2. Habilitar **2-Step Verification**
3. Ir a **App Passwords**
4. Generar un password para "Mail" + "Windows Computer"
5. Copiar el password de 16 caracteres
6. Usar ese password en `SmtpPassword` (sin espacios)

Ejemplo:
```json
{
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUser": "tu-email@gmail.com",
    "SmtpPassword": "abcd efgh ijkl mnop",  // 16 caracteres generados
    "FromEmail": "noreply@planilla.com",
    "FromName": "Planilla"
  }
}
```

#### Opción B: Less Secure Apps (NO Recomendado)

Solo para desarrollo local:
1. Ir a [Less Secure Apps](https://myaccount.google.com/lesssecureapps)
2. Activar "Allow less secure apps"
3. Usar tu password normal de Gmail

### 3. Configuración para Outlook/Hotmail

```json
{
  "Email": {
    "SmtpHost": "smtp-mail.outlook.com",
    "SmtpPort": 587,
    "SmtpUser": "your-email@outlook.com",
    "SmtpPassword": "your-password",
    "FromEmail": "noreply@planilla.com",
    "FromName": "Planilla"
  }
}
```

### 4. Configuración para SendGrid (Producción Recomendada)

```json
{
  "Email": {
    "SmtpHost": "smtp.sendgrid.net",
    "SmtpPort": 587,
    "SmtpUser": "apikey",  // Literal "apikey"
    "SmtpPassword": "SG.xxxxxxxxxxxxxxxxxxxxx",  // Tu API Key de SendGrid
    "FromEmail": "noreply@planilla.com",
    "FromName": "Planilla"
  }
}
```

**Ventajas de SendGrid:**
- 100 emails gratis por día
- No requiere 2FA
- Mejor deliverability
- Reportes de envíos
- Gestión de bounces

### 5. Configuración para Amazon SES (Empresas)

```json
{
  "Email": {
    "SmtpHost": "email-smtp.us-east-1.amazonaws.com",
    "SmtpPort": 587,
    "SmtpUser": "YOUR_SMTP_USERNAME",
    "SmtpPassword": "YOUR_SMTP_PASSWORD",
    "FromEmail": "noreply@planilla.com",
    "FromName": "Planilla"
  }
}
```

## Flujo de Invitación

### 1. Admin invita usuario

```http
POST /api/tenants/invite
Authorization: Bearer {token}
Content-Type: application/json

{
  "email": "nuevo@example.com",
  "role": "Manager"
}
```

### 2. Sistema envía email

El `InvitationService` automáticamente:

1. Crea la invitación en la base de datos
2. Genera un token único
3. Construye la URL de aceptación
4. Envía email con template HTML profesional
5. Si el email falla, la invitación se crea igual (no se bloquea)

### 3. Usuario recibe email

Email incluye:

- Nombre del tenant/empresa
- Nombre de quien invita
- Descripción de Planilla
- Botón "Aceptar Invitación"
- Advertencia de expiración (7 días)
- URL manual por si el botón no funciona

### 4. Usuario acepta invitación

Al hacer clic en el botón, va a:

```
https://localhost:5001/accept-invite?token=abc123def456
```

Frontend muestra formulario para:
- Confirmar email
- Crear contraseña (si es usuario nuevo)
- Aceptar términos

## Template HTML

El email incluye:

### Diseño

- **Header azul** con logo de Planilla
- **Sección de bienvenida** personalizada
- **Features del sistema** (bullet list)
- **CTA button** destacado
- **Warning de expiración** (amarillo)
- **Footer** con información legal

### Responsive

- Desktop: 600px de ancho
- Mobile: Se adapta automáticamente
- Email clients: Compatible con Gmail, Outlook, Apple Mail

### Personalización

El template usa:
- `recipientName`: Nombre del invitado (o email si no se provee)
- `tenantName`: Nombre de la empresa
- `inviterName`: Nombre de quien invita
- `inviteUrl`: URL completa con token
- `expiresAt`: Fecha de expiración

## Manejo de Errores

### Email no se envía

El sistema NO bloquea la invitación si el email falla:

```csharp
try
{
    await _emailService.SendInvitationEmailAsync(...);
}
catch (Exception ex)
{
    // Log warning pero continuar
    _logger.LogWarning(ex, "Failed to send email");
}
```

El admin puede:
1. Reenviar invitación
2. Ver invitaciones pendientes en `/admin/invitations`
3. Copiar URL manualmente y enviarla por otro medio

### Debugging

Para debug, revisar logs:

```bash
# Buscar logs de email
dotnet run | grep "EmailService"
dotnet run | grep "Invitation email"
```

Logs incluyen:
- ✅ "Invitation email sent successfully to {email}"
- ⚠️ "Failed to send invitation email to {email}"
- ❌ "SMTP error sending email: {error}"

## Validación en Producción

### Checklist antes de producción:

- [ ] **SMTP configurado correctamente**
  - Verificar que `SmtpUser` y `SmtpPassword` son válidos
  - Test de envío exitoso

- [ ] **FromEmail verificado**
  - Si usas SendGrid/SES, verificar dominio
  - Si usas Gmail, usar App Password

- [ ] **BaseUrl correcto**
  - Cambiar de `https://localhost:5001` a tu dominio real
  - Ejemplo: `https://planilla.tuempresa.com`

- [ ] **Rate limits**
  - Gmail: 500 emails/día
  - SendGrid Free: 100 emails/día
  - Amazon SES: Depende del plan

- [ ] **Logs monitoreados**
  - Configurar alertas para errores de SMTP
  - Monitorear bounce rate

### Test de envío

Crear invitación y verificar:

1. Email llega a inbox (no spam)
2. Template se ve correctamente
3. Botón funciona
4. URL manual funciona
5. Expiración es correcta (7 días)

## Personalización del Template

Para modificar el template HTML, editar:

`C:/Planilla/src/Infrastructure/Planilla.Infrastructure/Services/EmailService.cs`

Método: `GenerateInvitationTemplate()`

### Variables disponibles:

```csharp
recipientName  // Nombre del destinatario
tenantName     // Nombre del tenant/empresa
inviterName    // Nombre de quien invita
inviteUrl      // URL completa con token
expiresAt      // DateTime de expiración
expiresInDays  // Días restantes (calculado)
```

### Ejemplo de cambio:

```csharp
// Cambiar color del botón
.button {{
    background: linear-gradient(135deg, #10b981 0%, #059669 100%); // Verde
}}

// Cambiar logo
<h1>🏢 Mi Empresa</h1>
```

## Troubleshooting

### Email no llega

1. **Verificar spam**: Revisar carpeta de spam
2. **Verificar SMTP**: Probar credenciales con cliente email
3. **Verificar firewall**: Puerto 587 debe estar abierto
4. **Verificar logs**: Buscar errores en consola

### Email llega sin formato

1. **Cliente no soporta HTML**: Algunos clientes solo muestran texto plano
2. **CSS no soportado**: Usar inline styles (ya implementado)
3. **Imágenes bloqueadas**: No usar imágenes en template

### Token inválido

1. **URL mal formada**: Verificar `App:BaseUrl` en config
2. **Token expirado**: Invitación expira en 7 días
3. **Token ya usado**: Solo se puede aceptar una vez

## Próximos Pasos

Para mejorar el sistema de emails:

1. **Templates adicionales:**
   - Email de bienvenida
   - Email de cambio de contraseña
   - Email de recordatorio de expiración
   - Email de confirmación de pago

2. **Mejoras:**
   - Queue de emails (Hangfire)
   - Retry automático en fallas
   - Tracking de opens/clicks
   - A/B testing de templates

3. **Producción:**
   - Migrar a SendGrid o SES
   - Configurar SPF/DKIM/DMARC
   - Monitoreo de deliverability
   - Gestión de bounces

## Soporte

Para ayuda con configuración de emails:

- Gmail App Passwords: https://support.google.com/accounts/answer/185833
- SendGrid: https://sendgrid.com/docs/
- Amazon SES: https://docs.aws.amazon.com/ses/

---

**Implementado por:** PlanillaBackendArchitect
**Fecha:** 2026-01-31
**Versión:** 1.0
