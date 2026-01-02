
/**
 * Data required to perform a login
 */
export interface LoginRequest {
  email: string;
  password: string;
}

/**
 * Successful authentication payload from the server
 */
export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  userId: string;
}

/**
 * Interface defining authentication operations
 */
export interface AuthRepository {
  login(credentials: LoginRequest): Promise<AuthResponse>;
  register(data: RegisterRequest): Promise<RegisterResponse>;
  verifyEmail(data:VerifyEmailRequest): Promise<void>;
  resendVerificationCode(data: VerifyEmailRequest): Promise<void>;
  logout(): void;
}

export interface RegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  role: number;       
  phoneNumber: string;
}

export interface RegisterResponse {
  userId: string;
}


// Verify email request
export interface VerifyEmailRequest {
  userId: string;
  verificationCode: string;
}
