import { KeyboardAvoidingView, Platform, ScrollView, View, StyleSheet, type ViewStyle } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useTheme } from '@/theme/theme-provider';

interface Props {
  children: React.ReactNode;
  scrollable?: boolean;
  /** İçeriği yatay padding ile sarmala (default true) */
  padded?: boolean;
  style?: ViewStyle;
}

/**
 * Tüm ekranların tek tip kökü:
 * - SafeArea (notch/home indicator)
 * - KeyboardAvoiding (input'lar klavye altında kalmasın)
 * - Tema-aware arka plan
 * - Opsiyonel ScrollView
 *
 * Her ekranda KeyboardAvoidingView yazma derdi gitti.
 */
export function Screen({ children, scrollable = false, padded = true, style }: Props) {
  const { theme, tokens } = useTheme();

  const content = padded
    ? <View style={{ padding: tokens.spacing.lg, flex: scrollable ? undefined : 1 }}>{children}</View>
    : children;

  return (
    <SafeAreaView style={[styles.container, { backgroundColor: theme.colors.background }, style]}>
      <KeyboardAvoidingView
        style={styles.kav}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      >
        {scrollable ? (
          <ScrollView
            contentContainerStyle={{ flexGrow: 1 }}
            keyboardShouldPersistTaps="handled"
            showsVerticalScrollIndicator={false}
          >
            {content}
          </ScrollView>
        ) : (
          content
        )}
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1 },
  kav: { flex: 1 },
});
