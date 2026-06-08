export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  firstName?: string;
  lastName?: string;
}

export interface RefreshRequest {
  refreshToken: string;
}

export interface UserInfo {
  entityId: string;
  email: string;
  firstName?: string;
  lastName?: string;
  roles: string[];
}

export interface AuthTokens {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: UserInfo;
}

export interface JwtPayload {
  sub: string;
  uid: string;
  email: string;
  exp: number;
  role?: string | string[];
  jti?: string;
}
