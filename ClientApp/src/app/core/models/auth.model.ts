export interface LoginRequest {
  userName: string;
  password: string;
}

export interface CurrentUser {
  isAuthenticated: boolean;
  userName: string;
  fullName: string;
  role: string;
  isAdmin: boolean;
  isProduction: boolean;
  isViewer: boolean;
  canOperate: boolean;
}

export interface AuthenticationResponse {
  success: boolean;
  message: string;
  user: CurrentUser | null;
}
