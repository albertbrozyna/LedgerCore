import axios from 'axios';

export const apiClient = axios.create({
  baseURL: 'http://localhost:5109/api/v1', // Adres Web API .NET
  headers: {
    'Content-Type': 'application/json',
  },
});

// Opcjonalnie: Interceptor dla tokena
apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});