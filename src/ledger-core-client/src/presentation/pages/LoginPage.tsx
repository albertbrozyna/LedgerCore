import React, { useState } from 'react';
import { useLogin } from '../hooks/useLogin';

export const LoginPage: React.FC = () => {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const { login, isLoading, error } = useLogin();

  const handleFormSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    // Wywołanie hooka login
    const userId = await login(email, password);
    
    if (userId) {
      console.log('Login successful, userId:', userId);
      // Tutaj możesz przekierować np. do dashboardu
      // navigate('/dashboard') jeśli używasz react-router
    }
  };

  return (
    <div className="auth-wrapper">
      <form onSubmit={handleFormSubmit}>
        <h1>Sign In</h1>
        
        {error && <div className="alert alert-danger">{error}</div>}

        <input 
          type="email" 
          value={email} 
          onChange={(e) => setEmail(e.target.value)} 
          placeholder="Email Address"
          required 
        />
        
        <input 
          type="password" 
          value={password} 
          onChange={(e) => setPassword(e.target.value)} 
          placeholder="Password"
          required 
        />

        <button type="submit" disabled={isLoading}>
          {isLoading ? 'Processing...' : 'Login'}
        </button>
      </form>
    </div>
  );
};
