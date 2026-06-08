import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';

/**
 * Hassas veri (JWT) için iOS Keychain / Android Keystore wrapper'ı.
 * Web platformunda SecureStore yok — localStorage fallback'i.
 *
 * Interface ile soyutlandı → test'lerde InMemoryStorage ile mock'lanabilir.
 */
export interface SecureStorage {
  getItem(key: string): Promise<string | null>;
  setItem(key: string, value: string): Promise<void>;
  removeItem(key: string): Promise<void>;
}

class ExpoSecureStorage implements SecureStorage {
  async getItem(key: string): Promise<string | null> {
    if (Platform.OS === 'web') {
      return typeof localStorage !== 'undefined' ? localStorage.getItem(key) : null;
    }
    return SecureStore.getItemAsync(key);
  }

  async setItem(key: string, value: string): Promise<void> {
    if (Platform.OS === 'web') {
      if (typeof localStorage !== 'undefined') localStorage.setItem(key, value);
      return;
    }
    await SecureStore.setItemAsync(key, value);
  }

  async removeItem(key: string): Promise<void> {
    if (Platform.OS === 'web') {
      if (typeof localStorage !== 'undefined') localStorage.removeItem(key);
      return;
    }
    await SecureStore.deleteItemAsync(key);
  }
}

/** Test için in-memory implementasyon */
export class InMemoryStorage implements SecureStorage {
  private store = new Map<string, string>();
  async getItem(key: string) { return this.store.get(key) ?? null; }
  async setItem(key: string, value: string) { this.store.set(key, value); }
  async removeItem(key: string) { this.store.delete(key); }
}

export const defaultSecureStorage = new ExpoSecureStorage();
