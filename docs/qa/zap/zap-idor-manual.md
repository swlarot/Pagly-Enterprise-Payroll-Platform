# Pruebas IDOR Cross-Tenant — Planilla (Pagly)

**Tipo:** Manual (ZAP Requester o curl)
**Prioridad:** CRÍTICA — dedicar tiempo completo a esta sección
**Ambiente:** `https://staging-planilla.vorluno.dev`

---

## Preparación (hacer una sola vez)

### Obtener tokens de dos tenants diferentes

**Tenant A:**
```bash
RESPONSE_A=$(curl -s -X POST https://staging-planilla.vorluno.dev/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin-empresa-a@test.com","password":"PASSWORD_A"}')

TOKEN_A=$(echo $RESPONSE_A | python3 -c "import sys,json; print(json.load(sys.stdin)['accessToken'])")
echo "Token A: $TOKEN_A"
```

**Tenant B:**
```bash
RESPONSE_B=$(curl -s -X POST https://staging-planilla.vorluno.dev/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin-empresa-b@test.com","password":"PASSWORD_B"}')

TOKEN_B=$(echo $RESPONSE_B | python3 -c "import sys,json; print(json.load(sys.stdin)['accessToken'])")
echo "Token B: $TOKEN_B"
```

### Recolectar IDs de Tenant A

Con el token de A, obtén IDs reales de cada recurso:

```bash
BASE="https://staging-planilla.vorluno.dev"

# IDs de empleados
curl -s -H "Authorization: Bearer $TOKEN_A" "$BASE/api/empleados" | python3 -m json.tool | grep '"id"'

# IDs de planillas
curl -s -H "Authorization: Bearer $TOKEN_A" "$BASE/api/payrollheaders" | python3 -m json.tool | grep '"id"'

# IDs de anticipos
curl -s -H "Authorization: Bearer $TOKEN_A" "$BASE/api/anticipos" | python3 -m json.tool | grep '"id"'

# IDs de préstamos
curl -s -H "Authorization: Bearer $TOKEN_A" "$BASE/api/prestamos" | python3 -m json.tool | grep '"id"'

# IDs de deducciones
curl -s -H "Authorization: Bearer $TOKEN_A" "$BASE/api/deducciones" | python3 -m json.tool | grep '"id"'
```

Anota los IDs en una tabla:

| Recurso | ID ejemplo de Tenant A |
|---------|----------------------|
| Empleado | `__________` |
| PayrollHeader | `__________` |
| Anticipo | `__________` |
| Préstamo | `__________` |
| Deducción | `__________` |
| Reporte planilla | `__________` |

---

## Ejecución de pruebas IDOR

Para cada recurso, ejecutar los siguientes comandos usando **TOKEN_B** e **IDs de A**.

**Resultado esperado siempre:** HTTP 404 o HTTP 403
**Resultado vulnerable:** HTTP 200 con datos del recurso

### 1. Empleados

```bash
EMP_ID_A="REEMPLAZAR"  # ID de empleado de Tenant A

# GET individual
echo "=== GET /api/empleados/$EMP_ID_A con token B ==="
curl -s -o /dev/null -w "HTTP Status: %{http_code}\n" \
  -H "Authorization: Bearer $TOKEN_B" \
  "$BASE/api/empleados/$EMP_ID_A"

# PUT (modificar)
echo "=== PUT /api/empleados/$EMP_ID_A con token B ==="
curl -s -o /dev/null -w "HTTP Status: %{http_code}\n" \
  -X PUT \
  -H "Authorization: Bearer $TOKEN_B" \
  -H "Content-Type: application/json" \
  -d '{"nombre":"IDOR Test"}' \
  "$BASE/api/empleados/$EMP_ID_A"

# DELETE
echo "=== DELETE /api/empleados/$EMP_ID_A con token B ==="
curl -s -o /dev/null -w "HTTP Status: %{http_code}\n" \
  -X DELETE \
  -H "Authorization: Bearer $TOKEN_B" \
  "$BASE/api/empleados/$EMP_ID_A"

# Saldo inicial
echo "=== GET /api/empleados/$EMP_ID_A/saldo-inicial con token B ==="
curl -s -o /dev/null -w "HTTP Status: %{http_code}\n" \
  -H "Authorization: Bearer $TOKEN_B" \
  "$BASE/api/empleados/$EMP_ID_A/saldo-inicial"
```

### 2. Planillas (PayrollHeaders)

```bash
PH_ID_A="REEMPLAZAR"  # ID de planilla de Tenant A

echo "=== GET /api/payrollheaders/$PH_ID_A con token B ==="
curl -s -o /dev/null -w "HTTP Status: %{http_code}\n" \
  -H "Authorization: Bearer $TOKEN_B" \
  "$BASE/api/payrollheaders/$PH_ID_A"

echo "=== POST /api/payrollheaders/$PH_ID_A/calculate con token B ==="
curl -s -o /dev/null -w "HTTP Status: %{http_code}\n" \
  -X POST \
  -H "Authorization: Bearer $TOKEN_B" \
  "$BASE/api/payrollheaders/$PH_ID_A/calculate"

echo "=== POST /api/payrollheaders/$PH_ID_A/approve con token B ==="
curl -s -o /dev/null -w "HTTP Status: %{http_code}\n" \
  -X POST \
  -H "Authorization: Bearer $TOKEN_B" \
  "$BASE/api/payrollheaders/$PH_ID_A/approve"
```

