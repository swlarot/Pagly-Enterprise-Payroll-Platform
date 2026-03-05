using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Results;

namespace Vorluno.Planilla.Application.Interfaces;

public interface IDeduccionPrioridadEngine
{
    DeduccionesResult AplicarDeduccionesConPrelacion(
        decimal grossPay,
        decimal netoPostLegal,
        decimal salarioMinimoPeriodo,
        IEnumerable<DeduccionPendiente> deducciones);
}
