import { Component, type PropsWithChildren, type ReactNode } from 'react';
import { View, Text, TouchableOpacity, StyleSheet } from 'react-native';
import * as Sentry from '@sentry/react-native';

interface State {
  error: Error | null;
}

/**
 * App-wide error boundary. JS hatası tüm uygulamayı çökertmesin.
 * Hata Sentry'e gönderilir (DSN config'te ayarlı ise).
 *
 * Class component zorunlu — RN'de henüz hook-based error boundary yok.
 */
export class AppErrorBoundary extends Component<PropsWithChildren<{ fallback?: ReactNode }>, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: { componentStack?: string | null }) {
    Sentry.captureException(error, { extra: { componentStack: info.componentStack ?? '' } });
  }

  reset = () => this.setState({ error: null });

  render() {
    if (!this.state.error) return this.props.children;

    if (this.props.fallback) return this.props.fallback;

    return (
      <View style={styles.container}>
        <Text style={styles.title}>Beklenmedik bir hata oluştu</Text>
        <Text style={styles.message}>{this.state.error.message}</Text>
        <TouchableOpacity onPress={this.reset} style={styles.button}>
          <Text style={styles.buttonText}>Tekrar Dene</Text>
        </TouchableOpacity>
      </View>
    );
  }
}

const styles = StyleSheet.create({
  container: { flex: 1, justifyContent: 'center', alignItems: 'center', padding: 24, backgroundColor: '#fff' },
  title: { fontSize: 18, fontWeight: '600', marginBottom: 8, color: '#0f172a' },
  message: { fontSize: 14, color: '#475569', textAlign: 'center', marginBottom: 24 },
  button: { paddingHorizontal: 24, paddingVertical: 12, backgroundColor: '#2563eb', borderRadius: 8 },
  buttonText: { color: '#fff', fontWeight: '600' },
});
