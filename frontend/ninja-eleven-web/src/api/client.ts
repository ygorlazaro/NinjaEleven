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

/**
 * A refusal the backend wrote, carrying the two halves of it.
 *
 * <code> is the stable rule name and <code>detail</code> is what the rule says in the words
 * a manager should read. They are kept apart rather than joined into one string because a
 * screen has two different uses for them: the code is what a screen branches on, and the
 * detail is what it puts in front of somebody. A screen that had to cut the code back off
 * the message to get at the sentence would be parsing a string to learn a rule, which is the
 * one thing a stable code exists to stop.
 */
export class ApiProblemError extends Error {
  /** The rule that refused, by its own name — `ValidationFailed`, `ShirtNumberAlreadyTaken`. */
  readonly code: string;

  /** What the rule says, in the words the backend wrote. Null when it said nothing. */
  readonly detail: string | null;

  constructor(code: string, detail: string | null, fallback: string) {
    super(detail ?? code ?? fallback);
    this.name = 'ApiProblemError';
    this.code = code;
    this.detail = detail;
  }
}

api.interceptors.response.use(
  (response) => response,
  (error) => {
    const problem = error.response?.data;
    const code: string | undefined = problem?.code ?? problem?.title;
    const detail: string | undefined = problem?.detail;

    if (code) {
      return Promise.reject(
        new ApiProblemError(code, detail ?? null, error.message)
      );
    }

    // A 404 with no problem body is still a refusal, and one that reaches a screen as a bare
    // axios error is one that renders as "[object Object]" on the screen.
    if (error.response?.status === 404) {
      return Promise.reject(new ApiProblemError('NotFound', null, 'Recurso não encontrado'));
    }

    return Promise.reject(error);
  }
);

export default api;
