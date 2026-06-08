export interface ApiResponse<T> {
  success: boolean;
  data: T;
  message?: string | null;
}

export interface ProblemDetails {
  type?: string;
  title: string;
  status: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[]>;
}

export interface Customer {
  entityId: string;
  email: string;
  firstName?: string;
  lastName?: string;
  active: boolean;
  createdOnUtc: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  expiresAt: string;
}

export interface JwtPayload {
  sub: string;       // EntityId (Guid)
  uid: string;       // internal long Id
  email: string;
  exp: number;
  role?: string | string[];
}

export type Audience = 'public' | 'web' | 'admin';
