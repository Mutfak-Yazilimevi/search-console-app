import { View, Text } from 'react-native';
import { useNavigation } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useTranslation } from 'react-i18next';
import { Screen } from '@/components/screen';
import { Button } from '@/components/button';
import { useTheme } from '@/theme/theme-provider';
import type { PublicStackParams } from '@/navigation/root-navigator';

type Nav = NativeStackNavigationProp<PublicStackParams, 'Home'>;

export function HomeScreen() {
  const { t } = useTranslation();
  const { theme, tokens } = useTheme();
  const nav = useNavigation<Nav>();

  return (
    <Screen scrollable>
      <Text style={{ fontSize: tokens.typography['2xl'].fontSize, fontWeight: tokens.fontWeight.bold, color: theme.colors.text }}>
        {t('common.appName')}
      </Text>
      <Text style={{ fontSize: tokens.typography.base.fontSize, color: theme.colors.textMuted, marginTop: tokens.spacing.sm }}>
        Anonim ana sayfa. Üye girişine devam etmek için aşağıya tıklayın.
      </Text>
      <View style={{ marginTop: tokens.spacing.xl }}>
        <Button onPress={() => nav.navigate('Login')} fullWidth>
          {t('auth.login')}
        </Button>
      </View>
    </Screen>
  );
}
