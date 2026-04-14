# Pagly Payroll API — Quickstart

> **Panama Payroll Calculations as an API.**  
> Calculates CSS, Seguro Educativo, ISR, and employer costs for any employee — no backend required on your side.

**Base URL:** `https://api.pagly.com`  
**Version:** `v1` (stable)

---

## How it works

You send one POST with the employee's gross pay and profile. We return the full legal deduction breakdown — CSS (Ley 462 phases), Seguro Educativo, ISR (DGI brackets), employer risk contribution, and net pay.

No database connection. No SDK. No setup on your infrastructure. One authenticated HTTP call.

---

## From zero to first call — 3 steps

### Step 1 — Get an account

Contact [soporte@pagly.com](mailto:soporte@pagly.com) or your Pagly contact to create a tenant. You'll receive an invite link to set your password.

### Step 2 — Generate an API key

Log into your Pagly dashboard → **Settings → API Keys** → **Nueva API Key**.

Copy the key. You'll only see the full value once:

```
pk_live_4a9f2c1d8e3b7f0a5c6d9e2f1b4a7c0d3e6f9a2b5c8d1e4f7a0b3c6d9e2f5a8
```

Store it safely (environment variable, secrets manager — never in source code).

### Step 3 — Make the call

```bash
curl -X POST https://api.pagly.com/v1/payroll/calculate \
  -H "X-Api-Key: pk_live_YOUR_KEY_HERE" \
  -H "Content-Type: application/json" \
  -d '{
    "grossPay": 1500.00,
    "payFrequency": "Mensual",
    "yearsCotized": 5
  }'
```

**That's it.** You'll get back a full payroll breakdown in JSON.

---

## Endpoint reference

### `POST /v1/payroll/calculate`

Calculates legal deductions and employer costs for one employee in one pay period.

**Authentication:** `X-Api-Key` header  
**Content-Type:** `application/json`

#### Request body

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `grossPay` | `number` | Yes | — | Gross pay for the period (devengado). Min: 0.01, Max: 1,000,000 |
| `payFrequency` | `string` | Yes | `"Mensual"` | Pay frequency. See values below. |
| `yearsCotized` | `integer` | No | `0` | Years of CSS contributions. Affects pension ceiling (Ley 462 Art. 178) |
| `averageSalaryLast10Years` | `number` | No | `0` | Average salary over last 10 years. Used with yearsCotized for pension cap |
| `cssRiskPercentage` | `number` | No | `0` | Professional risk rate (Acuerdo N°2 1995). See common values below |
| `dependents` | `integer` | No | `0` | Number of declared dependents for ISR deduction |
| `isSubjectToCss` | `boolean` | No | `true` | Set `false` for contractors not subject to CSS |
| `isSubjectToEducationalInsurance` | `boolean` | No | `true` | Set `false` if exempt from Seguro Educativo |
| `isSubjectToIncomeTax` | `boolean` | No | `true` | Set `false` if exempt from ISR |
| `calculationDate` | `string (ISO 8601)` | No | Current date | Date used to resolve Ley 462 phase and ISR brackets. Format: `"2026-04-01"` |

**`payFrequency` values:**

| Value | Periods per year | Description |
|---|---|---|
| `"Semanal"` | 52 | Weekly |
| `"Bisemanal"` | 26 | Every two weeks |
| `"Quincenal"` | 24 | Twice a month |
| `"Mensual"` | 12 | Monthly |

**Common `cssRiskPercentage` values (Acuerdo N°2 de 1995):**

| Rate | Sector |
|---|---|
| `0.56` | Offices, professional services, technology |
| `1.03` | Retail, commerce |
| `2.10` | Light manufacturing |
| `5.67` | Construction |

#### Response body (200 OK)

```json
{
  "grossPay": 1500.00,
  "cssEmployee": 146.25,
  "educationalInsuranceEmployee": 18.75,
  "incomeTax": 0.00,
  "totalDeductions": 165.00,
  "netPay": 1335.00,
  "cssEmployer": 198.75,
  "educationalInsuranceEmployer": 22.50,
  "riskContribution": 8.40,
  "totalEmployerCost": 1729.65,
  "version": "2026.1",
  "calculationDate": "2026-04-13T00:00:00Z"
}
```

| Field | Description |
|---|---|
| `grossPay` | Echo of input gross pay |
| `cssEmployee` | Employee CSS contribution (9.75% with Ley 462 pension ceiling) |
| `educationalInsuranceEmployee` | Employee Seguro Educativo (1.25%, no ceiling) |
| `incomeTax` | ISR withholding (DGI brackets, annualized ×13 months including décimo) |
| `totalDeductions` | Sum of CSS + SE + ISR |
| `netPay` | Take-home pay (grossPay − totalDeductions) |
| `cssEmployer` | Employer CSS contribution (Ley 462 phase-dependent: 12.25% / 13.25% / 14.25%) |
| `educationalInsuranceEmployer` | Employer Seguro Educativo (1.50%, no ceiling) |
| `riskContribution` | Professional risk insurance (based on `cssRiskPercentage`) |
| `totalEmployerCost` | Total cost to employer (grossPay + employer contributions) |
| `version` | Rule set version used. Always include this in bug reports |
| `calculationDate` | Date used for rule resolution |

