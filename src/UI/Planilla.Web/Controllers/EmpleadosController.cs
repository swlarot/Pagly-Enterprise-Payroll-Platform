using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Infrastructure.Data;

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

        /// <summary>
        /// Inicializa una nueva instancia de la clase <see cref="EmpleadosController"/>.
        /// </summary>
        /// <param name="unitOfWork">La unidad de trabajo para el acceso a datos.</param>
        /// <param name="mapper">El servicio de mapeo de objetos (AutoMapper).</param>
        /// <param name="context">El contexto de la base de datos para consultas complejas.</param>
        /// <param name="tenantContext">El contexto del tenant actual.</param>
        /// <param name="planLimitService">Servicio de verificación de límites del plan.</param>
        public EmpleadosController(IUnitOfWork unitOfWork, IMapper mapper, ApplicationDbContext context, ITenantContext tenantContext, IPlanLimitService planLimitService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _context = context;
            _tenantContext = tenantContext;
            _planLimitService = planLimitService;
        }

        /// <summary>
        /// Obtiene una lista de todos los empleados con su departamento y posición.
        /// </summary>
        /// <returns>Una lista de empleados en formato DTO.</returns>
        [HttpGet]
        [Authorize(Roles = "Owner,Admin,Manager,Accountant")]
        public async Task<IActionResult> GetAll()
        {
            var tenantId = _tenantContext.TenantId;
            var empleados = await _context.Empleados
                .Where(e => e.TenantId == tenantId)
                .Include(e => e.Departamento)
                .Include(e => e.Posicion)
                .AsNoTracking()
                .ToListAsync();
            var empleadosDto = _mapper.Map<IEnumerable<EmpleadoVerDto>>(empleados);
            return Ok(empleadosDto);
        }

        /// <summary>
        /// Obtiene un empleado específico por su ID con su departamento y posición.
        /// </summary>
        /// <param name="id">El ID del empleado.</param>
        /// <returns>El empleado encontrado o un error 404 si no existe.</returns>
        [HttpGet("{id}")]
        [Authorize(Roles = "Owner,Admin,Manager,Accountant")]
        public async Task<IActionResult> GetById(int id)
        {
            var tenantId = _tenantContext.TenantId;
            var empleado = await _context.Empleados
                .Where(e => e.Id == id && e.TenantId == tenantId)
                .Include(e => e.Departamento)
                .Include(e => e.Posicion)
                .AsNoTracking()
                .FirstOrDefaultAsync();
            if (empleado == null)
            {
                return NotFound(); // Retorna un 404 Not Found si no existe o no pertenece al tenant
            }
            var empleadoDto = _mapper.Map<EmpleadoVerDto>(empleado);
            return Ok(empleadoDto);
        }

        /// <summary>
        /// Crea un nuevo empleado.
        /// </summary>
        /// <param name="empleadoDto">Los datos del nuevo empleado.</param>
        /// <returns>El nuevo empleado creado.</returns>
        [HttpPost]
        [Authorize(Roles = "Owner,Admin,Manager")]
        public async Task<IActionResult> Create(EmpleadoCrearDto empleadoDto)
        {
            var tenantId = _tenantContext.TenantId;

            // ✅ FEATURE GATING: Verificar límite de empleados del plan
            var (allowed, reason) = await _planLimitService.CanCreateEmployeeAsync(tenantId);
            if (!allowed)
            {
                return StatusCode(403, new { error = reason });
            }

            var empleado = _mapper.Map<Empleado>(empleadoDto);
            empleado.FechaContratacion = DateTime.UtcNow; // Lógica de negocio simple
            empleado.TenantId = tenantId; // ✅ SEGURIDAD: TenantId del token JWT

            await _unitOfWork.Empleados.AddAsync(empleado);
            await _unitOfWork.CompleteAsync(); // Guarda los cambios en la BD

            // Recargar con navegación para el DTO
            empleado = await _context.Empleados
                .Where(e => e.Id == empleado.Id && e.TenantId == tenantId)
                .Include(e => e.Departamento)
                .Include(e => e.Posicion)
                .FirstOrDefaultAsync();

            var empleadoCreadoDto = _mapper.Map<EmpleadoVerDto>(empleado);

            // Devuelve una respuesta 201 Created con la ubicación del nuevo recurso
            return CreatedAtAction(nameof(GetById), new { id = empleado!.Id }, empleadoCreadoDto);
        }

        /// <summary>
        /// Actualiza un empleado existente.
        /// </summary>
        /// <param name="id">El ID del empleado a actualizar.</param>
        /// <param name="empleadoDto">Los datos actualizados del empleado.</param>
        /// <returns>NoContent si la actualización fue exitosa, NotFound si no existe.</returns>
        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,Admin,Manager")]
        public async Task<IActionResult> Update(int id, EmpleadoActualizarDto empleadoDto)
        {
            var tenantId = _tenantContext.TenantId;
            var empleado = await _context.Empleados
                .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId);

            if (empleado == null)
            {
                return NotFound(); // Retorna un 404 Not Found si no existe o no pertenece al tenant
            }

            // Mantiene NumeroIdentificacion y FechaContratacion originales - solo actualiza campos permitidos
            _mapper.Map(empleadoDto, empleado);

            _unitOfWork.Empleados.Update(empleado);
            await _unitOfWork.CompleteAsync();

            return NoContent(); // Retorna un 204 No Content para indicar éxito
        }

        /// <summary>
        /// Elimina un empleado (soft delete usando EstaActivo = false).
        /// </summary>
        /// <param name="id">El ID del empleado a eliminar.</param>
        /// <returns>NoContent si la eliminación fue exitosa, NotFound si no existe.</returns>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner,Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var tenantId = _tenantContext.TenantId;
            var empleado = await _context.Empleados
                .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId);

            if (empleado == null)
            {
                return NotFound(); // Retorna un 404 Not Found si no existe o no pertenece al tenant
            }

            // Implementa soft delete usando EstaActivo = false; no hard delete de la base de datos
            empleado.EstaActivo = false;
            _unitOfWork.Empleados.Update(empleado);
            await _unitOfWork.CompleteAsync();

            return NoContent(); // Retorna un 204 No Content para indicar éxito
        }
    }
}
