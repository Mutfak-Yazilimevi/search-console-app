import { useCallback } from 'react';
import { FlatList, View, Text, Alert, RefreshControl, ActivityIndicator, TouchableOpacity } from 'react-native';
import { useTranslation } from 'react-i18next';
import { Screen } from '@/components/screen';
import { Button } from '@/components/button';
import { useTheme } from '@/theme/theme-provider';
import {
  useActiveSessions,
  useRevokeSession,
  useRevokeOtherSessions,
  type SessionDto,
} from '@/api/sessions-queries';

/**
 * "Oturumlarım" ekranı.
 * - Aktif oturumları listeler
 * - "Bu cihaz" işareti (isCurrent)
 * - Tek oturum revoke
 * - Diğer tüm oturumları revoke
 */
export function SessionsScreen() {
  const { t } = useTranslation();
  const { theme, tokens } = useTheme();
  const { data: sessions = [], isLoading, isError, isRefetching, refetch } = useActiveSessions();
  const revoke = useRevokeSession();
  const revokeOthers = useRevokeOtherSessions();

  const handleRevoke = useCallback((s: SessionDto) => {
    Alert.alert(
      'Oturumu kapat',
      `${shortenUa(s.userAgent)} oturumu kapatılacak.`,
      [
        { text: t('common.cancel'), style: 'cancel' },
        {
          text: 'Kapat',
          style: 'destructive',
          onPress: () => revoke.mutate(s.id),
        },
      ]
    );
  }, [revoke, t]);

  const handleRevokeOthers = useCallback(() => {
    Alert.alert(
      'Diğer oturumlardan çık',
      'Bu cihaz hariç tüm aktif oturumlar kapatılacak. Devam edilsin mi?',
      [
        { text: t('common.cancel'), style: 'cancel' },
        {
          text: 'Devam',
          style: 'destructive',
          onPress: () => revokeOthers.mutate(),
        },
      ]
    );
  }, [revokeOthers, t]);

  if (isLoading) {
    return <Screen><ActivityIndicator color={theme.colors.primary} /></Screen>;
  }

  if (isError) {
    return (
      <Screen>
        <Text style={{ color: theme.colors.danger }}>Oturumlar yüklenemedi.</Text>
      </Screen>
    );
  }

  return (
    <Screen padded={false}>
      <View style={{ padding: tokens.spacing.lg }}>
        <Text style={{
          fontSize: tokens.typography['2xl'].fontSize,
          fontWeight: tokens.fontWeight.bold,
          color: theme.colors.text
        }}>
          Aktif Oturumlar
        </Text>
        {sessions.length > 1 && (
          <View style={{ marginTop: tokens.spacing.md }}>
            <Button variant="secondary" onPress={handleRevokeOthers}>
              Diğer Cihazlardan Çık
            </Button>
          </View>
        )}
      </View>

      <FlatList
        data={sessions}
        keyExtractor={(s) => String(s.id)}
        refreshControl={
          <RefreshControl
            refreshing={isRefetching}
            onRefresh={refetch}
            tintColor={theme.colors.primary}
          />
        }
        renderItem={({ item }) => (
          <View style={{
            padding: tokens.spacing.lg,
            borderBottomWidth: 1,
            borderBottomColor: theme.colors.border,
            backgroundColor: item.isCurrent ? theme.colors.surface : 'transparent',
          }}>
            <View style={{ flexDirection: 'row', alignItems: 'center', flexWrap: 'wrap' }}>
              <Text style={{
                color: theme.colors.text,
                fontWeight: tokens.fontWeight.medium,
                marginRight: tokens.spacing.sm
              }}>
                {shortenUa(item.userAgent)}
              </Text>
              {item.isCurrent && (
                <View style={{
                  backgroundColor: theme.colors.primary,
                  paddingHorizontal: tokens.spacing.sm,
                  paddingVertical: 2,
                  borderRadius: tokens.iconSize.sm,
                }}>
                  <Text style={{
                    color: theme.colors.primaryForeground,
                    fontSize: tokens.typography.xs.fontSize
                  }}>
                    Bu cihaz
                  </Text>
                </View>
              )}
            </View>

            <Text style={{
              color: theme.colors.textMuted,
              fontSize: tokens.typography.xs.fontSize,
              marginTop: tokens.spacing.xs,
            }}>
              {item.ipAddress}{item.ipCountry ? ` · ${item.ipCountry}` : ''}
            </Text>
            <Text style={{
              color: theme.colors.textSubtle,
              fontSize: tokens.typography.xs.fontSize,
              marginTop: 2,
            }}>
              Son aktivite: {formatDate(item.lastActivityUtc)}
            </Text>

            {!item.isCurrent && (
              <TouchableOpacity
                onPress={() => handleRevoke(item)}
                style={{ marginTop: tokens.spacing.sm }}
              >
                <Text style={{
                  color: theme.colors.danger,
                  fontSize: tokens.typography.sm.fontSize
                }}>
                  Bu oturumu kapat
                </Text>
              </TouchableOpacity>
            )}
          </View>
        )}
        ListEmptyComponent={
          <Text style={{
            color: theme.colors.textMuted,
            textAlign: 'center',
            padding: tokens.spacing.xl,
          }}>
            Aktif oturum yok.
          </Text>
        }
      />
    </Screen>
  );
}

function shortenUa(ua: string | undefined): string {
  if (!ua) return 'Bilinmeyen cihaz';
  const browser = ua.match(/(?:Chrome|Firefox|Safari|Edge|Opera)\/[\d.]+/)?.[0];
  const os = ua.match(/(?:Windows|Macintosh|Linux|Android|iPhone|iPad)/)?.[0];
  const summary = [browser, os].filter(Boolean).join(' · ');
  return summary || ua.substring(0, 50);
}

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleString();
  } catch {
    return iso;
  }
}
