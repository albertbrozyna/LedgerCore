import React, { useState } from 'react';
import { useRegister } from '../hooks/useRegister';

export const RegisterPage: React.FC = () => {
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [role, setRole] = useState('User'); // domyślna rola
  const [phoneNumber, setPhoneNumber] = useState('');

  const { register, isLoading, error } = useRegister();

  const handleFormSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    const userId = await register({ firstName, lastName, email, password,role: Number(role), phoneNumber });

    if (userId) {
      console.log('Registration successful, userId:', userId);
      // Przekieruj do login lub dashboard
    }
  };

  return (
    <div className="auth-wrapper">
      <form onSubmit={handleFormSubmit}>
        <h1>Register</h1>

        {error && <div className="alert alert-danger">{error}</div>}

        <input
          type="text"
          value={firstName}
          onChange={(e) => setFirstName(e.target.value)}
          placeholder="First Name"
          required
        />
        <input
          type="text"
          value={lastName}
          onChange={(e) => setLastName(e.target.value)}
          placeholder="Last Name"
          required
        />
        <input
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder="Email"
          required
        />
        <input
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          placeholder="Password"
          required
        />
        <input
          type="text"
          value={role}
          onChange={(e) => setRole(e.target.value)}
          placeholder="Role (User/Admin)"
          required
        />
        <input
          type="text"
          value={phoneNumber}
          onChange={(e) => setPhoneNumber(e.target.value)}
          placeholder="Phone Number"
        />

        <button type="submit" disabled={isLoading}>
          {isLoading ? 'Processing...' : 'Register'}
        </button>
      </form>
    </div>
  );
};
