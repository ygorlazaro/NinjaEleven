import React, { useCallback, useEffect, useState } from 'react';
import { NavLink, Link, useLocation } from 'react-router-dom';
import { SeasonApi, TeamApi, ManagerApi } from '@/api';
import { useGameState } from '@/state';
import { useAuthStore } from '@/state/auth';
import { useNextFixture } from '@/hooks/useNextFixture';
import { usePendingOfferCount, useCurrentSeasonId } from '@/hooks/usePendingOffers';
import { useLiveMatch } from '@/hooks/useLiveMatch';
import { useUnreadMessageCount } from '@/hooks/useUnreadMessages';
import ClubMatchCard from '@/components/Common/ClubMatchCard';
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
  const setSelectedTeam = useGameState((s) => s.setSelectedTeam);
  const forgetClub = useGameState((s) => s.forgetClub);
  const leagueTeams = useGameState((s) => s.leagueTeams);
  const setLeagueTeams = useGameState((s) => s.setLeagueTeams);
  const authTeamId = useAuthStore((s) => s.teamId);
  const email = useAuthStore((s) => s.email);
  const { pathname } = useLocation();

  /**
   * Ends the session on purpose, which is the thing a manager could not do until now.
   *
   * The session goes first and the club with it, in the same tick and in the same order the two
   * failure paths already use: a token that has stopped being accepted and a token nobody wants
   * any more are the same fact about the account, and the club is a thing that account was
   * running rather than a thing the browser owns. Leaving it behind would hand the next person
   * to sign in on this browser a club that is already open — the one stale-club fault the
   * re-seeded world and a rejected token both produced, and the reason a screen that greys out
   * "not your club" still opens on somebody else's club.
   *
   * Clearing the token is what makes the login screen the next thing drawn: the route guards
   * read it, so a store with no session renders the login screen on the next paint. No
   * navigation is called for, so there is no history entry to go "back" into a session that no
   * longer exists.
   */
  const signOut = useCallback(() => {
    useAuthStore.getState().clearAuth();
    useGameState.getState().forgetClub();
  }, []);

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
  const bare = pathname === '/' || pathname === '/login' || pathname === '/register';

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

  /**
   * The club the account owns is the one the sidebar is built around, and it is the auth
   * store that owns it. A manager who lands on a route other than the club selector — the
   * inbox, the market, the calendar — arrives here with the game store empty, and without
   * this every door that belongs to a club was missing from the column. The auth store has
   * the team from the token; this loads the club row once and the sidebar reads it from
   * then on, so the two stores cannot disagree about which club the manager is running.
   */
  useEffect(() => {
    if (!authTeamId) return;
    if (selectedTeam?.id === authTeamId) return;

    let cancelled = false;
    TeamApi.get(authTeamId)
      .then(loaded => {
        if (!cancelled) {
          setSelectedTeam(loaded);
          if (!leagueTeams.some(t => t.id === loaded.id)) {
            setLeagueTeams([...leagueTeams, loaded]);
          }
        }
      })
      // The club the token names is not there, so the session is over rather than the club
      // being merely unfound. A world thrown away and drawn again gives every club a new id,
      // and the token in this browser carries the id of one of the old ones; swallowing that
      // left a dead club in the store, and a dead club explains everything that was wrong —
      // the training link never drawn because its gate compares two ids and one of them is
      // gone, and no screen able to say which club is the manager's, so nothing was ever
      // highlighted.
      //
      // Both halves go, and they are the same two halves `AuthRoot` drops on the same
      // failure. The session goes because the token names a club that does not exist, and
      // the club goes because there is nothing left to point at. Clearing the session is also
      // what stops this asking again: `authTeamId` is what the effect keys on, and it has just
      // been emptied, so a forgotten club is asked for once and never a second time.
      .catch(() => {
        if (cancelled) return;
        useAuthStore.getState().clearAuth();
        forgetClub();
      });

    return () => { cancelled = true; };
  }, [authTeamId, selectedTeam?.id]);

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
  // It used to be a line of the column and it is not one any more: the match card above
  // already says the same four facts and goes to the same screen, and a column offering two
  // ways into one match is a column that has to be read twice to be believed. The route is
  // still resolved here because the card is a card and the calendar is where a manager goes
  // when there is nothing to play.
  const { next } = useNextFixture(selectedTeam?.id, currentSeasonId);

  // The same season the next match is read against, so the two numbers in the column are
  // about the same world and one of them cannot be last season's answer to a question about
  // this one.
  const pendingOffers = usePendingOfferCount(selectedTeam?.id, currentSeasonId);

  // How much of the manager's own mail he has not opened. It is on the column rather than
  // inside the box for the same reason the pending offers are: a manager deciding whether to
  // leave the market to read his mail should be able to find out from the market that there
  // is any. The path is part of the key so that reading the box refreshes the badge.
  const unreadMessages = useUnreadMessageCount(selectedTeam?.id, pathname);

  // The club is playing right now, or it is not. It is asked again on every route because a
  // match is started from the lineup screen, and a card that turned live half a minute after
  // the whistle would be a card nobody could rely on to mean "go now".
  const liveMatch = useLiveMatch(selectedTeam?.id, pathname);

  // Whether the training tab is the one on screen, read off the query rather than off the
  // path: the tab and the squad share a route, so the path alone cannot say which of the
  // two the manager is looking at.
  const { search } = useLocation();
  const trainingTabOpen = new URLSearchParams(search).get('tab') === 'training';

  // And the same for the squad: the two tabs of a club share one route, so the path alone
  // cannot say which of them is open.
  const squadTabOpen = !trainingTabOpen;

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
            began with — and falls back to nothing when the career has not yet named him.

            The shield is the size of the one in the match card rather than the size of a
            badge, because this card and that card are read together and a column with two
            different crest sizes in it has no size of its own. */}
        {selectedTeam && (
          <Link to="/club" className="sidebar-club-card">
            <ClubCrest
              crest={selectedTeam.crest}
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

        {/* The match his club has in front of it: the one he is about to play, or the one he is
            playing right now, in one card.

            It is read together with the club's own card above it — who he is, and which match
            is his — so it belongs with them rather than a scroll away at the bottom. A
            manager choosing his eleven is answering to three things: which club he runs, who
            it is against, and whether that game has already started. The column says those
            first, and every other place after them. */}
          {selectedTeam && (
            <ClubMatchCard
              team={selectedTeam}
              next={next}
              live={liveMatch}
              seasonId={currentSeasonId}
            />
          )}

        <nav className="sidebar-nav">
          {/* His own mail. It is the first of the places to go because it is the only one the
              game writes to him: the market waits for a decision, the calendar waits for a
              round, and the box fills itself the moment the whistle goes, a line is written in
              the ledger, or somebody bids for one of his men. */}
          <NavLink
            to="/caixa"
            className={({ isActive }) => `sidebar-link ${isActive ? 'active' : ''}`}
          >
            <span className="sidebar-link__icon">📬</span>
            <span className="sidebar-link__label">Caixa de Entrada</span>
            {/* How much of it has not been opened, on the column rather than inside the screen
                it belongs to: a manager has to be able to find out that there is news from
                every screen, and not only from the one he remembered to open. */}
            {unreadMessages > 0 && (
              <span
                className="sidebar-link__badge"
                title={`${unreadMessages} mensagem(ns) não lida(s)`}
              >
                {unreadMessages}
              </span>
            )}
          </NavLink>

          {/* His own order. It sits directly under his mail because it is the other thing
              the game makes him decide rather than merely show him: everything else on this
              column reports, and this one is written down and then acted on without him.

              It is above the competitions rather than among them because it applies to all
              of them — a shape is not a championship shape or a cup shape, it is how his
              club plays, and the fixture it applies to is read from the calendar when the
              match opens. */}
          <NavLink
            to="/tactics"
            className={({ isActive }) => `sidebar-link ${isActive ? 'active' : ''}`}
          >
            <span className="sidebar-link__icon">📋</span>
            <span className="sidebar-link__label">Táticas</span>
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

          {/* What the manager does about his men between matches. It is a link of its own
              because a training session is a decision taken on a day rather than a tab
              looked at: the sheet is two clicks away from here and nowhere else, and a
              manager who has to go to the club, then find the second tab, and then find the
              row is not choosing to develop anybody. The tab is a parameter rather than a
              piece of screen state, so this link can actually land on it.

              The link says the training tab, and being active is decided by that parameter:
              a link whose active state is the path alone would light up for the squad too,
              and a manager standing on the roster could not tell which of the club's two
              screens he was on. */}
          {/* The club's twenty-three men, which is the first thing a manager opens after a
              match and the thing the training sheet acts upon. It is a link of its own for
              the same reason the training sheet is: the roster is where the answers are, and
              a manager who has to reach the club through a crest on the sidebar to ask "who
              is fit" is not asking it.

              Being active is decided by the tab rather than by the path alone, because the
              two share a route — otherwise this link would light up for the training sheet
              too, and the manager could not tell which of the club's two screens he was on. */}
          {selectedTeam && (
            <NavLink
              to={`/team/${selectedTeam.id}`}
              end
              className={({ isActive }) =>
                `sidebar-link ${isActive && squadTabOpen ? 'active' : ''}`
              }
            >
              <span className="sidebar-link__icon">👥</span>
              <span className="sidebar-link__label">Elenco</span>
            </NavLink>
          )}

          {selectedTeam && (
            <NavLink
              to={`/team/${selectedTeam.id}?tab=training`}
              className={({ isActive }) =>
                `sidebar-link ${isActive && trainingTabOpen ? 'active' : ''}`
              }
            >
              <span className="sidebar-link__icon">🏋️</span>
              <span className="sidebar-link__label">Treino</span>
            </NavLink>
          )}

          {selectedTeam && (
            <NavLink
              to={`/team/${selectedTeam.id}/base`}
              className={({ isActive }) => `sidebar-link ${isActive ? 'active' : ''}`}
            >
              <span className="sidebar-link__icon">🎓</span>
              <span className="sidebar-link__label">Base</span>
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
              to="/ranking"
              className={({ isActive }) => `sidebar-link ${isActive ? 'active' : ''}`}
            >
              <span className="sidebar-link__icon">🏆</span>
              <span className="sidebar-link__label">Ranking Ninja</span>
            </NavLink>
          )}

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

        {/* Who is signed in, and the way out.
            *
            * It is here and not among the club's own doors because it is not about the club. The
            * links above are places a manager goes inside a career; leaving the account is not a
            * place, and a row of doors that also opened "Sair" would be a row mixing two kinds of
            * thing. It sits under the column because a manager on any screen can reach it, which
            * is the whole point of a control that ends a session.
            *
            * The email travels with it because that is what makes the button safe to press: one
            * browser can hold one career's token and another account can sign in over it, and a
            * control that says "Sair" with nothing saying *whom* is a control nobody trusts. It
            * reads who is signed in before they end it.
            *
            * Both halves go, and they are the same two halves the failure paths above drop: the
            * session, because that is what ended, and the club, because a club left in the game
            * store is what a second account would inherit and open as though it were its own.
            * That inheritance is the same stale-club fault the world being re-seeded causes, and
            * the cure is the same: nothing is left pointing at a club nobody is running. */}
        {email && (
          <div className="sidebar-account">
            <span className="sidebar-account__email" title={email}>
              {email}
            </span>
            <button type="button" className="sidebar-account__signout" onClick={signOut}>
              Sair
            </button>
          </div>
        )}
      </aside>
      )}


      <main className="shell-main">{children}</main>
    </div>
  );
};

export default AppShell;