---

## Code examples

### cURL

```bash
curl -X POST https://api.pagly.com/v1/payroll/calculate \
  -H "X-Api-Key: pk_live_YOUR_KEY_HERE" \
  -H "Content-Type: application/json" \
  -d '{
    "grossPay": 2800.00,
    "payFrequency": "Quincenal",
    "yearsCotized": 12,
    "cssRiskPercentage": 0.56,
    "dependents": 2,
    "calculationDate": "2026-04-01"
  }'
```

### JavaScript / TypeScript (fetch)

```typescript
const PAGLY_API_KEY = process.env.PAGLY_API_KEY!;

interface PayrollRequest {
  grossPay: number;
  payFrequency: "Semanal" | "Bisemanal" | "Quincenal" | "Mensual";
  yearsCotized?: number;
  averageSalaryLast10Years?: number;
  cssRiskPercentage?: number;
  dependents?: number;
  isSubjectToCss?: boolean;
  isSubjectToEducationalInsurance?: boolean;
  isSubjectToIncomeTax?: boolean;
  calculationDate?: string; // ISO 8601
}

interface PayrollResult {
  grossPay: number;
  cssEmployee: number;
  educationalInsuranceEmployee: number;
  incomeTax: number;
  totalDeductions: number;
  netPay: number;
  cssEmployer: number;
  educationalInsuranceEmployer: number;
  riskContribution: number;
  totalEmployerCost: number;
  version: string;
  calculationDate: string;
}

async function calculatePayroll(request: PayrollRequest): Promise<PayrollResult> {
  const response = await fetch("https://api.pagly.com/v1/payroll/calculate", {
    method: "POST",
    headers: {
      "X-Api-Key": PAGLY_API_KEY,
      "Content-Type": "application/json",
    },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    const error = await response.json();
    throw new Error(`Pagly API error ${response.status}: ${error.detail}`);
  }

  return response.json();
}

// Usage
const result = await calculatePayroll({
  grossPay: 1500.00,
  payFrequency: "Mensual",
  yearsCotized: 5,
  cssRiskPercentage: 0.56,
});

console.log(`Net pay: $${result.netPay}`);
console.log(`Total employer cost: $${result.totalEmployerCost}`);
```

### C# / .NET

```csharp
// Install: dotnet add package System.Net.Http.Json

public record PayrollRequest(
    decimal GrossPay,
    string PayFrequency,
    int YearsCotized = 0,
    decimal AverageSalaryLast10Years = 0,
    decimal CssRiskPercentage = 0,
    int Dependents = 0,
    bool IsSubjectToCss = true,
    bool IsSubjectToEducationalInsurance = true,
    bool IsSubjectToIncomeTax = true,
    DateTime? CalculationDate = null
);

public record PayrollResult(
    decimal GrossPay,
    decimal CssEmployee,
    decimal EducationalInsuranceEmployee,
    decimal IncomeTax,
    decimal TotalDeductions,
    decimal NetPay,
    decimal CssEmployer,
    decimal EducationalInsuranceEmployer,
    decimal RiskContribution,
    decimal TotalEmployerCost,
    string Version,
    DateTime CalculationDate
);

// Registration (Program.cs / DI)
builder.Services.AddHttpClient("pagly", client =>
{
    client.BaseAddress = new Uri("https://api.pagly.com");
    client.DefaultRequestHeaders.Add("X-Api-Key", builder.Configuration["Pagly:ApiKey"]);
});

// Usage
var http = httpClientFactory.CreateClient("pagly");

var result = await http.PostAsJsonAsync("/v1/payroll/calculate", new PayrollRequest(
    GrossPay: 1500.00m,
    PayFrequency: "Mensual",
    YearsCotized: 5,
    CssRiskPercentage: 0.56m
));

result.EnsureSuccessStatusCode();

var payroll = await result.Content.ReadFromJsonAsync<PayrollResult>();
Console.WriteLine($"Net pay: {payroll!.NetPay:C}");
```

### Python

```python
import os
import requests

PAGLY_API_KEY = os.environ["PAGLY_API_KEY"]

def calculate_payroll(
    gross_pay: float,
    pay_frequency: str = "Mensual",
    years_cotized: int = 0,
    css_risk_percentage: float = 0.0,
    dependents: int = 0,
    calculation_date: str | None = None,
) -> dict:
    response = requests.post(
        "https://api.pagly.com/v1/payroll/calculate",
        headers={"X-Api-Key": PAGLY_API_KEY},
        json={
            "grossPay": gross_pay,
            "payFrequency": pay_frequency,
            "yearsCotized": years_cotized,
            "cssRiskPercentage": css_risk_percentage,
            "dependents": dependents,
            **({"calculationDate": calculation_date} if calculation_date else {}),
        },
    )
    response.raise_for_status()
    return response.json()

# Usage
result = calculate_payroll(gross_pay=1500.00, pay_frequency="Mensual")
print(f"Net pay: ${result['netPay']:.2f}")
print(f"Total employer cost: ${result['totalEmployerCost']:.2f}")
```

