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
    // A 401 is a fact about the session, not about the screen that asked. It is answered here,
    // once, because the alternative is what this file used to do: nothing. Each screen caught
    // its own refusal and printed its own "Erro" with the axios message, and a manager whose
    // token had expired walked from tab to tab collecting the same wall — a ranking that would
    // not open, a table that would not load, nothing anywhere saying that signing in again is
    // the whole of the fix. Every screen answering the same refusal the same way is the same
    // bug wearing eight hats.
    //
    // Ending the session is the honest reading: 401 is what the API says when the bearer token
    // is absent or no longer accepted, and a token it will not accept is not a screen's to
    // retry. Clearing the token is also all the redirect there is — every guarded route reads
    // it through `RequireAuth`, so a store with no token renders the login screen on the next
    // paint. No `window.location`, no router handle, and no second opinion about whether the
    // session is over.
    //
    // Guarded on a token being present, because a 401 with nobody signed in is not news and
    // must not bounce a manager who is already looking at the login screen.
    if (error.response?.status === 401 && useAuthStore.getState().token) {
      useAuthStore.getState().clearAuth();
    }

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
