# 🎯 PROMPT PARA: Claude Code CLI — Pagly P1: Sistema de Tipo de Período, Pay Info y Horas Trabajadas

═══════════════════════════════════════════════════════════════════════════

## ROL

Actúa como **Senior .NET Backend Developer + React Frontend Developer** con expertise profundo en:
- EF Core 9 migrations sobre PostgreSQL
- Clean Architecture estricta (Domain → Application → Infrastructure → Web)
- Sistemas de planilla panameños (CSS, SE, ISR, Ley 462)
- Multi-tenancy con aislamiento total por TenantId
- React 19 con Tailwind CSS y dark theme (emerald/navy)

## CONTEXTO DEL PROYECTO

- **Sistema**: Pagly (anteriormente Vorluno Planilla) — SaaS multi-tenant de planilla para Panamá
- **Repo**: `https://github.com/vorluno/Vorluno-Planilla` (branch: master)
- **Stack**: .NET 9, ASP.NET Core Web API, EF Core 9, PostgreSQL 16+, React 19, Vite, Tailwind CSS
- **Arquitectura**: Clean Architecture — `src/Core/Planilla.Domain/`, `src/Core/Planilla.Application/`, `src/Infrastructure/Planilla.Infrastructure/`, `src/UI/Planilla.Web/`
- **Namespace raíz**: `Vorluno.Planilla.*`
- **Dark Theme**: navy-950/900 backgrounds, emerald-500 accents, gray-100/200/300 text

## PROBLEMA A RESOLVER

El sistema tiene un gap crítico: **SalarioBase en Empleado no tiene contexto de qué representa** (¿mensual? ¿quincenal? ¿bisemanal?). Existe un campo `PayFrequency` (string, default "Quincenal") en el Empleado, pero:

1. **No se expone en el frontend** — ni en el formulario de empleados ni en el de planillas
2. **No existe "Bisemanal" como opción** — PayrollConstants solo tiene Quincenal(24), Mensual(12), Semanal(52)
3. **No hay campos de horas** — sin HoursPerWeek, HoursPerPeriod, ni HourlyRate
4. **PayrollHeader no tiene PayPeriodType** — la planilla no sabe si es semanal/quincenal/etc.
5. **No hay desglose de horas por empleado en cada planilla** — regulares, domingo, feriado, extras
6. **El ISR se calcula correctamente solo si PayFrequency es correcto** — actualmente usa el campo del empleado, no el de la planilla

## ARCHIVOS QUE SE DEBEN MODIFICAR/CREAR

### BACKEND

#### 1. NUEVO ENUM — `src/Core/Planilla.Domain/Enums/PayPeriodType.cs`

```csharp
namespace Vorluno.Planilla.Domain.Enums;

/// <summary>
/// Tipos de período de pago soportados.
/// Orden por frecuencia de uso en Panamá: Quincenal > Bisemanal > Semanal > Mensual
/// </summary>
public enum PayPeriodType
{
    /// <summary>Semanal — 52 períodos/año</summary>
    Semanal = 0,

    /// <summary>Bisemanal (cada 2 semanas) — 26 períodos/año</summary>
    Bisemanal = 1,

    /// <summary>Quincenal (1-15 y 16-fin de mes) — 24 períodos/año</summary>
    Quincenal = 2,

    /// <summary>Mensual — 12 períodos/año</summary>
    Mensual = 3
}
```

#### 2. MODIFICAR — `src/Core/Planilla.Domain/Entities/Empleado.cs`

Agregar después de `PayFrequency`:

