import React from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { useGameState } from '@/state';
import { ProfileProvider } from '@/state/ProfileProvider';
import StartScreen from '@/pages/StartScreen';
import LeagueScreen from '@/pages/LeagueScreen';
import LineupScreen from '@/pages/LineupScreen';
import TeamViewScreen from '@/pages/TeamViewScreen';
import MatchScreen from '@/pages/MatchScreen';
import '@/styles.css';

function App() {
  const selectedTeam = useGameState((s) => s.selectedTeam);

  return (
    <div className="app">
      <div className="topbar">
        <div className="brand">Ninja <span>Eleven</span></div>
        <div className="badge">POC 0.1 • Motor de partida</div>
      </div>

      {/* A career starts at the club: the manager's own squad is the first thing he reads,
          and the fixtures are one click away from there. */}
      <Routes>
        <Route
          path="/"
          element={
            selectedTeam ? <Navigate to={`/team/${selectedTeam.id}`} replace /> : <StartScreen />
          }
        />
        <Route path="/league" element={<LeagueScreen />} />
        <Route path="/match/lineup/:fixtureId" element={<LineupScreen />} />
        <Route path="/team/:teamId" element={<TeamViewScreen />} />
        <Route path="/match/:matchId" element={<MatchScreen />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </div>
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
