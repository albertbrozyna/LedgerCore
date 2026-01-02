export interface UserProps {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  isActive: boolean;
  avatarUrl?: string;
  createdAt: Date;
  lastLogin: Date;
}

export class User {
   public readonly props: UserProps;

  constructor(props: UserProps) {
    this.props = props;
  }

  get fullName(): string {
    return `${this.props.firstName} ${this.props.lastName}`;
  }


  get isOnline(): boolean {
    const fiveMinutesAgo = new Date(Date.now() - 5 * 60 * 1000);
    return this.props.lastLogin > fiveMinutesAgo;
  }
}