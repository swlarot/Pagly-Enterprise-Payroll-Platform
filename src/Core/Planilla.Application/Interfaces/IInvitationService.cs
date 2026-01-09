using Vorluno.Planilla.Application.Common;
using Vorluno.Planilla.Application.DTOs.Auth;
using Vorluno.Planilla.Application.DTOs.Tenant;

namespace Vorluno.Planilla.Application.Interfaces;

/// <summary>
/// Servicio para gestión de invitaciones de usuarios al tenant
/// </summary>
public interface IInvitationService
{
    /// <summary>
    /// Crea una invitación para un nuevo usuario, verificando límites del plan
    /// </summary>
    Task<Result<InvitationResponseDto>> CreateInvitationAsync(CreateInvitationDto dto);

    /// <summary>
    /// Acepta una invitación y crea/asocia el usuario al tenant
    /// </summary>
    Task<Result<AuthResponseDto>> AcceptInvitationAsync(AcceptInvitationDto dto);

    /// <summary>
    /// Obtiene las invitaciones pendientes del tenant actual
    /// </summary>
    Task<Result<List<InvitationDto>>> GetPendingInvitationsAsync();

    /// <summary>
    /// Revoca una invitación
    /// </summary>
    Task<Result<bool>> RevokeInvitationAsync(int invitationId);

    /// <summary>
    /// Valida un token de invitación sin aceptarlo
    /// </summary>
    Task<Result<InvitationDto>> ValidateInvitationTokenAsync(string token);
}
