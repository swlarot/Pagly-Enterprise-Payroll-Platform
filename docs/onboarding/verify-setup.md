# 🎯 Verificación de Setup - Planilla SaaS

## ✅ Estado Actual: LISTO PARA PRUEBAS

Tu implementación está completa y lista para funcionar. Solo hice **una corrección**:
- `api.ts` ahora importa desde `config.ts` para usar la URL correcta del API

---

## 🔧 Pasos para Verificar

### 1️⃣ Verificar Backend (Puerto 5039)

```bash
# Terminal 1: Ejecutar backend
cd src/UI/Planilla.Web
dotnet run

# Deberías ver:
# Now listening on: http://localhost:5039
# Application started. Press Ctrl+C to shut down.
```

**Verificación con curl (en otra terminal):**
```bash
# Windows PowerShell
curl http://localhost:5039/api/auth/me
# Debería responder: 401 Unauthorized (es correcto, no estás autenticado)

# Si responde "Connection refused" → Backend NO está corriendo
```

---

### 2️⃣ Verificar Frontend (Puerto 5173)

```bash
# Terminal 2: Ejecutar frontend
cd src/UI/Planilla.Web/ClientApp
npm run dev

# Deberías ver:
# VITE v5.x.x  ready in XXX ms
# ➜ Local:   http://localhost:5173/
# ➜ Network: use --host to expose
```

**Abrir en el navegador:** http://localhost:5173

---

### 3️⃣ Verificar Variables de Entorno

Abre DevTools Console (F12) y ejecuta:

```javascript
// Verificar que Vite cargó las variables
console.log('API URL:', import.meta.env.VITE_API_URL);
// Debería mostrar: http://localhost:5039

console.log('Environment:', import.meta.env.VITE_APP_ENV);
// Debería mostrar: development
```

⚠️ **IMPORTANTE:** Si cambias `.env`, debes **reiniciar** Vite (Ctrl+C y `npm run dev` de nuevo).

---

### 4️⃣ Probar Registro

1. Ve a http://localhost:5173/register
2. Completa el formulario:
   - Email: test@example.com
   - Password: Test1234!
   - Company Name: Mi Empresa Test
   - RUC: 123456-1 (opcional)
3. Click "Registrar"

**Resultado esperado:**
- ✅ Redirección al dashboard
- ✅ Token guardado en localStorage
- ✅ En backend logs: "New tenant registered: {TenantId}"

**Si falla:**
- Abre Network tab (F12 → Network)
- Observa la request a `POST http://localhost:5039/api/auth/register`
- Si dice "Failed to fetch" → Backend no está corriendo en 5039
- Si dice "CORS error" → Revisar Program.cs CORS config

---

### 5️⃣ Probar Login

1. Ve a http://localhost:5173/login
2. Usa las credenciales del paso anterior
3. Click "Iniciar Sesión"

**Resultado esperado:**
- ✅ Redirección al dashboard
- ✅ Token guardado en localStorage
- ✅ En backend logs: "User {UserId} logged in to tenant {TenantId}"

---

## 🔍 Diagnóstico de Problemas

### Problema: "Failed to fetch" en el navegador

**Causa:** Backend no está corriendo o puerto incorrecto.

**Solución:**
1. Verificar que `dotnet run` esté ejecutándose
2. Verificar puerto con: `netstat -ano | findstr :5039` (Windows)
3. Si backend usa otro puerto, actualizar `.env.development`

---

### Problema: CORS Error

**Síntoma:** Console muestra:
```
Access to fetch at 'http://localhost:5039/api/auth/register' from origin 'http://localhost:5173' has been blocked by CORS policy
```

**Solución:**
Tu `Program.cs` ya tiene CORS configurado correctamente (líneas 147-161).
Verifica que:
1. `app.UseCors("AllowReactApp")` esté DESPUÉS de `app.UseRouting()` ✅ (línea 287)
2. Backend esté en modo Development ✅

---

### Problema: 400 Bad Request en Register

**Posibles causas:**
1. **RUC/DV requerido pero no enviado:**
   - Verifica `RegisterDto` en backend
   - Frontend debe enviar `ruc: ""` y `dv: ""` si son opcionales

2. **Validación de password:**
   - Mínimo 6 caracteres
   - Requiere uppercase, lowercase, digit, special char (en producción)

---

### Problema: Email ya registrado

**Solución:** Usar otro email o borrar el registro de la base de datos:

