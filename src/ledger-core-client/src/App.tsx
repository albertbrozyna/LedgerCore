import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { LoginPage } from './presentation/pages/LoginPage';
import { Navbar } from './presentation/components/NavBar';
import { useAuthStore } from './presentation/state/AuthStore';
import { RegisterPage } from './presentation/pages/RegisterPage';

/**
 * Main Application Router
 * Defines the mapping between URLs and Page components
 */
function App() {
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);

  return (
    <Router>
      <Navbar /> {/* Navbar is visible on all pages */}
      
      <main className="content-container">
        <Routes>
          {/* Public Route */}
          <Route path="/login" element={!isAuthenticated ? <LoginPage /> : <Navigate to="/dashboard" />} />
          <Route path="/register" element={!isAuthenticated ? <RegisterPage /> : <Navigate to="/dashboard" />} />


          {/* Private Routes (Mocked for now) */}
          <Route 
            path="/dashboard" 
            element={isAuthenticated ? <div>Dashboard Page</div> : <Navigate to="/login" />} 
          />
          {/* Default Redirect */}
          <Route path="/" element={<Navigate to={isAuthenticated ? "/dashboard" : "/login"} />} />
        </Routes>
      </main>
    </Router>
  );
}

export default App;