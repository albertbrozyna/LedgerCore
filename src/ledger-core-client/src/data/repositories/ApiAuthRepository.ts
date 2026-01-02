import type{ AuthRepository, LoginRequest, AuthResponse, RegisterRequest, RegisterResponse, VerifyEmailRequest } from '../../domain/repositories/AuthRepository';
import { apiClient } from '../api/apiClient';
import { AuthMapper } from '../mappers/AuthMapper';
import axios from 'axios';

export class ApiAuthRepository implements AuthRepository {
  
  async login(credentials: LoginRequest): Promise<AuthResponse> {
    try {
      const apiData = AuthMapper.toApiLogin(credentials);

      const response = await apiClient.post('/auth/login', apiData);
      
      return AuthMapper.toDomainAuth(response.data);
    } catch (error: unknown) {
  let errorMessage = 'Coś poszło nie tak';

  // Sprawdzamy bezpiecznie czy to błąd Axiosa
  if (axios.isAxiosError(error)) {
    // Teraz TypeScript wie, że 'error' ma właściwości .response i .data
    errorMessage = error.response?.data?.detail || error.message;
  } else if (error instanceof Error) {
    // Jeśli to zwykły błąd JS (np. błąd składni)
    errorMessage = error.message;
  }

  throw new Error(errorMessage);
}
  }

  async register(data: RegisterRequest): Promise<RegisterResponse> {
    try {
      const apiData = AuthMapper.toApiRegister(data);
      
      const response = await apiClient.post('/auth/register', apiData);
      
      return {
        userId: response.data.UserId 
      };
    } catch (error: any) {
      const errorMessage = error.response?.data?.detail || 'Registration failed';
      throw new Error(errorMessage);
    }
  }



  async verifyEmail(data: VerifyEmailRequest): Promise<void> {

    try {
        const apiData = AuthMapper.toApiVerifyEmail(data);

        await apiClient.post('/auth/verify-email', apiData);

   } catch (error: any) {
      const errorMessage = error.response?.data?.detail || 'Registration failed';
      throw new Error(errorMessage);
    }
  }


  async resendVerificationCode(data: VerifyEmailRequest): Promise<void>{
    try {
        const apiData = AuthMapper.toApiVerifyEmail(data);
        await apiClient.post('/auth/resend-verification-code', apiData);
    } catch (error: any) {
      const errorMessage = error.response?.data?.detail || 'Resend verification code failed';
      throw new Error(errorMessage);
    }
  }


  logout(): void {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
  }
}