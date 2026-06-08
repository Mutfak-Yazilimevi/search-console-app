import { View, Text, TouchableOpacity, ScrollView } from 'react-native';
import { useTranslation } from 'react-i18next';
import { Screen } from '@/components/screen';
import { useTheme } from '@/theme/theme-provider';

export function SettingsScreen() {
  const { t, i18n } = useTranslation();
  const { theme, tokens, availableThemes, setTheme, setSystemMode } = useTheme();

  return (
    <Screen scrollable>
      <Text style={{ fontSize: tokens.typography['2xl'].fontSize, fontWeight: tokens.fontWeight.bold, color: theme.colors.text }}>
        {t('navigation.settings')}
      </Text>

      {/* Tema */}
      <Section title={t('profile.theme')}>
        <ScrollView horizontal showsHorizontalScrollIndicator={false}>
          <ThemeChip label={t('profile.systemTheme')} active={false} onPress={() => void setSystemMode()} />
          {availableThemes.map((t) => (
            <ThemeChip
              key={t.name}
              label={t.displayName}
              active={t.name === theme.name}
              onPress={() => void setTheme(t)}
            />
          ))}
        </ScrollView>
      </Section>

      {/* Dil */}
      <Section title={t('profile.language')}>
        <View style={{ flexDirection: 'row', gap: tokens.spacing.sm }}>
          <LangChip label="English" active={i18n.language === 'en'} onPress={() => void i18n.changeLanguage('en')} />
          <LangChip label="Türkçe" active={i18n.language === 'tr'} onPress={() => void i18n.changeLanguage('tr')} />
        </View>
      </Section>
    </Screen>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  const { theme, tokens } = useTheme();
  return (
    <View style={{ marginTop: tokens.spacing.xl }}>
      <Text style={{ color: theme.colors.text, fontSize: tokens.typography.lg.fontSize, fontWeight: tokens.fontWeight.semibold, marginBottom: tokens.spacing.sm }}>
        {title}
      </Text>
      {children}
    </View>
  );
}

function ThemeChip({ label, active, onPress }: { label: string; active: boolean; onPress: () => void }) {
  const { theme, tokens } = useTheme();
  return (
    <TouchableOpacity
      onPress={onPress}
      style={{
        paddingHorizontal: tokens.spacing.lg,
        paddingVertical: tokens.spacing.sm,
        backgroundColor: active ? theme.colors.primary : theme.colors.surface,
        borderColor: active ? theme.colors.primary : theme.colors.border,
        borderWidth: 1,
        borderRadius: theme.radius.full,
        marginRight: tokens.spacing.sm,
      }}
    >
      <Text style={{ color: active ? theme.colors.primaryForeground : theme.colors.text }}>{label}</Text>
    </TouchableOpacity>
  );
}

function LangChip({ label, active, onPress }: { label: string; active: boolean; onPress: () => void }) {
  return <ThemeChip label={label} active={active} onPress={onPress} />;
}
