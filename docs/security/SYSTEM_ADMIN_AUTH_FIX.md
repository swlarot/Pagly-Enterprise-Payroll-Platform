# System Admin Authorization Fix

## Problem

System admin users were receiving **403 Forbidden** errors when trying to access `/api/admin/*` endpoints, even though they were properly authenticated and had the `IsSystemAdmin` flag set to `true` in the database.

## Root Cause

The ASP.NET Core authorization system was configured to use the `tenant_role` claim for role-based authorization (line 91 in `Program.cs`):

```csharp
RoleClaimType = "tenant_role" // Map "tenant_role" claim to Role for [Authorize(Roles=...)]
```

However, system admin JWT tokens contain:
- `is_system_admin` = "true"
- `tenant_role` = "SystemAdmin" (a dummy value, not a valid `TenantRole` enum)
- `tenant_id` = "0" (no tenant)

The `AdminController` had `[Authorize]` at the controller level, which requires authentication but was failing because:
1. The `tenant_role` claim contained "SystemAdmin" which is not a valid role
2. Each endpoint manually checked `IsSystemAdminAsync()` but the request was already blocked by authorization

## Solution

Created a **policy-based authorization** system specifically for system admins:

### 1. Created `SystemAdminRequirement` (Authorization Requirement)

**File:** `src/UI/Planilla.Web/Authorization/SystemAdminRequirement.cs`

```csharp
public class SystemAdminRequirement : IAuthorizationRequirement
{
    // Marker requirement - no parameters needed
}
```

### 2. Created `SystemAdminAuthorizationHandler` (Authorization Handler)

**File:** `src/UI/Planilla.Web/Authorization/SystemAdminAuthorizationHandler.cs`

```csharp
public class SystemAdminAuthorizationHandler : AuthorizationHandler<SystemAdminRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SystemAdminRequirement requirement)
    {
        // Check for the is_system_admin claim
        var isSystemAdminClaim = context.User.FindFirst("is_system_admin");

        if (isSystemAdminClaim != null && isSystemAdminClaim.Value == "true")
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
```

### 3. Updated `Program.cs` to Register Policy and Handler

Added to authorization policies:

```csharp
// Phase 4: System Admin policy - verifica el claim is_system_admin
.AddPolicy("RequireSystemAdmin", p => p.Requirements.Add(new Vorluno.Planilla.Web.Authorization.SystemAdminRequirement()));

// Registrar el handler de autorización para SystemAdmin
builder.Services.AddSingleton<IAuthorizationHandler, Vorluno.Planilla.Web.Authorization.SystemAdminAuthorizationHandler>();
```

### 4. Updated `AdminController` Authorization

**Before:**
```csharp
[Authorize]  // Generic authorization - doesn't check system admin
[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    [HttpGet("tenants")]
    public async Task<IActionResult> GetAllTenants()
    {
        if (!await IsSystemAdminAsync())  // Manual check in every method
        {
            return Forbid();
        }
        // ...
    }
}
```

**After:**
```csharp
[Authorize(Policy = "RequireSystemAdmin")]  // Policy-based authorization
[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    [HttpGet("tenants")]
    public async Task<IActionResult> GetAllTenants()
    {
        // No manual check needed - policy enforces it at controller level
        try
        {
            // ...
        }
    }
}
```

Removed the redundant `IsSystemAdminAsync()` checks from all endpoints:
- `GetAllTenants()`
- `GetTenantById()`
- `CreateTenant()`
- `UpdateTenant()`
- `DeactivateTenant()`
- `GetSystemMetrics()`
- `GetTenantUsers()`

## Benefits

1. **Centralized Authorization**: The system admin check is now handled by the authorization pipeline, not scattered across endpoint methods
2. **Consistent Security**: All admin endpoints are protected at the controller level
3. **Cleaner Code**: Removed repetitive authorization checks from each method
4. **Standard ASP.NET Core Pattern**: Uses the built-in policy-based authorization system
5. **JWT Claim-Based**: Relies on the `is_system_admin` claim in the JWT token, which is already being set correctly

## How It Works

1. **Login**: System admin logs in via `/api/auth/login`
2. **JWT Generation**: `AuthController.GenerateSystemAdminJwtToken()` creates a JWT with:
   - `is_system_admin` = "true"
   - `tenant_id` = "0"
   - `tenant_role` = "SystemAdmin" (dummy)
3. **Request to Admin Endpoint**: User sends request to `/api/admin/metrics` with JWT in `Authorization: Bearer {token}` header
4. **Authentication**: JWT middleware validates the token signature and creates `ClaimsPrincipal`
5. **Authorization**:
   - `[Authorize(Policy = "RequireSystemAdmin")]` triggers
   - `SystemAdminAuthorizationHandler` checks for `is_system_admin` claim
   - If claim value is "true", authorization succeeds
   - If claim is missing or false, returns 403 Forbidden
6. **Endpoint Execution**: If authorized, the endpoint method executes

## Testing

Run the test script to verify the fix:

```powershell
.\test-admin-auth.ps1
```

This will:
1. Login as system admin (`admin@planilla.com`)
2. Test `GET /api/admin/metrics` (should return 200 OK)
3. Test `GET /api/admin/tenants` (should return 200 OK)
4. Decode JWT to verify `is_system_admin` claim is present

Expected output:
```
✓ Login successful
✓ GET /api/admin/metrics - SUCCESS (200 OK)
✓ GET /api/admin/tenants - SUCCESS (200 OK)
✓ JWT contains is_system_admin = true claim
```

## Files Changed

1. **Created:**
   - `src/UI/Planilla.Web/Authorization/SystemAdminRequirement.cs`
   - `src/UI/Planilla.Web/Authorization/SystemAdminAuthorizationHandler.cs`
   - `test-admin-auth.ps1`

2. **Modified:**
   - `src/UI/Planilla.Web/Program.cs` - Added policy and handler registration
   - `src/UI/Planilla.Web/Controllers/AdminController.cs` - Changed from `[Authorize]` to `[Authorize(Policy = "RequireSystemAdmin")]` and removed manual checks

## Restart Required

The application must be restarted for these changes to take effect, as the authorization pipeline is configured at startup.

## Security Notes

- The `is_system_admin` claim is only set during login if `AppUser.IsSystemAdmin` is true in the database
- System admins are seeded by `SystemAdminSeeder` and cannot be created through the API
- The policy ensures that ONLY users with `is_system_admin = true` can access admin endpoints
- Regular tenant users (even Owners) cannot access `/api/admin/*` endpoints
