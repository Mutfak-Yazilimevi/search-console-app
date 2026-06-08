import { NavigationContainer } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { ActivityIndicator, View } from 'react-native';
import { useAuthStore } from '@/auth/auth-store';
import { useTheme } from '@/theme/theme-provider';

// Public ekranlar
import { HomeScreen } from '@/screens/public/home-screen';
import { LoginScreen } from '@/screens/public/login-screen';

// Web (üye) ekranlar
import { ProfileScreen } from '@/screens/web/profile-screen';
import { SettingsScreen } from '@/screens/web/settings-screen';
import { SessionsScreen } from '@/screens/web/sessions-screen';

// Admin ekranlar
import { CustomersScreen } from '@/screens/admin/customers-screen';
import { AdminHomeScreen } from '@/screens/admin/admin-home-screen';

/**
 * Type-safe navigation. Her stack'in param map'i export edilir,
 * useNavigation/useRoute güvenli kullanılır.
 */
export type PublicStackParams = {
  Home: undefined;
  Login: { returnTo?: string } | undefined;
};

export type WebTabsParams = {
  Profile: undefined;
  Sessions: undefined;
  Settings: undefined;
};

export type AdminTabsParams = {
  AdminHome: undefined;
  Customers: undefined;
};

export type RootStackParams = {
  Public: undefined;
  Web: undefined;
  Admin: undefined;
};

const RootStack = createNativeStackNavigator<RootStackParams>();
const PublicStack = createNativeStackNavigator<PublicStackParams>();
const WebTabs = createBottomTabNavigator<WebTabsParams>();
const AdminTabs = createBottomTabNavigator<AdminTabsParams>();

function PublicNavigator() {
  return (
    <PublicStack.Navigator>
      <PublicStack.Screen name="Home" component={HomeScreen} />
      <PublicStack.Screen name="Login" component={LoginScreen} options={{ headerShown: false }} />
    </PublicStack.Navigator>
  );
}

function WebNavigator() {
  return (
    <WebTabs.Navigator>
      <WebTabs.Screen name="Profile" component={ProfileScreen} />
      <WebTabs.Screen name="Sessions" component={SessionsScreen} options={{ title: 'Oturumlar' }} />
      <WebTabs.Screen name="Settings" component={SettingsScreen} />
    </WebTabs.Navigator>
  );
}

function AdminNavigator() {
  return (
    <AdminTabs.Navigator>
      <AdminTabs.Screen name="AdminHome" component={AdminHomeScreen} options={{ title: 'Dashboard' }} />
      <AdminTabs.Screen name="Customers" component={CustomersScreen} />
    </AdminTabs.Navigator>
  );
}

/**
 * ROL TABANLI ROUTER — tek bundle, üç audience.
 *
 * - Login değil → Public stack (anonim ekranlar + Login)
 * - Login, admin değil → Web stack (üye paneli)
 * - Login, admin → Admin stack
 *
 * Kullanıcı admin app'te login olursa otomatik admin stack'e gelir; tek "SearchConsoleApp"
 * indirir, içeride rolüne göre deneyim alır.
 */
export function RootNavigator() {
  const isLoading = useAuthStore(s => s.isLoading);
  const isAuthenticated = useAuthStore(s => s.isAuthenticated);
  const roles = useAuthStore(s => s.roles);
  const { theme } = useTheme();

  if (isLoading) {
    return (
      <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center', backgroundColor: theme.colors.background }}>
        <ActivityIndicator color={theme.colors.primary} />
      </View>
    );
  }

  const isAdmin = roles.includes('admin');

  return (
    <NavigationContainer>
      <RootStack.Navigator screenOptions={{ headerShown: false }}>
        {!isAuthenticated ? (
          <RootStack.Screen name="Public" component={PublicNavigator} />
        ) : isAdmin ? (
          <RootStack.Screen name="Admin" component={AdminNavigator} />
        ) : (
          <RootStack.Screen name="Web" component={WebNavigator} />
        )}
      </RootStack.Navigator>
    </NavigationContainer>
  );
}