```csharp
// ====================================================================
// Pay Info — Configuración de pago por horas
// ====================================================================

/// <summary>
/// Tipo de período de pago (reemplaza PayFrequency string).
/// Determina cómo se anualiza el salario para ISR.
/// </summary>
public PayPeriodType PayPeriodType { get; set; } = PayPeriodType.Quincenal;

/// <summary>
/// Horas semanales del contrato laboral (Panamá estándar: 48 horas = 8h × 6 días)
/// Código de Trabajo Art. 31: máximo 48 horas semanales diurnas
/// </summary>
public int HoursPerWeek { get; set; } = 48;

/// <summary>
/// Horas del período, calculadas según PayPeriodType:
/// - Semanal: HoursPerWeek (48)
/// - Bisemanal: HoursPerWeek × 2 (96)
/// - Quincenal: HoursPerWeek × 2.167 (~104)
/// - Mensual: HoursPerWeek × 4.333 (~208)
/// El usuario puede overridear este cálculo.
/// </summary>
public decimal HoursPerPeriod { get; set; } = 104m;

/// <summary>
/// Tasa por hora calculada: SalarioBase / HoursPerPeriod.
/// Se almacena para performance pero se recalcula cuando cambia SalarioBase o HoursPerPeriod.
/// Usado para calcular horas extra, dominicales, feriados.
/// </summary>
[Column(TypeName = "decimal(18, 4)")]
public decimal HourlyRate { get; set; } = 0m;
```

**IMPORTANTE**: Mantener el campo `PayFrequency` (string) por retrocompatibilidad. Agregar un método helper:

```csharp
/// <summary>
/// Sincroniza PayFrequency (legacy string) con PayPeriodType (nuevo enum).
/// Llamar después de cambiar PayPeriodType.
/// </summary>
public void SyncPayFrequencyFromType()
{
    PayFrequency = PayPeriodType switch
    {
        PayPeriodType.Semanal => "Semanal",
        PayPeriodType.Bisemanal => "Bisemanal",
        PayPeriodType.Quincenal => "Quincenal",
        PayPeriodType.Mensual => "Mensual",
        _ => "Quincenal"
    };
}

/// <summary>
/// Recalcula HourlyRate basado en SalarioBase y HoursPerPeriod.
/// Llamar después de cambiar SalarioBase o HoursPerPeriod.
/// </summary>
public void RecalculateHourlyRate()
{
    HourlyRate = HoursPerPeriod > 0 ? Math.Round(SalarioBase / HoursPerPeriod, 4) : 0;
}

/// <summary>
/// Calcula HoursPerPeriod sugerido basado en HoursPerWeek y PayPeriodType.
/// </summary>
public static decimal CalculateSuggestedHoursPerPeriod(int hoursPerWeek, PayPeriodType periodType)
{
    return periodType switch
    {
        PayPeriodType.Semanal => hoursPerWeek,
        PayPeriodType.Bisemanal => hoursPerWeek * 2m,
        PayPeriodType.Quincenal => Math.Round(hoursPerWeek * (52m / 24m), 0), // ~104 para 48h
        PayPeriodType.Mensual => Math.Round(hoursPerWeek * (52m / 12m), 0),   // ~208 para 48h
        _ => hoursPerWeek * 2m
    };
}
```

#### 3. MODIFICAR — `src/Core/Planilla.Domain/Entities/PayrollHeader.cs`

Agregar después de `PayDate`:

```csharp
/// <summary>
/// Tipo de período de esta planilla.
/// CRÍTICO: Determina cómo se anualizan los salarios para ISR.
/// Se establece al crear la planilla y no debe cambiar después.
/// </summary>
public PayPeriodType PayPeriodType { get; set; } = PayPeriodType.Quincenal;
```

#### 4. NUEVO — `src/Core/Planilla.Domain/Entities/PayrollEmployeeHours.cs`

Crear esta entidad para el desglose de horas por empleado por planilla:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Vorluno.Planilla.Domain.Interfaces;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Horas trabajadas por un empleado en un período de planilla específico.
/// Permite ingresar manualmente las horas regulares, dominicales, feriados y extras.
/// Se utiliza para calcular el salario bruto del período.
/// </summary>
public class PayrollEmployeeHours : ITenantEntity
{
    public int Id { get; set; }

