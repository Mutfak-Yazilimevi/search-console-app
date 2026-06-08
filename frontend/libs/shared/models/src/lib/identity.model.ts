export interface SessionDto {
  id: number;
  deviceId: number;
  audience: string;
  ipAddress?: string;
  ipCountry?: string;
  userAgent?: string;
  startedUtc: string;
  lastActivityUtc: string;
  isCurrent: boolean;
  isActive: boolean;
  revokedUtc?: string;
  revokedReason?: string;
}

export interface DeviceDto {
  entityId: string;
  name?: string;
  deviceType: string;
  trusted: boolean;
  firstSeenUtc: string;
  lastSeenUtc: string;
  activeSessionCount: number;
}

export interface TwoFactorStatusResponse {
  enabled: boolean;
  remainingRecoveryCodes: number;
}

export interface TwoFactorSetupResponse {
  secret: string;
  otpAuthUri: string;
}

export interface TwoFactorEnableResponse {
  recoveryCodes: string[];
}
