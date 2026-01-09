using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Planilla.Web.IntegrationTests.Helpers;
using Vorluno.Planilla.Application.DTOs.Auth;
using Xunit;

namespace Planilla.Web.IntegrationTests;

public class AuthTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_ValidData_ReturnsTokenWithClaims()
    {
        // Arrange
        var registerDto = new RegisterDto
        {
            Email = $"test-{Guid.NewGuid()}@example.com",
            Password = "Test@1234",
            CompanyName = "Test Company",
            RUC = "12345678",
            DV = "12"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", registerDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        authResponse.Should().NotBeNull();
        authResponse!.Token.Should().NotBeNullOrEmpty();

        // Verify JWT claims
        var tenantId = JwtHelper.GetTenantId(authResponse.Token);
        tenantId.Should().BeGreaterThan(0, "tenant_id claim should be present and > 0");

        var role = JwtHelper.GetTenantRole(authResponse.Token);
        role.Should().Be("Owner", "newly registered user should be Owner");

        var plan = JwtHelper.GetPlan(authResponse.Token);
        plan.Should().Be("Professional", "new tenants get Professional trial");

        // Verify response DTOs
        authResponse.User.Should().NotBeNull();
        authResponse.User.Email.Should().Be(registerDto.Email);
        authResponse.Tenant.Should().NotBeNull();
        authResponse.Tenant.Name.Should().Be(registerDto.CompanyName);
        authResponse.Subscription.Should().NotBeNull();
        authResponse.Subscription.Plan.ToString().Should().Be("Professional");
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokenWithClaims()
    {
        // Arrange - Register first
        var email = $"test-{Guid.NewGuid()}@example.com";
        var password = "Test@1234";

        var registerDto = new RegisterDto
        {
            Email = email,
            Password = password,
            CompanyName = "Test Company Login",
            RUC = "87654321",
            DV = "34"
        };

        await _client.PostAsJsonAsync("/api/auth/register", registerDto);

        var loginDto = new LoginDto
        {
            Email = email,
            Password = password
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        authResponse.Should().NotBeNull();
        authResponse!.Token.Should().NotBeNullOrEmpty();

        var tenantId = JwtHelper.GetTenantId(authResponse.Token);
        tenantId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var loginDto = new LoginDto
        {
            Email = "nonexistent@example.com",
            Password = "WrongPassword"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
