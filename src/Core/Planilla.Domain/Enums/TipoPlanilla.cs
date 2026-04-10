namespace Vorluno.Planilla.Domain.Enums;

/// <summary>
/// Tipo de planilla segun las deducciones que aplica.
/// </summary>
public enum TipoPlanilla
{
    /// <summary>Planilla regular con todas las deducciones legales (CSS, SE, ISR).</summary>
    Regular = 0,

    /// <summary>Planilla sin deducciones legales. Solo calcula salario bruto y deducciones voluntarias/judiciales.</summary>
    SinDeducciones = 1
}
