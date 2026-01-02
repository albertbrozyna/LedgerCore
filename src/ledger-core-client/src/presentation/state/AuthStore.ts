import { create } from 'zustand';
import { User } from '../../domain/entities/User';

/**
 * 1. Define the shape of your store
 */
interface AuthState {
  user: User | null;
  isAuthenticated: boolean;
  setUser: (user: User | null) => void;
  logout: () => void;
}

/**
 * 2. Pass the interface <AuthState> to create()
 * This tells TypeScript what 'set' is allowed to do.
 */
export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  isAuthenticated: false,

  setUser: (user) => 
    set({ 
      user, 
      isAuthenticated: !!user 
    }),

  logout: () => {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    set({ user: null, isAuthenticated: false });
  },
}));