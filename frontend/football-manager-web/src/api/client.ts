import axios from 'axios';

const api = axios.create({
  baseURL: '/api',
  timeout: 10000,
});

api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 404) {
      return Promise.reject(new Error('Resource not found'));
    }
    return Promise.reject(error);
  }
);

export default api;