    /// <summary>ID de la planilla</summary>
    public int PayrollHeaderId { get; set; }

    /// <summary>ID del empleado</summary>
    public int EmpleadoId { get; set; }

    /// <summary>ID del tenant (multi-tenancy)</summary>
    public int TenantId { get; set; }

    // ====================================================================
    // Horas por tipo
    // ====================================================================

    /// <summary>Horas regulares trabajadas en el período</summary>
    [Column(TypeName = "decimal(8, 2)")]
    public decimal RegularHours { get; set; }

    /// <summary>Horas trabajadas en domingo (recargo 50%)</summary>
    [Column(TypeName = "decimal(8, 2)")]
    public decimal SundayHours { get; set; } = 0;

    /// <summary>Horas trabajadas en días feriados nacionales (recargo 50%)</summary>
    [Column(TypeName = "decimal(8, 2)")]
    public decimal HolidayHours { get; set; } = 0;

    /// <summary>Horas extra diurnas (recargo 25%)</summary>
    [Column(TypeName = "decimal(8, 2)")]
    public decimal OvertimeDayHours { get; set; } = 0;

    /// <summary>Horas extra nocturnas (recargo 50%)</summary>
    [Column(TypeName = "decimal(8, 2)")]
    public decimal OvertimeNightHours { get; set; } = 0;

    /// <summary>Horas de ausencia injustificada (se descuentan)</summary>
    [Column(TypeName = "decimal(8, 2)")]
    public decimal AbsenceHours { get; set; } = 0;

    /// <summary>Horas de incapacidad CSS (no se descuentan pero afectan cálculo)</summary>
    [Column(TypeName = "decimal(8, 2)")]
    public decimal DisabilityHours { get; set; } = 0;

    // ====================================================================
    // Montos calculados (se llenan al calcular planilla)
    // ====================================================================

    /// <summary>Pago por horas regulares: RegularHours × HourlyRate</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal RegularPay { get; set; }

    /// <summary>Pago por horas domingo: SundayHours × HourlyRate × 1.50</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal SundayPay { get; set; }

    /// <summary>Pago por horas feriado: HolidayHours × HourlyRate × 1.50</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal HolidayPay { get; set; }

    /// <summary>Pago horas extra diurnas: OvertimeDayHours × HourlyRate × 1.25</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal OvertimeDayPay { get; set; }

    /// <summary>Pago horas extra nocturnas: OvertimeNightHours × HourlyRate × 1.50</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal OvertimeNightPay { get; set; }

    /// <summary>Descuento por ausencias: AbsenceHours × HourlyRate</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal AbsenceDeduction { get; set; }

    /// <summary>Total pagado por horas: Suma de todos los pagos menos descuentos</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal TotalHoursPay { get; set; }

    // ====================================================================
    // Auditoría
    // ====================================================================
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // ====================================================================
    // Navegación
    // ====================================================================
    public virtual PayrollHeader? PayrollHeader { get; set; }
    public virtual Empleado? Empleado { get; set; }
    public virtual Tenant? Tenant { get; set; }
}
```

#### 5. MODIFICAR — `src/Core/Planilla.Application/Helpers/PayrollConstants.cs`

Agregar "Bisemanal" y agregar overload con enum:

```csharp
// AGREGAR en el diccionario PayFrequencies:
{ "Bisemanal", 26 },  // 2 pagos por mes (cada 2 semanas exactas)

