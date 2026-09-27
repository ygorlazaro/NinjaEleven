import React from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import { useGameState } from '@/state';
import { useNextFixture } from '@/hooks/useNextFixture';

/**
 * The frame every screen is read in: the game on the left, the screen on the right.
 *
 * The logo was a header across the top and is now a mark at the head of the column, because
 * a game that has a league table and a lineup to choose is not a single screen with a title
 * on it. The column is the list of places a manager goes — the eleven he is picking, the
 * table he is answering to — and it is a list that will grow: everything below the two links
 * is something the game answers to, not something this file knows about.
 */
const AppShell: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const navigate = useNavigate();
  const selectedTeam = useGameState((s) => s.selectedTeam);
  const selectedCompetition = useGameState((s) => s.selectedCompetition);
  const forgetClub = useGameState((s) => s.forgetClub);

  // The lineup is addressed by fixture, so the link has to know which one. When there is
  // none to play the link is not a broken door: it goes to the calendar, which is where a
  // manager goes to find out what there is.
  const { next } = useNextFixture(selectedTeam?.id, selectedCompetition?.id);
  const lineupTarget = next ? `/match/lineup/${next.fixture.id}` : '/league';

  return (
    <div className="shell">
      <aside className="sidebar">
        <NavLink to="/" className="sidebar-brand" onClick={event => {
          // The brand is a door home, and it is a button rather than a link-with-a-handler
          // so it keeps behaving like a link for the keyboard and for the middle button.
          if (window.location.pathname === '/') {
            event.preventDefault();
          }
        }}>
          <span className="sidebar-brand__ninja">Ninja</span>{' '}
          <span className="sidebar-brand__eleven">Eleven</span>
        </NavLink>

        <nav className="sidebar-nav">
          <NavLink
            to={lineupTarget}
            className={({ isActive }) => `sidebar-link ${isActive ? 'active' : ''}`}
          >
            <span className="sidebar-link__icon">📋</span>
            <span className="sidebar-link__label">Escalação</span>
            {next && <span className="sidebar-link__round">R{next.round.number}</span>}
          </NavLink>

          <NavLink
            to="/league"
            className={({ isActive }) => `sidebar-link ${isActive ? 'active' : ''}`}
          >
            <span className="sidebar-link__icon">🏆</span>
            <span className="sidebar-link__label">Campeonato</span>
          </NavLink>

          {/* The season whole: the three divisions and the knockout, in the order they are
              played. It is its own screen rather than another tab of the table because a
              table answers "who is above me" and a calendar answers "when do I play". */}
          <NavLink
            to="/calendar"
            className={({ isActive }) => `sidebar-link ${isActive ? 'active' : ''}`}
          >
            <span className="sidebar-link__icon">📅</span>
            <span className="sidebar-link__label">Calendário</span>
          </NavLink>
        </nav>

        {selectedTeam && (
          <div className="sidebar-club">
            <span
              className="sidebar-club__swatch"
              style={{
                background: selectedTeam.primaryColor || '#f2d34f',
                borderColor: selectedTeam.secondaryColor || '#f2d34f'
              }}
            />
            <span className="sidebar-club__name">{selectedTeam.name}</span>
          </div>
        )}

        {/* The club has to be forgotten before the root can be reached: the root sends a
            manager with a club to that club, so navigating there with one still selected
            would arrive back here having changed nothing. */}
        <button
          className="sidebar-back"
          onClick={() => {
            forgetClub();
            navigate('/');
          }}
        >
          Trocar de clube
        </button>
      </aside>

      <main className="shell-main">{children}</main>
    </div>
  );
};

export default AppShell;
