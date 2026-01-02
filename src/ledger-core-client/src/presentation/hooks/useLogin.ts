import { useState } from 'react';
import { ApiAuthRepository } from '../../data/repositories/ApiAuthRepository';
import type { AuthResponse } from '../../domain/repositories/AuthRepository';

export const useLogin = () => {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const login = async (email: string, password: string): Promise<string | null> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const repo = new ApiAuthRepository();
      const result: AuthResponse = await repo.login({ Email, Password });

      // Zapisujemy tokeny w localStorage
      localStorage.setItem('accessToken', result.AccessToken);
      localStorage.setItem('refreshToken', result.RefreshToken);

      // Zwracamy userId (możesz też zwracać cały obiekt result jeśli chcesz)
      return result.userId;
    } catch (err: any) {
      setError(err.message || 'Unknown error');
      return null;
    } finally {
      setIsLoading(false);
    }
  };

  return { login, isLoading, error };
};
