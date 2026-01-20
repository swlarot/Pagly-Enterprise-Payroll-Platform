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
  expiresAt: string;
  user: UserInfoDto;
  tenant: TenantInfoDto;
  subscription: SubscriptionInfoDto;
}

export interface UserInfoDto {
  userId: string;
  email: string;
  role: TenantRole;
  roleName: string;
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
  Admin = 1,
  Manager = 2,
  Accountant = 3,
  Employee = 4
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
}

export interface AuditLogDto {
  id: number;
  tenantId: number;
  actorUserId?: string;
  actorEmail?: string;
  action: string;
  entityType?: string;
  entityId?: string;
  details?: string;
  ipAddress?: string;
  timestamp: string;
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

export interface TenantDto {
  id: number;
  name: string;
  subdomain: string;
  ruc: string;
  dv: string;
  isActive: boolean;
}
