import { TouchableOpacity, Text, ActivityIndicator, StyleSheet, type ViewStyle } from 'react-native';
import { useTheme } from '@/theme/theme-provider';

export type ButtonVariant = 'primary' | 'secondary' | 'danger' | 'ghost';
export type ButtonSize = 'sm' | 'md' | 'lg';

interface Props {
  onPress?: () => void;
  variant?: ButtonVariant;
  size?: ButtonSize;
  disabled?: boolean;
  loading?: boolean;
  fullWidth?: boolean;
  children: React.ReactNode;
  style?: ViewStyle;
}

export function Button({
  onPress,
  variant = 'primary',
  size = 'md',
  disabled = false,
  loading = false,
  fullWidth = false,
  children,
  style,
}: Props) {
  const { theme, tokens } = useTheme();

  const sizeStyles = {
    sm: { paddingVertical: tokens.spacing.xs, paddingHorizontal: tokens.spacing.md, fontSize: tokens.typography.sm.fontSize },
    md: { paddingVertical: tokens.spacing.sm, paddingHorizontal: tokens.spacing.lg, fontSize: tokens.typography.base.fontSize },
    lg: { paddingVertical: tokens.spacing.md, paddingHorizontal: tokens.spacing.xl, fontSize: tokens.typography.lg.fontSize },
  }[size];

  const variantStyles = (() => {
    switch (variant) {
      case 'primary':
        return { backgroundColor: theme.colors.primary, borderColor: theme.colors.primary, color: theme.colors.primaryForeground };
      case 'danger':
        return { backgroundColor: theme.colors.danger, borderColor: theme.colors.danger, color: '#fff' };
      case 'secondary':
        return { backgroundColor: theme.colors.surface, borderColor: theme.colors.border, color: theme.colors.text };
      case 'ghost':
        return { backgroundColor: 'transparent', borderColor: 'transparent', color: theme.colors.text };
    }
  })();

  const isDisabled = disabled || loading;

  return (
    <TouchableOpacity
      onPress={onPress}
      disabled={isDisabled}
      activeOpacity={0.7}
      style={[
        styles.button,
        {
          backgroundColor: variantStyles.backgroundColor,
          borderColor: variantStyles.borderColor,
          borderRadius: theme.radius.md,
          paddingVertical: sizeStyles.paddingVertical,
          paddingHorizontal: sizeStyles.paddingHorizontal,
          opacity: isDisabled ? 0.5 : 1,
          alignSelf: fullWidth ? 'stretch' : 'flex-start',
        },
        style,
      ]}
    >
      {loading ? (
        <ActivityIndicator color={variantStyles.color} size="small" />
      ) : (
        <Text style={{ color: variantStyles.color, fontSize: sizeStyles.fontSize, fontWeight: tokens.fontWeight.semibold }}>
          {children}
        </Text>
      )}
    </TouchableOpacity>
  );
}

const styles = StyleSheet.create({
  button: { borderWidth: 1, alignItems: 'center', justifyContent: 'center' },
});
