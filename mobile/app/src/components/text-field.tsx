import { useState } from 'react';
import { View, Text, TextInput, type TextInputProps, StyleSheet } from 'react-native';
import { useTheme } from '@/theme/theme-provider';

interface Props extends Omit<TextInputProps, 'style'> {
  label?: string;
  error?: string;
  hint?: string;
}

/**
 * Form input. react-hook-form Controller içinde kullanılır:
 *
 *   <Controller
 *     control={control}
 *     name="email"
 *     render={({ field, fieldState }) => (
 *       <TextField
 *         label="Email"
 *         value={field.value}
 *         onChangeText={field.onChange}
 *         onBlur={field.onBlur}
 *         error={fieldState.error?.message}
 *       />
 *     )}
 *   />
 */
export function TextField({ label, error, hint, ...inputProps }: Props) {
  const { theme, tokens } = useTheme();
  const [focused, setFocused] = useState(false);

  const borderColor = error
    ? theme.colors.danger
    : focused
      ? theme.colors.primary
      : theme.colors.border;

  return (
    <View style={{ marginBottom: tokens.spacing.md }}>
      {label && (
        <Text style={{ color: theme.colors.textMuted, fontSize: tokens.typography.sm.fontSize, marginBottom: tokens.spacing.xs }}>
          {label}
        </Text>
      )}
      <TextInput
        {...inputProps}
        onFocus={(e) => { setFocused(true); inputProps.onFocus?.(e); }}
        onBlur={(e) => { setFocused(false); inputProps.onBlur?.(e); }}
        placeholderTextColor={theme.colors.textSubtle}
        style={[
          styles.input,
          {
            borderColor,
            borderRadius: theme.radius.sm,
            color: theme.colors.text,
            backgroundColor: theme.colors.background,
            paddingHorizontal: tokens.spacing.md,
            paddingVertical: tokens.spacing.sm,
            fontSize: tokens.typography.base.fontSize,
          },
        ]}
      />
      {error ? (
        <Text style={{ color: theme.colors.danger, fontSize: tokens.typography.xs.fontSize, marginTop: tokens.spacing.xs }}>
          {error}
        </Text>
      ) : hint ? (
        <Text style={{ color: theme.colors.textSubtle, fontSize: tokens.typography.xs.fontSize, marginTop: tokens.spacing.xs }}>
          {hint}
        </Text>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  input: { borderWidth: 1 },
});
