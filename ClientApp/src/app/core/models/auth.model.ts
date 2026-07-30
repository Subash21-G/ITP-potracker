export interface LoginRequest {
  readonly userName: string;
  readonly password: string;
}

export interface RegisterRequest {
  readonly fullName: string;
  readonly userName: string;
  readonly password: string;
}

export interface CurrentUser {
  readonly isAuthenticated: boolean;
  readonly userName: string;
  readonly fullName: string;
  readonly role: string;
  readonly isAdmin: boolean;
  readonly isProduction: boolean;
  readonly isViewer: boolean;
  readonly canOperate: boolean;
}

export interface AuthenticationResponse {
  readonly success: boolean;
  readonly message: string;
  readonly user: CurrentUser | null;
}

export interface RegistrationResponse {
  readonly success: boolean;
  readonly message: string;
  readonly isFirstAdministrator: boolean;
  readonly role: string;
}