// AGREGAR nuevo método con enum:
/// <summary>
/// Obtiene períodos/año desde el enum PayPeriodType.
/// </summary>
public static int GetPeriodsPerYear(PayPeriodType periodType)
{
    return periodType switch
    {
        PayPeriodType.Semanal => 52,
        PayPeriodType.Bisemanal => 26,
        PayPeriodType.Quincenal => 24,
        PayPeriodType.Mensual => 12,
        _ => throw new ArgumentException($"Tipo de período inválido: {periodType}")
    };
}
```

#### 6. MODIFICAR — `src/Core/Planilla.Application/DTOs/EmpleadoDtos.cs`

Actualizar los 3 records:

```csharp
// EmpleadoCrearDto — AGREGAR campos:
public record EmpleadoCrearDto(
    [Required] [StringLength(100)] string Nombre,
    [Required] [StringLength(100)] string Apellido,
    [Required] [StringLength(20)] string NumeroIdentificacion,
    [EmailAddress] [StringLength(256)] string? Email,
    [Range(0.01, double.MaxValue)] decimal SalarioBase,
    int? DepartamentoId,
    int? PosicionId,
    // === NUEVOS CAMPOS ===
    PayPeriodType PayPeriodType = PayPeriodType.Quincenal,
    int HoursPerWeek = 48,
    decimal? HoursPerPeriod = null  // null = auto-calcular
);

// EmpleadoActualizarDto — AGREGAR campos:
public record EmpleadoActualizarDto(
    [Required] [StringLength(100)] string Nombre,
    [Required] [StringLength(100)] string Apellido,
    [EmailAddress] [StringLength(256)] string? Email,
    [Range(0.01, double.MaxValue)] decimal SalarioBase,
    bool EstaActivo,
    int? DepartamentoId,
    int? PosicionId,
    // === NUEVOS CAMPOS ===
    PayPeriodType PayPeriodType = PayPeriodType.Quincenal,
    int HoursPerWeek = 48,
    decimal? HoursPerPeriod = null
);

// EmpleadoVerDto — AGREGAR campos al final ANTES de los defaults:
public record EmpleadoVerDto(
    int Id,
    string Nombre,
    string Apellido,
    string NumeroIdentificacion,
    string? Email,
    decimal SalarioBase,
    DateTime FechaContratacion,
    bool EstaActivo,
    int? DepartamentoId,
    string? DepartamentoNombre,
    int? PosicionId,
    string? PosicionNombre,
    bool TieneAccesoSistema,
    string? RolSistema,
    // === NUEVOS CAMPOS ===
    string PayPeriodTypeName = "Quincenal",
    int HoursPerWeek = 48,
    decimal HoursPerPeriod = 104,
    decimal HourlyRate = 0,
    // === CAMPOS EXISTENTES CON DEFAULT ===
    bool IsDeleted = false,
    string? UsuarioVinculadoEmail = null
);
```

**NOTA**: Agregar `using Vorluno.Planilla.Domain.Enums;` al inicio del archivo.

#### 7. MODIFICAR — `src/Core/Planilla.Application/Mappings/MappingProfile.cs`

Agregar mappings para los nuevos campos:

```csharp
// En CreateMap<Empleado, EmpleadoVerDto>():
.ForMember(dest => dest.PayPeriodTypeName, opt => opt.MapFrom(src => src.PayPeriodType.ToString()))
.ForMember(dest => dest.HoursPerWeek, opt => opt.MapFrom(src => src.HoursPerWeek))
.ForMember(dest => dest.HoursPerPeriod, opt => opt.MapFrom(src => src.HoursPerPeriod))
.ForMember(dest => dest.HourlyRate, opt => opt.MapFrom(src => src.HourlyRate))

// En CreateMap<EmpleadoCrearDto, Empleado>():
// AutoMapper mapea automáticamente propiedades con mismo nombre,
// pero agregar AfterMap para cálculos:
CreateMap<EmpleadoCrearDto, Empleado>()
    .AfterMap((src, dest) =>
    {
        // Si HoursPerPeriod no se proporcionó, auto-calcular
        if (src.HoursPerPeriod == null || src.HoursPerPeriod == 0)
        {
            dest.HoursPerPeriod = Empleado.CalculateSuggestedHoursPerPeriod(
                dest.HoursPerWeek, dest.PayPeriodType);
        }
        dest.RecalculateHourlyRate();
        dest.SyncPayFrequencyFromType();
    });

