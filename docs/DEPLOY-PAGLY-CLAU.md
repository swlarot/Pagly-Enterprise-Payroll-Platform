# Despliegue de Pagly en pagly.clau.com.pa (CapRover + GitHub)

Guía completa para desplegar la aplicación Pagly/Planilla en `https://pagly.clau.com.pa` usando CapRover con despliegue automático desde GitHub.

## Resumen del flujo

```
GitHub (push) → Webhook → CapRover (git clone + docker build en servidor) → Deploy
```

**Ventaja del método GitHub:** No subes la imagen Docker. CapRover clona el repo y construye en el servidor. Más liviano y permite CI/CD automático.

---

## Parte 1: DNS en cPanel (clau.com.pa)

### 1.1 Crear subdominio pagly.clau.com.pa

1. Entra a **cPanel** de clau.com.pa.
2. Busca **Subdominios** (Subdomains).
3. Crea:
   - **Subdominio:** `pagly`
   - **Dominio:** `clau.com.pa`
   - **Document Root:** (puedes dejar el default o el que genere cPanel).

### 1.2 Apuntar al servidor CapRover

Necesitas la **IP del servidor** donde está CapRover (ej. droplet DigitalOcean).

**Opción A: Zona DNS en cPanel**

1. cPanel → **Editor de zona** (Zone Editor).
2. Busca registros para `clau.com.pa`.
3. Añade registro **A**:
   - **Nombre:** `pagly` (o `pagly.clau.com.pa` según la interfaz)
   - **TTL:** 14400
   - **Tipo:** A
   - **Registro:** `IP_DE_TU_SERVIDOR_CAPROVER`
4. Guarda.

**Opción B: Si los DNS están en otro proveedor**

Si clau.com.pa usa nameservers externos, configura el registro A en ese panel apuntando `pagly` a la IP del servidor CapRover.

### 1.3 Verificar propagación

```bash
# Debería devolver la IP de tu servidor
nslookup pagly.clau.com.pa
```

---

## Parte 2: CapRover

### 2.1 Requisitos previos

- CapRover instalado en un servidor (1–2 GB RAM recomendado).
- PostgreSQL 16+ (mismo servidor como One-Click App o Managed DB externa).
- Puerto 80/443 accesible.

### 2.2 Crear la aplicación

1. CapRover Dashboard → **Apps** → **Create New App**.
2. **App Name:** `planilla` (o `pagly`).
3. **Has Persistent Data:** No (base de datos va separada).
4. Crear.

### 2.3 Configurar dominio y SSL

1. En la app → **App Configs** → **Domain**.
2. **Captain Root Domain:** Si CapRover usa algo como `captain.tudominio.com`, deja el root configurado.
3. **App Domain:** `pagly.clau.com.pa`
4. **Enable HTTPS:** Activar (Let's Encrypt).
5. Guardar. CapRover solicitará el certificado automáticamente.

### 2.4 Habilitar despliegue desde GitHub

1. En la app → **Deployment**.
2. **Method:** GitHub.
3. Activar **Enable GitHub Deployment**.
4. CapRover mostrará:
   - **Webhook URL** (para configurar en GitHub)
   - **Branch** (ej. `main` o `master`)
5. Copia el Webhook URL.

---

## Parte 3: GitHub

### 3.1 Añadir webhook

1. Repositorio en GitHub → **Settings** → **Webhooks** → **Add webhook**.
2. **Payload URL:** pegar el Webhook URL de CapRover.
3. **Content type:** `application/json`.
4. **Events:** "Just the push event".
5. **Active:** Sí.
6. Crear webhook.

### 3.2 Push para desplegar

Cada `git push` al branch configurado (ej. `main`) disparará:

1. CapRover recibe el webhook.
2. Clona el repo (o hace pull).
3. Ejecuta `docker build` en el servidor.
4. Despliega la nueva imagen.

---

## Parte 4: Variables de entorno en CapRover

En la app → **App Configs** → **Environment Variables** añade:

### Obligatorias

| Variable | Valor | Descripción |
|----------|-------|-------------|
| `ConnectionStrings__DefaultConnection` | `Host=...;Port=5432;Database=PlanillaDB;Username=...;Password=...` | PostgreSQL |
| `Jwt__Key` | Clave secreta ≥32 caracteres | Para firmar JWTs |
| `App__BaseUrl` | `https://app.pagly.clau.com.pa` | URL pública de la app |
| `Cors__AllowedOrigins` | `https://app.pagly.clau.com.pa,tauri://localhost,https://tauri.localhost,http://tauri.localhost` | Orígenes permitidos para CORS (web + desktop) |

### Opcionales (según uso)

| Variable | Valor |
|----------|-------|
| `Jwt__Issuer` | `Pagly` |
| `Jwt__Audience` | `Pagly` |
| `Stripe__SecretKey` | Si usas pagos |
| `Stripe__PublishableKey` | Si usas pagos |
| `Stripe__WebhookSecret` | Si usas webhooks |
| `Stripe__SuccessUrl` | `https://app.pagly.clau.com.pa/dashboard?checkout=success` |
| `Stripe__CancelUrl` | `https://app.pagly.clau.com.pa/billing?checkout=cancel` |
| `Brevo__ApiKey` | API Key de Brevo |
| `Brevo__SenderEmail` | ej. `noreply@clau.com.pa` |
| `Brevo__SenderName` | `Pagly` |

---

## Parte 5: Build Arguments (para el frontend)

SPA y API van en el mismo dominio, así que el frontend usa URLs relativas. En CapRover:

**App Configs → Build Arguments** (si CapRover lo soporta para tu método de deploy):

| Argument | Valor |
|----------|-------|
| `VITE_API_URL` | *(dejar vacío)* |

Si no hay Build Arguments, el `Dockerfile` usa `ARG VITE_API_URL=` (vacío por defecto), lo cual es correcto para same-origin.

---

## Parte 6: Health check

En CapRover → App Configs:

- **Health Check Path:** `/health`
- **Health Check Port:** 80

---

## Parte 7: Migraciones

Las migraciones de EF Core se ejecutan **automáticamente al arrancar** la app (en `Program.cs`). No hace falta ejecutarlas manualmente.

---

## Resumen de URLs y flujo

| Concepto | Valor |
|----------|-------|
| URL pública | `https://app.pagly.clau.com.pa` |
| API | `https://app.pagly.clau.com.pa/api/...` (mismo origen) |
| Health | `https://app.pagly.clau.com.pa/health` |
| Despliegue | Push a GitHub → build en servidor → deploy |

---

## Checklist final

- [ ] DNS: `pagly.clau.com.pa` apunta a la IP del servidor CapRover.
- [ ] CapRover: app creada con dominio `pagly.clau.com.pa` y SSL.
- [ ] GitHub: webhook configurado con la URL de CapRover.
- [ ] Variables: `ConnectionStrings__DefaultConnection`, `Jwt__Key`, `App__BaseUrl`, `Cors__AllowedOrigins`.
- [ ] Base de datos PostgreSQL accesible desde el contenedor.
- [ ] Health check responde 200 en `/health`.
- [ ] Login y navegación funcionan en `https://app.pagly.clau.com.pa`.

---

## Alternativa: Deploy manual con CLI

Si prefieres no usar GitHub:

```bash
npm i -g caprover
caprover serversetup   # una vez
caprover deploy        # desde la raíz del repo
```

Elegir la app, branch, y que construya desde el Dockerfile.
