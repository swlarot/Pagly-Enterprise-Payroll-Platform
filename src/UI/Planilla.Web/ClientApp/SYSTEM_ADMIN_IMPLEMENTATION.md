# System Admin Panel - Implementation Documentation

## Overview

This document describes the complete implementation of the System Admin panel for Planilla, allowing system administrators to manage all tenants in the platform.

## Architecture

### Authentication Flow

System administrators are identified by the `is_system_admin` claim in their JWT token. The frontend checks this claim to:

1. Show/hide the System Admin access banner in tenant views
2. Protect System Admin routes with `SystemAdminRoute` component
3. Redirect to the appropriate dashboard after login

### File Structure

```
src/UI/Planilla.Web/ClientApp/
├── src/
│   ├── components/
│   │   ├── auth/
│   │   │   └── SystemAdminRoute.tsx          # Route protection for system admins
│   │   ├── layout/
│   │   │   ├── SystemAdminLayout.tsx         # Layout for system admin pages
│   │   │   └── AuthLayout.tsx                # Updated with system admin banner
│   │   └── ui/                               # Reusable UI components
│   │       ├── Button.tsx
│   │       ├── Card.tsx
│   │       ├── Modal.tsx
│   │       ├── Badge.tsx
│   │       ├── Input.tsx
│   │       └── Select.tsx
│   ├── pages/
│   │   ├── SystemAdminDashboardPage.tsx      # System metrics dashboard
│   │   ├── TenantsManagementPage.tsx         # List and filter tenants
│   │   ├── CreateTenantPage.tsx              # Create new tenant form
│   │   └── TenantDetailsPage.tsx             # View and manage tenant
│   ├── services/
│   │   └── systemAdminService.ts             # API calls for admin endpoints
│   ├── types/
│   │   └── api.ts                            # Updated with System Admin DTOs
│   ├── contexts/
│   │   └── AuthContext.tsx                   # Updated with isSystemAdmin
│   └── utils/
│       └── jwt.ts                            # Updated with is_system_admin claim
```

## Components

### 1. SystemAdminRoute.tsx

Protected route component that:
- Checks if user is authenticated
- Verifies `isSystemAdmin` flag from AuthContext
- Shows loading state while checking
- Displays access denied message for non-admins
- Redirects to login if not authenticated

### 2. SystemAdminLayout.tsx

Layout component featuring:
- Top navigation bar with System Admin branding
- Navigation links to Dashboard and Tenants
- User menu with email and role display
- Option to return to tenant view (if user has a tenant)
- Logout functionality

### 3. UI Components

Reusable components following Planilla's design system:

**Button.tsx**
- Variants: primary, secondary, danger, success, outline, ghost
- Sizes: sm, md, lg
- Loading state support
- Icon support with Lucide React

**Card.tsx**
- Card container with consistent styling
- CardHeader, CardBody, CardFooter sub-components
- Border and shadow styling

**Modal.tsx**
- Centered modal with backdrop
- Sizes: sm, md, lg, xl, full
- Close button and escape key support
- Body scrolling for long content

**Badge.tsx**
- Variants: default, success, danger, warning, info
- Used for plan and status indicators

**Input.tsx** & **Select.tsx**
- Label and error message support
- Required field indicator
- Helper text support
- Consistent focus states

## Pages

### 1. SystemAdminDashboardPage

**Route:** `/system-admin/dashboard`

**Features:**
- Total tenants, users, and employees metrics
- Plan distribution chart with percentages
- Recent growth statistics (7 and 30 days)
- Quick action links to tenant management

**Data Source:** `GET /api/admin/metrics`

### 2. TenantsManagementPage

**Route:** `/system-admin/tenants`

**Features:**
- Paginated table of all tenants
- Filters by:
  - Search (name, RUC, subdomain)
  - Plan (Free, Starter, Professional, Enterprise)
  - Subscription status
  - Active/Inactive
- Displays:
  - Company name and owner email
  - RUC and DV
  - Plan badge
  - Status badge
  - Employee and user counts
  - Creation date
- View details action for each tenant

**Data Source:** `GET /api/admin/tenants?page=1&pageSize=10&filters...`

### 3. CreateTenantPage

**Route:** `/system-admin/tenants/create`

**Features:**
- Three-section form:
  1. Company Information (name, RUC, DV)
  2. Owner Details (email, password)
  3. Subscription Config (plan, trial days)
- Client-side validation with error messages
- Success screen with created tenant details
- Navigate to tenant details or back to list

