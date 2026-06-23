using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Planilla.Application.Services;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;
using Vorluno.Planilla.Infrastructure.Services;
using Vorluno.Planilla.Web.Authorization;
using Vorluno.Planilla.Web.Extensions;
using Vorluno.Planilla.Web.Filters;

namespace Vorluno.Planilla.Web.Controllers
{
    /// <summary>
    /// Controlador de API para gestionar las operaciones CRUD de los empleados.
    /// </summary>
    [Authorize] // ✅ SEGURIDAD: Todos los endpoints requieren autenticación
    [ApiController]
    [Route("api/[controller]")] // La ruta de acceso será /api/empleados
    public class EmpleadosController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ApplicationDbContext _context;
        private readonly ITenantContext _tenantContext;
        private readonly IPlanLimitService _planLimitService;
        private readonly IAuditLogService _auditLogService;
        private readonly UserManager<AppUser> _userManager;
        private readonly ILogger<EmpleadosController> _logger;
        private readonly IEmployeeDeletionValidationService _employeeDeletionValidationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Inicializa una nueva instancia de la clase <see cref="EmpleadosController"/>.
        /// </summary>
        public EmpleadosController(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ApplicationDbContext context,
            ITenantContext tenantContext,
            IPlanLimitService planLimitService,
            IAuditLogService auditLogService,
            UserManager<AppUser> userManager,
            ILogger<EmpleadosController> logger,
            IEmployeeDeletionValidationService employeeDeletionValidationService,
            ICurrentUserService currentUserService,
            IEmailService emailService,
            IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _context = context;
            _tenantContext = tenantContext;
            _planLimitService = planLimitService;
            _auditLogService = auditLogService;
            _userManager = userManager;
            _logger = logger;
            _employeeDeletionValidationService = employeeDeletionValidationService;
            _currentUserService = currentUserService;
            _emailService = emailService;
            _configuration = configuration;
        }

        /// <summary>
        /// Obtiene una lista de todos los empleados con su departamento y posición.
        /// 🔐 EMPLOYEE SELF-SERVICE: Si el usuario está vinculado a un empleado, solo ve su propia información.
        /// </summary>
        /// <returns>Una lista de empleados en formato DTO.</returns>
        [HttpGet]
        [RequirePermission(SystemPermission.EmployeesRead, SystemPermission.EmployeeViewSelf)]
        public async Task<IActionResult> GetAll()
        {
            var tenantId = _tenantContext.TenantId;

            var query = _context.Empleados
                .Where(e => e.TenantId == tenantId && !e.IsDeleted)
                .Include(e => e.Departamento)
                .Include(e => e.Posicion)
                .Include(e => e.User)
                .AsNoTracking();

            // 🎯 EMPLOYEE SELF-SERVICE: Si el usuario está vinculado a un empleado, solo mostrar SU registro
            var linkedEmployeeId = _currentUserService.GetLinkedEmployeeId();
            if (linkedEmployeeId.HasValue)
            {
                _logger.LogInformation("Usuario vinculado a empleado {EmpleadoId} - filtrando lista",
                    linkedEmployeeId.Value);
                query = query.Where(e => e.Id == linkedEmployeeId.Value);
            }

            var empleados = await query
                .Select(e => new
                {
                    Empleado = e,
                    RolSistema = e.UserId != null
                        ? _context.TenantUsers
                            .Where(tu => tu.UserId == e.UserId && tu.TenantId == tenantId)
                            .Select(tu => tu.Role.ToString())
                            .FirstOrDefault()
                        : null,
                    UsuarioVinculadoEmail = e.User != null ? e.User.Email : null
                })
                .ToListAsync();

            var empleadosDto = empleados.Select(e => 
            {
                // Recalcular HourlyRate si es 0 (empleados creados antes de la corrección)
                var hourlyRate = e.Empleado.HourlyRate;
                if (hourlyRate <= 0 && e.Empleado.SalarioBase > 0 && e.Empleado.HoursPerWeek > 0)
                {
                    hourlyRate = Domain.Entities.Empleado.ComputeHourlyRateFromMonthly(
                        e.Empleado.SalarioBase, 
                        e.Empleado.HoursPerWeek
                    );
                }
                
                return new EmpleadoVerDto(
                    e.Empleado.Id,
                    e.Empleado.Nombre,
                    e.Empleado.Apellido,
                    e.Empleado.NumeroIdentificacion,
                    e.Empleado.Email,
                    e.Empleado.SalarioBase,
                    e.Empleado.FechaContratacion,
                    e.Empleado.EstaActivo,
                    e.Empleado.DepartamentoId,
                    e.Empleado.Departamento?.Nombre,
                    e.Empleado.PosicionId,
                    e.Empleado.Posicion?.Nombre,
                    !string.IsNullOrEmpty(e.Empleado.UserId),
                    e.RolSistema,
                    e.Empleado.PayPeriodType.ToString(),
                    e.Empleado.HoursPerWeek,
                    e.Empleado.HoursPerPeriod,
                    hourlyRate,
                    e.Empleado.IsDeleted,
                    e.UsuarioVinculadoEmail,
                    e.Empleado.YearsCotized,
                    e.Empleado.AverageSalaryLast10Years,
                    e.Empleado.Dependents,
                    e.Empleado.CssRiskPercentage,
                    (int)e.Empleado.PayPeriodType,
                    e.Empleado.TipoContrato
                );
            }).ToList();

            return Ok(empleadosDto);
        }

