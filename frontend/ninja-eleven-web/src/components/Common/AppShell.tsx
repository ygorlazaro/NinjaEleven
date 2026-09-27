import React, { useEffect, useState } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import { SeasonApi } from '@/api';
import { useGameState } from '@/state';
import { useNextFixture } from '@/hooks/useNextFixture';
import ClubCrest from '@/components/Club/ClubCrest';
import NextMatchBox from '@/components/Common/NextMatchBox';

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
  // The season being played. The sidebar's next match is a question about the calendar and
  // not about a store value, so the season is asked for rather than remembered — and the
  // career's season can change between matches.
  const [currentSeasonId, setCurrentSeasonId] = useState<string | undefined>(undefined);

  useEffect(() => {
    let alive = true;

    SeasonApi.current()
      .then(season => alive && setCurrentSeasonId(season.id))
      .catch(() => alive && setCurrentSeasonId(undefined));

    return () => {
      alive = false;
    };
  }, []);
  const forgetClub = useGameState((s) => s.forgetClub);

  // The lineup is addressed by fixture, so the link has to know which one. When there is
  // none to play the link is not a broken door: it goes to the calendar, which is where a
  // manager goes to find out what there is.
  //
  // It is the next match of the *season* and not of the competition on the filter, because the
  // link and the box at the foot of the column are the same door: a sidebar offering two
  // different games in two different places is a sidebar that has to be read twice to be
  // believed.
  const { next } = useNextFixture(selectedTeam?.id, currentSeasonId);
  const lineupTarget = next ? `/match/lineup/${next.fixture.id}` : '/calendar';

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
            {/* The shield, not a pair of stripes: the block in the column is the club, and a
                club is recognised by its badge before it is read by its name. It is a
                placeholder drawn in the club's own colours, like the one on the club's page. */}
            <ClubCrest
              primary={selectedTeam.primaryColor}
              secondary={selectedTeam.secondaryColor}
              name={selectedTeam.name}
            />
            <span className="sidebar-club__name">{selectedTeam.name}</span>
          </NavLink>
        )}

        {/* The match he is about to play, said before he goes and play it, and pinned to the
            foot of the column so it is on every screen: the eleven he picks is chosen for
            this opponent, at this ground, and the column is the one place a manager is on
            whatever screen he happens to be reading. */}
        {selectedTeam && <NextMatchBox team={selectedTeam} next={next} />}

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
