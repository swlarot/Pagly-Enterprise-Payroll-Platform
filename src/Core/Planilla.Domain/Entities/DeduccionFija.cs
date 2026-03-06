using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Domain.Interfaces;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Representa una deducción fija que se aplica automáticamente al empleado en cada planilla.
/// </summary>
public class DeduccionFija : ITenantEntity
{
    public int Id { get; set; }

    /// <summary>
    /// ID del empleado al que se aplica la deducción
    /// </summary>
    public int EmpleadoId { get; set; }
    public Empleado Empleado { get; set; } = null!;

    /// <summary>
    /// ID del tenant al que pertenece esta deducción
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// Tipo de deducción
    /// </summary>
    public TipoDeduccion TipoDeduccion { get; set; }

    /// <summary>
    /// Descripción de la deducción (ej: "Pensión alimenticia - Exp. 123-2024")
    /// </summary>
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>
    /// Monto mensual fijo (si no es porcentaje). Se prorrata al período al procesar la planilla.
    /// </summary>
    public decimal Monto { get; set; }

    /// <summary>
    /// Porcentaje sobre salario bruto (alternativa a monto fijo)
    /// </summary>
    public decimal? Porcentaje { get; set; }

    /// <summary>
    /// Indica si la deducción se calcula como porcentaje del salario
    /// </summary>
    public bool EsPorcentaje { get; set; }

    /// <summary>
    /// Fecha de inicio de la deducción
    /// </summary>
    public DateTime FechaInicio { get; set; }

    /// <summary>
    /// Fecha de fin de la deducción (null = indefinida)
    /// </summary>
    public DateTime? FechaFin { get; set; }

    /// <summary>
    /// Indica si la deducción está activa
    /// </summary>
    public bool EstaActivo { get; set; }

    /// <summary>
    /// Número de referencia (expediente, contrato, etc.)
    /// </summary>
    public string? Referencia { get; set; }

    /// <summary>
    /// Prioridad de aplicación (menor número = mayor prioridad)
    /// Útil cuando hay múltiples deducciones
    /// </summary>
    public int Prioridad { get; set; }

    /// <summary>
    /// Observaciones adicionales
    /// </summary>
    public string? Observaciones { get; set; }

    // ====================================================================
    // Referencia al Catálogo de Acreedores
    // ====================================================================

    /// <summary>FK al catálogo de acreedores (nullable para backward compatibility)</summary>
    public int? AcreedorId { get; set; }
    public virtual Acreedor? Acreedor { get; set; }

    // ====================================================================
    // Información del Acreedor (campos embebidos - auto-llenados desde catálogo)
    // ====================================================================

    /// <summary>Nombre del beneficiario/acreedor</summary>
    public string? NombreAcreedor { get; set; }

    /// <summary>RUC/Cédula del acreedor</summary>
    public string? IdentificacionAcreedor { get; set; }

    /// <summary>Cuenta bancaria para transferencia al acreedor</summary>
    public string? CuentaBancariaAcreedor { get; set; }

    /// <summary>Banco destino del acreedor</summary>
    public string? BancoAcreedor { get; set; }

    // ====================================================================
    // Orden Judicial (para PensionAlimenticia y Embargo)
    // ====================================================================

    /// <summary>Número de expediente judicial</summary>
    public string? NumeroExpediente { get; set; }

    /// <summary>Juzgado emisor de la orden</summary>
    public string? Juzgado { get; set; }

    /// <summary>Fecha de la orden judicial</summary>
    public DateTime? FechaOrdenJudicial { get; set; }

    /// <summary>Juez firmante de la orden</summary>
    public string? NombreJuez { get; set; }

    /// <summary>Estado de la orden judicial: Vigente/Suspendida/Levantada</summary>
    public EstadoOrdenJudicial? EstadoOrdenJudicial { get; set; }

    /// <summary>Fecha en que se levantó la orden</summary>
    public DateTime? FechaLevantamiento { get; set; }

    /// <summary>Razón del levantamiento (cumplimiento total, orden judicial, etc.)</summary>
    public string? MotivoLevantamiento { get; set; }

    // ====================================================================
    // Control de Cálculo
    // ====================================================================

    /// <summary>Base de cálculo: sobre bruto o sobre neto post-legales</summary>
    public BaseCalculoDeduccion BaseCalculo { get; set; } = BaseCalculoDeduccion.SalarioBruto;

    /// <summary>Categoría de prelación (se infiere del TipoDeduccion si no se establece)</summary>
    public CategoriaDeduccion Categoria { get; set; } = CategoriaDeduccion.Voluntaria;

    /// <summary>Para embargos con monto total definido por el juzgado</summary>
    public decimal? MontoTotalACobrar { get; set; }

    /// <summary>Acumulado ya cobrado hacia MontoTotalACobrar</summary>
    public decimal MontoCobradoAcumulado { get; set; }

    // ====================================================================
    // Autorización del Trabajador (para voluntarias)
    // ====================================================================

    /// <summary>Indica si tiene autorización escrita del trabajador</summary>
    public bool TieneAutorizacionEscrita { get; set; }

    /// <summary>Fecha en que el trabajador firmó la autorización</summary>
    public DateTime? FechaAutorizacion { get; set; }

    /// <summary>Referencia al documento de autorización</summary>
    public string? DocumentoAutorizacionRef { get; set; }

    // ====================================================================
    // Aplicación Especial
    // ====================================================================

    /// <summary>Si aplica al décimo tercer mes (por orden judicial)</summary>
    public bool AplicaADecimoTercerMes { get; set; }

    /// <summary>Si aplica a prestaciones laborales (por orden judicial)</summary>
    public bool AplicaAPrestaciones { get; set; }

    // Auditoría
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation property
    public virtual Tenant? Tenant { get; set; }
}