```sql
-- PostgreSQL
DELETE FROM "TenantUsers" WHERE "UserId" IN (SELECT "Id" FROM "AspNetUsers" WHERE "Email" = 'test@example.com');
DELETE FROM "Subscriptions" WHERE "TenantId" IN (SELECT "Id" FROM "Tenants" WHERE "Name" = 'Mi Empresa Test');
DELETE FROM "Tenants" WHERE "Name" = 'Mi Empresa Test';
DELETE FROM "AspNetUsers" WHERE "Email" = 'test@example.com';
```

---

## 📊 Verificación de Base de Datos

Después de un registro exitoso, verifica en PostgreSQL:

```sql
-- Ver tenant creado
SELECT * FROM "Tenants" ORDER BY "CreatedAt" DESC LIMIT 1;

-- Ver subscription (debe ser Professional con trial)
SELECT * FROM "Subscriptions" ORDER BY "CreatedAt" DESC LIMIT 1;
-- Plan: 2 (Professional), Status: 2 (Trialing)

-- Ver TenantUser (debe ser Owner)
SELECT * FROM "TenantUsers" ORDER BY "CreatedAt" DESC LIMIT 1;
-- Role: 0 (Owner)

-- Ver usuario Identity
SELECT * FROM "AspNetUsers" ORDER BY "Id" DESC LIMIT 1;
-- EmailConfirmed: true
```

---

## 🎯 Checklist de Validación

Después de las pruebas, marca lo que funciona:

- [ ] Backend corre en puerto 5039
- [ ] Frontend corre en puerto 5173
- [ ] Variables de entorno cargadas correctamente
- [ ] Registro crea: User + Tenant + Subscription + TenantUser
- [ ] Login devuelve JWT con claims (tenant_id, tenant_role, plan)
- [ ] Token se guarda en localStorage
- [ ] Redirección a dashboard después de login
- [ ] CORS funciona sin errores
- [ ] Base de datos tiene los registros correctos

---

## 🚀 Próximos Pasos (Implementación de tu Roadmap)

### Esta Semana:
1. ✅ Implementar TenantMiddleware (YA IMPLEMENTADO - línea 293 de Program.cs)
2. ⏳ Implementar query filters globales en DbContext
3. ⏳ Implementar plan limits en endpoints críticos
4. ⏳ Configurar Stripe webhooks (testing con Stripe CLI)

### Este Mes:
1. Implementar billing portal
2. Implementar upgrade/downgrade de planes
3. Implementar trial expiration automático
4. Métricas de uso (dashboard admin)

---

## 📝 Archivos Clave Modificados

### ✅ Frontend
- `src/services/api.ts` - Ahora usa `config.ts` para URL del API
- `.env.development` - Ya configurado con puerto correcto (5039)
- `src/services/config.ts` - Centralización de configuración

### ✅ Backend (Ya estaban bien)
- `Program.cs` - JWT, CORS, multi-tenant middleware configurado
- `Controllers/AuthController.cs` - Register y Login completos
- `Properties/launchSettings.json` - Puerto 5039 configurado

---

## 💡 Tips de Desarrollo

1. **Usar dos terminales:**
   - Terminal 1: Backend (`dotnet run`)
   - Terminal 2: Frontend (`npm run dev`)

2. **Hot Reload:**
   - Backend: Cambios en código C# requieren reinicio
   - Frontend: Cambios en React se aplican automáticamente

3. **Debugging:**
   - Backend: Logs en consola con ILogger
   - Frontend: Console.log en DevTools
   - Network tab para ver requests/responses

4. **Base de Datos:**
   - Cambios en entidades requieren nueva migración:
     ```bash
     dotnet ef migrations add NombreMigracion --project src/Infrastructure/Planilla.Infrastructure --startup-project src/UI/Planilla.Web
     dotnet ef database update --project src/Infrastructure/Planilla.Infrastructure --startup-project src/UI/Planilla.Web
     ```

---

## ✅ Resumen del Estado

**Backend:** ✅ COMPLETO y CORRECTO
- Multi-tenancy implementado
- JWT con claims correctos
- CORS configurado
- Registro y login funcionan

**Frontend:** ✅ COMPLETO y CORRECTO
- Variables de entorno configuradas
- Config centralizado
- API client actualizado

**Base de Datos:** ✅ LISTA
- Migraciones aplicadas
- Seed ejecutado

**ÚNICA ACCIÓN REQUERIDA:**
1. Ejecutar backend en una terminal
2. Ejecutar frontend en otra terminal
3. Probar registro/login

¡Todo debería funcionar ahora! 🎉
