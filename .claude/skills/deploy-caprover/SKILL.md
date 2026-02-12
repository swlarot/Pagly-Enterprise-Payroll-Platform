---
name: deploy-caprover
description: >
  Deploy Planilla/Pagly to CapRover on DigitalOcean. Use when asked to deploy,
  check deploy status, diagnose build failures, or review if changes break the pipeline.
  Also use when modifying Dockerfile, captain-definition, vite.config.js, or package.json.
disable-model-invocation: true
allowed-tools: Bash, Read, Grep
---
# Deploy Planilla a CapRover (DigitalOcean)

## Cómo funciona el pipeline automático

```
git push origin master
       ↓
GitHub webhook → CapRover
       ↓
CapRover corre: docker build -f ./Dockerfile .
       ↓
Contenedor arranca → Program.cs ejecuta MigrateAsync()
       ↓
App en producción en https://app.pagly.app (o dominio configurado)
```

**NO se necesita correr `caprover deploy` manualmente.** El push a `master` dispara el deploy automáticamente.

---

## Arquitectura del Dockerfile (3 stages)

```
Stage 1: node:20-alpine       → Build React (Vite)
         WORKDIR /app/client
         outDir: '../wwwroot' → genera /app/wwwroot/index.html + app.js + app.css

Stage 2: dotnet/sdk:9.0       → dotnet restore + publish
         COPY --from=stage1 /app/wwwroot → src/UI/Planilla.Web/wwwroot
         dotnet publish --no-restore → /app/publish

Stage 3: dotnet/aspnet:9.0    → Runtime mínimo
         Puerto: 80
         ENTRYPOINT: dotnet Vorluno.Planilla.Web.dll
```

---

## Archivos críticos del deploy — NUNCA modificar sin revisar

| Archivo | Qué hace | Riesgo |
|---------|----------|--------|
| `Dockerfile` | Define el build completo | Rompe todo el deploy |
| `captain-definition` | Apunta CapRover al Dockerfile | Sin esto CapRover no sabe qué hacer |
| `vite.config.js` | `outDir: '../wwwroot'` genera el build en el path que espera el Dockerfile | Cambiar outDir = frontend no se incluye |
| `src/UI/Planilla.Web/Vorluno.Planilla.Web.csproj` | El proyecto principal que publica dotnet | Cambiar nombre = Dockerfile falla |
| `Planilla.sln` | dotnet restore lee el .sln | Proyectos no incluidos no se restauran |

---

## Checklist ANTES de hacer push a master

### Frontend
- [ ] `cd src/UI/Planilla.Web/ClientApp && npm run build` — exitoso sin errores
- [ ] `src/UI/Planilla.Web/wwwroot/index.html` existe después del build
- [ ] Si se agregaron dependencias npm: `package-lock.json` está commiteado (el Dockerfile usa `npm ci`)
- [ ] `vite.config.js` no modificado (especialmente `outDir` y `rollupOptions`)

### Backend
- [ ] `dotnet build src/UI/Planilla.Web/Vorluno.Planilla.Web.csproj` — 0 errores
- [ ] Nuevos servicios registrados en `Program.cs`
- [ ] Si hay nuevas migraciones: están en `src/Infrastructure/Planilla.Infrastructure/Migrations/` y commiteadas
- [ ] `Planilla.sln` incluye cualquier proyecto nuevo

### General
- [ ] `captain-definition` existe en la raíz y apunta a `./Dockerfile`
- [ ] No hay secretos hardcodeados (connection strings, JWT keys, API keys)

---

## Variables de entorno requeridas en CapRover

Estas deben estar configuradas en el panel de CapRover (App Config → Environment Variables):

```
ConnectionStrings__DefaultConnection   = Host=...;Database=...;Username=...;Password=...
Jwt__Key                               = [clave secreta larga]
Jwt__Issuer                            = Planilla
Jwt__Audience                          = Planilla
ASPNETCORE_ENVIRONMENT                 = Production
Stripe__PublishableKey                 = pk_live_...  (opcional)
Stripe__SecretKey                      = sk_live_...  (opcional)
Stripe__WebhookSecret                  = whsec_...    (opcional)
```

---

## Cosas que el Dockerfile NO hace (y deben hacerse en otro lado)

- **NO copia `tests/`** — los tests solo corren localmente o en CI
- **NO aplica migraciones durante el build** — las aplica en runtime (`Program.cs` → `MigrateAsync()`)
- **NO incluye archivos `.env`** — todo viene de variables de entorno de CapRover
- **NO usa `dotnet restore` en el publish** — usa `--no-restore` porque ya se hizo antes

---

## Diagnóstico de fallos comunes

### "Frontend build failed: wwwroot not found"
```bash
# Verificar que vite.config.js tiene:
outDir: '../wwwroot'  # relativo al WORKDIR /app/client → genera /app/wwwroot
# Y que index.html se genera ahí:
ls src/UI/Planilla.Web/wwwroot/
```

### "npm ci" falla
```bash
# package-lock.json debe estar commiteado y sincronizado con package.json
npm install  # regenera el lock file
git add src/UI/Planilla.Web/ClientApp/package-lock.json
git commit -m "chore: actualizar package-lock.json"
```

### "dotnet restore" falla — proyecto no encontrado
```bash
# Verificar que el .csproj está en Planilla.sln
dotnet sln list
# Si falta:
dotnet sln add ruta/al/proyecto.csproj
```

### Migración falla en startup (app crashea al arrancar)
```bash
# Ver logs en CapRover → App → Logs
# Causa común: nueva migración con cambio destructivo o FK inválido
# Solución: corregir la migración ANTES del push
dotnet ef migrations script --idempotent  # genera SQL para revisar
```

### App arranca pero frontend muestra pantalla en blanco
```bash
# El build de Vite no copió los archivos correctamente
# Verificar en CapRover logs que NO hay errores de "Static files"
# Verificar que wwwroot/index.html referencia app.js y app.css (sin hashes)
```

---

## Verificación post-deploy

```bash
# Health check del backend
curl https://TU_DOMINIO/health
# Debe retornar: {"status":"Healthy",...}

# Health check simple
curl https://TU_DOMINIO/api/health
# Debe retornar: {"status":"healthy",...}

# Frontend cargando
curl -I https://TU_DOMINIO/
# Debe retornar: HTTP/2 200

# Verificar migraciones aplicadas (en logs de CapRover)
# Buscar: "Migraciones aplicadas correctamente"
```

---

## Rollback

Si el deploy rompe producción:
1. Ir al panel de CapRover → seleccionar la app
2. **Deployment tab** → buscar el deploy anterior (green)
3. Click "Deploy" en la versión anterior
4. CapRover restaura el contenedor anterior en ~30 segundos

Alternativa por git:
```bash
git revert HEAD
git push origin master
# Esto dispara un nuevo deploy con el código revertido
```
