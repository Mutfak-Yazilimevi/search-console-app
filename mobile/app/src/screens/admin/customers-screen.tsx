import { useCallback } from 'react';
import { FlatList, View, Text, RefreshControl, ActivityIndicator, TouchableOpacity, Alert } from 'react-native';
import { useTranslation } from 'react-i18next';
import { Screen } from '@/components/screen';
import { useTheme } from '@/theme/theme-provider';
import { useAdminCustomers, useDeleteAdminCustomer } from '@/api/queries';
import type { Customer } from '@/models';

const ITEM_HEIGHT = 72; // sabit, getItemLayout için

export function CustomersScreen() {
  const { t } = useTranslation();
  const { theme, tokens } = useTheme();
  const { data: customers = [], isLoading, isRefetching, refetch, isError } = useAdminCustomers();
  const deleteCustomer = useDeleteAdminCustomer();

  const handleDelete = useCallback((c: Customer) => {
    Alert.alert(
      t('common.delete'),
      `${c.email}?`,
      [
        { text: t('common.cancel'), style: 'cancel' },
        { text: t('common.delete'), style: 'destructive', onPress: () => deleteCustomer.mutate(c.entityId) },
      ]
    );
  }, [deleteCustomer, t]);

  const renderItem = useCallback(({ item }: { item: Customer }) => (
    <View style={{
      height: ITEM_HEIGHT,
      paddingHorizontal: tokens.spacing.lg,
      flexDirection: 'row',
      alignItems: 'center',
      borderBottomWidth: 1,
      borderBottomColor: theme.colors.border,
    }}>
      <View style={{ flex: 1 }}>
        <Text style={{ color: theme.colors.text, fontWeight: tokens.fontWeight.medium }}>{item.email}</Text>
        <Text style={{ color: theme.colors.textMuted, fontSize: tokens.typography.xs.fontSize, marginTop: 2 }}>
          {item.firstName} {item.lastName} · {item.active ? t('profile.active') : t('profile.inactive')}
        </Text>
      </View>
      <TouchableOpacity onPress={() => handleDelete(item)}>
        <Text style={{ color: theme.colors.danger, fontWeight: tokens.fontWeight.medium }}>
          {t('common.delete')}
        </Text>
      </TouchableOpacity>
    </View>
  ), [theme, tokens, handleDelete, t]);

  if (isLoading) {
    return (
      <Screen>
        <ActivityIndicator color={theme.colors.primary} />
      </Screen>
    );
  }

  if (isError) {
    return (
      <Screen>
        <Text style={{ color: theme.colors.danger }}>{t('errors.generic')}</Text>
      </Screen>
    );
  }

  return (
    <Screen padded={false}>
      <View style={{ padding: tokens.spacing.lg }}>
        <Text style={{ fontSize: tokens.typography['2xl'].fontSize, fontWeight: tokens.fontWeight.bold, color: theme.colors.text }}>
          {t('navigation.customers')}
        </Text>
      </View>
      <FlatList
        data={customers}
        keyExtractor={c => c.entityId}
        renderItem={renderItem}
        // === Performance optimizasyonları ===
        getItemLayout={(_, index) => ({ length: ITEM_HEIGHT, offset: ITEM_HEIGHT * index, index })}
        windowSize={10}
        initialNumToRender={10}
        maxToRenderPerBatch={10}
        removeClippedSubviews
        refreshControl={
          <RefreshControl
            refreshing={isRefetching}
            onRefresh={() => void refetch()}
            tintColor={theme.colors.primary}
          />
        }
        ListEmptyComponent={
          <Text style={{ color: theme.colors.textMuted, textAlign: 'center', padding: tokens.spacing.xl }}>
            {t('common.empty')}
          </Text>
        }
      />
    </Screen>
  );
}
