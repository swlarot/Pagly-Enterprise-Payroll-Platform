namespace Vorluno.Planilla.Application.DTOs.ApiKeys;

/// <summary>
/// Response de <c>POST /api/api-keys</c>.
///
/// <b>IMPORTANTE</b>: <c>PlaintextKey</c> solo se retorna en este response
/// del create. Nunca se puede recuperar después — si el usuario la pierde,
/// debe generar una nueva y revocar la vieja.
/// </summary>
public record CreateApiKeyResponse(
    ApiKeyDto Key,

    /// <summary>
    /// La key plaintext completa (ej: <c>pk_live_abc12345xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx</c>).
    /// Es el ÚNICO momento en que el sistema retorna el secret. Guardar de inmediato.
    /// </summary>
    string PlaintextKey
);