CreateMap<EmpleadoActualizarDto, Empleado>()
    .AfterMap((src, dest) =>
    {
        if (src.HoursPerPeriod == null || src.HoursPerPeriod == 0)
        {
            dest.HoursPerPeriod = Empleado.CalculateSuggestedHoursPerPeriod(
                dest.HoursPerWeek, dest.PayPeriodType);
        }
        dest.RecalculateHourlyRate();
        dest.SyncPayFrequencyFromType();
    });
```

#### 8. MODIFICAR — `src/Infrastructure/Planilla.Infrastructure/Data/ApplicationDbContext.cs`

Agregar `DbSet<PayrollEmployeeHours>` y configurar relaciones e índices:

```csharp
// Agregar DbSet:
public DbSet<PayrollEmployeeHours> PayrollEmployeeHours { get; set; } = null!;

// En OnModelCreating, agregar configuración:
modelBuilder.Entity<PayrollEmployeeHours>(entity =>
{
    // Índice único: un registro de horas por empleado por planilla
    entity.HasIndex(e => new { e.PayrollHeaderId, e.EmpleadoId })
        .IsUnique()
        .HasDatabaseName("IX_PayrollEmployeeHours_HeaderId_EmpleadoId");

    // Índice por tenant para queries filtradas
    entity.HasIndex(e => e.TenantId)
        .HasDatabaseName("IX_PayrollEmployeeHours_TenantId");

    // Global query filter para multi-tenancy
    entity.HasQueryFilter(e =>
        _currentUserService == null ||
        _currentUserService.CompanyId == null ||
        (int?)e.TenantId == _currentUserService.CompanyId);

    // Relaciones
    entity.HasOne(e => e.PayrollHeader)
        .WithMany()
        .HasForeignKey(e => e.PayrollHeaderId)
        .OnDelete(DeleteBehavior.Cascade);

    entity.HasOne(e => e.Empleado)
        .WithMany()
        .HasForeignKey(e => e.EmpleadoId)
        .OnDelete(DeleteBehavior.Restrict);
});

// Agregar índice de PayPeriodType en PayrollHeader:
modelBuilder.Entity<PayrollHeader>()
    .HasIndex(e => new { e.TenantId, e.PayPeriodType })
    .HasDatabaseName("IX_PayrollHeaders_TenantId_PayPeriodType");
```

#### 9. MODIFICAR — `CreatePayrollHeaderRequest` (al final de `PayrollHeadersController.cs`)

```csharp
public record CreatePayrollHeaderRequest(
    string PayrollNumber,
    DateTime PeriodStartDate,
    DateTime PeriodEndDate,
    DateTime PayDate,
    PayPeriodType PayPeriodType = PayPeriodType.Quincenal  // NUEVO
);
```

Y en el método `CreatePayrollHeader`, agregar:
```csharp
PayPeriodType = request.PayPeriodType,
```
al objeto `PayrollHeader` que se crea.

#### 10. MODIFICAR — `CalculatePayroll` en `PayrollHeadersController.cs`

Cambiar la línea que pasa `payFrequency` al orquestador:
```csharp
// ANTES:
payFrequency: employee.PayFrequency,

// DESPUÉS:
payFrequency: payrollHeader.PayPeriodType.ToString(),
```

Esto asegura que el ISR se calcule según el período de la **planilla**, no del empleado.

Además, antes del loop de cálculo, agregar lógica para incorporar horas trabajadas:
```csharp
// Obtener horas registradas para esta planilla (si existen)
var employeeHoursMap = await _context.PayrollEmployeeHours
    .Where(h => h.PayrollHeaderId == payrollHeader.Id && h.TenantId == tenantId)
    .ToDictionaryAsync(h => h.EmpleadoId);

// Dentro del foreach de activeEmployees:
decimal grossPay = employee.SalarioBase; // Default: salario base completo

