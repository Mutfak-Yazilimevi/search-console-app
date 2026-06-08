import { View, Text, ActivityIndicator } from 'react-native';
import { useTranslation } from 'react-i18next';
import { Screen } from '@/components/screen';
import { Button } from '@/components/button';
import { useTheme } from '@/theme/theme-provider';
import { useWebMe } from '@/api/queries';
import { useAuthActions } from '@/auth/auth-provider';

export function ProfileScreen() {
  const { t } = useTranslation();
  const { theme, tokens } = useTheme();
  const { logout } = useAuthActions();
  const { data, isLoading, isError, refetch } = useWebMe();

  return (
    <Screen scrollable>
      <Text style={{ fontSize: tokens.typography['2xl'].fontSize, fontWeight: tokens.fontWeight.bold, color: theme.colors.text }}>
        {t('profile.title')}
      </Text>

      {isLoading && <ActivityIndicator color={theme.colors.primary} style={{ marginTop: tokens.spacing.xl }} />}

      {isError && (
        <View style={{ marginTop: tokens.spacing.xl }}>
          <Text style={{ color: theme.colors.danger, marginBottom: tokens.spacing.md }}>
            {t('errors.generic')}
          </Text>
          <Button variant="secondary" onPress={() => void refetch()}>
            {t('common.retry')}
          </Button>
        </View>
      )}

      {data && (
        <View style={{ marginTop: tokens.spacing.xl }}>
          <Row label={t('profile.name')} value={`${data.firstName ?? ''} ${data.lastName ?? ''}`} />
          <Row label={t('profile.email')} value={data.email} />
          <Row label={t('profile.status')} value={data.active ? t('profile.active') : t('profile.inactive')} />
        </View>
      )}

      <View style={{ marginTop: tokens.spacing['2xl'] }}>
        <Button variant="danger" onPress={() => void logout()} fullWidth>
          {t('common.logout')}
        </Button>
      </View>
    </Screen>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  const { theme, tokens } = useTheme();
  return (
    <View style={{ paddingVertical: tokens.spacing.sm, borderBottomWidth: 1, borderBottomColor: theme.colors.border }}>
      <Text style={{ color: theme.colors.textMuted, fontSize: tokens.typography.xs.fontSize }}>{label}</Text>
      <Text style={{ color: theme.colors.text, fontSize: tokens.typography.base.fontSize, marginTop: 2 }}>{value}</Text>
    </View>
  );
}
