using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Helpers;
using Vorluno.Planilla.Application.Results;
using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.Services;

/// <summary>
/// Motor de deducciones con prelacion legal y proteccion de salario minimo.
/// Logica pura sin dependencias de DB - ideal para unit testing.
///
/// Orden de prelacion:
///   1. CSS + SE + ISR (ya aplicados antes de llamar a este motor)
///   2. Pension Alimenticia - PUEDE reducir bajo salario minimo
///   3. Embargos Judiciales - Solo excedente sobre salario minimo
///   4. Voluntarias (prestamos, sindicato, cooperativas, etc.) - Solo excedente sobre salario minimo
/// </summary>
public class DeduccionPrioridadEngine
{
    /// <summary>
    /// Aplica deducciones con prelacion legal y proteccion de salario minimo.
    /// </summary>
    /// <param name="grossPay">Salario bruto del periodo</param>
    /// <param name="netoPostLegal">Neto despues de CSS + SE + ISR</param>
    /// <param name="salarioMinimoPeriodo">Salario minimo prorrateado al periodo</param>
    /// <param name="deducciones">Lista de deducciones pendientes</param>
    /// <returns>Resultado con desglose completo</returns>
    public DeduccionesResult AplicarDeduccionesConPrelacion(
        decimal grossPay,
        decimal netoPostLegal,
        decimal salarioMinimoPeriodo,
        IEnumerable<DeduccionPendiente> deducciones)
    {
        var result = new DeduccionesResult
        {
            SalarioMinimoAplicado = salarioMinimoPeriodo
        };

        if (deducciones == null || !deducciones.Any())
            return result;

        // Ordenar por categoria (prelacion) y luego por prioridad dentro de cada grupo
        var deduccionesOrdenadas = deducciones
            .OrderBy(d => (int)d.Categoria)
            .ThenBy(d => d.Prioridad)
            .ToList();

        decimal saldoDisponible = netoPostLegal;
        int ordenAplicacion = 0;

        // Separar por grupo de prelacion
        var pensiones = deduccionesOrdenadas.Where(d => d.Categoria == CategoriaDeduccion.PensionAlimenticia).ToList();
        var embargos = deduccionesOrdenadas.Where(d => d.Categoria == CategoriaDeduccion.EmbargoJudicial).ToList();
        var voluntarias = deduccionesOrdenadas.Where(d => d.Categoria == CategoriaDeduccion.Voluntaria).ToList();

        // ================================================================
        // GRUPO 1: Pension Alimenticia - Prioridad absoluta
        // PUEDE reducir bajo salario minimo por orden judicial
        // ================================================================
        foreach (var deduccion in pensiones)
        {
            ordenAplicacion++;
            decimal montoCalculado = CalcularMontoDeduccion(deduccion, grossPay, netoPostLegal);
            montoCalculado = LimitarPorMontoTotal(deduccion, montoCalculado);

            decimal montoAplicado = Math.Min(montoCalculado, saldoDisponible);
            // Pension alimenticia puede llevar saldo a negativo si el neto es menor que la pension,
            // pero limitamos a lo que hay disponible (no puede ser negativo)
            if (montoAplicado < 0) montoAplicado = 0;

            decimal saldoAntes = saldoDisponible;
            saldoDisponible -= montoAplicado;

            var item = CrearItem(deduccion, montoCalculado, montoAplicado, saldoAntes, saldoDisponible, ordenAplicacion);
            result.Detalle.Add(item);
            result.TotalPensionAlimenticia += montoAplicado;

            if (montoCalculado > montoAplicado)
            {
                result.MontoLimitadoPorSalarioMinimo += (montoCalculado - montoAplicado);
                result.TuvoLimitacion = true;
            }

            TrackIds(result, deduccion);
        }

        // ================================================================
        // GRUPO 2: Embargos Judiciales
        // Solo afecta excedente sobre salario minimo
        // ================================================================
        decimal excedenteEmbargable = Math.Max(0, saldoDisponible - salarioMinimoPeriodo);

        foreach (var deduccion in embargos)
        {
            ordenAplicacion++;
            decimal montoCalculado = CalcularMontoDeduccion(deduccion, grossPay, netoPostLegal);
            montoCalculado = LimitarPorMontoTotal(deduccion, montoCalculado);

            decimal montoAplicado = Math.Min(montoCalculado, excedenteEmbargable);
            if (montoAplicado < 0) montoAplicado = 0;

            decimal saldoAntes = saldoDisponible;
            saldoDisponible -= montoAplicado;
            excedenteEmbargable -= montoAplicado;

            var item = CrearItem(deduccion, montoCalculado, montoAplicado, saldoAntes, saldoDisponible, ordenAplicacion);

            if (montoCalculado > montoAplicado)
            {
                item.RazonLimitacion = "Proteccion salario minimo";
                result.MontoLimitadoPorSalarioMinimo += (montoCalculado - montoAplicado);
                result.TuvoLimitacion = true;
            }

            result.Detalle.Add(item);
            result.TotalEmbargos += montoAplicado;

            TrackIds(result, deduccion);
        }

        // ================================================================
        // GRUPO 3: Voluntarias (prestamos, sindicato, cooperativas, etc.)
        // No puede reducir bajo salario minimo
        // ================================================================
        decimal disponibleVoluntarias = Math.Max(0, saldoDisponible - salarioMinimoPeriodo);

        foreach (var deduccion in voluntarias)
        {
            ordenAplicacion++;
            decimal montoCalculado = CalcularMontoDeduccion(deduccion, grossPay, netoPostLegal);
            montoCalculado = LimitarPorMontoTotal(deduccion, montoCalculado);

            decimal montoAplicado = Math.Min(montoCalculado, disponibleVoluntarias);
            if (montoAplicado < 0) montoAplicado = 0;

            decimal saldoAntes = saldoDisponible;
            saldoDisponible -= montoAplicado;
            disponibleVoluntarias -= montoAplicado;

            var item = CrearItem(deduccion, montoCalculado, montoAplicado, saldoAntes, saldoDisponible, ordenAplicacion);

            if (montoCalculado > montoAplicado)
            {
                item.RazonLimitacion = disponibleVoluntarias <= 0
                    ? "Salario minimo alcanzado"
                    : "Proteccion salario minimo";
                result.MontoLimitadoPorSalarioMinimo += (montoCalculado - montoAplicado);
                result.TuvoLimitacion = true;
            }

            result.Detalle.Add(item);
            result.TotalVoluntarias += montoAplicado;

            // Track prestamos y anticipos por separado
            if (deduccion.OrigenPrestamoId.HasValue)
            {
                result.TotalPrestamos += montoAplicado;
            }
            else if (deduccion.OrigenAnticipoId.HasValue)
            {
                result.TotalAnticipos += montoAplicado;
            }

            TrackIds(result, deduccion);
        }

        return result;
    }