        /// <summary>
        /// Obtiene un empleado específico por su ID con su departamento y posición.
        /// 🔐 EMPLOYEE SELF-SERVICE: Si el usuario está vinculado a un empleado, solo puede ver su propio perfil.
        /// </summary>
        /// <param name="id">El ID del empleado.</param>
        /// <returns>El empleado encontrado o un error 404 si no existe.</returns>
        [HttpGet("{id}")]
        [RequirePermission(SystemPermission.EmployeesRead, SystemPermission.EmployeeViewSelf)]
        public async Task<IActionResult> GetById(int id)
        {
            var tenantId = _tenantContext.TenantId;

            // 🎯 EMPLOYEE SELF-SERVICE: Verificar que solo pueda acceder a SU empleado
            var linkedEmployeeId = _currentUserService.GetLinkedEmployeeId();
            if (linkedEmployeeId.HasValue && linkedEmployeeId.Value != id)
            {
                _logger.LogWarning("Usuario vinculado a empleado {LinkedId} intentó acceder a empleado {RequestedId}",
                    linkedEmployeeId.Value, id);
                return Forbid(); // 403 Forbidden - no puede ver otros empleados
            }

            var result = await _context.Empleados
                .Where(e => e.Id == id && e.TenantId == tenantId && !e.IsDeleted)
                .Include(e => e.Departamento)
                .Include(e => e.Posicion)
                .Include(e => e.User)
                .AsNoTracking()
                .Select(e => new
                {
                    Empleado = e,
                    RolSistema = e.UserId != null
                        ? _context.TenantUsers
                            .Where(tu => tu.UserId == e.UserId && tu.TenantId == tenantId)
                            .Select(tu => tu.Role.ToString())
                            .FirstOrDefault()
                        : null,
                    UsuarioVinculadoEmail = e.User != null ? e.User.Email : null
                })
                .FirstOrDefaultAsync();

            if (result == null)
            {
                return NotFound(); // Retorna un 404 Not Found si no existe o no pertenece al tenant
            }

            // Recalcular HourlyRate si es 0 (empleados creados antes de la corrección)
            var hourlyRate = result.Empleado.HourlyRate;
            if (hourlyRate <= 0 && result.Empleado.SalarioBase > 0 && result.Empleado.HoursPerWeek > 0)
            {
                hourlyRate = Domain.Entities.Empleado.ComputeHourlyRateFromMonthly(
                    result.Empleado.SalarioBase, 
                    result.Empleado.HoursPerWeek
                );
            }

            var empleadoDto = new EmpleadoVerDto(
                result.Empleado.Id,
                result.Empleado.Nombre,
                result.Empleado.Apellido,
                result.Empleado.NumeroIdentificacion,
                result.Empleado.Email,
                result.Empleado.SalarioBase,
                result.Empleado.FechaContratacion,
                result.Empleado.EstaActivo,
                result.Empleado.DepartamentoId,
                result.Empleado.Departamento?.Nombre,
                result.Empleado.PosicionId,
                result.Empleado.Posicion?.Nombre,
                !string.IsNullOrEmpty(result.Empleado.UserId),
                result.RolSistema,
                result.Empleado.PayPeriodType.ToString(),
                result.Empleado.HoursPerWeek,
                result.Empleado.HoursPerPeriod,
                hourlyRate,
                result.Empleado.IsDeleted,
                result.UsuarioVinculadoEmail,
                result.Empleado.YearsCotized,
                result.Empleado.AverageSalaryLast10Years,
                result.Empleado.Dependents,
                result.Empleado.CssRiskPercentage,
                (int)result.Empleado.PayPeriodType,
                result.Empleado.TipoContrato
            );

            return Ok(empleadoDto);
        }

