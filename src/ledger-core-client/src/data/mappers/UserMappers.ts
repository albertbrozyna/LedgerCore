import { User } from '../../domain/entities/User';

export class UserMapper {
  static toDomainUser(raw: any): User {
    return new User({
      id: raw.id,
      firstName: raw.firstName,
      lastName: raw.lastName,
      email: raw.email,
      isActive: raw.isActive,
      avatarUrl: raw.avatarUrl,
      createdAt: new Date(raw.createdAt),
      lastLogin: new Date(raw.lastLogin),
    });
  }
}