import { View, Text } from 'react-native';
import { useTranslation } from 'react-i18next';
import { Screen } from '@/components/screen';
import { Button } from '@/components/button';
import { useTheme } from '@/theme/theme-provider';
import { useAuthActions } from '@/auth/auth-provider';
import { useAuthStore } from '@/auth/auth-store';

export function AdminHomeScreen() {
  const { t } = useTranslation();
  const { theme, tokens } = useTheme();
  const { logout } = useAuthActions();
  const user = useAuthStore(s => s.user);

  return (
    <Screen scrollable>
      <Text style={{ fontSize: tokens.typography['2xl'].fontSize, fontWeight: tokens.fontWeight.bold, color: theme.colors.text }}>
        Admin Dashboard
      </Text>
      <Text style={{ color: theme.colors.textMuted, marginTop: tokens.spacing.sm }}>
        {user?.email}
      </Text>

      <View style={{ marginTop: tokens.spacing['2xl'] }}>
        <Button variant="danger" onPress={() => void logout()}>
          {t('common.logout')}
        </Button>
      </View>
    </Screen>
  );
}
