import React, { useEffect, useState } from 'react';
import { NavLink, Link, useLocation } from 'react-router-dom';
import { SeasonApi, ManagerApi } from '@/api';
import { useGameState } from '@/state';
import { useNextFixture } from '@/hooks/useNextFixture';
import { usePendingOfferCount, useCurrentSeasonId } from '@/hooks/usePendingOffers';
import NextMatchBox from '@/components/Common/NextMatchBox';
import ClubCrest from '@/components/Club/ClubCrest';

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
  const selectedTeam = useGameState((s) => s.selectedTeam);
  const { pathname } = useLocation();

  /**
   * The screen that opens the career is read without the column.
   *
   * Every link in the column is a place inside a career, and the screen that chooses the
   * career is not inside one: a manager who has not taken a club is sent to a lineup with no
   * lineup, to a next match that is not his, to a market that has not decided who is selling.
   * The column also takes a fifth of the width away from a division of twelve clubs read side
   * by side, so the screen that needs the most room is the one screen that does without it.
   *
   * It is the path that decides, not the career: a manager who refreshes `/` and lands back
   * here has still not taken a club, and a manager whose club is in the store is on a path
   * of its own a moment later.
   */
  const bare = pathname === '/';

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
  const [managerName, setManagerName] = useState<string | null>(null);

  useEffect(() => {
    if (!selectedTeam) {
      setManagerName(null);
      return;
    }

    let alive = true;

    ManagerApi.getByTeam(selectedTeam.id)
      .then(manager => { if (alive) setManagerName(manager.name); })
      .catch(() => { if (alive) setManagerName(null); });

    return () => { alive = false; };
  }, [selectedTeam?.id]);

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

  // The same season the next match is read against, so the two numbers in the column are
  // about the same world and one of them cannot be last season's answer to a question about
  // this one.
  const pendingOffers = usePendingOfferCount(selectedTeam?.id, currentSeasonId);

  return (
    <div className={`shell${bare ? ' shell--bare' : ''}`}>
      {!bare && (
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

        {/* The club the manager is running: its crest, its name and its coach, as a card at the
            top of the column so the manager never loses track of who he is before he thinks about
            where to go. The coach's name comes from the backend — the manager entity the career
            began with — and falls back to nothing when the career has not yet named him. */}
        {selectedTeam && (
          <Link to="/club" className="sidebar-club-card">
            <ClubCrest
              primary={selectedTeam.primaryColor}
              secondary={selectedTeam.secondaryColor}
              name={selectedTeam.name}
              className="sidebar-club-card__crest"
            />
            <div className="sidebar-club-card__body">
              <span className="sidebar-club-card__name">{selectedTeam.name}</span>
              <span className="sidebar-club-card__coach">
                {managerName ?? '…'}
              </span>
            </div>
          </Link>
        )}

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

          {/* The knockout. It is below the championship because it is the other half of the
              season and the other kind of football: a table says who is above you and a bracket
              says who is left in the cup, and a manager reads them at different moments — the
              table on a matchday, the bracket when two legs are done and one club is through. */}
          <NavLink
            to="/copa"
            className={({ isActive }) => `sidebar-link ${isActive ? 'active' : ''}`}
          >
            <span className="sidebar-link__icon">🥇</span>
            <span className="sidebar-link__label">Copa</span>
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

           {selectedTeam && (
             <NavLink
               to="/transfer"
               className={({ isActive }) => `sidebar-link ${isActive ? 'active' : ''}`}
             >
               <span className="sidebar-link__icon">🔄</span>
               <span className="sidebar-link__label">Mercado</span>
               {/* An offer nobody answers dies at the end of the season, so the count of the
                   ones waiting is on the column rather than inside the screen it belongs to:
                   a manager has to be able to find out there is a decision to make without
                   already knowing that he has one. */}
               {pendingOffers > 0 && (
                 <span
                   className="sidebar-link__badge"
                   title={`${pendingOffers} proposta(s) aguardando sua resposta`}
                 >
                   {pendingOffers}
                 </span>
               )}
             </NavLink>
           )}
         </nav>

        {/* The match he is about to play, said before he goes and play it, and pinned to the
            foot of the column so it is on every screen: the eleven he picks is chosen for
            this opponent, at this ground, and the column is the one place a manager is on
            whatever screen he happens to be reading. */}
          {selectedTeam && <NextMatchBox team={selectedTeam} next={next} />}
      </aside>
      )}


      <main className="shell-main">{children}</main>
    </div>
  );
};

export default AppShell;