    /// <summary>
    /// Prorratear salario minimo mensual al periodo de pago.
    /// </summary>
    public static decimal ProrratearSalarioMinimo(decimal salarioMinimoMensual, PayPeriodType payPeriodType)
    {
        decimal resultado = payPeriodType switch
        {
            PayPeriodType.Mensual => salarioMinimoMensual,
            PayPeriodType.Quincenal => salarioMinimoMensual * 12m / 24m,
            PayPeriodType.Bisemanal => salarioMinimoMensual * 12m / 26m,
            PayPeriodType.Semanal => salarioMinimoMensual * 12m / 52m,
            _ => salarioMinimoMensual
        };
        return RoundingPolicy.Round(resultado);
    }

    /// <summary>
    /// Calcula el monto de una deduccion segun su configuracion.
    /// </summary>
    private static decimal CalcularMontoDeduccion(DeduccionPendiente deduccion, decimal grossPay, decimal netoPostLegal)
    {
        if (!deduccion.EsPorcentaje)
            return deduccion.MontoFijo;

        decimal porcentaje = deduccion.Porcentaje ?? 0;
        decimal baseCalculo = deduccion.BaseCalculo == BaseCalculoDeduccion.SalarioBruto
            ? grossPay
            : netoPostLegal;

        return RoundingPolicy.CalculatePercentage(baseCalculo, porcentaje);
    }

    /// <summary>
    /// Limita el monto si la deduccion tiene un MontoTotalACobrar.
    /// </summary>
    private static decimal LimitarPorMontoTotal(DeduccionPendiente deduccion, decimal montoCalculado)
    {
        if (!deduccion.MontoTotalACobrar.HasValue)
            return montoCalculado;

        decimal remanente = deduccion.MontoTotalACobrar.Value - deduccion.MontoCobradoAcumulado;
        if (remanente <= 0) return 0;
        return Math.Min(montoCalculado, remanente);
    }

    private static DeduccionAplicadaItem CrearItem(
        DeduccionPendiente deduccion,
        decimal montoSolicitado,
        decimal montoAplicado,
        decimal saldoAntes,
        decimal saldoDespues,
        int orden)
    {
        return new DeduccionAplicadaItem
        {
            OrigenDeduccionFijaId = deduccion.OrigenDeduccionFijaId,
            OrigenPrestamoId = deduccion.OrigenPrestamoId,
            OrigenAnticipoId = deduccion.OrigenAnticipoId,
            TipoDeduccion = deduccion.TipoDeduccion,
            Categoria = deduccion.Categoria,
            Descripcion = deduccion.Descripcion,
            MontoSolicitado = montoSolicitado,
            MontoAplicado = montoAplicado,
            MontoLimitado = montoSolicitado - montoAplicado,
            SaldoDisponibleAntes = saldoAntes,
            SaldoDisponibleDespues = saldoDespues,
            OrdenAplicacion = orden,
            NombreAcreedor = deduccion.NombreAcreedor
        };
    }

    private static void TrackIds(DeduccionesResult result, DeduccionPendiente deduccion)
    {
        if (deduccion.OrigenPrestamoId.HasValue)
            result.PrestamoIds.Add(deduccion.OrigenPrestamoId.Value);
        if (deduccion.OrigenAnticipoId.HasValue)
            result.AnticipoIds.Add(deduccion.OrigenAnticipoId.Value);
    }
}
