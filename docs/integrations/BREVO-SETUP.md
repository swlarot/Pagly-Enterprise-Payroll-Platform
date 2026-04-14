# Configuración de Brevo para Envío de Emails

## Requisitos Previos

1. **Cuenta de Brevo**: Debes tener una cuenta activa en [Brevo](https://www.brevo.com/)
2. **API Key**: Necesitas generar una API Key desde tu panel de Brevo
3. **Sender Email Verificado**: El email remitente debe estar verificado en Brevo
4. **Dominio Verificado**: El dominio del sender email debe estar verificado en Brevo

## Pasos de Configuración

### 1. Obtener API Key de Brevo

1. Inicia sesión en tu cuenta de Brevo
2. Ve a **Settings** → **API Keys** (o **SMTP & API** → **API Keys**)
3. Haz clic en **Generate a new API key**
4. Asigna un nombre descriptivo (ej: "Planilla Production")
5. Selecciona los permisos necesarios:
   - ✅ **Send emails** (Transactional emails)
   - ✅ **Access account information** (opcional, para estadísticas)
6. Copia la API Key **inmediatamente** (solo se muestra una vez)
7. La API Key debe verse como: `xkeysib-abc123...` (no debe estar en Base64)

### 2. Verificar Sender Email

1. En Brevo, ve a **Settings** → **Senders & IP**
2. Haz clic en **Add a sender**
3. Ingresa el email: `contacto@vorluno.dev`
4. Ingresa el nombre: `Planilla`
5. Brevo enviará un email de verificación a `contacto@vorluno.dev`
6. Haz clic en el enlace de verificación en el email
7. Espera a que el estado cambie a **Verified** ✅

### 3. Verificar Dominio (Recomendado)

1. En Brevo, ve a **Settings** → **Senders & IP** → **Domains**
2. Haz clic en **Add a domain**
3. Ingresa el dominio: `vorluno.dev`
4. Brevo te dará registros DNS para agregar:
   - **SPF**: `v=spf1 include:spf.brevo.com ~all`
   - **DKIM**: Registros CNAME específicos
   - **DMARC**: (opcional pero recomendado)
5. Agrega estos registros en tu proveedor DNS
6. Espera a que Brevo verifique el dominio (puede tomar hasta 48 horas)

### 4. Configurar en appsettings.json

```json
{
  "Brevo": {
    "ApiKey": "xkeysib-tu-api-key-aqui",
    "SenderEmail": "contacto@vorluno.dev",
    "SenderName": "Planilla"
  }
}
```

**IMPORTANTE**: 
- La API Key debe ser el valor **directo** de Brevo (no Base64)
- El formato debe ser: `xkeysib-...`
- El sender email DEBE estar verificado en Brevo antes de usar

## Verificación de Configuración

### Probar Envío de Email

Usa el endpoint de prueba (solo SystemAdmin):

```bash
POST /api/admin/test-email
Content-Type: application/json
Authorization: Bearer <tu-token>

{
  "toEmail": "tu-email@ejemplo.com",
  "toName": "Tu Nombre",
  "tenantName": "Prueba"
}
```

### Verificar Logs

Revisa los logs de la aplicación para ver:
- ✅ "BrevoEmailService inicializado" - Configuración cargada correctamente
- ✅ "Email enviado exitosamente" - Email enviado correctamente
- ❌ "Error de API de Brevo" - Revisa el ErrorContent para detalles

## Errores Comunes

### Error 401: Unauthorized
- **Causa**: API Key incorrecta o inválida
- **Solución**: Verifica que la API Key sea correcta y tenga permisos de "Send emails"

### Error 400: Invalid sender
- **Causa**: El sender email no está verificado en Brevo
- **Solución**: Verifica el sender email en Brevo (Settings → Senders)

### Error 403: Domain not verified
- **Causa**: El dominio del sender no está verificado
- **Solución**: Verifica el dominio en Brevo o usa un sender de un dominio verificado

### Error 402: Insufficient credits
- **Causa**: No hay créditos suficientes en tu cuenta de Brevo
- **Solución**: Recarga créditos en tu cuenta de Brevo

## Troubleshooting

### Los emails no llegan

1. **Verifica los logs**: Busca errores específicos de Brevo
2. **Revisa la carpeta de spam**: Los emails pueden estar en spam
3. **Verifica el sender**: Asegúrate de que esté verificado
4. **Revisa créditos**: Verifica que tengas créditos disponibles en Brevo
5. **Prueba con el endpoint de prueba**: Usa `/api/admin/test-email` para diagnosticar

### La API Key no funciona

1. **Formato**: Debe empezar con `xkeysib-`
2. **Permisos**: Debe tener permisos de "Send emails"
3. **Estado**: Verifica que la API Key esté activa en Brevo
4. **Regenerar**: Si es necesario, genera una nueva API Key

## Recursos Adicionales

- [Documentación de Brevo API](https://developers.brevo.com/)
- [Guía de API Keys](https://help.brevo.com/hc/en-us/articles/209467485-Create-and-manage-your-API-keys)
- [Verificación de Senders](https://help.brevo.com/hc/en-us/articles/209467265-Verify-your-sender-address)
- [Verificación de Dominios](https://help.brevo.com/hc/en-us/articles/209467485-Create-and-manage-your-API-keys)

## Notas Importantes

- ⚠️ La API Key solo se muestra **una vez** al crearla. Guárdala de forma segura.
- ⚠️ El sender email debe estar verificado antes de enviar emails.
- ⚠️ Los emails pueden tardar unos minutos en llegar.
- ⚠️ Revisa los logs para diagnosticar problemas específicos.
