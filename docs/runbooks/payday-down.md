# Runbook: Payday-down — API Platform caído en día crítico

> **Criticidad: P0.** Si un cliente usa `/v1/payroll/calculate` para procesar planilla el día de pago y el endpoint está caído, le estás bloqueando pagar a sus empleados. La legislación panameña (Código de Trabajo) obliga a pagar salario a más tardar el día acordado — incumplimiento del empleador frente a MITRADEL.
>
> **Objetivo:** restaurar el servicio en **< 15 min**. Si en 5 min no detectaste la causa, ejecuta rollback directamente.

---

## 1. Señales que disparan este runbook

Cualquiera de las siguientes:

- Alerta en [status page](https://status.planilla.cloud) con `/v1/payroll/calculate` en rojo.
- Alertas del cliente por email / WhatsApp / soporte ("nuestra planilla está cayendo con 500/429/timeout").
- `GET /health` devuelve status distinto de `Healthy`.
- Error rate en el dashboard **`/system-admin/api-usage`** en global > 10% en los últimos 5 min.
- Latencia P95 > 5000 ms sostenida.

## 2. Triage inicial — 2 minutos

Ejecuta en orden. No salta pasos.

### 2.1. Health check

```bash
curl -sS -w "\n--- HTTP %{http_code} in %{time_total}s ---\n" \
     https://planilla-api.tu-dominio.com/health
```

**Lectura:**
| Respuesta | Significado | Acción siguiente |
|---|---|---|
| `200 Healthy` + JSON de checks OK | El backend responde; el problema puede ser auth, rate limit, o un endpoint específico | § 3.1 — check auth/rate limit |
| `200` pero `"Unhealthy"` en algún check | Postgres o MultiTenant roto | § 3.2 — DB check |
| `500` / `502` / `503` | App caída o crasheando | § 3.3 — logs + rollback |
| **Timeout o connection refused** | CapRover / servidor down | § 3.4 — infra |

### 2.2. ¿Quién está afectado?

Login al **System Admin Dashboard** → `/system-admin/api-usage`:

- **Todos los tenants fallan** → problema de infra o código, sigue § 3.3 / § 3.4
- **Un solo tenant falla (alto error-rate en ranking)** → problema probablemente del cliente (payload malformado, key revocada). Contactar al tenant, no rollback.

### 2.3. ¿Cuándo empezó?

En CapRover → App → Deployment → Ver último deploy.
Si el último deploy fue **< 30 min antes** de las alertas, el deploy es la causa más probable. **Rollback inmediato** (§ 4).

---

## 3. Diagnóstico por escenario

### 3.1. App responde pero endpoint falla

Causas más probables, en orden:

1. **Rate limiter saturado** — el cliente excedió 60 req/min y está recibiendo 429 en masa.
   ```bash
   # Verificar si son 429s
   curl -sS -o /dev/null -w "%{http_code}" \
        -X POST https://planilla-api.tu-dominio.com/v1/payroll/calculate \
        -H "X-Api-Key: <key-de-test>" \
        -H "Content-Type: application/json" \
        -d @/tmp/payload.json
   ```
   Si es 429 legítimo: **subir el límite temporalmente** (ver § 5.1).

2. **Token firmado mal / key expirada** — 401.
   Verificar en DB:
   ```sql
   SELECT "KeyPrefix", "IsActive", "RevokedAt", "ExpiresAt"
   FROM "ApiKeys"
   WHERE "TenantId" = <tenant_id_afectado>
   ORDER BY "CreatedAt" DESC;
   ```

3. **Plan cambió y perdió `CanUseApi`** — 403. Cliente cayó a Free / Starter.
   ```sql
   SELECT t."Name", s."Plan", s."Status"
   FROM "Tenants" t JOIN "Subscriptions" s ON s."TenantId" = t."Id"
   WHERE t."Id" = <tenant_id>;
   ```

### 3.2. Health reporta Unhealthy

- **Postgres down** → revisar DigitalOcean Managed DB dashboard. Si hay failover en curso, esperar 1-2 min y reintentar. Si está totalmente caído → escalar a DigitalOcean support (soporte prioritario si tienes plan Business).
- **MultiTenant check falla** → suele indicar que una migración no aplicó. Entrar por CapRover terminal:
  ```bash
  dotnet ef database update \
    --project src/Infrastructure/Planilla.Infrastructure \
    --startup-project src/UI/Planilla.Web
  ```
  Si falla → rollback (§ 4).

### 3.3. App crasheando (500s en masa)

Leer logs del último deploy en CapRover:

1. Panel CapRover → App → **Logs**
2. Filtrar los últimos 5 min
3. Buscar excepciones reales (no warnings NULL-ref de `DecimoCalculationService` que son preexistentes)

**Causas comunes:**
- `NullReferenceException` en un code path nuevo → **rollback inmediato** (§ 4)
- `DbUpdateException` por constraint nueva → hay migración rota, rollback + investigar
- `OutOfMemoryException` → ajustar el container size en CapRover → **App Configs → Instance Count = 2** (como patch temporal)

### 3.4. Servidor / CapRover down

1. Verificar DigitalOcean droplet está up: `ping droplet-ip`
2. Si droplet up pero CapRover no responde → SSH y `sudo docker ps` para ver si los containers corren
3. Si el container de la app está caído: `sudo docker restart $(docker ps -aqf "name=planilla")`
4. Si DigitalOcean droplet down → reboot desde panel DO

---

## 4. Rollback — el botón de pánico

**Usar cuando:** el incidente empezó tras un deploy reciente y no has podido diagnosticar en 5 min.

**Pasos:**

1. Panel CapRover → App → **Deployment** tab
2. En "Recent Deployments", clic en la versión anterior a la problemática
3. **Deploy that Version** → confirmar
4. Esperar 30-60 seg a que el nuevo container suba
5. Re-ejecutar § 2.1 (health check)

**Tiempo estimado:** 1 minuto. Es el camino rápido — no dudes en usarlo.

**Después del rollback:**
- Notificar al cliente afectado: "Rollback realizado. Tu servicio está restaurado. Estamos investigando."
- Crear un ticket urgente (Linear: `Bug` + `API` + `P0`) con los logs del fallo
- **NO re-desplegar** el cambio problemático hasta reproducir el bug en staging

---

## 5. Acciones de mitigación sin rollback

### 5.1. Subir rate limit temporalmente

Editar `appsettings.Production.json` (variables de entorno en CapRover) → incrementar las settings del rate limiter. O directamente en `Program.cs` si está hardcoded. Redeploy.

> **Nota:** si el cliente realmente necesita mucho más volumen sostenido, es conversación comercial, no operacional. Upgradear su plan a Enterprise con límite acordado.

### 5.2. Excluir un tenant problemático temporalmente

Si un solo tenant está cayendo con payloads malformados y spameando 500s:

```sql
-- Revocar sus keys temporalmente (no borra, solo revoca)
UPDATE "ApiKeys"
SET "RevokedAt" = NOW(), "RevocationReason" = 'Auto-revoked due to payday-down incident'
WHERE "TenantId" = <tenant_id_problemático>
  AND "RevokedAt" IS NULL;
```

Contactar al cliente de inmediato y escalar su incidente aparte.

### 5.3. Feature flag: deshabilitar idempotency

Si sospechas que el filter de idempotency está causando latencia extra o un deadlock:

1. Comentar `[Idempotent]` en `CalculatorController.Calculate`
2. Commit + push → redeploy (~2 min)

Los clientes seguirán funcionando sin idempotencia hasta que restaures el feature.

### 5.4. Failover a cálculo manual (caso extremo)

Si por alguna razón `/v1/payroll/calculate` está completamente inusable y un cliente en vivo necesita calcular planilla del día:

- Asistir al cliente con cálculo manual usando la **calculadora del SaaS** (`/planillas/nueva`)
- Generar PDF y enviárselo por email
- Documentar el incidente y cobrar el uso después (o sin costo, según la criticidad)

---

## 6. Post-mortem — dentro de 24 horas del incidente

Obligatorio si el downtime fue > 5 min o afectó a > 1 cliente.

Template en `docs/post-mortems/` (crear si no existe):

```markdown
# Post-mortem: <fecha> — <título breve>

## Impacto
- Tenants afectados: <lista>
- Downtime total: <minutos>
- Cálculos perdidos: <número>

## Línea de tiempo (UTC)
- HH:MM — Primera alerta
- HH:MM — Diagnóstico: <qué detectaste>
- HH:MM — Mitigación aplicada
- HH:MM — Servicio restaurado

## Causa raíz
<Qué rompió técnicamente>

## Qué funcionó bien
<Ej: alertas detectaron el problema en X segundos>

## Qué debe mejorar
<Ej: logs no eran suficientes, agregar X instrumentación>

## Acción items
- [ ] <tarea específica con ticket Linear>
- [ ] <otra>
```

Compartir el post-mortem con el cliente afectado si lo pide. Transparencia = confianza.

---

## 7. Contactos

| Rol | Nombre | Canal |
|---|---|---|
| Owner técnico | Jose (Vorluno) | Email, WhatsApp |
| Backup (bus factor) | _TBD — contratar on-call de respaldo_ | — |
| DigitalOcean support | — | Panel DO → Create ticket |
| Stripe support | — | dashboard.stripe.com/support |

> **Nota:** hoy el bus factor es 1. Antes del primer cliente Enterprise pagado, contratar al menos un developer de confianza que pueda ejecutar este runbook sin ti. Un acuerdo de 4 horas de on-call al mes como retainer cubre el caso extremo (tú enfermo el día de pago).

---

## 8. Lista de comandos útiles (copy-paste ready)

```bash
# Health check
curl -sS https://planilla-api.tu-dominio.com/health | jq .

# Tail de logs del último container
# (ejecutar desde SSH al droplet de CapRover)
sudo docker logs --tail 200 -f $(docker ps -qf "name=srv-captain--planilla")

# Restart del container sin redeploy
sudo docker restart $(docker ps -qf "name=srv-captain--planilla")

# Conexión directa a Postgres (tiene superadmin rights — usar con cuidado)
psql "$CONNECTION_STRING"

# Last 20 requests del API Platform
SELECT "CreatedAt", "TenantId", "ApiKeyId", "Endpoint", "StatusCode", "ResponseTimeMs"
FROM "ApiUsageRecords"
ORDER BY "CreatedAt" DESC
LIMIT 20;

# Count de errores por hora del último día (quick visual)
SELECT DATE_TRUNC('hour', "CreatedAt") AS hora, COUNT(*) AS total,
       COUNT(*) FILTER (WHERE "StatusCode" >= 500) AS errors_5xx
FROM "ApiUsageRecords"
WHERE "CreatedAt" > NOW() - INTERVAL '24 hours'
GROUP BY 1 ORDER BY 1 DESC;
```

---

**Última actualización:** 2026-04-13
**Siguiente revisión:** antes de onboardear el primer cliente Enterprise pagado
