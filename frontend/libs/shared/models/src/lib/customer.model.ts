/**
 * Backend Customer entity'sinin frontend karşılığı.
 * `entityId` (Guid) public ID — URL'lerde bu kullanılır.
 * `id` (long) gönderilmez normalde.
 */
export interface Customer {
  entityId: string;       // Guid
  email: string;
  firstName?: string;
  lastName?: string;
  active: boolean;
  createdOnUtc: string;
}

export interface CustomerCreateRequest {
  email: string;
  firstName?: string;
  lastName?: string;
  password: string;
}

export interface CustomerUpdateRequest {
  firstName?: string;
  lastName?: string;
}