### 3. Anticipos

```bash
ANT_ID_A="REEMPLAZAR"

echo "=== GET /api/anticipos/$ANT_ID_A con token B ==="
curl -s -o /dev/null -w "HTTP Status: %{http_code}\n" \
  -H "Authorization: Bearer $TOKEN_B" \
  "$BASE/api/anticipos/$ANT_ID_A"

echo "=== POST /api/anticipos/$ANT_ID_A/aprobar con token B ==="
curl -s -o /dev/null -w "HTTP Status: %{http_code}\n" \
  -X POST \
  -H "Authorization: Bearer $TOKEN_B" \
  "$BASE/api/anticipos/$ANT_ID_A/aprobar"
```

### 4. Préstamos

```bash
PRES_ID_A="REEMPLAZAR"

echo "=== GET /api/prestamos/$PRES_ID_A con token B ==="
curl -s -o /dev/null -w "HTTP Status: %{http_code}\n" \
  -H "Authorization: Bearer $TOKEN_B" \
  "$BASE/api/prestamos/$PRES_ID_A"

echo "=== PUT /api/prestamos/$PRES_ID_A con token B ==="
curl -s -o /dev/null -w "HTTP Status: %{http_code}\n" \
  -X PUT \
  -H "Authorization: Bearer $TOKEN_B" \
  -H "Content-Type: application/json" \
  -d '{"monto":0}' \
  "$BASE/api/prestamos/$PRES_ID_A"
```

### 5. Deducciones

```bash
DED_ID_A="REEMPLAZAR"

echo "=== GET /api/deducciones/$DED_ID_A con token B ==="
curl -s -o /dev/null -w "HTTP Status: %{http_code}\n" \
  -H "Authorization: Bearer $TOKEN_B" \
  "$BASE/api/deducciones/$DED_ID_A"
```

### 6. Reportes (mayor riesgo: expone datos masivos)

```bash
echo "=== GET /api/reportes/planilla-regular/$PH_ID_A con token B ==="
curl -s -o /dev/null -w "HTTP Status: %{http_code}\n" \
  -H "Authorization: Bearer $TOKEN_B" \
  "$BASE/api/reportes/planilla-regular/$PH_ID_A"

echo "=== GET /api/reportes/planilla-regular/$PH_ID_A/pdf con token B ==="
curl -s -o /dev/null -w "HTTP Status: %{http_code}\n" \
  -H "Authorization: Bearer $TOKEN_B" \
  "$BASE/api/reportes/planilla-regular/$PH_ID_A/pdf"
```

### 7. Verificar que listados no mezclan datos entre tenants

```bash
echo "=== Listado empleados Tenant A (debe tener SOLO empleados de A) ==="
curl -s -H "Authorization: Bearer $TOKEN_A" "$BASE/api/empleados" \
  | python3 -c "import sys,json; data=json.load(sys.stdin); print(f'Total empleados: {len(data)}')"

echo "=== Listado empleados Tenant B (debe tener SOLO empleados de B, count diferente) ==="
curl -s -H "Authorization: Bearer $TOKEN_B" "$BASE/api/empleados" \
  | python3 -c "import sys,json; data=json.load(sys.stdin); print(f'Total empleados: {len(data)}')"
```

---

## Registro de resultados

Completa esta tabla con los resultados reales:

| Endpoint | Token usado | HTTP Status | ¿Vulnerable? |
|----------|-------------|-------------|--------------|
| GET /api/empleados/{id_A} | Token B | | |
| PUT /api/empleados/{id_A} | Token B | | |
| DELETE /api/empleados/{id_A} | Token B | | |
| GET /api/empleados/{id_A}/saldo-inicial | Token B | | |
| GET /api/payrollheaders/{id_A} | Token B | | |
| POST /api/payrollheaders/{id_A}/calculate | Token B | | |
| POST /api/payrollheaders/{id_A}/approve | Token B | | |
| GET /api/anticipos/{id_A} | Token B | | |
| POST /api/anticipos/{id_A}/aprobar | Token B | | |
| GET /api/prestamos/{id_A} | Token B | | |
| PUT /api/prestamos/{id_A} | Token B | | |
| GET /api/deducciones/{id_A} | Token B | | |
| GET /api/reportes/planilla-regular/{id_A} | Token B | | |
| GET /api/reportes/planilla-regular/{id_A}/pdf | Token B | | |

---

## Si encuentras un IDOR

1. **NO continúes explorando el dato expuesto** — solo confirma el status code
2. **Reporta INMEDIATAMENTE a José** por mensaje directo
3. Documenta el request completo (headers, URL, body) y el response
4. Crea ticket en Linear con severidad CRÍTICA
