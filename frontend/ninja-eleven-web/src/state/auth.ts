import { create } from 'zustand';
import { persist } from 'zustand/middleware';

interface AuthState {
  token: string | null;
  userId: string | null;
  email: string | null;
  teamId: string | null;
  coachName: string | null;
  isLoading: boolean;

  /**
   * Set by a sign-in that brought a dismissed account back, and cleared by the club page once
   * the manager has read it. It is here rather than in the login screen's own state because the
   * news is about the club page: the manager lands on a different club from the one he left,
   * and the page he lands on is where that has to be said.
   */
  returnedAfterDismissal: boolean;

  setAuth: (token: string, userId: string, email: string, teamId: string | null, coachName: string | null) => void;
  clearAuth: () => void;
  setToken: (token: string | null) => void;
  setTeamId: (teamId: string | null) => void;
  setCoachName: (name: string) => void;
  setIsLoading: (loading: boolean) => void;
  markReturnedAfterDismissal: () => void;
  clearReturnedAfterDismissal: () => void;
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
      returnedAfterDismissal: false,

      setAuth: (token, userId, email, teamId, coachName) =>
        set({ token, userId, email, teamId, coachName }),

      clearAuth: () =>
        set({
          token: null,
          userId: null,
          email: null,
          teamId: null,
          coachName: null,
          returnedAfterDismissal: false,
        }),

       setToken: (token) => set({ token }),
      setTeamId: (teamId) => set({ teamId }),
      setCoachName: (name) => set({ coachName: name }),
      setIsLoading: (loading) => set({ isLoading: loading }),

      markReturnedAfterDismissal: () => set({ returnedAfterDismissal: true }),
      clearReturnedAfterDismissal: () => set({ returnedAfterDismissal: false }),
    }),
    {
      name: 'ninja-eleven:auth',
      partialize: (state) => ({
        token: state.token,
        userId: state.userId,
        email: state.email,
        teamId: state.teamId,
        coachName: state.coachName,
        // Deliberately not persisted. A manager who read this on the day it was news should
        // not meet it again every morning for ever, and a manager who never logged in again
        // does not need to be told about a dismissal he has not been shown.
        returnedAfterDismissal: false,
      }),
    }
  )
);
