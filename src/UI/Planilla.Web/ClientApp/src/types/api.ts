// Auth DTOs
export interface RegisterDto {
  email: string;
  password: string;
  companyName: string;
  ruc?: string;
  dv?: string;
}

export interface LoginDto {
  email: string;
  password: string;
}

export interface AcceptInvitationDto {
  token: string;
  password: string;
  confirmPassword: string;
}

export interface AuthResponseDto {
  token: string;
  refreshToken: string;
  expiresAt: string;
  user: UserInfoDto;
  tenant: TenantInfoDto | null;
  subscription: SubscriptionInfoDto | null;
  availableTenants?: TenantSummaryDto[];
  requiresTenantSelection?: boolean;
}

export interface TenantSummaryDto {
  id: number;
  name: string;
  role: TenantRole;
  roleName: string;
  subdomain: string;
}

export interface SelectTenantDto {
  tenantId: number;
}

export interface UserInfoDto {
  userId: string;
  email: string;
  fullName?: string;
  role: TenantRole;
  roleName: string;
  isSystemAdmin?: boolean;
  permissions?: string[];
}

export interface TenantInfoDto {
  id: number;
  name: string;
  subdomain: string;
  ruc: string;
  dv: string;
}

export interface SubscriptionInfoDto {
  plan: SubscriptionPlan;
  planName: string;
  status: SubscriptionStatus;
  statusName: string;
  trialEndsAt?: string;
  maxEmployees: number;
  maxUsers: number;
  maxCompanies: number;
  canExportExcel: boolean;
  canExportPdf: boolean;
  canUseApi: boolean;
  monthlyPrice: number;
}

export enum TenantRole {
  Owner = 0,
  User = 1
}

export enum SubscriptionPlan {
  Free = 0,
  Starter = 1,
  Professional = 2,
  Enterprise = 3
}

export enum SubscriptionStatus {
  Active = 0,
  Trialing = 1,
  PastDue = 2,
  Canceled = 3,
  Incomplete = 4
}

// Tenant Management DTOs
export interface TenantUserDto {
  id: number;
  userId: string;
  email: string;
  role: TenantRole;
  roleName: string;
  customRoleName?: string | null;
  isActive: boolean;
  joinedAt: string;
  lastLoginAt?: string;
}

export interface CreateInvitationDto {
  email: string;
  role: TenantRole;
}

export interface InvitationDto {
  id: number;
  email: string;
  role: TenantRole;
  roleName: string;
  token: string;
  expiresAt: string;
  isActive: boolean;
  createdAt: string;
  inviteUrl?: string;
}

export interface ValidateInviteResponseDto {
  isValid: boolean;
  tenantName: string;
  role: TenantRole;
  roleName: string;
  email: string;
  message?: string;
}

export interface UpdateTenantUserDto {
  role?: TenantRole;
  isActive?: boolean;
}

export interface AuditLogDto {
  id: number;
  tenantId?: number;
  actorUserId?: string;
  actorEmail?: string;
  action: string;
  entityType?: string;
  entityId?: string;
  details?: string;
  ipAddress?: string;
  userAgent?: string;
  metadataJson?: string;
  timestamp?: string;
  createdAt?: string;
}

export interface PagedResultDto<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface TenantUsageDto {
  tenantId: number;
  employeesCount: number;
  maxEmployees: number;
  usersCount: number;
  maxUsers: number;
  companiesCount: number;
  maxCompanies: number;
}

export interface SubscriptionUsageDto {
  plan: SubscriptionPlan;
  planName: string;
  status: SubscriptionStatus;
  statusName: string;
  trialEndsAt?: string;
  employeeCount: number;
  employeeLimit: number;
  employeePercentage: number;
  employeeLimitReached?: boolean;
  userCount: number;
  userLimit: number;
  userPercentage: number;
  userLimitReached?: boolean;
  canExportExcel: boolean;
  canExportPdf: boolean;
  canUseApi: boolean;
  hasEmailNotifications: boolean;
  hasAuditLog: boolean;
  monthlyPrice: number;
  upgradeUrl?: string;
  billingUrl?: string;
  hasWarnings?: boolean;
  warningMessages: string[];
}

export interface TenantDto {
  id: number;
  name: string;
  subdomain: string;
  ruc: string;
  dv: string;
  isActive: boolean;
}

// System Admin DTOs
export interface SystemMetricsDto {
  totalTenants: number;
  activeTenants: number;
  totalUsers: number;
  totalEmployees: number;
  planDistribution: {
    free: number;
    starter: number;
    professional: number;
    enterprise: number;
  };
  recentGrowth: {
    last7Days: {
      newTenants: number;
      newUsers: number;
    };
    last30Days: {
      newTenants: number;
      newUsers: number;
    };
  };
}

