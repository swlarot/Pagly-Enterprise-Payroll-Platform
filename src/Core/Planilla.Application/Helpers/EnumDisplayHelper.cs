using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.Helpers;

/// <summary>
/// Métodos centralizados de conversión enum → nombre legible.
/// </summary>
public static class EnumDisplayHelper
{
    public static string ToNombre(this TipoAcreedor tipo) => tipo switch
    {
        TipoAcreedor.PersonaNatural => "Persona Natural",
        TipoAcreedor.Banco => "Banco",
        TipoAcreedor.Cooperativa => "Cooperativa",
        TipoAcreedor.Sindicato => "Sindicato",
        TipoAcreedor.Aseguradora => "Aseguradora",
        TipoAcreedor.EntidadGubernamental => "Entidad Gubernamental",
        TipoAcreedor.Otro => "Otro",
        _ => "Desconocido"
    };

    public static string ToNombre(this EstadoAnticipo estado) => estado switch
    {
        EstadoAnticipo.Pendiente => "Pendiente",
        EstadoAnticipo.Aprobado => "Aprobado",
        EstadoAnticipo.Descontado => "Descontado",
        EstadoAnticipo.Rechazado => "Rechazado",
        EstadoAnticipo.Cancelado => "Cancelado",
        _ => estado.ToString()
    };

    public static string ToNombre(this EstadoPrestamo estado) => estado switch
    {
        EstadoPrestamo.Activo => "Activo",
        EstadoPrestamo.Pagado => "Pagado",
        EstadoPrestamo.Cancelado => "Cancelado",
        EstadoPrestamo.Suspendido => "Suspendido",
        _ => estado.ToString()
    };
}