if (employeeHoursMap.TryGetValue(employee.Id, out var hours))
{
    // Calcular pago basado en horas registradas
    var hourlyRate = employee.HourlyRate > 0
        ? employee.HourlyRate
        : (employee.HoursPerPeriod > 0 ? employee.SalarioBase / employee.HoursPerPeriod : employee.SalarioBase);

    hours.RegularPay = hours.RegularHours * hourlyRate;
    hours.SundayPay = hours.SundayHours * hourlyRate * 1.50m;
    hours.HolidayPay = hours.HolidayHours * hourlyRate * 1.50m;
    hours.OvertimeDayPay = hours.OvertimeDayHours * hourlyRate * 1.25m;
    hours.OvertimeNightPay = hours.OvertimeNightHours * hourlyRate * 1.50m;
    hours.AbsenceDeduction = hours.AbsenceHours * hourlyRate;
    hours.TotalHoursPay = hours.RegularPay + hours.SundayPay + hours.HolidayPay
        + hours.OvertimeDayPay + hours.OvertimeNightPay - hours.AbsenceDeduction;
    hours.UpdatedAt = DateTime.UtcNow;

    grossPay = hours.TotalHoursPay;
}

// Pasar grossPay al orquestador en lugar de employee.SalarioBase
var calculationResult = await _orchestrator.CalculateEmployeePayrollAsync(
    companyId: tenantId,
    grossPay: grossPay,
    payFrequency: payrollHeader.PayPeriodType.ToString(),
    // ... resto de parámetros igual
);

// Actualizar PayrollDetail con desglose
var detail = new PayrollDetail
{
    // ... campos existentes ...
    BaseSalary = employee.SalarioBase,
    OvertimePay = employeeHoursMap.ContainsKey(employee.Id)
        ? employeeHoursMap[employee.Id].OvertimeDayPay + employeeHoursMap[employee.Id].OvertimeNightPay
        : 0,
    // ...
};
```

#### 11. NUEVO ENDPOINT — Gestión de horas por planilla

En `PayrollHeadersController.cs`, agregar:

```csharp
/// <summary>
/// Obtiene las horas registradas para todos los empleados de una planilla.
/// GET /api/payrollheaders/{id}/hours
/// </summary>
[HttpGet("{id}/hours")]
[Authorize(Roles = "Owner,Admin,Manager")]
public async Task<ActionResult> GetPayrollHours(int id) { /* ... */ }

/// <summary>
/// Registra/actualiza las horas de un empleado en una planilla específica.
/// PUT /api/payrollheaders/{payrollId}/hours/{empleadoId}
/// </summary>
[HttpPut("{payrollId}/hours/{empleadoId}")]
[Authorize(Roles = "Owner,Admin,Manager")]
public async Task<ActionResult> UpsertEmployeeHours(
    int payrollId, int empleadoId, [FromBody] UpsertEmployeeHoursRequest request) { /* ... */ }

/// <summary>
/// Auto-genera horas regulares default para todos los empleados activos.
/// POST /api/payrollheaders/{id}/hours/generate-defaults
/// </summary>
[HttpPost("{id}/hours/generate-defaults")]
[Authorize(Roles = "Owner,Admin,Manager")]
public async Task<ActionResult> GenerateDefaultHours(int id) { /* ... */ }
```

DTO para el request:
```csharp
public record UpsertEmployeeHoursRequest(
    decimal RegularHours,
    decimal SundayHours = 0,
    decimal HolidayHours = 0,
    decimal OvertimeDayHours = 0,
    decimal OvertimeNightHours = 0,
    decimal AbsenceHours = 0,
    decimal DisabilityHours = 0
);
```

#### 12. MIGRACIÓN EF Core

```bash
dotnet ef migrations add AddPayPeriodTypeAndHoursTracking \
  --project src/Infrastructure/Planilla.Infrastructure \
  --startup-project src/UI/Planilla.Web
