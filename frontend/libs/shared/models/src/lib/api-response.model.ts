/**
 * Backend tüm başarılı yanıtları bu zarfla döner.
 * ApiClient unwrap eder, bu interface'i direkt göreceğin yer az olur.
 */
export interface ApiResponse<T> {
  success: boolean;
  data: T;
  message?: string | null;
}

/**
 * RFC 7807 ProblemDetails — backend GlobalExceptionFilter çıktısı.
 */
export interface ProblemDetails {
  type?: string;
  title: string;
  status: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[]>;
}
