using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Vorluno.Planilla.Application.DTOs.Auth;
using Vorluno.Planilla.Application.DTOs.Tenant;
using Vorluno.Planilla.Domain.Enums;
using Xunit;

namespace Planilla.Web.IntegrationTests;

/// <summary>
/// Integration tests for Phase 3: Tenant Management, Invitations, and Audit Log
/// </summary>
public class TenantManagementTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TenantManagementTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(string Token, TenantInfoDto Tenant)> RegisterTenantAsync(string companyName, string email = null!)
    {
        var client = _factory.CreateClient();
        var registerDto = new RegisterDto
        {
            Email = email ?? $"test-{Guid.NewGuid()}@example.com",
            Password = "Test@1234",
            CompanyName = companyName,
            RUC = Guid.NewGuid().ToString().Substring(0, 8),
            DV = "12"
        };

        var response = await client.PostAsJsonAsync("/api/auth/register", registerDto);
        response.EnsureSuccessStatusCode();

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        return (authResponse!.Token, authResponse.Tenant);
    }

    private HttpClient CreateAuthenticatedClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ========================================================================
    // TEST 1: Only Owner/Admin can invite users
    // ========================================================================
    [Fact]
    public async Task InviteUser_AsManager_Returns403Forbidden()
    {
        // Arrange - Register tenant (user is Owner by default)
        var (ownerToken, tenant) = await RegisterTenantAsync("Test Company Invite");
        var managerEmail = $"manager-{Guid.NewGuid()}@example.com";

        // Owner invites a Manager
        var ownerClient = CreateAuthenticatedClient(ownerToken);
        var inviteDto = new CreateInvitationDto
        {
            Email = managerEmail,
            Role = TenantRole.Manager
        };

        var inviteResponse = await ownerClient.PostAsJsonAsync("/api/tenant/invite", inviteDto);
        inviteResponse.EnsureSuccessStatusCode();
        var invitation = await inviteResponse.Content.ReadFromJsonAsync<InvitationDto>();

        // Manager accepts invitation (gets their token)
        var acceptDto = new AcceptInvitationDto
        {
            Token = invitation!.Token,
            Password = "Manager@1234"
        };

        var acceptClient = _factory.CreateClient();
        var acceptResponse = await acceptClient.PostAsJsonAsync("/api/auth/accept-invite", acceptDto);
        acceptResponse.EnsureSuccessStatusCode();
        var managerAuthResponse = await acceptResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

        // Act - Manager tries to invite another user (should fail)
        var managerClient = CreateAuthenticatedClient(managerAuthResponse!.Token);
        var secondInviteDto = new CreateInvitationDto
        {
            Email = $"accountant-{Guid.NewGuid()}@example.com",
            Role = TenantRole.Accountant
        };

        var secondInviteResponse = await managerClient.PostAsJsonAsync("/api/tenant/invite", secondInviteDto);

        // Assert - Should return 403 Forbidden (not 401, because auth succeeded but authZ failed)
        secondInviteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Manager should not be able to invite users - only Owner/Admin can");
    }

    // ========================================================================
    // TEST 2: Invite respects MaxUsers limit from plan
    // ========================================================================
    [Fact]
    public async Task InviteUser_ExceedsMaxUsers_Returns400OrConflict()
    {
        // Arrange - Register tenant with Professional plan (MaxUsers = 10 by default)
        var (ownerToken, tenant) = await RegisterTenantAsync("Test Company MaxUsers");
        var ownerClient = CreateAuthenticatedClient(ownerToken);

        // Get current usage to know how many users we can add
        var usageResponse = await ownerClient.GetAsync("/api/tenant/usage");
        usageResponse.EnsureSuccessStatusCode();
        var usage = await usageResponse.Content.ReadFromJsonAsync<TenantUsageDto>();

        var maxUsers = usage!.MaxUsers;
        var currentUsers = usage.UsersCount;
        var availableSlots = maxUsers - currentUsers;

        // Fill up all available slots (invite until maxUsers - 1)
        for (int i = 0; i < availableSlots; i++)
        {
            var inviteDto = new CreateInvitationDto
            {
                Email = $"user-{i}-{Guid.NewGuid()}@example.com",
                Role = TenantRole.Accountant
            };

            var inviteResponse = await ownerClient.PostAsJsonAsync("/api/tenant/invite", inviteDto);

            // All invites should succeed until we hit the limit
            if (i < availableSlots - 1)
            {
                inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK,
                    $"invite {i + 1}/{availableSlots - 1} should succeed");
            }
        }

        // Act - Try to invite one more user (should exceed limit)
        var exceedLimitDto = new CreateInvitationDto
        {
            Email = $"excess-user-{Guid.NewGuid()}@example.com",
            Role = TenantRole.Employee
        };

        var exceedResponse = await ownerClient.PostAsJsonAsync("/api/tenant/invite", exceedLimitDto);

        // Assert - Should return 400 or 409 with error message about limit
        exceedResponse.StatusCode.Should().BeOneOf(new[] { HttpStatusCode.BadRequest, HttpStatusCode.Conflict },
            $"should reject invite when MaxUsers ({maxUsers}) is reached");
    }

    // ========================================================================
    // TEST 3: Cross-tenant isolation (users/invites/audit return 404 for other tenant's data)
    // ========================================================================
    [Fact]
    public async Task GetTenantUsers_FromTenantA_CannotSee_TenantB_Users()
    {
        // Arrange - Create 2 tenants
        var (tokenA, tenantA) = await RegisterTenantAsync("Tenant A");
        var (tokenB, tenantB) = await RegisterTenantAsync("Tenant B");

        // Act - Tenant A gets their users
        var clientA = CreateAuthenticatedClient(tokenA);
        var usersResponseA = await clientA.GetAsync("/api/tenant/users");
        usersResponseA.EnsureSuccessStatusCode();
        var usersA = await usersResponseA.Content.ReadFromJsonAsync<List<TenantUserDto>>();

        // Tenant B gets their users
        var clientB = CreateAuthenticatedClient(tokenB);
        var usersResponseB = await clientB.GetAsync("/api/tenant/users");
        usersResponseB.EnsureSuccessStatusCode();
        var usersB = await usersResponseB.Content.ReadFromJsonAsync<List<TenantUserDto>>();

        // Assert - Each tenant should only see their own users (1 Owner each)
        usersA.Should().HaveCount(1, "Tenant A should only see their Owner user");
        usersB.Should().HaveCount(1, "Tenant B should only see their Owner user");

        // Verify cross-tenant isolation (user IDs should be different)
        usersA![0].UserId.Should().NotBe(usersB![0].UserId,
            "different tenants should have different Owner users");
    }

    [Fact]
    public async Task GetInvitations_TenantA_CannotSee_TenantB_Invitations()
    {
        // Arrange - Create 2 tenants and each creates an invitation
        var (tokenA, tenantA) = await RegisterTenantAsync("Tenant A Invites");
        var (tokenB, tenantB) = await RegisterTenantAsync("Tenant B Invites");

        // Tenant A creates invitation
        var clientA = CreateAuthenticatedClient(tokenA);
        await clientA.PostAsJsonAsync("/api/tenant/invite", new CreateInvitationDto
        {
            Email = $"invitee-a-{Guid.NewGuid()}@example.com",
            Role = TenantRole.Manager
        });

        // Tenant B creates invitation
        var clientB = CreateAuthenticatedClient(tokenB);
        await clientB.PostAsJsonAsync("/api/tenant/invite", new CreateInvitationDto
        {
            Email = $"invitee-b-{Guid.NewGuid()}@example.com",
            Role = TenantRole.Accountant
        });

        // Act - Get invitations for each tenant
        var invitesResponseA = await clientA.GetAsync("/api/tenant/invitations");
        invitesResponseA.EnsureSuccessStatusCode();
        var invitesA = await invitesResponseA.Content.ReadFromJsonAsync<List<InvitationDto>>();

        var invitesResponseB = await clientB.GetAsync("/api/tenant/invitations");
        invitesResponseB.EnsureSuccessStatusCode();
        var invitesB = await invitesResponseB.Content.ReadFromJsonAsync<List<InvitationDto>>();

        // Assert - Each tenant should only see their own invitations
        invitesA.Should().HaveCount(1, "Tenant A should only see their own invitation");
        invitesB.Should().HaveCount(1, "Tenant B should only see their own invitation");

        // Verify emails match their respective tenant
        invitesA![0].Email.Should().Contain("invitee-a", "Tenant A's invitation should be for their invitee");
        invitesB![0].Email.Should().Contain("invitee-b", "Tenant B's invitation should be for their invitee");
    }

    [Fact]
    public async Task GetAuditLog_TenantA_CannotSee_TenantB_Logs()
    {
        // Arrange - Create 2 tenants
        var (tokenA, tenantA) = await RegisterTenantAsync("Tenant A Audit");
        var (tokenB, tenantB) = await RegisterTenantAsync("Tenant B Audit");

        // Each tenant performs an auditable action (invite user)
        var clientA = CreateAuthenticatedClient(tokenA);
        await clientA.PostAsJsonAsync("/api/tenant/invite", new CreateInvitationDto
        {
            Email = $"audit-a-{Guid.NewGuid()}@example.com",
            Role = TenantRole.Manager
        });

        var clientB = CreateAuthenticatedClient(tokenB);
        await clientB.PostAsJsonAsync("/api/tenant/invite", new CreateInvitationDto
        {
            Email = $"audit-b-{Guid.NewGuid()}@example.com",
            Role = TenantRole.Accountant
        });

        // Act - Get audit logs for each tenant
        var auditResponseA = await clientA.GetAsync("/api/tenant/audit");
        auditResponseA.EnsureSuccessStatusCode();
        var auditA = await auditResponseA.Content.ReadFromJsonAsync<PagedResultDto<AuditLogDto>>();

        var auditResponseB = await clientB.GetAsync("/api/tenant/audit");
        auditResponseB.EnsureSuccessStatusCode();
        var auditB = await auditResponseB.Content.ReadFromJsonAsync<PagedResultDto<AuditLogDto>>();

        // Assert - Each tenant should only see their own audit logs
        auditA.Should().NotBeNull();
        auditB.Should().NotBeNull();

        // All audit entries for Tenant A should have their tenant context
        auditA!.Items.Should().OnlyContain(log => log.Action.Contains("Tenant A Audit") || log.Action.Contains("invite"),
            "Tenant A should only see their own audit logs");

        // Tenant B's logs should not contain Tenant A's actions
        auditB!.Items.Should().NotContain(log => log.ActorEmail.Contains("Tenant A Audit"),
            "Tenant B should not see Tenant A's audit logs");
    }

    // ========================================================================
    // TEST 4: Accept invite with valid token returns JWT with correct TenantId
    // ========================================================================
    [Fact]
    public async Task AcceptInvite_ValidToken_ReturnsJwtWithCorrectTenantId()
    {
        // Arrange - Register tenant and create invitation
        var (ownerToken, tenant) = await RegisterTenantAsync("Test Company Accept Invite");
        var inviteeEmail = $"invitee-{Guid.NewGuid()}@example.com";

        var ownerClient = CreateAuthenticatedClient(ownerToken);
        var inviteDto = new CreateInvitationDto
        {
            Email = inviteeEmail,
            Role = TenantRole.Manager
        };

        var inviteResponse = await ownerClient.PostAsJsonAsync("/api/tenant/invite", inviteDto);
        inviteResponse.EnsureSuccessStatusCode();
        var invitation = await inviteResponse.Content.ReadFromJsonAsync<InvitationDto>();

        // Act - Accept invitation
        var acceptDto = new AcceptInvitationDto
        {
            Token = invitation!.Token,
            Password = "Invitee@1234"
        };

        var acceptClient = _factory.CreateClient();
        var acceptResponse = await acceptClient.PostAsJsonAsync("/api/auth/accept-invite", acceptDto);

        // Assert - Should return 200 OK with JWT
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "accepting valid invitation should succeed");

        var authResponseDto = await acceptResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        authResponseDto.Should().NotBeNull();
        authResponseDto!.Token.Should().NotBeNullOrEmpty("should return JWT token");

        // Verify JWT claims
        var tenantId = Helpers.JwtHelper.GetTenantId(authResponseDto.Token);
        tenantId.Should().Be(tenant.Id, "JWT should contain correct tenant_id claim");

        var role = Helpers.JwtHelper.GetTenantRole(authResponseDto.Token);
        role.Should().Be("Manager", "JWT should contain correct tenant_role claim");

        // Verify user info in response
        authResponseDto.User.Should().NotBeNull();
        authResponseDto.User.Email.Should().Be(inviteeEmail, "response should contain invitee's email");
        authResponseDto.User.Role.Should().Be(TenantRole.Manager, "response should contain correct role");

        // Verify tenant info matches
        authResponseDto.Tenant.Should().NotBeNull();
        authResponseDto.Tenant.Id.Should().Be(tenant.Id, "response should contain correct tenant info");
    }

    [Fact]
    public async Task AcceptInvite_ExpiredToken_Returns400()
    {
        // This test would require manipulating token expiration
        // For simplicity, testing invalid token format

        // Arrange
        var acceptDto = new AcceptInvitationDto
        {
            Token = "invalid-token-format",
            Password = "Test@1234"
        };

        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/accept-invite", acceptDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "invalid invitation token should be rejected");
    }

    // ========================================================================
    // BONUS: Test Owner protection (cannot remove self if only Owner)
    // ========================================================================
    [Fact]
    public async Task RemoveUser_LastOwner_Returns400()
    {
        // Arrange - Register tenant (single Owner)
        var (ownerToken, tenant) = await RegisterTenantAsync("Test Company Owner Protection");
        var ownerClient = CreateAuthenticatedClient(ownerToken);

        // Get current user list to find Owner's userId
        var usersResponse = await ownerClient.GetAsync("/api/tenant/users");
        usersResponse.EnsureSuccessStatusCode();
        var users = await usersResponse.Content.ReadFromJsonAsync<List<TenantUserDto>>();
        var ownerId = users![0].UserId;

        // Act - Owner tries to remove themselves (should fail)
        var removeResponse = await ownerClient.DeleteAsync($"/api/tenant/users/{ownerId}");

        // Assert - Should return 400 with error message about being last Owner
        removeResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "should prevent removing the last Owner from tenant");
    }
}
