import axios from 'axios';
import { API_BASE_URL } from '@/config/env';

import { useAuthStore } from '@/state/auth';

const api = axios.create({
  baseURL: API_BASE_URL,
  timeout: 10000,
});

// Attach the JWT bearer token to every request when the user is logged in. The token
// is read from the auth store at call-time so a refresh that updates the store is
// reflected without a page reload.
api.interceptors.request.use(
  (config) => {
    const token = useAuthStore.getState().token;
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

api.interceptors.response.use(
  (response) => response,
  (error) => {
    const problem = error.response?.data;
    const code = problem?.code ?? problem?.title;
    const detail = problem?.detail;

    if (error.response?.status === 404) {
      return Promise.reject(new Error(detail ?? code ?? 'Resource not found'));
    }

    if (code) {
      return Promise.reject(new Error(`${code}: ${detail ?? error.message}`));
    }

    return Promise.reject(error);
  }
);

export default api;
