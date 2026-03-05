using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;
using Vorluno.Planilla.Infrastructure.Services;

namespace Vorluno.Planilla.Web.Controllers;

/// <summary>
/// Controlador para gestionar horas extra de empleados
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class HorasExtraController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly IOvertimeFactorService _overtimeFactorService;

    public HorasExtraController(
        IUnitOfWork unitOfWork,
        ApplicationDbContext context,
        ITenantContext tenantContext,
        IOvertimeFactorService overtimeFactorService)
    {
        _unitOfWork = unitOfWork;
        _context = context;
        _tenantContext = tenantContext;
        _overtimeFactorService = overtimeFactorService;
    }

    /// <summary>
    /// Obtiene lista de horas extra con filtros
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? empleadoId = null,
        [FromQuery] DateTime? fecha = null,
        [FromQuery] TipoHoraExtra? tipo = null,
        [FromQuery] bool? aprobadas = null)
    {
        var tenantId = _tenantContext.TenantId;
        var query = _context.HorasExtra
            .Where(h => h.TenantId == tenantId)
            .Include(h => h.Empleado)
            .AsQueryable();

        if (empleadoId.HasValue)
            query = query.Where(h => h.EmpleadoId == empleadoId.Value);

        if (fecha.HasValue)
            query = query.Where(h => h.Fecha.Date == fecha.Value.Date);

        if (tipo.HasValue)
            query = query.Where(h => h.TipoHoraExtra == tipo.Value);

        if (aprobadas.HasValue)
            query = query.Where(h => h.EstaAprobada == aprobadas.Value);

        var horasExtra = await query.AsNoTracking().OrderByDescending(h => h.Fecha).ToListAsync();

        var dtos = horasExtra.Select(MapToDto);
        return Ok(dtos);
    }

    /// <summary>
    /// Obtiene una hora extra por ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var horaExtra = await _context.HorasExtra
            .Where(h => h.Id == id && h.TenantId == tenantId)
            .Include(h => h.Empleado)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (horaExtra == null)
            return NotFound();

        return Ok(MapToDto(horaExtra));
    }

    /// <summary>
    /// Obtiene horas extra de un empleado específico
    /// </summary>
    [HttpGet("empleado/{empleadoId}")]
    public async Task<IActionResult> GetByEmpleado(int empleadoId)
    {
        var tenantId = _tenantContext.TenantId;
        var horasExtra = await _context.HorasExtra
            .Where(h => h.EmpleadoId == empleadoId && h.TenantId == tenantId)
            .Include(h => h.Empleado)
            .AsNoTracking()
            .OrderByDescending(h => h.Fecha)
            .ToListAsync();

        var dtos = horasExtra.Select(MapToDto);
        return Ok(dtos);
    }

    /// <summary>
    /// Obtiene horas extra pendientes de aprobación
    /// </summary>
    [HttpGet("pendientes")]
    public async Task<IActionResult> GetPendientes()
    {
        var tenantId = _tenantContext.TenantId;
        var pendientes = await _context.HorasExtra
            .Where(h => h.TenantId == tenantId && !h.EstaAprobada && h.PlanillaDetailId == null)
            .Include(h => h.Empleado)
            .AsNoTracking()
            .OrderBy(h => h.Fecha)
            .ToListAsync();

        var dtos = pendientes.Select(MapToDto);
        return Ok(dtos);
    }

    /// <summary>
    /// Obtiene tipos de hora extra con factores
    /// </summary>
    [HttpGet("tipos")]
    public IActionResult GetTipos()
    {
        var tipos = new[]
        {
            new { Valor = (int)TipoHoraExtra.Diurna, Nombre = "Diurna (1.25x)", Factor = 1.25m },
            new { Valor = (int)TipoHoraExtra.Nocturna, Nombre = "Nocturna (1.50x)", Factor = 1.50m },
            new { Valor = (int)TipoHoraExtra.DomingoFeriado, Nombre = "Domingo/Feriado (1.50x)", Factor = 1.50m },
            new { Valor = (int)TipoHoraExtra.NocturnaDomingoFeriado, Nombre = "Nocturna Dom/Fer (2.25x)", Factor = 2.25m },
            new { Valor = (int)TipoHoraExtra.FiestaNacionalDiurna, Nombre = "Fiesta Nacional Diurna (3.125x)", Factor = 3.125m },
            new { Valor = (int)TipoHoraExtra.FiestaNacionalNocturna, Nombre = "Fiesta Nacional Nocturna (3.75x)", Factor = 3.75m },
            new { Valor = (int)TipoHoraExtra.MixtaDiurnaNocturna, Nombre = "Mixta Diurna-Nocturna (1.50x)", Factor = 1.50m },
            new { Valor = (int)TipoHoraExtra.MixtaNocturnaDiurna, Nombre = "Mixta Nocturna-Diurna (1.75x)", Factor = 1.75m }
        };

        return Ok(tipos);
    }

    /// <summary>
    /// Crea una nueva hora extra
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> Create(CreateHoraExtraRequest request)
    {
        var tenantId = _tenantContext.TenantId;

        // Verificar que el empleado existe
        var empleado = await _context.Empleados
            .FirstOrDefaultAsync(e => e.Id == request.EmpleadoId && e.TenantId == tenantId);
        if (empleado == null)
            return BadRequest(new { message = "El empleado especificado no existe." });

        // Calcular horas
        var cantidadHoras = CalcularHoras(request.HoraInicio, request.HoraFin);

        // Determinar tipo automáticamente si no se especifica explícitamente (opcional - por ahora usar el del request)
        // En el futuro se podría auto-detectar: var tipoDetectado = _overtimeFactorService.DetermineOvertimeType(request.Fecha, request.HoraInicio, request.HoraFin);
        var tipo = request.TipoHoraExtra;

        // Validar límites legales
        var (esExceso, mensajeValidacion) = await _overtimeFactorService.ValidateOvertimeLimits(
            request.EmpleadoId, request.Fecha, cantidadHoras);

        // Calcular factores
        var factorBase = _overtimeFactorService.CalculateBaseFactor(tipo);
        var factorTotal = _overtimeFactorService.CalculateFactor(tipo, esExceso);
        decimal? factorExceso = esExceso ? 1.75m : null;

        // Calcular monto
        var montoCalculado = CalcularMonto(empleado, cantidadHoras, factorBase, factorExceso);

        var horaExtra = new HoraExtra
        {
            TenantId = tenantId,
            EmpleadoId = request.EmpleadoId,
            Fecha = request.Fecha.Date,
            TipoHoraExtra = tipo,
            HoraInicio = request.HoraInicio,
            HoraFin = request.HoraFin,
            CantidadHoras = cantidadHoras,
            FactorMultiplicador = factorTotal, // Factor total (base × exceso si aplica)
            EsExceso = esExceso,
            FactorExceso = factorExceso,
            MontoCalculado = montoCalculado,
            Motivo = request.Motivo,
            EstaAprobada = false,
            CreatedAt = DateTime.UtcNow
        };

        // Si hay mensaje de validación (exceso), agregarlo a observaciones
        if (!string.IsNullOrEmpty(mensajeValidacion))
        {
            horaExtra.Observaciones = mensajeValidacion;
        }

        await _unitOfWork.Repository<HoraExtra>().AddAsync(horaExtra);
        await _unitOfWork.CompleteAsync();

        // Recargar con navegación
        horaExtra = await _context.HorasExtra
            .Include(h => h.Empleado)
            .FirstOrDefaultAsync(h => h.Id == horaExtra.Id);

        return CreatedAtAction(nameof(GetById), new { id = horaExtra!.Id }, MapToDto(horaExtra));
    }

    /// <summary>
    /// Crea múltiples horas extra (batch)
    /// </summary>
    [HttpPost("batch")]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> CreateBatch([FromBody] List<CreateHoraExtraRequest> requests)
    {
        if (requests == null || requests.Count == 0)
            return BadRequest(new { message = "Debe proporcionar al menos una hora extra." });

        var tenantId = _tenantContext.TenantId;
        var horasExtra = new List<HoraExtra>();

        foreach (var request in requests)
        {
            var empleado = await _context.Empleados
                .FirstOrDefaultAsync(e => e.Id == request.EmpleadoId && e.TenantId == tenantId);
            if (empleado == null)
                continue;

            var cantidadHoras = CalcularHoras(request.HoraInicio, request.HoraFin);
            var tipo = request.TipoHoraExtra;

            // Validar límites legales
            var (esExceso, mensajeValidacion) = await _overtimeFactorService.ValidateOvertimeLimits(
                request.EmpleadoId, request.Fecha, cantidadHoras);

            // Calcular factores
            var factorBase = _overtimeFactorService.CalculateBaseFactor(tipo);
            var factorTotal = _overtimeFactorService.CalculateFactor(tipo, esExceso);
            decimal? factorExceso = esExceso ? 1.75m : null;

            // Calcular monto
            var montoCalculado = CalcularMonto(empleado, cantidadHoras, factorBase, factorExceso);

            var horaExtra = new HoraExtra
            {
                TenantId = tenantId,
                EmpleadoId = request.EmpleadoId,
                Fecha = request.Fecha.Date,
                TipoHoraExtra = tipo,
                HoraInicio = request.HoraInicio,
                HoraFin = request.HoraFin,
                CantidadHoras = cantidadHoras,
                FactorMultiplicador = factorTotal,
                EsExceso = esExceso,
                FactorExceso = factorExceso,
                MontoCalculado = montoCalculado,
                Motivo = request.Motivo,
                EstaAprobada = false,
                CreatedAt = DateTime.UtcNow,
                Observaciones = !string.IsNullOrEmpty(mensajeValidacion) ? mensajeValidacion : null
            };

            horasExtra.Add(horaExtra);
        }

        foreach (var he in horasExtra)
        {
            await _unitOfWork.Repository<HoraExtra>().AddAsync(he);
        }

        await _unitOfWork.CompleteAsync();

        return Ok(new { message = $"{horasExtra.Count} horas extra creadas exitosamente.", count = horasExtra.Count });
    }

    /// <summary>
    /// Actualiza una hora extra
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> Update(int id, CreateHoraExtraRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        var horaExtra = await _context.HorasExtra
            .FirstOrDefaultAsync(h => h.Id == id && h.TenantId == tenantId);

        if (horaExtra == null)
            return NotFound();

        if (horaExtra.EstaAprobada)
            return BadRequest(new { message = "No se puede modificar una hora extra ya aprobada." });

        if (horaExtra.PlanillaDetailId != null)
            return BadRequest(new { message = "No se puede modificar una hora extra ya pagada." });

        // Obtener empleado para calcular monto
        var empleado = await _context.Empleados
            .FirstOrDefaultAsync(e => e.Id == horaExtra.EmpleadoId && e.TenantId == tenantId);
        if (empleado == null)
            return BadRequest(new { message = "El empleado asociado no existe." });

        var cantidadHoras = CalcularHoras(request.HoraInicio, request.HoraFin);
        var tipo = request.TipoHoraExtra;

        // Validar límites legales (excluir la hora extra actual del cálculo)
        var horasExistentesDelDia = await _context.HorasExtra
            .Where(h => h.EmpleadoId == horaExtra.EmpleadoId 
                && h.Fecha.Date == request.Fecha.Date
                && h.Id != id // Excluir la hora extra que se está actualizando
                && h.PlanillaDetailId == null)
            .SumAsync(h => h.CantidadHoras);

        var horasExistentesDeLaSemana = await _context.HorasExtra
            .Where(h => h.EmpleadoId == horaExtra.EmpleadoId
                && h.Fecha >= request.Fecha.Date.AddDays(-((int)request.Fecha.DayOfWeek + 6) % 7)
                && h.Fecha <= request.Fecha.Date.AddDays(6 - ((int)request.Fecha.DayOfWeek + 6) % 7)
                && h.Id != id
                && h.PlanillaDetailId == null)
            .SumAsync(h => h.CantidadHoras);

        bool esExceso = (horasExistentesDelDia + cantidadHoras) > 3m || (horasExistentesDeLaSemana + cantidadHoras) > 9m;

        // Calcular factores
        var factorBase = _overtimeFactorService.CalculateBaseFactor(tipo);
        var factorTotal = _overtimeFactorService.CalculateFactor(tipo, esExceso);
        decimal? factorExceso = esExceso ? 1.75m : null;

        // Calcular monto
        var montoCalculado = CalcularMonto(empleado, cantidadHoras, factorBase, factorExceso);

        horaExtra.Fecha = request.Fecha.Date;
        horaExtra.TipoHoraExtra = tipo;
        horaExtra.HoraInicio = request.HoraInicio;
        horaExtra.HoraFin = request.HoraFin;
        horaExtra.CantidadHoras = cantidadHoras;
        horaExtra.FactorMultiplicador = factorTotal;
        horaExtra.EsExceso = esExceso;
        horaExtra.FactorExceso = factorExceso;
        horaExtra.MontoCalculado = montoCalculado;
        horaExtra.Motivo = request.Motivo;
        horaExtra.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Repository<HoraExtra>().Update(horaExtra);
        await _unitOfWork.CompleteAsync();

        return NoContent();
    }

    /// <summary>
    /// Aprueba una hora extra
    /// </summary>
    [HttpPost("{id}/aprobar")]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> Aprobar(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var horaExtra = await _context.HorasExtra
            .FirstOrDefaultAsync(h => h.Id == id && h.TenantId == tenantId);

        if (horaExtra == null)
            return NotFound();

        if (horaExtra.EstaAprobada)
            return BadRequest(new { message = "La hora extra ya está aprobada." });

        // Obtener empleado para recalcular monto (por si cambió el salario)
        var empleado = await _context.Empleados
            .FirstOrDefaultAsync(e => e.Id == horaExtra.EmpleadoId && e.TenantId == tenantId);
        if (empleado == null)
            return BadRequest(new { message = "El empleado asociado no existe." });

        // Recalcular monto al aprobar (por si cambió el salario del empleado)
        // Usar factor base y factor exceso por separado para cálculo correcto
        var factorBase = _overtimeFactorService.CalculateBaseFactor(horaExtra.TipoHoraExtra);
        var montoCalculado = CalcularMonto(empleado, horaExtra.CantidadHoras, factorBase, horaExtra.FactorExceso);

        horaExtra.EstaAprobada = true;
        horaExtra.FechaAprobacion = DateTime.UtcNow;
        horaExtra.AprobadoPor = User.FindFirst("sub")?.Value ?? User.FindFirst("email")?.Value ?? "Sistema";
        horaExtra.MontoCalculado = montoCalculado;
        horaExtra.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Repository<HoraExtra>().Update(horaExtra);
        await _unitOfWork.CompleteAsync();

        return NoContent();
    }

    /// <summary>
    /// Rechaza una hora extra
    /// </summary>
    [HttpPost("{id}/rechazar")]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> Rechazar(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var horaExtra = await _context.HorasExtra
            .FirstOrDefaultAsync(h => h.Id == id && h.TenantId == tenantId);

        if (horaExtra == null)
            return NotFound();

        _context.HorasExtra.Remove(horaExtra);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Elimina una hora extra
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var horaExtra = await _context.HorasExtra
            .FirstOrDefaultAsync(h => h.Id == id && h.TenantId == tenantId);

        if (horaExtra == null)
            return NotFound();

        if (horaExtra.PlanillaDetailId != null)
            return BadRequest(new { message = "No se puede eliminar una hora extra ya pagada." });

        _context.HorasExtra.Remove(horaExtra);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // Métodos privados
    private decimal CalcularHoras(TimeSpan inicio, TimeSpan fin)
    {
        var diferencia = fin - inicio;
        if (diferencia.TotalHours < 0)
            diferencia = diferencia.Add(TimeSpan.FromHours(24));

        return (decimal)diferencia.TotalHours;
    }

    /// <summary>
    /// Calcula el monto de horas extra usando el HourlyRate del empleado.
    /// Si HourlyRate es 0, calcula con base mensual: SalarioMensual / (HoursPerWeek × 4.333).
    /// Considera FactorExceso si aplica.
    /// </summary>
    private decimal CalcularMonto(Empleado empleado, decimal cantidadHoras, decimal factorMultiplicador, decimal? factorExceso = null)
    {
        decimal hourlyRate = empleado.HourlyRate;

        if (hourlyRate <= 0)
        {
            // SalarioBase ya es mensual, no necesita conversión por período
            hourlyRate = Empleado.ComputeHourlyRateFromMonthly(
                empleado.SalarioBase, empleado.HoursPerWeek);
        }

        // Calcular monto: tasa × horas × factor base × (factor exceso si aplica)
        decimal factorTotal = factorMultiplicador;
        if (factorExceso.HasValue && factorExceso.Value > 0)
        {
            factorTotal *= factorExceso.Value; // Multiplicar por factor de exceso (1.75x)
        }

        var monto = hourlyRate * cantidadHoras * factorTotal;
        return Math.Round(monto, 2);
    }

    private decimal ObtenerFactor(TipoHoraExtra tipo)
    {
        return tipo switch
        {
            TipoHoraExtra.Diurna => 1.25m,
            TipoHoraExtra.Nocturna => 1.50m,
            TipoHoraExtra.DomingoFeriado => 1.50m,
            TipoHoraExtra.NocturnaDomingoFeriado => 2.25m, // 1.50 × 1.50 según Código de Trabajo Art. 50
            TipoHoraExtra.FiestaNacionalDiurna => 3.125m, // 2.50 × 1.25 según Código de Trabajo Art. 49
            TipoHoraExtra.FiestaNacionalNocturna => 3.75m, // 2.50 × 1.50 según Código de Trabajo Art. 49
            TipoHoraExtra.MixtaDiurnaNocturna => 1.50m, // Según Código de Trabajo Art. 33
            TipoHoraExtra.MixtaNocturnaDiurna => 1.75m, // Según Código de Trabajo Art. 33
            _ => 1.25m
        };
    }

    private string ObtenerNombreTipo(TipoHoraExtra tipo)
    {
        return tipo switch
        {
            TipoHoraExtra.Diurna => "Diurna (1.25x)",
            TipoHoraExtra.Nocturna => "Nocturna (1.50x)",
            TipoHoraExtra.DomingoFeriado => "Domingo/Feriado (1.50x)",
            TipoHoraExtra.NocturnaDomingoFeriado => "Nocturna Dom/Fer (2.25x)",
            TipoHoraExtra.FiestaNacionalDiurna => "Fiesta Nacional Diurna (3.125x)",
            TipoHoraExtra.FiestaNacionalNocturna => "Fiesta Nacional Nocturna (3.75x)",
            TipoHoraExtra.MixtaDiurnaNocturna => "Mixta Diurna-Nocturna (1.50x)",
            TipoHoraExtra.MixtaNocturnaDiurna => "Mixta Nocturna-Diurna (1.75x)",
            _ => "Desconocido"
        };
    }

    private HoraExtraDto MapToDto(HoraExtra horaExtra)
    {
        return new HoraExtraDto(
            horaExtra.Id,
            horaExtra.EmpleadoId,
            $"{horaExtra.Empleado.Nombre} {horaExtra.Empleado.Apellido}",
            horaExtra.Fecha,
            horaExtra.TipoHoraExtra,
            ObtenerNombreTipo(horaExtra.TipoHoraExtra),
            horaExtra.HoraInicio,
            horaExtra.HoraFin,
            horaExtra.CantidadHoras,
            horaExtra.FactorMultiplicador,
            horaExtra.EsExceso,
            horaExtra.FactorExceso,
            horaExtra.MontoCalculado,
            horaExtra.EstaAprobada,
            horaExtra.AprobadoPor,
            horaExtra.Motivo,
            horaExtra.Observaciones,
            horaExtra.PlanillaDetailId
        );
    }

    /// <summary>
    /// Obtiene estadísticas agregadas de horas extra
    /// GET /api/horasextra/estadisticas?empleadoId=&fechaInicio=&fechaFin=
    /// </summary>
    [HttpGet("estadisticas")]
    [Authorize(Roles = "Owner,Admin,Manager,Accountant")]
    public async Task<ActionResult<HorasExtraEstadisticasDto>> GetEstadisticas(
        [FromQuery] int? empleadoId = null,
        [FromQuery] DateTime? fechaInicio = null,
        [FromQuery] DateTime? fechaFin = null)
    {
        var tenantId = _tenantContext.TenantId;
        var query = _context.HorasExtra
            .Where(h => h.TenantId == tenantId && h.EstaAprobada);

        if (empleadoId.HasValue)
        {
            query = query.Where(h => h.EmpleadoId == empleadoId.Value);
        }

        if (fechaInicio.HasValue)
        {
            query = query.Where(h => h.Fecha >= fechaInicio.Value.Date);
        }

        if (fechaFin.HasValue)
        {
            query = query.Where(h => h.Fecha <= fechaFin.Value.Date);
        }

        var horasExtra = await query
            .Include(h => h.Empleado)
            .ToListAsync();

        if (!horasExtra.Any())
        {
            return Ok(new HorasExtraEstadisticasDto
            {
                TotalHoras = 0,
                MontoTotal = 0,
                PromedioHorasPorSemana = 0,
                PromedioHorasPorMes = 0,
                DiasFestivosTrabajados = new List<DateTime>(),
                TotalHorasConExceso = 0,
                PorcentajeHorasExceso = 0
            });
        }

        // Calcular totales por tipo
        decimal totalHorasDiurnas = horasExtra
            .Where(h => h.TipoHoraExtra == TipoHoraExtra.Diurna)
            .Sum(h => h.CantidadHoras);

        decimal totalHorasNocturnas = horasExtra
            .Where(h => h.TipoHoraExtra == TipoHoraExtra.Nocturna)
            .Sum(h => h.CantidadHoras);

        decimal totalHorasDomingoFeriado = horasExtra
            .Where(h => h.TipoHoraExtra == TipoHoraExtra.DomingoFeriado || h.TipoHoraExtra == TipoHoraExtra.NocturnaDomingoFeriado)
            .Sum(h => h.CantidadHoras);

        decimal totalHorasFestivos = horasExtra
            .Where(h => h.TipoHoraExtra == TipoHoraExtra.FiestaNacionalDiurna || h.TipoHoraExtra == TipoHoraExtra.FiestaNacionalNocturna)
            .Sum(h => h.CantidadHoras);

        decimal totalHorasMixtas = horasExtra
            .Where(h => h.TipoHoraExtra == TipoHoraExtra.MixtaDiurnaNocturna || h.TipoHoraExtra == TipoHoraExtra.MixtaNocturnaDiurna)
            .Sum(h => h.CantidadHoras);

        decimal totalHorasExceso = horasExtra
            .Where(h => h.EsExceso)
            .Sum(h => h.CantidadHoras);

        decimal totalHoras = horasExtra.Sum(h => h.CantidadHoras);

        // Calcular montos por tipo
        decimal montoDiurnas = horasExtra
            .Where(h => h.TipoHoraExtra == TipoHoraExtra.Diurna)
            .Sum(h => h.MontoCalculado ?? 0);

        decimal montoNocturnas = horasExtra
            .Where(h => h.TipoHoraExtra == TipoHoraExtra.Nocturna)
            .Sum(h => h.MontoCalculado ?? 0);

        decimal montoDomingoFeriado = horasExtra
            .Where(h => h.TipoHoraExtra == TipoHoraExtra.DomingoFeriado || h.TipoHoraExtra == TipoHoraExtra.NocturnaDomingoFeriado)
            .Sum(h => h.MontoCalculado ?? 0);

        decimal montoFestivos = horasExtra
            .Where(h => h.TipoHoraExtra == TipoHoraExtra.FiestaNacionalDiurna || h.TipoHoraExtra == TipoHoraExtra.FiestaNacionalNocturna)
            .Sum(h => h.MontoCalculado ?? 0);

        decimal montoMixtas = horasExtra
            .Where(h => h.TipoHoraExtra == TipoHoraExtra.MixtaDiurnaNocturna || h.TipoHoraExtra == TipoHoraExtra.MixtaNocturnaDiurna)
            .Sum(h => h.MontoCalculado ?? 0);

        decimal montoExceso = horasExtra
            .Where(h => h.EsExceso)
            .Sum(h => h.MontoCalculado ?? 0);

        decimal montoTotal = horasExtra.Sum(h => h.MontoCalculado ?? 0);

        // Calcular promedios
        var fechaMin = fechaInicio ?? horasExtra.Min(h => h.Fecha);
        var fechaMax = fechaFin ?? horasExtra.Max(h => h.Fecha);
        var diasTotales = (fechaMax - fechaMin).TotalDays + 1;
        var semanas = diasTotales / 7.0;
        var meses = diasTotales / 30.0;

        decimal promedioHorasPorSemana = semanas > 0 ? totalHoras / (decimal)semanas : 0;
        decimal promedioHorasPorMes = meses > 0 ? totalHoras / (decimal)meses : 0;

        // Días festivos trabajados
        var diasFestivosTrabajados = horasExtra
            .Where(h => h.TipoHoraExtra == TipoHoraExtra.FiestaNacionalDiurna || h.TipoHoraExtra == TipoHoraExtra.FiestaNacionalNocturna)
            .Select(h => h.Fecha.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        decimal porcentajeHorasExceso = totalHoras > 0 ? (totalHorasExceso / totalHoras) * 100 : 0;

        // Comparación con período anterior (si aplica)
        ComparacionPeriodoDto? comparacion = null;
        if (fechaInicio.HasValue && fechaFin.HasValue)
        {
            var duracionPeriodo = (fechaFin.Value - fechaInicio.Value).TotalDays;
            var fechaInicioAnterior = fechaInicio.Value.AddDays(-duracionPeriodo - 1);
            var fechaFinAnterior = fechaInicio.Value.AddDays(-1);

            var horasAnteriores = await _context.HorasExtra
                .Where(h => h.TenantId == tenantId 
                    && h.EstaAprobada
                    && h.Fecha >= fechaInicioAnterior.Date
                    && h.Fecha <= fechaFinAnterior.Date
                    && (!empleadoId.HasValue || h.EmpleadoId == empleadoId.Value))
                .ToListAsync();

            var totalHorasAnterior = horasAnteriores.Sum(h => h.CantidadHoras);
            var montoTotalAnterior = horasAnteriores.Sum(h => h.MontoCalculado ?? 0);

            if (totalHorasAnterior > 0 || montoTotalAnterior > 0)
            {
                var diferenciaHoras = totalHoras - totalHorasAnterior;
                var diferenciaMonto = montoTotal - montoTotalAnterior;
                var porcentajeCambioHoras = totalHorasAnterior > 0 ? (diferenciaHoras / totalHorasAnterior) * 100 : 0;
                var porcentajeCambioMonto = montoTotalAnterior > 0 ? (diferenciaMonto / montoTotalAnterior) * 100 : 0;

                comparacion = new ComparacionPeriodoDto
                {
                    TotalHorasAnterior = totalHorasAnterior,
                    MontoTotalAnterior = montoTotalAnterior,
                    DiferenciaHoras = diferenciaHoras,
                    DiferenciaMonto = diferenciaMonto,
                    PorcentajeCambioHoras = porcentajeCambioHoras,
                    PorcentajeCambioMonto = porcentajeCambioMonto
                };
            }
        }

        var estadisticas = new HorasExtraEstadisticasDto
        {
            TotalHorasDiurnas = totalHorasDiurnas,
            TotalHorasNocturnas = totalHorasNocturnas,
            TotalHorasDomingoFeriado = totalHorasDomingoFeriado,
            TotalHorasFestivos = totalHorasFestivos,
            TotalHorasMixtas = totalHorasMixtas,
            TotalHorasExceso = totalHorasExceso,
            TotalHoras = totalHoras,
            MontoDiurnas = montoDiurnas,
            MontoNocturnas = montoNocturnas,
            MontoDomingoFeriado = montoDomingoFeriado,
            MontoFestivos = montoFestivos,
            MontoMixtas = montoMixtas,
            MontoExceso = montoExceso,
            MontoTotal = montoTotal,
            PromedioHorasPorSemana = promedioHorasPorSemana,
            PromedioHorasPorMes = promedioHorasPorMes,
            DiasFestivosTrabajados = diasFestivosTrabajados,
            TotalHorasConExceso = totalHorasExceso,
            PorcentajeHorasExceso = porcentajeHorasExceso,
            ComparacionPeriodoAnterior = comparacion
        };

        return Ok(estadisticas);
    }

    /// <summary>
    /// Valida límites legales de horas extra para un empleado en una fecha específica
    /// GET /api/horasextra/validar-limites?empleadoId=&fecha=&horasNuevas=
    /// </summary>
    [HttpGet("validar-limites")]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<ActionResult> ValidarLimites(
        [FromQuery] int empleadoId,
        [FromQuery] DateTime fecha,
        [FromQuery] decimal horasNuevas = 0)
    {
        var tenantId = _tenantContext.TenantId;

        var (esExceso, mensaje) = await _overtimeFactorService.ValidateOvertimeLimits(
            empleadoId, fecha, horasNuevas);

        // Obtener horas extra del día y de la semana
        var inicioSemana = fecha.Date.AddDays(-(int)fecha.DayOfWeek);
        var finSemana = inicioSemana.AddDays(6);

        var horasDelDia = await _context.HorasExtra
            .Where(h => h.TenantId == tenantId
                && h.EmpleadoId == empleadoId
                && h.Fecha.Date == fecha.Date
                && h.EstaAprobada)
            .SumAsync(h => h.CantidadHoras);

        var horasDeLaSemana = await _context.HorasExtra
            .Where(h => h.TenantId == tenantId
                && h.EmpleadoId == empleadoId
                && h.Fecha >= inicioSemana
                && h.Fecha <= finSemana
                && h.EstaAprobada)
            .SumAsync(h => h.CantidadHoras);

        var horasDisponiblesDia = Math.Max(0, 3m - horasDelDia);
        var horasDisponiblesSemana = Math.Max(0, 9m - horasDeLaSemana);

        return Ok(new
        {
            esExceso,
            mensaje,
            horasDelDia,
            horasDeLaSemana,
            horasDisponiblesDia,
            horasDisponiblesSemana,
            limiteDiario = 3m,
            limiteSemanal = 9m,
            porcentajeDia = (horasDelDia / 3m) * 100,
            porcentajeSemana = (horasDeLaSemana / 9m) * 100
        });
    }

    /// <summary>
    /// Obtiene información sobre si una fecha es día festivo nacional
    /// GET /api/horasextra/es-festivo?fecha=
    /// </summary>
    [HttpGet("es-festivo")]
    [Authorize]
    public ActionResult EsFestivo([FromQuery] DateTime fecha)
    {
        var holidayService = HttpContext.RequestServices.GetRequiredService<PanamaHolidayService>();
        var esFestivo = holidayService.IsNationalHoliday(fecha);
        var nombreFestivo = esFestivo ? holidayService.GetHolidayName(fecha) : null;

        return Ok(new
        {
            esFestivo,
            nombreFestivo,
            fechaStr = fecha.ToString("yyyy-MM-dd")
        });
    }

    /// <summary>
    /// Sugiere el tipo de hora extra basado en fecha y horario
    /// GET /api/horasextra/sugerir-tipo?fecha=&horaInicio=&horaFin=
    /// </summary>
    [HttpGet("sugerir-tipo")]
    [Authorize]
    public ActionResult SugerirTipo(
        [FromQuery] DateTime fecha,
        [FromQuery] string horaInicio,
        [FromQuery] string horaFin)
    {
        var holidayService = HttpContext.RequestServices.GetRequiredService<PanamaHolidayService>();
        var overtimeFactorService = HttpContext.RequestServices.GetRequiredService<IOvertimeFactorService>();

        if (!TimeSpan.TryParse(horaInicio, out var inicio) || !TimeSpan.TryParse(horaFin, out var fin))
        {
            return BadRequest(new { message = "Formato de hora inválido. Use formato HH:mm" });
        }

        var tipoSugerido = overtimeFactorService.DetermineOvertimeType(fecha, inicio, fin);
        var esFestivo = holidayService.IsNationalHoliday(fecha);
        var nombreFestivo = esFestivo ? holidayService.GetHolidayName(fecha) : null;
        var esDomingo = fecha.DayOfWeek == DayOfWeek.Sunday;

        return Ok(new
        {
            tipoSugerido = (int)tipoSugerido,
            nombreTipo = ObtenerNombreTipo(tipoSugerido),
            esFestivo,
            nombreFestivo,
            esDomingo,
            factor = overtimeFactorService.CalculateBaseFactor(tipoSugerido)
        });
    }
}
