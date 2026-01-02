import React from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuthStore } from '../state/AuthStore';

/**
 * Global Navigation component
 * Provides links to main pages and displays auth status
 */
export const Navbar: React.FC = () => {
  const { user, logout, isAuthenticated } = useAuthStore();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login'); // Redirect to login after logout
  };

  return (
    <nav className="main-nav">
      <div className="nav-logo">LedgerApp</div>
      <div className="nav-links">
        {isAuthenticated ? (
          <>
            <Link to="/dashboard">Dashboard</Link>\
            <Link to="/register">Register</Link>
            <Link to="/transactions">Transactions</Link>
            <span className="user-info">Hello, {user?.props.firstName}</span>
            <button onClick={handleLogout} className="logout-btn">Logout</button>
          </>
        ) : (
          <Link to="/login">Login</Link>
        )}
      </div>
    </nav>
  );
};