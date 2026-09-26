import axios from 'axios';
import { API_BASE_URL } from '@/config/env';

const api = axios.create({
  baseURL: API_BASE_URL,
  timeout: 10000,
});

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