        /// <summary>
        /// Crea un nuevo empleado.
        /// </summary>
        /// <param name="empleadoDto">Los datos del nuevo empleado.</param>
        /// <returns>El nuevo empleado creado.</returns>
        [HttpPost]
        [RequirePermission(SystemPermission.EmployeesCreate)]
        [PlanLimits(PlanLimitType.CreateEmployee)] // ✅ PLAN LIMITS: Verifica automáticamente límite de empleados
        public async Task<IActionResult> Create(EmpleadoCrearDto empleadoDto)
        {
            var tenantId = _tenantContext.TenantId;
            var createdById = this.GetCurrentUserId();

            // Validar número de identificación único dentro del tenant
            // DEV-34: Filtrar explícitamente por TenantId para evitar fuga cross-tenant.
            // Sin este filtro, revelaría si la cédula existe en otro tenant.
            var numeroIdentificacion = (empleadoDto.NumeroIdentificacion ?? "").Trim();
            var yaExiste = await _context.Empleados
                .AnyAsync(e => e.TenantId == tenantId && e.NumeroIdentificacion == numeroIdentificacion && !e.IsDeleted);
            if (yaExiste)
            {
                return BadRequest(new { message = "Este número de identificación ya está registrado. Use otro número o verifique si el empleado ya existe en la lista." });
            }

            var empleado = _mapper.Map<Empleado>(empleadoDto);
            empleado.FechaContratacion = DateTime.UtcNow; // Lógica de negocio simple
            empleado.TenantId = tenantId; // ✅ SEGURIDAD: TenantId del token JWT

            // ✅ AUTO-VINCULACIÓN: Si tiene email, crear/vincular usuario
            if (!string.IsNullOrWhiteSpace(empleadoDto.Email))
            {
                try
                {
                    await CreateOrLinkUserAsync(empleado, empleadoDto.Email, tenantId, createdById);
                }
                catch (Exception ex)
                {
                    return BadRequest(new { message = $"Error vinculando usuario: {ex.Message}" });
                }
            }

            try
            {
                await _unitOfWork.Empleados.AddAsync(empleado);
                await _unitOfWork.CompleteAsync(); // Guarda los cambios en la BD
            }
            catch (DbUpdateException ex)
            {
                // Llave duplicada (PostgreSQL 23505) u otra violación de restricción → devolver JSON, no HTML
                var msg = ex.InnerException?.Message ?? ex.Message;
                if (msg.Contains("23505") || msg.Contains("NumeroIdentificacion", StringComparison.OrdinalIgnoreCase) || msg.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { message = "Este número de identificación ya está registrado. Use otro número o verifique si el empleado ya existe en la lista." });
                }
                _logger.LogError(ex, "Error al crear empleado");
                return StatusCode(500, new { message = "Error al guardar el empleado. Intente de nuevo." });
            }

            // Recargar con navegación y rol del sistema
            var result = await _context.Empleados
                .Where(e => e.Id == empleado.Id && e.TenantId == tenantId)
                .Include(e => e.Departamento)
                .Include(e => e.Posicion)
                .Include(e => e.User)
                .Select(e => new
                {
                    Empleado = e,
                    RolSistema = e.UserId != null
                        ? _context.TenantUsers
                            .Where(tu => tu.UserId == e.UserId && tu.TenantId == tenantId)
                            .Select(tu => tu.Role.ToString())
                            .FirstOrDefault()
                        : null,
                    UsuarioVinculadoEmail = e.User != null ? e.User.Email : null
                })
                .FirstOrDefaultAsync();

            empleado = result!.Empleado;

            // Recalcular HourlyRate si es 0 (por seguridad)
            var hourlyRate = empleado.HourlyRate;
            if (hourlyRate <= 0 && empleado.SalarioBase > 0 && empleado.HoursPerWeek > 0)
            {
                hourlyRate = Domain.Entities.Empleado.ComputeHourlyRateFromMonthly(
                    empleado.SalarioBase, 
                    empleado.HoursPerWeek
                );
            }

            var empleadoCreadoDto = new EmpleadoVerDto(
                empleado.Id,
                empleado.Nombre,
                empleado.Apellido,
                empleado.NumeroIdentificacion,
                empleado.Email,
                empleado.SalarioBase,
                empleado.FechaContratacion,
                empleado.EstaActivo,
                empleado.DepartamentoId,
                empleado.Departamento?.Nombre,
                empleado.PosicionId,
                empleado.Posicion?.Nombre,
                !string.IsNullOrEmpty(empleado.UserId),
                result.RolSistema,
                empleado.PayPeriodType.ToString(),
                empleado.HoursPerWeek,
                empleado.HoursPerPeriod,
                hourlyRate,
                false,
                result.UsuarioVinculadoEmail,
                empleado.YearsCotized,
                empleado.AverageSalaryLast10Years,
                empleado.Dependents,
                empleado.CssRiskPercentage,
                (int)empleado.PayPeriodType,
                empleado.TipoContrato
            );

