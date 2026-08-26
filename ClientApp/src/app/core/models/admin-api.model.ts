export type AdminRole =
  | 'Viewer'
  | 'Production'
  | 'Admin';

export interface AdminUser {
  readonly id: number;
  readonly fullName: string;
  readonly userName: string;
  readonly role: AdminRole;
  readonly isCurrentUser: boolean;
}

export type AdminUserSortColumn =
  | 'fullName'
  | 'userName'
  | 'role';

export type AdminUserSortDirection =
  | 'asc'
  | 'desc';

export type AdminUserExcelFilterColumn =
  AdminUserSortColumn;

export interface AdminUserExcelFilterValue {
  readonly value: string;
  readonly label: string;
  readonly count: number;
  readonly selected?: boolean;
}

export type AdminUserExcelFilterSelections =
  Partial<
    Record<
      AdminUserExcelFilterColumn,
      readonly string[]
    >
  >;

export interface AdminUserSummary {
  readonly totalUsers: number;
  readonly administrators: number;
  readonly productionUsers: number;
  readonly viewers: number;
}

export interface AdminUsersResponse {
  readonly users: readonly AdminUser[];
  readonly summary: AdminUserSummary;
  readonly allowedRoles: readonly AdminRole[];
}

export interface UpdateUserRoleRequest {
  readonly role: AdminRole;
}

export interface AdminUserMutationResponse {
  readonly success: boolean;
  readonly message: string;
  readonly user: AdminUser | null;
}