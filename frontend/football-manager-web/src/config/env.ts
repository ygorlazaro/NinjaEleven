/**
 * Environment configuration. Every URL used by the frontend comes from here, so there
 * is a single place to point the app at a different backend. The values are read from
 * Vite env variables (`.env`, `.env.local`, or the shell) and fall back to the local
 * development API.
 */
const DEFAULT_API_BASE_URL = 'http://localhost:5100';

function normalizeBaseUrl(value: string | undefined): string {
  const candidate = (value ?? '').trim();
  if (candidate.length === 0) {
    return DEFAULT_API_BASE_URL;
  }

  return candidate.replace(/\/+$/, '');
}

export const API_BASE_URL = normalizeBaseUrl(import.meta.env.VITE_API_BASE_URL);

/**
 * The hub lives on the same host as the API by default. It can be overridden when the
 * SignalR endpoint is published somewhere else.
 */
export const HUB_BASE_URL = normalizeBaseUrl(
  import.meta.env.VITE_HUB_BASE_URL || import.meta.env.VITE_API_BASE_URL
);

export const MATCH_HUB_PATH = '/matchHub';
