// ====================================================================
// Planilla - Importación de empleados (onboarding)
// Contrato entre la plantilla de Excel, la validación, la pantalla de
// revisión y la confirmación. La misma fila viaja de ida (validar) y de
// vuelta (confirmar, ya corregida en pantalla).
// ====================================================================

namespace Vorluno.Planilla.Application.DTOs.Importacion;

/// <summary>Un mes de salario tal como viene en la hoja "Salarios".</summary>
public sealed class MesImportadoDto
{
    public int Anio { get; set; }
    public int Mes { get; set; }

    /// <summary>Devengado del mes. Nulo = celda vacía (sin dato), distinto de cero.</summary>
    public decimal? Monto { get; set; }
}

/// <summary>Saldos del año en curso para la ficha de renta (hoja "Saldos renta").</summary>
public sealed class SaldosRentaImportadosDto
{
    public int Anio { get; set; }
    public decimal? IsrRetenido { get; set; }
    public decimal? DecimoPagado { get; set; }
    public int? PartidasDecimo { get; set; }
    public decimal? GastoRepresentacionPagado { get; set; }
    public decimal? IsrGastoRepresentacion { get; set; }
}

/// <summary>Una fila de la hoja "Empleados", con sus meses y saldos ya unidos por cédula.</summary>
public sealed class FilaImportacionDto
{
    /// <summary>Número de fila en la hoja Empleados (para señalar en pantalla).</summary>
    public int Fila { get; set; }

    public string Cedula { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string? Email { get; set; }
    public decimal? SalarioBase { get; set; }
    public DateTime? FechaContratacion { get; set; }

    /// <summary>Semanal, Bisemanal, Quincenal o Mensual, como texto de la lista.</summary>
    public string? TipoPeriodo { get; set; }

    /// <summary>Indefinido, Definido o PorObra.</summary>
    public string? TipoContrato { get; set; }

    public string? Departamento { get; set; }
    public string? Posicion { get; set; }
    public int? Dependientes { get; set; }

    /// <summary>Prima de riesgo profesional, una de las cinco clases (0.56, 0.98, 2.10, 3.64, 5.67).</summary>
    public decimal? RiesgoProfesional { get; set; }

    public decimal? GastoRepresentacionMensual { get; set; }
    public bool? SujetoCss { get; set; }
    public bool? SujetoSe { get; set; }
    public bool? SujetoIsr { get; set; }

    public List<MesImportadoDto> Meses { get; set; } = new();
    public SaldosRentaImportadosDto? Saldos { get; set; }

    /// <summary>true si la cédula ya existe en la empresa: se actualizará en vez de crearse.</summary>
    public bool YaExiste { get; set; }
}

public enum TipoProblema
{
    /// <summary>Impide importar esa fila hasta corregirlo.</summary>
    Error = 0,

    /// <summary>Se puede importar, pero conviene revisarlo.</summary>
    Aviso = 1
}

public sealed class ProblemaImportacionDto
{
    /// <summary>Nombre del campo o "meses" / "saldos".</summary>
    public string Campo { get; set; } = string.Empty;
    public TipoProblema Tipo { get; set; }
    public string Mensaje { get; set; } = string.Empty;
}

public sealed class FilaValidadaDto
{
    public FilaImportacionDto Datos { get; set; } = new();
    public List<ProblemaImportacionDto> Problemas { get; set; } = new();

    public bool TieneErrores => Problemas.Any(p => p.Tipo == TipoProblema.Error);
    public bool TieneAvisos => Problemas.Any(p => p.Tipo == TipoProblema.Aviso);
}

public sealed class ResultadoValidacionDto
{
    public List<FilaValidadaDto> Filas { get; set; } = new();

    /// <summary>Problemas del archivo en sí (falta una hoja, encabezados distintos…).</summary>
    public List<string> ProblemasDelArchivo { get; set; } = new();

    /// <summary>Los meses que trae la hoja Salarios, en orden, para pintar la cuadrícula.</summary>
    public List<string> MesesDelArchivo { get; set; } = new();

    public int Listos => Filas.Count(f => !f.TieneErrores && !f.TieneAvisos);
    public int ConAvisos => Filas.Count(f => !f.TieneErrores && f.TieneAvisos);
    public int ConErrores => Filas.Count(f => f.TieneErrores);
}

public sealed class ConfirmarImportacionRequest
{
    /// <summary>Las filas tal como quedaron después de corregirlas en pantalla.</summary>
    public List<FilaImportacionDto> Filas { get; set; } = new();

    /// <summary>Cédulas que el usuario decidió omitir.</summary>
    public List<string> Omitidas { get; set; } = new();

    public string? NombreArchivo { get; set; }
}

public sealed class ResumenImportacionDto
{
    public int Creados { get; set; }
    public int Actualizados { get; set; }
    public int Omitidos { get; set; }
    public int MesesGuardados { get; set; }
    public int SaldosGuardados { get; set; }
    public List<EmpleadoImportadoDto> Empleados { get; set; } = new();
}

public sealed class EmpleadoImportadoDto
{
    public int Id { get; set; }
    public string Cedula { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public bool Creado { get; set; }
}
