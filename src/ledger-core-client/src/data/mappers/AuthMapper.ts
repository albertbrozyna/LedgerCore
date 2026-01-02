import type { AuthResponse, RegisterRequest, LoginRequest, VerifyEmailRequest } from '../../domain/repositories/AuthRepository';

/**
 * AuthMapper handles the translation between .NET PascalCase and Frontend camelCase
 */

export class AuthMapper {

    // Dla logowania: zamieniamy małe litery z frontu na duże dla .NET Command
    static toApiLogin(domainData: LoginRequest): any {
        return {
            Email: domainData.email,
            Password: domainData.password
        };
    }

    static toApiRegister(domainData: RegisterRequest): any {
        return {
            FirstName: domainData.firstName,
            LastName: domainData.lastName,
            Email: domainData.email,
            Password: domainData.password,
            Role: domainData.role,
            PhoneNumber: domainData.phoneNumber
        };
    }

    static toDomainAuth(apiResponse: any): AuthResponse {
        return {
            accessToken: apiResponse.AccessToken,
            refreshToken: apiResponse.RefreshToken,
            userId: apiResponse.UserId
        };
    }

    static toApiVerifyEmail(domainData: VerifyEmailRequest): any {
        return {
            UserId: domainData.userId,
            VerificationCode: domainData.verificationCode
        }
    }
}
