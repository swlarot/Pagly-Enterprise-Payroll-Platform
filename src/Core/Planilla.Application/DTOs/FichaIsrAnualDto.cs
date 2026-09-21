// ====================================================================
// Planilla - Ficha anual de ISR
//
// Es la hoja que el contador lleva en Excel, reproducida columna por columna
// y con sus mismos nombres: una hoja por empleado, 24 filas fijas (una por
// quincena) agrupadas por mes, y una fila de totales. Las columnas de cálculo
// siguen sus fórmulas literales:
//
//   ACUMULADO          = SUM(SALARIOS..XIII MEX) + ACUMULADO anterior
//   INGRESO GRAVABLE   = ACUMULADO / PERIODOS x 26
//   RENTA ANUAL        = tarifa Art. 700 sobre INGRESO GRAVABLE
//   RENTA POR PERIODO  = RENTA ANUAL / 26
//   IMPUESTO CAUSADO   = RENTA POR PERIODO x PERIODOS
//   IMPUESTO A PAGAR   = IMPUESTO CAUSADO
//   RENTA ACUMULADA    = IMPUESTO A PAGAR
//
// La base es el BRUTO: en la hoja no se resta el Seguro Social en ninguna celda.
// ====================================================================

namespace Vorluno.Planilla.Application.DTOs;

/// <summary>Una fila de la hoja: una quincena (o el período que toque según la frecuencia).</summary>
public class FilaFichaIsrDto
{
    /// <summary>Columna MESES. Solo va en la primera fila de cada mes, como en la hoja.</summary>
    public string Mes { get; set; } = string.Empty;

    /// <summary>Columna QUINCENAS: 1…24.</summary>
    public int Quincena { get; set; }

    /// <summary>Columna PERIODOS: la quincena más 0.667 por cada partida de décimo pagada.</summary>
    public decimal Periodos { get; set; }

    public decimal Salarios { get; set; }
    public decimal Vacaciones { get; set; }
    public decimal Extras { get; set; }
    public decimal Comision { get; set; }

    /// <summary>Columna XIII MEX: la partida de décimo, en la fila de la quincena en que se paga.</summary>
    public decimal XiiiMes { get; set; }

    public decimal Acumulado { get; set; }
    public decimal IngresoGravable { get; set; }
    public decimal RentaAnual { get; set; }
    public decimal RentaPorPeriodo { get; set; }
    public decimal ImpuestoCausado { get; set; }
    public decimal ImpuestoAPagar { get; set; }
    public decimal RentaAcumulada { get; set; }

    /// <summary>true en los meses con partida de décimo; la hoja los resalta.</summary>
    public bool EsMesDecimo { get; set; }

    /// <summary>true si en esa quincena hay una planilla guardada (para distinguir 0 de "sin datos").</summary>
    public bool TieneDatos { get; set; }

    /// <summary>true si la fila viene de un mes importado o escrito a mano, no de una planilla de Pagly.</summary>
    public bool EsImportado { get; set; }
}

/// <summary>Ficha anual de ISR de un empleado: la hoja del contador.</summary>
public class FichaIsrAnualDto
{
    // ── Cabecera de la hoja ──
    public int EmpleadoId { get; set; }

    /// <summary>Celda "Empleado".</summary>
    public string Empleado { get; set; } = string.Empty;

    public string? Cedula { get; set; }
    public int Anio { get; set; }

    /// <summary>Celda "Salario Base": el salario mensual.</summary>
    public decimal SalarioBase { get; set; }

    /// <summary>Celda "Conyuge es dependiente": SI / NO.</summary>
    public string ConyugeDependiente { get; set; } = "NO";

    /// <summary>Celda "Periodos de Pagos": 26 en quincenal, 13 en mensual.</summary>
    public decimal PeriodosDePago { get; set; }

    /// <summary>La palabra que va al lado: "Quincenas", "Meses", "Semanas", "Bisemanas".</summary>
    public string NombrePeriodo { get; set; } = "Quincenas";

    // ── Cuerpo ──
    public List<FilaFichaIsrDto> Filas { get; set; } = new();

    // ── Fila de totales ──
    public decimal TotalSalarios { get; set; }
    public decimal TotalVacaciones { get; set; }
    public decimal TotalExtras { get; set; }
    public decimal TotalComision { get; set; }
    public decimal TotalXiiiMes { get; set; }
}