export interface TenantListItemDto {
  id: number;
  name: string;
  subdomain: string;
  ruc: string;
  dv: string;
  isActive: boolean;
  createdAt: string;
  plan: SubscriptionPlan;
  planName: string;
  subscriptionStatus: SubscriptionStatus;
  subscriptionStatusName: string;
  employeeCount: number;
  userCount: number;
  ownerEmail: string;
}

export interface TenantDetailDto {
  id: number;
  name: string;
  subdomain: string;
  ruc: string;
  dv: string;
  isActive: boolean;
  createdAt: string;
  subscription: {
    plan: SubscriptionPlan;
    planName: string;
    status: SubscriptionStatus;
    statusName: string;
    startDate: string;
    trialEndsAt?: string;
    maxEmployees: number;
    maxUsers: number;
    monthlyPrice: number;
  };
  owner: {
    userId: string;
    email: string;
    joinedAt: string;
    lastLoginAt?: string;
  };
  usage: {
    employeesCount: number;
    usersCount: number;
    companiesCount: number;
  };
  users: {
    userId: string;
    email: string;
    role: TenantRole;
    roleName: string;
    isActive: boolean;
    joinedAt: string;
    lastLoginAt?: string;
  }[];
}

export interface CreateTenantDto {
  name: string;
  ruc: string;
  dv: string;
  ownerEmail: string;
  ownerPassword: string;
  ownerFullName?: string;
  address?: string;
  phone?: string;
  companyEmail?: string;
}

export interface UpdateTenantDto {
  name?: string;
  ruc?: string;
  dv?: string;
  isActive?: boolean;
}

export interface UpdateTenantSubscriptionDto {
  plan: SubscriptionPlan;
  extendTrialDays?: number;
}

// Admin Panel DTOs
export interface AdminTenantDto {
  id: number;
  name: string;
  subdomain: string;
  ruc?: string;
  dv?: string;
  address?: string;
  phone?: string;
  email?: string;
  createdAt: string;
  isActive: boolean;
  subscription?: SubscriptionInfoDto;
  owner?: OwnerInfoDto;
  usage: AdminTenantUsageDto;
}

export interface OwnerInfoDto {
  userId: string;
  email: string;
  fullName?: string;
  joinedAt: string;
  lastLoginAt?: string;
}

export interface AdminTenantUsageDto {
  totalUsers: number;
  activeUsers: number;
  totalEmployees: number;
  activeEmployees: number;
  totalPayrolls: number;
  pendingInvitations: number;
  maxUsers: number;
  maxEmployees: number;
  userUsagePercentage: number;
  employeeUsagePercentage: number;
}

export interface CreateTenantDto {
  name: string;
  ruc: string;
  dv: string;
  ownerEmail: string;
  ownerPassword: string;
  ownerFullName?: string;
  address?: string;
  phone?: string;
  companyEmail?: string;
}

/** Payload para PUT /api/admin/tenants/{id} - nombres en camelCase como espera el backend */
export interface UpdateAdminTenantDto {
  nombre?: string;
  telefono?: string;
  correo?: string;
  direccion?: string;
  ruc?: string;
  dv?: string;
  isActive?: boolean;
  tipoContribuyente?: string;
  pais?: string;
  sitioWeb?: string;
  logoUrl?: string;
}

// Admin Tenant User DTO (for system admin panel)
export interface AdminTenantUserDto {
  id: number;
  userId: string;
  email: string;
  fullName?: string;
  role: TenantRole;
  roleName: string;
  customTenantRoleId?: number;
  customRoleName?: string;
  customRoleColor?: string;
  isActive: boolean;
  joinedAt: string;
  lastLoginAt?: string;
  isPendingInvitation: boolean;
  invitationExpiresAt?: string;
}

// Invite User Request (system admin) - DEPRECATED: Use AssignUserToTenantDto instead
export interface InviteUserRequest {
  email: string;
  fullName: string;
  password: string;
  role: TenantRole;
}

// Assign User to Tenant (system admin) - NEW
export interface AssignUserToTenantDto {
  userEmail: string;
  role: TenantRole;
}

// Audit log paged result (system admin)
export interface AuditLogPagedResultDto {
  total: number;
  page: number;
  pageSize: number;
  data: AuditLogDto[];
}

// System User DTOs (for listing ALL users in the system)
export interface SystemUserDto {
  userId: string;
  email: string;
  fullName: string;
  createdAt: string;
  isActive: boolean;
  isSystemAdmin: boolean;
  tenants: UserTenantMembershipDto[];
}

