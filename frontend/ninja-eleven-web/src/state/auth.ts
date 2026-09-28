import { create } from 'zustand';
import { persist } from 'zustand/middleware';

interface AuthState {
  token: string | null;
  userId: string | null;
  email: string | null;
  teamId: string | null;
  coachName: string | null;
  isLoading: boolean;

  setAuth: (token: string, userId: string, email: string, teamId: string | null, coachName: string | null) => void;
  clearAuth: () => void;
  setToken: (token: string | null) => void;
  setTeamId: (teamId: string | null) => void;
  setCoachName: (name: string) => void;
  setIsLoading: (loading: boolean) => void;
}

// The token and the user's identity are persisted so a refresh keeps the manager
// logged in. The whole club choice stays in the game store (useGameState), but the
// fact that *this* browser belongs to *this* account lives here.
export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      token: null,
      userId: null,
      email: null,
      teamId: null,
      coachName: null,
      isLoading: false,

      setAuth: (token, userId, email, teamId, coachName) =>
        set({ token, userId, email, teamId, coachName }),

      clearAuth: () =>
        set({ token: null, userId: null, email: null, teamId: null, coachName: null }),

       setToken: (token) => set({ token }),
      setTeamId: (teamId) => set({ teamId }),
      setCoachName: (name) => set({ coachName: name }),
      setIsLoading: (loading) => set({ isLoading: loading }),
    }),
    {
      name: 'ninja-eleven:auth',
      partialize: (state) => ({
        token: state.token,
        userId: state.userId,
        email: state.email,
        teamId: state.teamId,
        coachName: state.coachName,
      }),
    }
  )
);
