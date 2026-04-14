# Despliegue de Vorluno Planilla en CapRover

Guía para desplegar la aplicación multi-tenant en un servidor CapRover (por ejemplo, un droplet de DigitalOcean).

> **Despliegue en pagly.clau.com.pa:** Ver la guía completa en [DEPLOY-PAGLY-CLAU.md](./DEPLOY-PAGLY-CLAU.md), que incluye DNS en cPanel, GitHub webhook y todas las variables para ese dominio.

## Requisitos

- Servidor con CapRover instalado y un droplet con Docker (recomendado 1 GB RAM mínimo; 2 GB si PostgreSQL va en el mismo servidor).
- PostgreSQL 16+ (en el mismo servidor como One-Click App o externo como DigitalOcean Managed Database).
- Dominio o subdominio apuntando al servidor (para SSL con Let's Encrypt).

## Variables de entorno en CapRover

Configurar en la app de CapRover (App Configs > Environment Variables). **No subir secretos al repositorio.**

| Variable | Obligatorio | Descripción |
|----------|-------------|-------------|
| `ConnectionStrings__DefaultConnection` | Sí | Cadena de conexión PostgreSQL. Ej: `Host=...;Port=5432;Database=PlanillaDB;Username=...;Password=...` |
| `Jwt__Key` | Sí | Clave secreta para firmar JWTs (mín. 32 caracteres). |
| `Jwt__Issuer` | No | Por defecto: `Planilla`. |
| `Jwt__Audience` | No | Por defecto: `Planilla`. |
| `App__BaseUrl` | Sí | URL pública de la app. Ej: `https://pagly.clau.com.pa` |
| `Cors__AllowedOrigins` | Sí (prod) | Orígenes permitidos separados por coma. Ej: `https://pagly.clau.com.pa` |
| `Stripe__SecretKey` | Si usas Stripe | Clave secreta de Stripe. |
| `Stripe__PublishableKey` | Si usas Stripe | Clave pública de Stripe. |
| `Stripe__WebhookSecret` | Si usas Stripe | Secret del webhook de Stripe. |
| `Stripe__SuccessUrl`, `Stripe__CancelUrl` | Si usas Stripe | URLs de retorno tras checkout. |
| `Brevo__ApiKey` | Si usas email | API Key de Brevo. |
| `Brevo__SenderEmail`, `Brevo__SenderName` | Si usas email | Remitente de correos. |

**Cómo CapRover inyecta las variables:** Cada variable que definas en "Environment Variables" se pasa al contenedor al arrancar. ASP.NET Core lee `ConnectionStrings__DefaultConnection` (doble guión) como la clave `ConnectionStrings:DefaultConnection` en configuración.

## Pasos de deploy

### Recomendado: GitHub Webhook (sin subir imagen)

CapRover clona el repo desde GitHub y hace el build en el servidor. No subes la imagen; solo haces push al repo. Más liviano y permite CI/CD automático.

Ver [DEPLOY-PAGLY-CLAU.md](./DEPLOY-PAGLY-CLAU.md) para la ruta completa con pagly.clau.com.pa.

### Opción A: CLI de CapRover

1. Instalar CLI: `npm i -g caprover`
2. Configurar servidor (una vez): `caprover serversetup`
3. Crear la app en el dashboard de CapRover (por ejemplo nombre: `planilla`).
4. En el directorio raíz del repo (donde está `Dockerfile` y `captain-definition`):

   ```bash
   caprover deploy
   ```

   Elegir la app, branch y que construya desde el Dockerfile.

### Opción B: Webhook de Git (GitHub/GitLab)

1. En CapRover: App > Deployment > "Enable GitHub Deployment" (o GitLab).
2. Añadir el webhook que te indique CapRover en tu repo.
3. En cada push al branch configurado, CapRover hará build y deploy.

**Build args (opcional):** Si el frontend debe apuntar a una URL de API distinta en build time, en CapRover en "Build Arguments" añadir:

- `VITE_API_URL` = URL pública del API (por ejemplo la misma que `App__BaseUrl` si SPA y API van juntos).

## Migraciones

Las migraciones de EF Core se ejecutan **automáticamente al arrancar la aplicación** (en `Program.cs`). No hace falta ejecutar `dotnet ef database update` a mano. Si la migración falla, el contenedor no pasará a escuchar peticiones y CapRover marcará el deploy como fallido.

En los logs deberías ver líneas como:

- `Aplicando migraciones pendientes...`
- `Migraciones aplicadas correctamente`

## Health check

- **URL:** `GET /health`
- **Respuesta:** JSON con `status`, `checks` (postgres, multi_tenant), etc.

En CapRover, en la configuración de la app, puedes definir "Health Check Path" = `/health` para que el dashboard use este endpoint.

## Rollback

1. **Versión anterior:** En CapRover, en la app > "Version Management" puedes desplegar una versión (imagen) anterior si quedó guardada.
2. **Base de datos:** Las migraciones son aplicadas automáticamente; no hay rollback automático de esquema. Antes de un deploy con migraciones nuevas, conviene:
   - Tener backup de PostgreSQL (pg_dump o backup gestionado de tu proveedor).
   - Probar migraciones en un entorno de staging.

## Troubleshooting

### Memoria (OOM)

Si el contenedor se mata por memoria (droplet 1 GB con app + PostgreSQL en el mismo servidor):

- Reducir workers o considerar mover PostgreSQL a un Managed Database.
- Aumentar RAM del droplet (p. ej. 2 GB).

### Migraciones fallan al arrancar

- Revisar logs en CapRover (App > App Logs).
- Verificar que `ConnectionStrings__DefaultConnection` sea correcta y que PostgreSQL esté accesible desde el contenedor (red/puerto).
- Si la DB está en otro host, comprobar reglas de firewall.

### CORS

Si el frontend (mismo dominio o distinto) no puede llamar al API:

- Añadir el origen exacto (con protocolo y sin barra final) en `Cors__AllowedOrigins`, separado por comas si hay varios.
- Ejemplo: `https://planilla.tudominio.com,https://app.tudominio.com`.

### Logs en producción

En producción el logging usa **JSON** (console) e incluye scope con **TenantId** cuando el request es de un tenant. Puedes enviar stdout a un agregador (ej. archivo, syslog, servicio externo) y parsear el JSON.

### Imagen muy grande o build lento

- El `.dockerignore` ya excluye `node_modules`, `bin/obj`, tests y docs.
- Build típico: 3–5 min. Imagen final aproximada: 200–280 MB.

## Checklist de verificación en CapRover

- [ ] La imagen se construye sin errores.
- [ ] El health check responde 200 en `GET /health` y el JSON incluye `postgres` y `multi_tenant`.
- [ ] En los logs aparece "Migraciones aplicadas correctamente" (o equivalente).
- [ ] CORS permite el origen de producción (login y llamadas API desde el frontend).
- [ ] Con un usuario de tenant, en los logs se ve `TenantId` en el scope de las peticiones.