export interface UserTenantMembershipDto {
  tenantId: number;
  tenantName: string;
  role: string;
  joinedAt: string;
  isActive: boolean;
  lastLoginAt?: string;
}

export interface SystemUserPagedResultDto {
  data: SystemUserDto[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

// Plan Usage DTOs
export interface PlanUsageDto {
  planName: string;
  planDisplayName: string;
  monthlyPrice: number;
  limits: PlanLimitsDto;
  usage: PlanUsageStatsDto;
  remaining: PlanRemainingDto;
  features: FeatureAvailabilityDto[];
  canInviteUsers: boolean;
  canCreateEmployees: boolean;
  canCreateCompanies: boolean;
  shouldUpgrade: boolean;
  upgradeMessage: string | null;
}

export interface PlanLimitsDto {
  maxEmployees: number;
  maxUsers: number;
  maxCompanies: number;
}

export interface PlanUsageStatsDto {
  activeEmployees: number;
  activeUsers: number;
  pendingInvitations: number;
  activeCompanies: number;
}

export interface PlanRemainingDto {
  employees: number;
  users: number;
  companies: number;
  employeesPercentage: number;
  usersPercentage: number;
  companiesPercentage: number;
}

export interface FeatureAvailabilityDto {
  featureName: string;
  isAvailable: boolean;
  description: string;
}

// Custom Tenant Roles DTOs
export interface CustomTenantRoleDto {
  id: number;
  tenantId: number;
  name: string;
  description: string;
  isSystem: boolean;
  color: string;
  displayOrder: number;
  permissionCount: number;
  userCount: number;
  permissions?: string[];
  createdAt: string;
  updatedAt?: string;
}

export interface CreateCustomTenantRoleDto {
  name: string;
  description: string;
  color: string;
}

export interface UpdateCustomTenantRoleDto {
  name: string;
  description: string;
  color?: string;
}

export interface PermissionDto {
  key: string;
  category: string;
  name: string;
  description: string;
}

export interface UpdateRolePermissionsDto {
  permissions: string[];
}

export interface AssignRoleToUserDto {
  /** ID del rol personalizado a asignar (opcional) */
  customRoleId?: number;
  /** Rol del sistema: 0 = Owner, 1 = User. Usar cuando no se asigna rol personalizado. */
  systemRole?: number;
}

// Employee Deletion DTOs
export interface DeletionBlocker {
  reason: string;
  entity: string;
  count: number;
  resolution: string;
}

export interface DeletionWarning {
  message: string;
  entity: string;
  count: number;
}

export interface DeletionResult {
  canDelete: boolean;
  requiresConfirmation: boolean;
  blockers: DeletionBlocker[];
  warnings: DeletionWarning[];
}

// Payroll DTOs (dashboard/summary shapes)
export interface EmpleadoSummaryDto {
  id: number;
  nombre: string;
  apellido: string;
  estaActivo: boolean;
  salarioBase: number;
}

export interface PayrollHeaderSummaryDto {
  id: number;
  payrollNumber: string;
  periodStartDate: string;
  periodEndDate: string;
  payDate: string;
  payPeriodType: number;
  status: number;
  totalGrossPay: number;
  totalDeductions: number;
  totalNetPay: number;
  totalEmployerCost: number;
  totalEmployerCss: number;
  totalEmployerSe: number;
  totalRiskInsurance: number;
  isApproved: boolean;
  isPaid: boolean;
  createdAt: string;
}

// MiPerfilPage view models
export interface EmpleadoDetalleDto {
  id: number;
  nombre: string;
  apellido: string;
  email: string;
  salarioBase: number;
  fechaContratacion: string;
  numeroIdentificacion: string;
  departamentoNombre?: string;
  posicionNombre?: string;
  estaActivo?: boolean;
  hourlyRate?: number;
  hoursPerWeek?: number;
}

export interface PlanillaMiPerfilDto {
  id: number;
  periodStartDate: string;
  periodEndDate: string;
  status: string;
  netPay?: number;
  details: Array<{
    empleadoId: number;
    netPay: number;
    grossPay: number;
  }>;
}

export interface VacacionResumenDto {
  id: number;
  fechaInicio: string;
  fechaFin: string;
  diasSolicitados: number;
  estado: string;
}

export interface UpdateSystemUserDto {
  nombreCompleto: string;
  email?: string;
  telefono?: string;
}

// SystemUsersPage view model
export interface SystemUserViewModel {
  id: string;
  nombreCompleto: string;
  email: string;
  telefono?: string;
  isSystemAdmin: boolean;
  emailConfirmed: boolean;
  tenantAssignments?: { tenantName: string; role: string }[];
}
