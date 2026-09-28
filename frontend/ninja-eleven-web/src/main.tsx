import React from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter, Navigate, Route, Routes, useLocation } from 'react-router-dom';
import { useGameState } from '@/state';
import { useAuthStore } from '@/state/auth';
import { ProfileProvider } from '@/state/ProfileProvider';
import { OfferProvider } from '@/state/OfferProvider';
import AppShell from '@/components/Common/AppShell';
import AuthRoot from '@/components/Auth/AuthRoot';
import LoginScreen from '@/pages/LoginScreen';
import RegisterScreen from '@/pages/RegisterScreen';
import StartScreen from '@/pages/StartScreen';
import LeagueScreen from '@/pages/LeagueScreen';
import CupScreen from '@/pages/CupScreen';
import LineupScreen from '@/pages/LineupScreen';
import TeamViewScreen from '@/pages/TeamViewScreen';
import MatchScreen from '@/pages/MatchScreen';
import CalendarScreen from '@/pages/CalendarScreen';
import FinanceiroScreen from '@/pages/FinanceiroScreen';
import ClubScreen from '@/pages/ClubScreen';
import StadiumScreen from '@/pages/StadiumScreen';
import SponsorsScreen from '@/pages/SponsorsScreen';
import ScorersScreen from '@/pages/ScorersScreen';
import PlayerProfileScreen from '@/pages/PlayerProfileScreen';
import TransferScreen from '@/pages/TransferScreen';
import '@/styles.css';

/**
 * Guards a route behind an authenticated account. A user without a token is sent to
 * the login screen; the team check is owned by the AuthRoot at `/`.
 */
function RequireAuth({ children }: { children: React.ReactNode }) {
  const token = useAuthStore((s) => s.token);
  const location = useLocation();

  if (!token) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  return children;
}

function App() {
  // The logo and the two places a manager goes live in the column on the left, so the
  // screen has the whole width to itself.
  return (
    <AppShell>
      <Routes>
        <Route path="/login" element={<LoginScreen />} />
        <Route path="/register" element={<RegisterScreen />} />
        {/* The root is auth-aware: login screen when logged out, the club selector
            or the manager's team otherwise. */}
        <Route path="/" element={<AuthRoot />} />
        <Route
          path="/league"
          element={<RequireAuth><LeagueScreen /></RequireAuth>}
        />
        <Route
          path="/copa"
          element={<RequireAuth><CupScreen /></RequireAuth>}
        />
        <Route
          path="/calendar"
          element={<RequireAuth><CalendarScreen /></RequireAuth>}
        />
        <Route
          path="/financeiro"
          element={<RequireAuth><FinanceiroScreen /></RequireAuth>}
        />
        <Route
          path="/club"
          element={<RequireAuth><ClubScreen /></RequireAuth>}
        />
        <Route
          path="/estadio"
          element={<RequireAuth><StadiumScreen /></RequireAuth>}
        />
        <Route
          path="/patrocinadores"
          element={<RequireAuth><SponsorsScreen /></RequireAuth>}
        />
        <Route
          path="/artilheiros"
          element={<RequireAuth><ScorersScreen /></RequireAuth>}
        />
        <Route
          path="/transfer"
          element={<RequireAuth><TransferScreen /></RequireAuth>}
        />
        <Route
          path="/match/lineup/:fixtureId"
          element={<RequireAuth><LineupScreen /></RequireAuth>}
        />
        <Route
          path="/team/:teamId"
          element={<RequireAuth><TeamViewScreen /></RequireAuth>}
        />
        <Route
          path="/player/:playerId"
          element={<RequireAuth><PlayerProfileScreen /></RequireAuth>}
        />
        <Route
          path="/match/:matchId"
          element={<RequireAuth><MatchScreen /></RequireAuth>}
        />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </AppShell>
  );
}

const root = ReactDOM.createRoot(
  document.getElementById('root') as HTMLElement
);

root.render(
  <React.StrictMode>
    <BrowserRouter>
      <ProfileProvider>
        <OfferProvider>
          <App />
        </OfferProvider>
      </ProfileProvider>
    </BrowserRouter>
  </React.StrictMode>
);
