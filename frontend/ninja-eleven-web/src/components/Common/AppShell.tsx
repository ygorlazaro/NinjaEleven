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

          {/* The club's books. Its own screen rather than a panel of the club's, because
              money is a long read of many small lines and the club is a glance at a squad. */}
          <NavLink
            to="/financeiro"
            className={({ isActive }) => `sidebar-link ${isActive ? 'active' : ''}`}
          >
            <span className="sidebar-link__icon">💰</span>
            <span className="sidebar-link__label">Financeiro</span>
          </NavLink>

          {/* The ground and the shirt. They are the club's, so they are doors to the manager's
              own club and not to anybody else's: a stadium is a thing a club has and not a
              thing it is looking at. They are in the column and on the club's page because
              both are places a manager goes, and a place that is only reachable from one other
              page is a place nobody finds. */}
          {selectedTeam && (
            <NavLink
              to="/estadio"
              className={({ isActive }) => `sidebar-link ${isActive ? 'active' : ''}`}
            >
              <span className="sidebar-link__icon">🏟</span>
              <span className="sidebar-link__label">Estádio</span>
            </NavLink>
          )}

          {selectedTeam && (
            <NavLink
              to="/patrocinadores"
              className={({ isActive }) => `sidebar-link ${isActive ? 'active' : ''}`}
            >
              <span className="sidebar-link__icon">📣</span>
              <span className="sidebar-link__label">Patrocinadores</span>
            </NavLink>
          )}

          {/* Who scores for the club, over every season and every competition. It sits with
              the club's own pages because it is a page about the club: the league's scorers
              answer "who leads the division" and this one answers "who is this club's". */}
          {selectedTeam && (
            <NavLink
              to="/artilheiros"
              className={({ isActive }) => `sidebar-link ${isActive ? 'active' : ''}`}
            >
              <span className="sidebar-link__icon">⚽</span>
              <span className="sidebar-link__label">Artilheiros</span>
            </NavLink>
          )}
        </nav>

        {/* The club's own page, on the block that was already showing which club is being
            managed: a manager who clicks his own name is asking about his own club, and the
            answer is a page — the shield, the manager, the bill, the shelf and the history —
            rather than a squad he can already reach from the table. */}
        {selectedTeam && (
          <NavLink
            to="/club"
            className={({ isActive }) => `sidebar-club${isActive ? ' active' : ''}`}
          >
            <span
              className="sidebar-club__swatch"
              style={{
                background: selectedTeam.primaryColor || '#f2d34f',
                borderColor: selectedTeam.secondaryColor || '#f2d34f'
              }}
            />
            <span className="sidebar-club__name">{selectedTeam.name}</span>
          </NavLink>
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
