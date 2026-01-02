import { useState } from 'react';
import { ApiAuthRepository } from '../../data/repositories/ApiAuthRepository'
import type { RegisterRequest, RegisterResponse } from '../../domain/repositories/AuthRepository';

export const useRegister = () => {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const register = async (data: RegisterRequest): Promise<string | null> => {
    setIsLoading(true);
    setError(null);

    try {
      const repo = new ApiAuthRepository();
      const result: RegisterResponse = await repo.register(data);
      return result.userId;
    } catch (err: any) {
      setError(err.message || 'Unknown error');
      return null;
    } finally {
      setIsLoading(false);
    }
  };

  return { register, isLoading, error };
};