```

La migración debe:
- Agregar columnas `PayPeriodType`, `HoursPerWeek`, `HoursPerPeriod`, `HourlyRate` a `Empleados`
- Agregar columna `PayPeriodType` a `PayrollHeaders`
- Crear tabla `PayrollEmployeeHours` con todos sus índices
- Poblar valores default para empleados existentes:
  - `PayPeriodType = 2` (Quincenal)
  - `HoursPerWeek = 48`
  - `HoursPerPeriod = 104`
  - `HourlyRate = SalarioBase / 104` (calculado)

### FRONTEND

#### 13. MODIFICAR — `src/UI/Planilla.Web/ClientApp/src/pages/EmpleadosPage.jsx`

**A. Agregar campos al formData:**
```javascript
const [formData, setFormData] = useState({
    nombre: '',
    apellido: '',
    numeroIdentificacion: '',
    email: '',
    salarioBase: '',
    fechaContratacion: '',
    departamentoId: '',
    posicionId: '',
    // === NUEVOS ===
    payPeriodType: 2,       // Quincenal por defecto
    hoursPerWeek: 48,
    hoursPerPeriod: 104,
});
```

**B. Agregar sección "Información de Pago" al modal:**

Después de los campos de Departamento/Posición, agregar una sección visual separada con:

1. **Select de Tipo de Período**: Semanal, Bisemanal, Quincenal, Mensual
2. **Input Horas/Semana**: default 48, readonly sugerido
3. **Input Horas/Período**: auto-calculado cuando cambia tipo de período o horas/semana
4. **Display Tasa por Hora**: calculado automáticamente (SalarioBase ÷ HorasPeríodo), solo lectura
5. **Display Períodos/Año**: informativo (52, 26, 24, 12 según tipo)

**C. Lógica de auto-cálculo en el formulario:**
```javascript
const PAY_PERIOD_CONFIG = {
    0: { name: 'Semanal', periodsPerYear: 52, weekMultiplier: 1 },
    1: { name: 'Bisemanal', periodsPerYear: 26, weekMultiplier: 2 },
    2: { name: 'Quincenal', periodsPerYear: 24, weekMultiplier: 52/24 },
    3: { name: 'Mensual', periodsPerYear: 12, weekMultiplier: 52/12 },
};