**Data Source:** `POST /api/admin/tenants`

### 4. TenantDetailsPage

**Route:** `/system-admin/tenants/:id`

**Features:**
- Tenant header with status badge
- Usage statistics (employees, users, companies)
- Subscription information card with:
  - Current plan and status
  - Monthly price
  - Start date and trial expiration
  - Usage progress bars
- Owner information card
- Users list table with roles and status
- Administrative actions:
  - Change plan modal
  - Extend trial modal
  - Deactivate/Reactivate tenant modal

**Data Source:**
- `GET /api/admin/tenants/:id`
- `PUT /api/admin/tenants/:id/subscription`
- `DELETE /api/admin/tenants/:id`
- `POST /api/admin/tenants/:id/reactivate`

## Services

### systemAdminService.ts

Provides methods for all System Admin API operations:

```typescript
{
  getMetrics(): Promise<SystemMetricsDto>
  getAllTenants(params): Promise<PagedResultDto<TenantListItemDto>>
  getTenantById(id): Promise<TenantDetailDto>
  createTenant(data): Promise<TenantDetailDto>
  updateTenant(id, data): Promise<TenantDetailDto>
  updateTenantSubscription(id, data): Promise<TenantDetailDto>
  deactivateTenant(id): Promise<void>
  reactivateTenant(id): Promise<void>
}
```

## Types

### New DTOs in api.ts

```typescript
SystemMetricsDto
TenantListItemDto
TenantDetailDto
CreateTenantDto
UpdateTenantDto
UpdateTenantSubscriptionDto
```

## Context Updates

### AuthContext.tsx

Added:
- `isSystemAdmin: boolean` state
- JWT token parsing to extract `is_system_admin` claim
- Reset `isSystemAdmin` on logout

## Routing

### App.tsx

Added routes:
```tsx
/system-admin/dashboard      → SystemAdminDashboardPage
/system-admin/tenants        → TenantsManagementPage
/system-admin/tenants/create → CreateTenantPage
/system-admin/tenants/:id    → TenantDetailsPage
```

All routes wrapped with `<SystemAdminRoute>` component.

## Login Flow

### LoginPage.tsx

Updated to:
1. Parse JWT token after successful login
2. Check `is_system_admin` claim
3. Redirect to `/system-admin/dashboard` if true
4. Otherwise redirect to tenant dashboard

## Navigation

### Tenant Users with System Admin Access

- **In Tenant View:** Blue banner at top with "Ir al Panel de Admin" button
- **In System Admin View:** User menu includes "Ir a Mi Tenant" option (if they have a tenant)

## Design System

### Colors

- Primary: Blue 600 (#2563eb)
- Success: Green 600 (#16a34a)
- Warning: Amber 600 (#d97706)
- Danger: Red 600 (#dc2626)
- Background: Gray 50 (#f8fafc)
- Card Background: White (#ffffff)

### Plan Badge Colors

- Free: Gray (default)
- Starter: Blue (info)
- Professional: Green (success)
- Enterprise: Amber (warning)

### Status Badge Colors

- Active: Green (success)
- Trialing: Blue (info)
- Past Due: Amber (warning)
- Canceled: Red (danger)

## Usage

### Creating a New Tenant

1. Navigate to `/system-admin/tenants`
2. Click "Crear Tenant" button
3. Fill in company info (name, RUC, DV)
4. Provide owner email and password
5. Select subscription plan and trial days
6. Submit form
7. View success screen with credentials
8. Navigate to tenant details or tenants list

### Managing a Tenant

1. Navigate to `/system-admin/tenants/:id`
2. View tenant metrics and subscription info
3. Use action buttons to:
   - Change plan (opens modal)
   - Extend trial (opens modal)
   - Deactivate/Reactivate tenant (opens confirmation modal)

### Viewing System Metrics

1. Navigate to `/system-admin/dashboard`
2. View overall system statistics
3. Check plan distribution and growth metrics
4. Use quick actions to navigate to tenant management

## Security

- All routes protected by `SystemAdminRoute` component
- API calls use JWT token from localStorage
- Backend validates `is_system_admin` claim on all admin endpoints
- Non-admins see access denied screen if they try to access admin routes

## Future Enhancements

Potential improvements:
- Export tenant list to Excel/CSV
- Bulk actions (deactivate multiple tenants)
- Advanced search with more filters
- Tenant activity logs
- Revenue reports and analytics
- Email notification settings
- System-wide announcements
- Tenant impersonation for support