            // ✅ AUDIT LOG: Registrar creación de empleado
            try
            {
                await _auditLogService.LogAsync(
                    "EmployeeCreated",
                    "Employee",
                    empleado!.Id.ToString(),
                    new Dictionary<string, string>
                    {
                        ["EmployeeName"] = $"{empleado.Nombre} {empleado.Apellido}",
                        ["IdentificationNumber"] = empleado.NumeroIdentificacion ?? "N/A",
                        ["DepartmentId"] = empleado.DepartamentoId?.ToString() ?? "N/A",
                        ["PositionId"] = empleado.PosicionId?.ToString() ?? "N/A",
                        ["BaseSalary"] = empleado.SalarioBase.ToString("N2")
                    });
            }
            catch (Exception)
            {
                // No bloqueamos la operación si falla el audit log
            }

            // Devuelve una respuesta 201 Created con la ubicación del nuevo recurso
            return CreatedAtAction(nameof(GetById), new { id = empleado!.Id }, empleadoCreadoDto);
        }

        /// <summary>
        /// Actualiza un empleado existente.
        /// 🔐 EMPLOYEE SELF-SERVICE: Si el usuario está vinculado a un empleado, solo puede actualizar su propio perfil.
        /// </summary>
        /// <param name="id">El ID del empleado a actualizar.</param>
        /// <param name="empleadoDto">Los datos actualizados del empleado.</param>
        /// <returns>NoContent si la actualización fue exitosa, NotFound si no existe.</returns>
        [HttpPut("{id}")]
        [RequirePermission(SystemPermission.EmployeesUpdate, SystemPermission.EmployeeUpdateSelf)]
        public async Task<IActionResult> Update(int id, EmpleadoActualizarDto empleadoDto)
        {
            var tenantId = _tenantContext.TenantId;

            // 🎯 EMPLOYEE SELF-SERVICE: Verificar que solo pueda actualizar SU empleado
            var linkedEmployeeId = _currentUserService.GetLinkedEmployeeId();
            if (linkedEmployeeId.HasValue && linkedEmployeeId.Value != id)
            {
                _logger.LogWarning("Usuario vinculado a empleado {LinkedId} intentó actualizar empleado {RequestedId}",
                    linkedEmployeeId.Value, id);
                return Forbid(); // 403 Forbidden - no puede editar otros empleados
            }

            var empleado = await _context.Empleados
                .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId);

            if (empleado == null)
            {
                return NotFound(); // Retorna un 404 Not Found si no existe o no pertenece al tenant
            }

            // Si tiene usuario vinculado, no permitir cambiar el email del empleado (es el del usuario)
            var emailOriginal = empleado.Email;

            // Mantiene NumeroIdentificacion y FechaContratacion originales - solo actualiza campos permitidos
            _mapper.Map(empleadoDto, empleado);

            if (!string.IsNullOrEmpty(empleado.UserId))
                empleado.Email = emailOriginal;

            _unitOfWork.Empleados.Update(empleado);
            await _unitOfWork.CompleteAsync();

            // ✅ AUDIT LOG: Registrar actualización de empleado
            try
            {
                await _auditLogService.LogAsync(
                    "EmployeeUpdated",
                    "Employee",
                    id.ToString(),
                    new Dictionary<string, string>
                    {
                        ["EmployeeName"] = $"{empleado.Nombre} {empleado.Apellido}",
                        ["DepartmentId"] = empleado.DepartamentoId?.ToString() ?? "N/A",
                        ["PositionId"] = empleado.PosicionId?.ToString() ?? "N/A"
                    });
            }
            catch (Exception)
            {
                // No bloqueamos la operación si falla el audit log
            }

            return NoContent(); // Retorna un 204 No Content para indicar éxito
        }

        /// <summary>
        /// Valida si un empleado puede ser eliminado. No elimina; devuelve bloqueadores y advertencias.
        /// El frontend usa este resultado para mostrar el modal y luego llama a force-delete si el usuario confirma.
        /// </summary>
        /// <param name="id">El ID del empleado a validar.</param>
        /// <returns>200 OK con DeletionResult (canDelete, requiresConfirmation, blockers, warnings).</returns>
        [HttpDelete("{id}")]
        [RequirePermission(SystemPermission.EmployeesDelete)]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _employeeDeletionValidationService.ValidateEmployeeDeletionAsync(id);
            return Ok(result);
        }

        /// <summary>
        /// Eliminación efectiva (soft delete): marca el empleado como IsDeleted. Desaparece de las listas por el filtro !IsDeleted.
        /// Solo debe llamarse después de que el usuario confirme en el modal (tras DELETE que devuelve validación).
        /// Hard delete no se usa por FKs Restrict (planilla, préstamos, deducciones, etc.).
        /// </summary>
        /// <param name="id">ID del empleado.</param>
        /// <param name="body">Debe incluir confirmed: true.</param>
        [HttpPost("{id}/force-delete")]
        [RequirePermission(SystemPermission.EmployeesDelete)]
        public async Task<IActionResult> ForceDelete(int id, [FromBody] ForceDeleteRequest? body)
        {
            if (body?.Confirmed != true)
            {
                return BadRequest(new { error = "Debe confirmar la eliminación enviando { \"confirmed\": true }" });
            }

            var tenantId = _tenantContext.TenantId;
            var currentUserId = this.GetCurrentUserId();
            var empleado = await _context.Empleados
                .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId);

            if (empleado == null)
                return NotFound(new { error = "Empleado no encontrado" });

            if (empleado.IsDeleted)
                return BadRequest(new { error = "El empleado ya está eliminado" });

            empleado.IsDeleted = true;
            empleado.DeletedAt = DateTime.UtcNow;
            empleado.DeletedBy = currentUserId;
            empleado.EstaActivo = false;
            _unitOfWork.Empleados.Update(empleado);
            await _unitOfWork.CompleteAsync();

            try
            {
                await _auditLogService.LogAsync(
                    "EmployeeDeleted",
                    "Employee",
                    id.ToString(),
                    new Dictionary<string, string>
                    {
                        ["EmployeeName"] = $"{empleado.Nombre} {empleado.Apellido}",
                        ["IdentificationNumber"] = empleado.NumeroIdentificacion ?? "N/A",
                        ["DeletedBy"] = currentUserId
                    });
            }
            catch (Exception) { /* no bloquear */ }

            _logger.LogInformation("Employee {EmpleadoId} soft-deleted by user {UserId} in tenant {TenantId}", id, currentUserId, tenantId);
            return NoContent();
        }

        /// <summary>
        /// Reactiva un empleado previamente eliminado (soft delete).
        /// </summary>
        [HttpPost("{id}/reactivate")]
        [RequirePermission(SystemPermission.EmployeesUpdate)]
        public async Task<IActionResult> Reactivate(int id)
        {
            var tenantId = _tenantContext.TenantId;
            var empleado = await _context.Empleados
                .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId);

            if (empleado == null)
                return NotFound(new { error = "Empleado no encontrado" });

            if (!empleado.IsDeleted)
                return BadRequest(new { error = "El empleado no está eliminado" });

            empleado.IsDeleted = false;
            empleado.DeletedAt = null;
            empleado.DeletedBy = null;
            empleado.EstaActivo = true;
            _unitOfWork.Empleados.Update(empleado);
            await _unitOfWork.CompleteAsync();

            try
            {
                await _auditLogService.LogAsync(
                    "EmployeeReactivated",
                    "Employee",
                    id.ToString(),
                    new Dictionary<string, string>
                    {
                        ["EmployeeName"] = $"{empleado.Nombre} {empleado.Apellido}",
                        ["IdentificationNumber"] = empleado.NumeroIdentificacion ?? "N/A"
                    });
            }
            catch (Exception) { /* no bloquear */ }

            _logger.LogInformation("Employee {EmpleadoId} reactivated in tenant {TenantId}", id, tenantId);
            return NoContent();
        }

        /// <summary>
        /// Crea o vincula un usuario al empleado según el email proporcionado.
        /// </summary>
        private async Task CreateOrLinkUserAsync(Empleado empleado, string email, int tenantId, string createdById)
        {
            var existingUser = await _userManager.FindByEmailAsync(email);

            if (existingUser != null)
            {
                // Usuario existe → solo vincular
                empleado.UserId = existingUser.Id;

                // Verificar que tenga acceso al tenant
                var tenantAccess = await _context.TenantUsers
                    .AnyAsync(tu => tu.UserId == existingUser.Id && tu.TenantId == tenantId);

                if (!tenantAccess)
                {
                    // Agregar acceso al tenant como Employee
                    _context.TenantUsers.Add(new TenantUser
                    {
                        TenantId = tenantId,
                        UserId = existingUser.Id,
                        Role = TenantRole.User,
                        IsActive = true,
                        JoinedAt = DateTime.UtcNow
                    });
                }
            }
            else
            {
                // Usuario no existe → crear + vincular
                var newUser = new AppUser
                {
                    UserName = email,
                    Email = email,
                    NombreCompleto = $"{empleado.Nombre} {empleado.Apellido}",
                    EmailConfirmed = true // Evitar bloqueos si en el futuro se activa RequireConfirmedAccount
                };

                // Generar password temporal
                var tempPassword = GenerateTemporaryPassword();
                var result = await _userManager.CreateAsync(newUser, tempPassword);

                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Error creando usuario: {errors}");
                }

                // Vincular empleado con usuario
                empleado.UserId = newUser.Id;

                // Crear acceso al tenant como Employee
                _context.TenantUsers.Add(new TenantUser
                {
                    TenantId = tenantId,
                    UserId = newUser.Id,
                    Role = TenantRole.User,
                    IsActive = true,
                    JoinedAt = DateTime.UtcNow
                });

                // Enviar email de bienvenida con contraseña temporal (no bloquear si falla)
                try
                {
                    var baseUrl = _configuration["App:BaseUrl"] ?? "https://localhost:5001";
                    var loginUrl = baseUrl.TrimEnd('/') + "/login";
                    var tenant = await _context.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId);
                    var tenantName = tenant?.Name ?? "la empresa";
                    await _emailService.SendEmployeeWelcomeAsync(
                        recipientEmail: email,
                        recipientName: $"{empleado.Nombre} {empleado.Apellido}",
                        tenantName: tenantName,
                        temporaryPassword: tempPassword,
                        loginUrl: loginUrl);
                    _logger.LogInformation("Employee welcome email sent to {Email}", email);
                }
                catch (Exception emailEx)
                {
                    _logger.LogWarning(emailEx, "Failed to send welcome email to {Email}. User was created successfully.", email);
                }
            }
        }

        /// <summary>
        /// POST /api/empleados/{empleadoId}/vincular-usuario - Vincula un empleado a un usuario existente
        /// Requiere: Owner o Admin
        /// </summary>
        [HttpPost("{empleadoId}/vincular-usuario")]
        [RequirePermission(SystemPermission.EmployeesUpdate)]
        public async Task<ActionResult> VincularUsuario(int empleadoId, [FromBody] VincularUsuarioDto dto)
        {
            var tenantId = _tenantContext.TenantId;

            var empleado = await _context.Empleados
                .FirstOrDefaultAsync(e => e.Id == empleadoId && e.TenantId == tenantId);

            if (empleado == null)
            {
                return NotFound(new { error = "Empleado no encontrado" });
            }

            // Verificar que el usuario pertenece al tenant
            var tenantUser = await _context.TenantUsers
                .Include(tu => tu.User)
                .FirstOrDefaultAsync(tu => tu.UserId == dto.UserId && tu.TenantId == tenantId && tu.IsActive);

            if (tenantUser == null)
            {
                return BadRequest(new { error = "El usuario no pertenece a este tenant o no está activo" });
            }

            // Solo permitir vincular a usuarios con rol User (no Owner): el vínculo es para "solo ver su información"
            if (tenantUser.Role != TenantRole.User)
            {
                return BadRequest(new { error = "Solo se puede vincular empleados a usuarios con rol 'Usuario'. Los usuarios con rol 'Propietario' no deben vincularse a un empleado." });
            }

            // Verificar que el usuario no esté vinculado a otro empleado
            var usuarioYaVinculado = await _context.Empleados
                .AnyAsync(e => e.UserId == dto.UserId && e.TenantId == tenantId && e.Id != empleadoId);

            if (usuarioYaVinculado)
            {
                return BadRequest(new { error = "El usuario ya está vinculado a otro empleado en este tenant" });
            }

            empleado.UserId = dto.UserId;

            await _unitOfWork.CompleteAsync();

            _logger.LogInformation(
                "User {UserId} linked employee {EmpleadoId} to user {TargetUserId} in tenant {TenantId}",
                _tenantContext.UserId, empleadoId, dto.UserId, tenantId);

            return Ok(new
            {
                success = true,
                message = "Usuario vinculado exitosamente",
                empleado = new
                {
                    empleado.Id,
                    empleado.Nombre,
                    empleado.Apellido,
                    usuarioVinculado = new
                    {
                        id = tenantUser.UserId,
                        email = tenantUser.User?.Email ?? "",
                        nombreCompleto = tenantUser.User?.NombreCompleto
                    }
                }
            });
        }

        /// <summary>
        /// POST /api/empleados/{empleadoId}/desvincular-usuario - Desvincula un empleado de su usuario
        /// Requiere: Owner o Admin
        /// </summary>
        [HttpPost("{empleadoId}/desvincular-usuario")]
        [RequirePermission(SystemPermission.EmployeesUpdate)]
        public async Task<ActionResult> DesvincularUsuario(int empleadoId)
        {
            var tenantId = _tenantContext.TenantId;

            var empleado = await _context.Empleados
                .FirstOrDefaultAsync(e => e.Id == empleadoId && e.TenantId == tenantId);

            if (empleado == null)
            {
                return NotFound(new { error = "Empleado no encontrado" });
            }

            if (empleado.UserId == null)
            {
                return BadRequest(new { error = "El empleado no tiene un usuario vinculado" });
            }

            var anteriorUserId = empleado.UserId;
            empleado.UserId = null;

            await _unitOfWork.CompleteAsync();

            _logger.LogInformation(
                "User {UserId} unlinked employee {EmpleadoId} from user {UnlinkedUserId} in tenant {TenantId}",
                _tenantContext.UserId, empleadoId, anteriorUserId, tenantId);

            return Ok(new
            {
                success = true,
                message = "Usuario desvinculado exitosamente"
            });
        }

        /// <summary>
        /// GET /api/empleados/{empleadoId}/sugerencias-usuarios - Obtiene usuarios disponibles para vincular
        /// Requiere: Owner o Admin
        /// Devuelve usuarios del tenant que no están vinculados a ningún empleado, con sugerencia si el email coincide
        /// </summary>
        [HttpGet("{empleadoId}/sugerencias-usuarios")]
        [RequirePermission(SystemPermission.EmployeesUpdate)]
        public async Task<ActionResult> GetSugerenciasUsuarios(int empleadoId)
        {
            try
            {
                var tenantId = _tenantContext.TenantId;

                // ✅ VALIDACIÓN: TenantId no puede ser 0 o null
                if (tenantId <= 0)
                {
                    _logger.LogWarning(
                        "GetSugerenciasUsuarios called with invalid TenantId: {TenantId}",
                        tenantId);
                    return BadRequest(new { error = "TenantId inválido o no encontrado" });
                }

                // Buscar empleado
                var empleado = await _context.Empleados
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Id == empleadoId && e.TenantId == tenantId);

                if (empleado == null)
                {
                    _logger.LogWarning(
                        "Employee {EmpleadoId} not found in tenant {TenantId}",
                        empleadoId, tenantId);
                    return NotFound(new { error = "Empleado no encontrado" });
                }

                // Obtener IDs de usuarios ya vinculados a empleados en este tenant
                var usuariosVinculados = await _context.Empleados
                    .Where(e => e.TenantId == tenantId && e.UserId != null)
                    .Select(e => e.UserId)
                    .Distinct()
                    .ToListAsync();

                _logger.LogDebug(
                    "Found {Count} linked users in tenant {TenantId}",
                    usuariosVinculados.Count, tenantId);

                // Obtener usuarios del tenant con rol User (no Owner): solo ellos pueden vincularse a un empleado para "ver solo su información"
                var tenantUsersData = await _context.TenantUsers
                    .Where(tu => tu.TenantId == tenantId && tu.IsActive && tu.Role == TenantRole.User)
                    .Where(tu => !usuariosVinculados.Contains(tu.UserId))
                    .Include(tu => tu.User)
                    .AsNoTracking()
                    .ToListAsync();

                _logger.LogDebug(
                    "Found {Count} available tenant users for tenant {TenantId}",
                    tenantUsersData.Count, tenantId);

                // ✅ VALIDACIÓN: Manejar casos donde User puede ser null
                var usuariosDisponibles = tenantUsersData
                    .Where(tu => tu.User != null) // Filtrar usuarios nulos
                    .Select(tu => new
                    {
                        userId = tu.UserId,
                        email = tu.User!.Email ?? "Sin email",
                        nombreCompleto = tu.User.NombreCompleto ?? "Sin nombre",
                        telefono = tu.User.Telefono,
                        rol = tu.Role.ToString(),
                        // Sugerir si el email del empleado coincide con el email del usuario
                        esSugerencia = !string.IsNullOrWhiteSpace(empleado.Email) &&
                                      !string.IsNullOrWhiteSpace(tu.User.Email) &&
                                      tu.User.Email.Equals(empleado.Email, StringComparison.OrdinalIgnoreCase)
                    })
                    .OrderByDescending(u => u.esSugerencia)  // Sugerencias primero
                    .ThenBy(u => u.nombreCompleto)
                    .ToList();

                _logger.LogInformation(
                    "User {UserId} retrieved {Count} user suggestions for employee {EmpleadoId} in tenant {TenantId}",
                    _tenantContext.UserId, usuariosDisponibles.Count, empleadoId, tenantId);

                return Ok(usuariosDisponibles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error getting user suggestions for employee {EmpleadoId} in tenant {TenantId}",
                    empleadoId, _tenantContext.TenantId);

                // ✅ SIEMPRE devolver JSON, incluso en errores
                return StatusCode(500, new
                {
                    error = "Error interno al obtener sugerencias de usuarios",
                    message = ex.Message,
                    type = ex.GetType().Name
                });
            }
        }

        /// <summary>
        /// Genera un password temporal seguro.
        /// </summary>
        private string GenerateTemporaryPassword()
        {
            return $"Temp{Guid.NewGuid().ToString("N")[..8]}!1Aa";
        }

        /// <summary>
        /// Obtiene el saldo inicial de un empleado (para empresas que migraron al sistema a mitad de año).
        /// GET /api/empleados/{id}/saldo-inicial
        /// </summary>
        [HttpGet("{id}/saldo-inicial")]
        [RequirePermission(SystemPermission.EmployeesRead)]
        public async Task<ActionResult<SaldoInicialDto>> GetSaldoInicial(int id)
        {
            var tenantId = _tenantContext.TenantId;

            var empleado = await _context.Empleados
                .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId && !e.IsDeleted);
            if (empleado == null)
                return NotFound(new { message = $"Empleado {id} no encontrado" });

            var saldo = await _context.SaldosInicialesEmpleados
                .FirstOrDefaultAsync(s => s.EmpleadoId == id && s.TenantId == tenantId);

            if (saldo == null)
                return Ok(new SaldoInicialDto(id, null, 0, 0, 0, null));

            return Ok(new SaldoInicialDto(
                EmpleadoId: saldo.EmpleadoId,
                FechaCorte: saldo.FechaCorte,
                DiasVacacionesAcumulados: saldo.DiasVacacionesAcumulados,
                DiasVacacionesTomados: saldo.DiasVacacionesTomados,
                MontoDecimoAcumulado: saldo.MontoDecimoAcumulado,
                Notas: saldo.Notas
            ));
        }

        /// <summary>
        /// Crea o actualiza el saldo inicial de un empleado (upsert).
        /// POST /api/empleados/{id}/saldo-inicial
        /// </summary>
        [HttpPost("{id}/saldo-inicial")]
        [RequirePermission(SystemPermission.EmployeesUpdate)]
        public async Task<ActionResult<SaldoInicialDto>> UpsertSaldoInicial(
            int id, [FromBody] UpsertSaldoInicialRequest request)
        {
            var tenantId = _tenantContext.TenantId;

            var empleado = await _context.Empleados
                .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId && !e.IsDeleted);
            if (empleado == null)
                return NotFound(new { message = $"Empleado {id} no encontrado" });

            var existing = await _context.SaldosInicialesEmpleados
                .FirstOrDefaultAsync(s => s.EmpleadoId == id && s.TenantId == tenantId);

            if (existing != null)
            {
                existing.FechaCorte = request.FechaCorte;
                existing.DiasVacacionesAcumulados = request.DiasVacacionesAcumulados;
                existing.DiasVacacionesTomados = request.DiasVacacionesTomados;
                existing.MontoDecimoAcumulado = request.MontoDecimoAcumulado;
                existing.Notas = request.Notas;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.SaldosInicialesEmpleados.Add(new SaldoInicialEmpleado
                {
                    EmpleadoId = id,
                    TenantId = tenantId,
                    FechaCorte = request.FechaCorte,
                    DiasVacacionesAcumulados = request.DiasVacacionesAcumulados,
                    DiasVacacionesTomados = request.DiasVacacionesTomados,
                    MontoDecimoAcumulado = request.MontoDecimoAcumulado,
                    Notas = request.Notas
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new SaldoInicialDto(id, request.FechaCorte, request.DiasVacacionesAcumulados,
                request.DiasVacacionesTomados, request.MontoDecimoAcumulado, request.Notas));
        }
    }

    /// <summary>
    /// Cuerpo para confirmar eliminación forzada de un empleado.
    /// </summary>
    public class ForceDeleteRequest
    {
        public bool Confirmed { get; set; }
    }

    public record SaldoInicialDto(
        int EmpleadoId,
        DateTime? FechaCorte,
        decimal DiasVacacionesAcumulados,
        decimal DiasVacacionesTomados,
        decimal MontoDecimoAcumulado,
        string? Notas
    );

    public record UpsertSaldoInicialRequest(
        DateTime FechaCorte,
        decimal DiasVacacionesAcumulados = 0,
        decimal DiasVacacionesTomados = 0,
        decimal MontoDecimoAcumulado = 0,
        string? Notas = null
    );
}
