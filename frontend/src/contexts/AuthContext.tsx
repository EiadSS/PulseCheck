import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api, clearStoredToken, getStoredToken, storeToken } from '../api/client';
import type { User } from '../types';

interface AuthContextValue {
  user: User | null;
  isLoading: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, password: string, workspaceName: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    let active = true;

    async function loadUser() {
      if (!getStoredToken()) {
        setIsLoading(false);
        return;
      }

      try {
        const currentUser = await api.me();
        if (active) {
          setUser(currentUser);
        }
      } catch {
        clearStoredToken();
      } finally {
        if (active) {
          setIsLoading(false);
        }
      }
    }

    void loadUser();
    return () => {
      active = false;
    };
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isLoading,
      async login(email, password) {
        const response = await api.login(email, password);
        storeToken(response.token);
        setUser(response.user);
      },
      async register(email, password, workspaceName) {
        const response = await api.register(email, password, workspaceName);
        storeToken(response.token);
        setUser(response.user);
      },
      logout() {
        clearStoredToken();
        setUser(null);
      }
    }),
    [isLoading, user]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);

  if (!context) {
    throw new Error('useAuth must be used inside AuthProvider');
  }

  return context;
}
