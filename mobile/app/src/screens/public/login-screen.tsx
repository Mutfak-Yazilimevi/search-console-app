import { useState } from 'react';
import { View, Text } from 'react-native';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslation } from 'react-i18next';
import { Screen } from '@/components/screen';
import { Button } from '@/components/button';
import { TextField } from '@/components/text-field';
import { useTheme } from '@/theme/theme-provider';
import { useAuthActions } from '@/auth/auth-provider';

/**
 * Form validation: zod schema → react-hook-form resolver.
 * i18n translation key'leri schema'da kullanılır.
 *
 * Validation kuralları backend ile aynı tutulmalı:
 *   - email RFC compliant
 *   - password min 8 char
 */
function createLoginSchema(t: (key: string) => string) {
  return z.object({
    email: z.string()
      .min(1, t('auth.emailRequired'))
      .email(t('auth.emailInvalid')),
    password: z.string()
      .min(1, t('auth.passwordRequired'))
      .min(8, t('auth.passwordTooShort')),
  });
}

type LoginFormValues = z.infer<ReturnType<typeof createLoginSchema>>;

export function LoginScreen() {
  const { t } = useTranslation();
  const { theme, tokens } = useTheme();
  const { login, loginWithTwoFactor } = useAuthActions();
  const [submitError, setSubmitError] = useState<string | null>(null);

  // 2FA state — null = normal login, string = preAuthToken bekleniyor
  const [preAuthToken, setPreAuthToken] = useState<string | null>(null);
  const [twoFactorCode, setTwoFactorCode] = useState('');
  const [useRecoveryCode, setUseRecoveryCode] = useState(false);
  const [twoFactorLoading, setTwoFactorLoading] = useState(false);

  const { control, handleSubmit, formState: { isSubmitting } } = useForm<LoginFormValues>({
    resolver: zodResolver(createLoginSchema(t)),
    defaultValues: { email: '', password: '' },
  });

  const onSubmit = async (values: LoginFormValues) => {
    setSubmitError(null);
    try {
      const result = await login(values);
      if (result.type === 'two_factor_required') {
        setPreAuthToken(result.preAuthToken);
      }
      // type === 'success' → RootNavigator otomatik geçer
    } catch (err) {
      setSubmitError(err instanceof Error ? err.message : t('auth.loginFailed'));
    }
  };

  const submitTwoFactor = async () => {
    if (!preAuthToken || !twoFactorCode.trim()) return;
    setTwoFactorLoading(true);
    setSubmitError(null);
    try {
      await loginWithTwoFactor(preAuthToken, twoFactorCode.trim(), useRecoveryCode);
      // Login başarılı → RootNavigator devralır
    } catch (err) {
      setSubmitError(err instanceof Error ? err.message : t('auth.loginFailed'));
    } finally {
      setTwoFactorLoading(false);
    }
  };

  // 2FA ekranı
  if (preAuthToken) {
    return (
      <Screen scrollable>
        <View style={{ marginTop: tokens.spacing['2xl'] }}>
          <Text style={{
            fontSize: tokens.typography['2xl'].fontSize,
            fontWeight: tokens.fontWeight.bold,
            color: theme.colors.text,
            marginBottom: tokens.spacing.md,
          }}>
            {t('twoFactor.title')}
          </Text>
          <Text style={{
            color: theme.colors.textMuted,
            marginBottom: tokens.spacing.lg,
          }}>
            {useRecoveryCode ? t('twoFactor.useRecovery') : t('twoFactor.code')}
          </Text>

          <TextField
            label={t('twoFactor.code')}
            value={twoFactorCode}
            onChangeText={setTwoFactorCode}
            keyboardType={useRecoveryCode ? 'default' : 'number-pad'}
            autoCapitalize="none"
          />

          {submitError && (
            <Text style={{ color: theme.colors.danger, marginBottom: tokens.spacing.md }}>
              {submitError}
            </Text>
          )}

          <Button onPress={submitTwoFactor} loading={twoFactorLoading} fullWidth>
            {t('auth.loginButton')}
          </Button>

          <View style={{ marginTop: tokens.spacing.md }}>
            <Button
              variant="ghost"
              onPress={() => {
                setUseRecoveryCode(!useRecoveryCode);
                setTwoFactorCode('');
                setSubmitError(null);
              }}
            >
              {useRecoveryCode ? t('twoFactor.code') : t('twoFactor.useRecovery')}
            </Button>
          </View>
        </View>
      </Screen>
    );
  }

  // Normal login ekranı

  return (
    <Screen scrollable>
      <View style={{ marginTop: tokens.spacing['2xl'] }}>
        <Text style={{ fontSize: tokens.typography['2xl'].fontSize, fontWeight: tokens.fontWeight.bold, color: theme.colors.text, marginBottom: tokens.spacing.lg }}>
          {t('auth.login')}
        </Text>

        <Controller
          control={control}
          name="email"
          render={({ field, fieldState }) => (
            <TextField
              label={t('auth.email')}
              value={field.value}
              onChangeText={field.onChange}
              onBlur={field.onBlur}
              error={fieldState.error?.message}
              keyboardType="email-address"
              autoCapitalize="none"
              autoComplete="email"
            />
          )}
        />

        <Controller
          control={control}
          name="password"
          render={({ field, fieldState }) => (
            <TextField
              label={t('auth.password')}
              value={field.value}
              onChangeText={field.onChange}
              onBlur={field.onBlur}
              error={fieldState.error?.message}
              secureTextEntry
              autoComplete="password"
            />
          )}
        />

        {submitError && (
          <Text style={{ color: theme.colors.danger, marginBottom: tokens.spacing.md }}>
            {submitError}
          </Text>
        )}

        <Button onPress={handleSubmit(onSubmit)} loading={isSubmitting} fullWidth>
          {t('auth.loginButton')}
        </Button>
      </View>
    </Screen>
  );
}
