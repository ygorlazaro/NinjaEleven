import React from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { useGameState } from '@/state';
import { useCareerCheck } from '@/hooks/useCareerCheck';
import { ProfileProvider } from '@/state/ProfileProvider';
import AppShell from '@/components/Common/AppShell';
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
import '@/styles.css';

/**
 * The root of the game.
 *
 * It goes to the manager's club when there is one, and to the club list when there is not —
 * but it never navigates on the strength of a club the browser merely remembers. A remembered
 * club is checked against the backend first, and the check is not skippable: navigating to
 * `/team/{id}` for a club that is not in the world is a screen that cannot answer anything,
 * and the manager's only way out of it is to edit the address bar.
 */
function Root() {
  const selectedTeam = useGameState((s) => s.selectedTeam);
  const career = useCareerCheck();

  if (career === 'checking') {
    return <div className="card start"><p>Carregando...</p></div>;
  }

  return selectedTeam
    ? <Navigate to={`/team/${selectedTeam.id}`} replace />
    : <StartScreen />;
}

function App() {
  // The logo and the two places a manager goes live in the column on the left, so the
  // screen has the whole width to itself.
  return (
    <AppShell>
      <Routes>
        <Route path="/" element={<Root />} />
        <Route path="/league" element={<LeagueScreen />} />
        <Route path="/copa" element={<CupScreen />} />
        <Route path="/calendar" element={<CalendarScreen />} />
        <Route path="/financeiro" element={<FinanceiroScreen />} />
        <Route path="/club" element={<ClubScreen />} />
        <Route path="/estadio" element={<StadiumScreen />} />
        <Route path="/patrocinadores" element={<SponsorsScreen />} />
        <Route path="/artilheiros" element={<ScorersScreen />} />
        <Route path="/match/lineup/:fixtureId" element={<LineupScreen />} />
        <Route path="/team/:teamId" element={<TeamViewScreen />} />
        <Route path="/player/:playerId" element={<PlayerProfileScreen />} />
        <Route path="/match/:matchId" element={<MatchScreen />} />
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
        <App />
      </ProfileProvider>
    </BrowserRouter>
  </React.StrictMode>
);