---

## Error handling

All errors follow [RFC 7807 Problem Details](https://www.rfc-editor.org/rfc/rfc7807). The response `Content-Type` is `application/problem+json`.

```json
{
  "type": "https://api.pagly.com/errors/invalid_request_error",
  "title": "Invalid request",
  "status": 400,
  "detail": "grossPay debe estar entre 0.01 y 1,000,000",
  "errorCode": "invalid_request_error",
  "traceId": "00-4af9b2c1d3e5f6a7b8c9d0e1f2a3b4c5-1d2e3f4a5b6c7d8e-00"
}
```

| HTTP Status | `errorCode` | Description |
|---|---|---|
| `400` | `invalid_request_error` | Validation failed. `detail` explains which field and why |
| `401` | `authentication_error` | Missing, invalid, revoked, or expired API key |
| `422` | `idempotency_mismatch` | Same `Idempotency-Key` was sent before with a different request body |
| `429` | `rate_limit_error` | Exceeded 60 requests/minute. Check `Retry-After` header |
| `500` | `api_error` | Internal server error. Include `traceId` when reporting |

---

## Idempotency

For retry-safe requests, send an `Idempotency-Key` header with any unique string (UUID recommended). If you retry the same request within 24 hours with the same key, you'll get the original response without consuming an additional API call from your quota.

```bash
curl -X POST https://api.pagly.com/v1/payroll/calculate \
  -H "X-Api-Key: pk_live_YOUR_KEY_HERE" \
  -H "Idempotency-Key: emp-42-period-2026-04" \
  -H "Content-Type: application/json" \
  -d '{ "grossPay": 1500.00, "payFrequency": "Mensual" }'
```

Replayed responses include:
```
Idempotent-Replay: true
Idempotent-Created: 2026-04-13T14:32:00Z
```

> Sending the same `Idempotency-Key` with a **different request body** returns `422 Unprocessable Entity`.

---

## Rate limits

| Plan | Limit | Window |
|---|---|---|
| Professional | 60 requests/minute | Sliding |
| Enterprise | Custom | Negotiated |

When you exceed the limit, you receive:

```http
HTTP/1.1 429 Too Many Requests
Retry-After: 23
```

Wait `Retry-After` seconds before retrying.

---

## Quota alerts

When you reach **80%** or **100%** of your monthly request quota, the account Owner receives an email notification automatically. No configuration needed.

Monthly quotas:
- **Professional plan:** 10,000 requests/month
- **Enterprise plan:** 100,000 requests/month (custom limits available)

---

## Laws and rates applied

The API applies Panama labor law as of `calculationDate`. All rates are hardcoded per official regulations:

| Contribution | Employee | Employer | Ceiling |
|---|---|---|---|
| CSS — Pension (Ley 462) | 9.75% | Phase-dependent¹ | Art. 178 (years-based) |
| CSS — Enfermedad-Maternidad | — | Included above | None |
| Seguro Educativo | 1.25% | 1.50% | None |
| ISR | Progressive (DGI brackets) | — | Annual projection |
| Riesgo Profesional | — | `cssRiskPercentage` | None |

¹ Employer CSS rate by Ley 462 phase:
- `12.25%` — until February 2027
- `13.25%` — March 2027 – February 2029
- `14.25%` — from March 2029

---

## FAQ

**Do I need a database or backend to use this?**  
No. The API is fully stateless. You pass all employee data in the request; we calculate and return the result. Nothing is stored from your data.

**Can I use this to calculate for multiple employees?**  
Yes — make one request per employee. There's no batch endpoint in v1, but requests complete in under 50ms so batching client-side is straightforward.

**Does this handle the Décimo Tercer Mes in ISR?**  
Yes. ISR is annualized using ×13 periods (12 regular + 1 décimo) regardless of `payFrequency`, as required by DGI.

**What if I don't know the risk rate for my client's sector?**  
Pass `cssRiskPercentage: 0` and `riskContribution` will be `0.00`. The rest of the calculation is unaffected.

**What does `version` in the response mean?**  
It identifies which rule set was applied (e.g. `"2026.1"`). Always include it when reporting unexpected results.

**What happens when the law changes?**  
We update the rule set and publish a new version. Existing integrations continue to work — pass an explicit `calculationDate` in the past to get historical rules.

---

## Support

- Email: [soporte@pagly.com](mailto:soporte@pagly.com)
- Status page: [status.pagly.com](https://status.pagly.com)
- Response time: < 4 business hours for Professional, < 1 hour for Enterprise

Include the `traceId` from error responses when contacting support.

---

*Last updated: 2026-04-13 — Rule set version 2026.1 (Ley 462 phases, DGI brackets 2026)*
