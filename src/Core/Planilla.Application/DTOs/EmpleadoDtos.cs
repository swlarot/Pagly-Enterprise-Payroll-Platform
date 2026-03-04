using System.ComponentModel.DataAnnotations;
using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.DTOs
{
    /// <summary>
    /// DTO para visualizar los datos de un empleado. Contiene los campos seguros para mostrar al exterior.
    /// </summary>
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
        // === Pay Info ===
        string PayPeriodTypeName = "Quincenal",
        int HoursPerWeek = 48,
        decimal HoursPerPeriod = 104,
        decimal HourlyRate = 0,
        // === Campos existentes con default ===
        bool IsDeleted = false,
        string? UsuarioVinculadoEmail = null,
        // === CSS e ISR ===
        int YearsCotized = 0,
        decimal AverageSalaryLast10Years = 0,
        int Dependents = 0,
        decimal CssRiskPercentage = 0.56m,
        // === Pay Period numeric value para edición ===
        int PayPeriodTypeValue = 2
    );

    /// <summary>
    /// DTO utilizado para crear un nuevo empleado. Solo incluye los campos que el cliente puede proporcionar.
    /// </summary>
    public record EmpleadoCrearDto(
        [Required]
        [StringLength(100)]
        string Nombre,

        [Required]
        [StringLength(100)]
        string Apellido,

        [Required]
        [StringLength(20)]
        string NumeroIdentificacion,

        [EmailAddress]
        [StringLength(256)]
        string? Email,

        [Range(0.01, double.MaxValue)]
        decimal SalarioBase,

        int? DepartamentoId,

        int? PosicionId,

        // === Pay Info ===
        PayPeriodType PayPeriodType = PayPeriodType.Quincenal,
        int HoursPerWeek = 48,
        decimal? HoursPerPeriod = null,

        // === CSS e ISR ===
        [Range(0, 100)]
        int YearsCotized = 0,

        [Range(0, double.MaxValue)]
        decimal AverageSalaryLast10Years = 0,

        [Range(0, 3)]
        int Dependents = 0,

        [Range(0, 100)]
        decimal CssRiskPercentage = 0.56m
    );

    /// <summary>
    /// DTO utilizado para actualizar un empleado existente.
    /// </summary>
    public record EmpleadoActualizarDto(
        [Required]
        [StringLength(100)]
        string Nombre,

        [Required]
        [StringLength(100)]
        string Apellido,

        [EmailAddress]
        [StringLength(256)]
        string? Email,

        [Range(0.01, double.MaxValue)]
        decimal SalarioBase,

        bool EstaActivo,

        int? DepartamentoId,

        int? PosicionId,

        // === Pay Info ===
        PayPeriodType PayPeriodType = PayPeriodType.Quincenal,
        int HoursPerWeek = 48,
        decimal? HoursPerPeriod = null,

        // === CSS e ISR ===
        [Range(0, 100)]
        int YearsCotized = 0,

        [Range(0, double.MaxValue)]
        decimal AverageSalaryLast10Years = 0,

        [Range(0, 3)]
        int Dependents = 0,

        [Range(0, 100)]
        decimal CssRiskPercentage = 0.56m,

        // === Fecha de Contratación (editable) ===
        DateTime? FechaContratacion = null
    );

    /// <summary>
    /// DTO para vincular un empleado a un usuario existente.
    /// </summary>
    public record VincularUsuarioDto(
        [Required(ErrorMessage = "El ID del usuario es requerido")]
        string UserId
    );
}
