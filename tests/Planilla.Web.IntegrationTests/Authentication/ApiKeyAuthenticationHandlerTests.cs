using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Web.Authentication;

namespace Planilla.Web.IntegrationTests.Authentication;

/// <summary>
/// Tests unitarios del ApiKeyAuthenticationHandler usando DefaultHttpContext
/// + stub manual de IApiKeyService. Sin WebApplicationFactory — pura unidad.
/// </summary>
public class ApiKeyAuthenticationHandlerTests
{
    [Fact]
    public async Task NoHeader__ReturnsNoResult()
    {
        var stub = new StubApiKeyService();
        var handler = await CreateHandlerAsync(stub, headerValue: null);

        var result = await handler.AuthenticateAsync();

        result.None.Should().BeTrue("sin header, dejamos pasar a otros schemes");
        stub.ValidateCallCount.Should().Be(0, "sin header, no debe invocar ValidateAsync");
    }

    [Fact]
    public async Task EmptyHeader__ReturnsFail()
    {
        var stub = new StubApiKeyService();
        var handler = await CreateHandlerAsync(stub, headerValue: "   ");

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Missing");
    }

    [Fact]
    public async Task InvalidKey__ReturnsFailWithOpaqueMessage()
    {
        var stub = new StubApiKeyService { ValidateResult = null };
        var handler = await CreateHandlerAsync(stub, headerValue: "pk_live_abcdefgh" + new string('1', 32));

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Be("Invalid API key",
            "el mensaje debe ser opaco — no revelar si fue revocada, expirada, o no existe");
    }

    [Fact]
    public async Task ValidKey__ReturnsSuccessWithClaims()
    {
        var stub = new StubApiKeyService
        {
            ValidateResult = new ApiKey
            {
                Id = 42,
                TenantId = 7,
                Mode = "Live",
                Name = "Test Key",
                KeyPrefix = "abcdef12",
                KeyHash = "hash",
                IsActive = true
            }
        };
        var handler = await CreateHandlerAsync(stub, headerValue: "pk_live_abcdef12" + new string('1', 32));

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
        result.Principal.Should().NotBeNull();

        var identity = result.Principal!.Identity as System.Security.Claims.ClaimsIdentity;
        identity!.AuthenticationType.Should().Be(ApiKeyAuthenticationHandler.SchemeName);

        result.Principal.FindFirst(ApiKeyAuthenticationHandler.ClaimTenantId)!.Value.Should().Be("7");
        result.Principal.FindFirst(ApiKeyAuthenticationHandler.ClaimApiKeyId)!.Value.Should().Be("42");
        result.Principal.FindFirst(ApiKeyAuthenticationHandler.ClaimApiKeyMode)!.Value.Should().Be("Live");
        result.Principal.FindFirst(ApiKeyAuthenticationHandler.ClaimAuthType)!.Value.Should().Be("api_key");
    }

    [Fact]
    public async Task ValidKey__DoesNotAssignTenantRole()
    {
        var stub = new StubApiKeyService
        {
            ValidateResult = new ApiKey { Id = 1, TenantId = 1, Mode = "Live", Name = "k", KeyPrefix = "p", KeyHash = "h", IsActive = true }
        };
        var handler = await CreateHandlerAsync(stub, headerValue: "pk_live_abcdef12" + new string('1', 32));

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
        // Un API key NO representa un usuario — no debe heredar role de tenant.
        result.Principal!.FindFirst("tenant_role").Should().BeNull();
    }

    // ====================================================================
    // Helpers
    // ====================================================================

    private static async Task<ApiKeyAuthenticationHandler> CreateHandlerAsync(
        IApiKeyService service,
        string? headerValue)
    {
        var options = new ApiKeyAuthenticationOptions();
        var optionsMonitor = new StubOptionsMonitor(options);

        var handler = new ApiKeyAuthenticationHandler(
            optionsMonitor,
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            service);

        var context = new DefaultHttpContext();
        if (headerValue is not null)
        {
            context.Request.Headers[ApiKeyAuthenticationHandler.HeaderName] = headerValue;
        }

        var scheme = new AuthenticationScheme(
            ApiKeyAuthenticationHandler.SchemeName,
            ApiKeyAuthenticationHandler.SchemeName,
            typeof(ApiKeyAuthenticationHandler));
        await handler.InitializeAsync(scheme, context);
        return handler;
    }

    /// <summary>
    /// Stub manual de IApiKeyService — evita dependencia de Moq en IntegrationTests.
    /// </summary>
    private class StubApiKeyService : IApiKeyService
    {
        public ApiKey? ValidateResult { get; set; }
        public int ValidateCallCount { get; private set; }

        public Task<ApiKey?> ValidateAsync(string plaintextKey, CancellationToken cancellationToken = default)
        {
            ValidateCallCount++;
            return Task.FromResult(ValidateResult);
        }

        public Task<(ApiKey entity, string plaintextKey)> GenerateAsync(
            int tenantId, string name, string mode, DateTime? expiresAt, string? createdByUserId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> RevokeAsync(int keyId, int tenantId, string? reason, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private class StubOptionsMonitor : IOptionsMonitor<ApiKeyAuthenticationOptions>
    {
        private readonly ApiKeyAuthenticationOptions _options;
        public StubOptionsMonitor(ApiKeyAuthenticationOptions options) { _options = options; }
        public ApiKeyAuthenticationOptions CurrentValue => _options;
        public ApiKeyAuthenticationOptions Get(string? name) => _options;
        public IDisposable? OnChange(Action<ApiKeyAuthenticationOptions, string> listener) => null;
    }
}
