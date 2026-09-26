import React from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { useGameState } from '@/state';
import StartScreen from '@/pages/StartScreen';
import LeagueScreen from '@/pages/LeagueScreen';
import SquadScreen from '@/pages/SquadScreen';
import LineupScreen from '@/pages/LineupScreen';
import TeamViewScreen from '@/pages/TeamViewScreen';
import MatchScreen from '@/pages/MatchScreen';
import '@/styles.css';

function App() {
  const selectedTeam = useGameState((s) => s.selectedTeam);
  const currentMatch = useGameState((s) => s.currentMatch);

  return (
    <div className="app">
      <div className="topbar">
        <div className="brand">Football <span>Manager</span> Prototype</div>
        <div className="badge">POC 0.1 • Motor de partida</div>
      </div>

      <Routes>
        {!selectedTeam && <Route path="/" element={<StartScreen />} />}
        {selectedTeam && <Route path="/" element={<LeagueScreen />} />}
        <Route path="/squad" element={<SquadScreen />} />
        <Route path="/match/lineup/:fixtureId" element={<LineupScreen />} />
        <Route path="/team/:teamId" element={<TeamViewScreen />} />
        <Route path="/match/:matchId" element={<MatchScreen />} />
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
      <App />
    </BrowserRouter>
  </React.StrictMode>
);
