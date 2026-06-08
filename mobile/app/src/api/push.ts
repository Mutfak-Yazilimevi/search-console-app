import { Platform } from 'react-native';
import Constants from 'expo-constants';
import type { ApiClient } from './api-client';

/**
 * Push notification kurulumu. Expo SDK'ye dayalı.
 *
 * NOT: Bu modülün çalışması için `expo-notifications` ve `expo-device` paketleri
 * yüklü olmalı. Şablonda dependency yer tutucu — gerektiğinde:
 *
 *   npx expo install expo-notifications expo-device
 *
 * Kullanım (App.tsx içinde, login sonrası):
 *
 *   const client = useApiClient();
 *   useEffect(() => {
 *     if (isAuthenticated) {
 *       registerPushTokenAsync(client).catch(err => console.warn(err));
 *     }
 *   }, [isAuthenticated, client]);
 */
export async function registerPushTokenAsync(client: ApiClient): Promise<string | null> {
  // expo-notifications dynamic import — paket yüklü değilse hata fırlatmasın
  let Notifications: typeof import('expo-notifications');
  let Device: typeof import('expo-device');
  try {
    Notifications = await import('expo-notifications');
    Device = await import('expo-device');
  } catch {
    console.warn('expo-notifications/expo-device yüklü değil. Push devre dışı.');
    return null;
  }

  if (!Device.isDevice) {
    console.warn('Push notifications fiziksel cihaz gerektirir.');
    return null;
  }

  // İzin iste
  const { status: existing } = await Notifications.getPermissionsAsync();
  let finalStatus = existing;
  if (existing !== 'granted') {
    const { status } = await Notifications.requestPermissionsAsync();
    finalStatus = status;
  }
  if (finalStatus !== 'granted') return null;

  // Android için channel kurulumu
  if (Platform.OS === 'android') {
    await Notifications.setNotificationChannelAsync('default', {
      name: 'default',
      importance: Notifications.AndroidImportance.DEFAULT,
      vibrationPattern: [0, 250, 250, 250],
      lightColor: '#FF231F7C',
    });
  }

  // Token al
  const projectId = Constants.expoConfig?.extra?.eas?.projectId;
  if (!projectId || projectId === 'REPLACE_WITH_EAS_PROJECT_ID') {
    console.warn('EAS project ID yok — push token alınamıyor.');
    return null;
  }

  const tokenData = await Notifications.getExpoPushTokenAsync({ projectId });
  const token = tokenData.data;

  // Backend'e kaydet
  try {
    await client.post('web', 'devices/register', {
      token,
      provider: 'expo',
      platform: Platform.OS,
      deviceName: Device.deviceName ?? undefined,
      appVersion: Constants.expoConfig?.version ?? undefined,
    });
  } catch (err) {
    console.warn('Push token backend\'e kaydedilemedi', err);
  }

  return token;
}

/**
 * Logout öncesi token'ı backend'den unregister eder.
 */
export async function unregisterPushTokenAsync(client: ApiClient, token: string): Promise<void> {
  try {
    await client.post('web', 'devices/unregister', { token });
  } catch {
    // Sessizce geç — logout devam etsin
  }
}
