namespace Vorluno.Planilla.Domain.Enums;

/// <summary>Qué se pide revisar.</summary>
public enum TipoRevision
{
    Planilla = 1,
    Decimo = 2,
    Liquidacion = 3,
    /// <summary>Las horas extra de un mes (EntidadId = AAAAMM).</summary>
    HorasExtra = 4,
}

/// <summary>En qué quedó un pedido de revisión.</summary>
public enum EstadoRevision
{
    Pendiente = 0,
    /// <summary>El revisor dio el visto bueno.</summary>
    Aprobada = 1,
    /// <summary>El revisor la devolvió con un comentario para corregir.</summary>
    Devuelta = 2,
    /// <summary>Quien la pidió la retiró.</summary>
    Cancelada = 3,
}