// Cuando cambia payPeriodType o hoursPerWeek:
useEffect(() => {
    const config = PAY_PERIOD_CONFIG[formData.payPeriodType];
    const suggestedHours = Math.round(formData.hoursPerWeek * config.weekMultiplier);
    setFormData(prev => ({
        ...prev,
        hoursPerPeriod: suggestedHours
    }));
}, [formData.payPeriodType, formData.hoursPerWeek]);
```

**D. Mostrar tasa por hora en la tabla de empleados:**

Agregar columna "Tasa/h" después de Salario Base mostrando `$X.XX/h`.

**E. En la función de editar empleado**, poblar los nuevos campos:
```javascript
setFormData({
    // campos existentes...
    payPeriodType: empleado.payPeriodType ?? 2,
    hoursPerWeek: empleado.hoursPerWeek ?? 48,
    hoursPerPeriod: empleado.hoursPerPeriod ?? 104,
});
```

#### 14. MODIFICAR — `src/UI/Planilla.Web/ClientApp/src/pages/PlanillasPage.jsx`

**A. Agregar campo payPeriodType al formData:**
```javascript
const [formData, setFormData] = useState({
    payrollNumber: '',
    periodStartDate: '',
    periodEndDate: '',
    payDate: '',
    companyId: 1,
    payPeriodType: 2,  // NUEVO
});
```

**B. Agregar selector de tipo de período al formulario de nueva planilla.**

**C. Mostrar tipo de período en la lista de planillas** (badge al lado del número).

**D. Agregar panel de horas trabajadas:**

Cuando una planilla está en estado "Draft", mostrar una pestaña/sección "Horas Trabajadas" donde:
- Se listan todos los empleados activos
- Por cada uno hay inputs para: Regulares, Domingo, Feriado, Extra Diurna, Extra Nocturna, Ausencias
- Botón "Auto-llenar" que carga las horas regulares default de cada empleado
- Botón "Calcular" que muestra un preview del bruto antes de ejecutar el cálculo real
- Los montos calculados (tasa × horas × recargo) se muestran en tiempo real

---

## FORMATO DE ENTREGA

1. **Cada archivo modificado**: mostrar diff con contexto (3 líneas antes y después)
2. **Archivos nuevos**: contenido completo
3. **Comando de migración**: `dotnet ef migrations add ...` exacto
4. **Verificación**: `dotnet build Planilla.sln` sin errores
5. **SQL de migración generado** para revisión

## CHECKLIST DE VERIFICACIÓN

### Arquitectura
- [ ] Domain no tiene dependencias externas
- [ ] DTOs están en Application, no en Domain
- [ ] DbContext y configuraciones solo en Infrastructure
- [ ] Controllers solo en Web

### Multi-Tenancy
- [ ] PayrollEmployeeHours tiene TenantId
- [ ] Global query filter aplicado a PayrollEmployeeHours
- [ ] Todos los queries filtran por TenantId
- [ ] Índices incluyen TenantId

### Cálculos / Compliance
- [ ] ISR se anualiza usando PayPeriodType de la PLANILLA (no del empleado)
- [ ] PayrollConstants incluye Bisemanal (26 períodos)
- [ ] Horas domingo/feriado × 1.50 (Código de Trabajo Panamá)
- [ ] Horas extra diurnas × 1.25
- [ ] Horas extra nocturnas × 1.50
- [ ] Horario semanal default: 48 horas (Art. 31 Código de Trabajo)

### Retrocompatibilidad
- [ ] Campo PayFrequency (string) se mantiene y sincroniza
- [ ] Empleados sin horas registradas usan SalarioBase completo
- [ ] Planillas existentes siguen funcionando (PayPeriodType default = Quincenal)
- [ ] ISR no cambia para planillas donde PayPeriodType coincide con PayFrequency del empleado

### Frontend
- [ ] Dark theme (navy-950/900 bg, emerald-500 accents)
- [ ] Formularios responsivos
- [ ] Auto-cálculo de horas por período y tasa por hora
- [ ] Validación client-side de rangos numéricos

### Testing (mínimo 70% coverage en lógica nueva)
- [ ] Unit test: CalculateSuggestedHoursPerPeriod para cada PayPeriodType
- [ ] Unit test: RecalculateHourlyRate con edge cases (0 horas, salario 0)
- [ ] Unit test: PayrollConstants.GetPeriodsPerYear con enum
- [ ] Integration test: Crear empleado con nuevos campos vía API
- [ ] Integration test: Calcular planilla CON horas registradas
- [ ] Integration test: Calcular planilla SIN horas (retrocompatibilidad)
- [ ] Integration test: Intentar acceso cross-tenant a PayrollEmployeeHours

---

## ORDEN DE EJECUCIÓN RECOMENDADO

1. Crear enum `PayPeriodType`
2. Modificar `Empleado.cs` (nuevos campos + métodos helper)
3. Crear `PayrollEmployeeHours.cs`
4. Modificar `PayrollHeader.cs`
5. Modificar `PayrollConstants.cs`
6. Modificar `EmpleadoDtos.cs`
7. Modificar `MappingProfile.cs`
8. Modificar `ApplicationDbContext.cs`
9. Generar y aplicar migración EF Core
10. Modificar `CreatePayrollHeaderRequest` y `CreatePayrollHeader`
11. Modificar `CalculatePayroll` (usar PayPeriodType de header, integrar horas)
12. Crear endpoints de horas
13. Compilar backend y ejecutar tests
14. Modificar `EmpleadosPage.jsx`
15. Modificar `PlanillasPage.jsx`
16. Test E2E del flujo completo

═══════════════════════════════════════════════════════════════════════════
