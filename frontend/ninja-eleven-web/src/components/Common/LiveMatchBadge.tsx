import React from 'react';
import { NavLink } from 'react-router-dom';
import type { LiveMatchDto } from '@/types';
import ClubCrest from '@/components/Club/ClubCrest';

/**
 * The club is playing: a badge in the navigation column that goes straight to the match.
 *
 * A manager managing a club is somewhere else for most of a matchday, and the match is the
 * one thing on the screen that is happening *now* rather than being a document to read. So
 * the badge is in the column on every screen, and it is a door: one click and he is in the
 * match, at the minute it is at, with the eleven he picked.
 *
 * **It says who it is against, the score and the minute**, because a badge that only said
 * "ao vivo" would tell a manager that something is happening and nothing about what — and
 * the man who has to decide whether to leave the screen he is on to watch it is deciding it
 * on incomplete information. The two shields are the match's own clubs, in the order the
 * badge reads them: the opponent first, because the question is "who do I have to deal
 * with", and the manager's own second, marked, because that is the side he commands.
 *
 * **The interval says "intervalo" rather than a minute.** The clock is not late there, it is
 * stopped on a manager's decision, and a badge reading "45'" would suggest a game dragging on
 * and send him back to a half he had already settled.
 */
const LiveMatchBadge: React.FC<{ live: LiveMatchDto }> = ({ live }) => {
  const opponent = live.isHome ? live.awayTeamName : live.homeTeamName;
  const opponentShort = live.isHome ? live.awayShortName : live.homeShortName;
  const opponentPrimary = live.isHome ? live.awayPrimaryColor : live.homePrimaryColor;
  const opponentSecondary = live.isHome ? live.awaySecondaryColor : live.homeSecondaryColor;
  const mineName = live.isHome ? live.homeTeamName : live.awayTeamName;
  const mineShort = live.isHome ? live.homeShortName : live.awayShortName;
  const minePrimary = live.isHome ? live.homePrimaryColor : live.awayPrimaryColor;
  const mineSecondary = live.isHome ? live.homeSecondaryColor : live.awaySecondaryColor;

  // The score is written in the match's own order — home on the left — because that is how
  // every other scoreline in the game reads, and a badge that flipped it to "me first" would
  // be a different scoreline from the one on the match screen.
  const score = `${live.homeGoals} × ${live.awayGoals}`;

  return (
    <NavLink
      to={`/match/${live.matchId}`}
      className="live-badge"
      title={`${live.homeTeamName} ${score} ${live.awayTeamName} — ${live.atHalfTime ? 'no intervalo' : `minuto ${live.minute}`}`}
    >
      <span className="live-badge__head">
        <span className="live-badge__pulse" aria-hidden="true" />
        <span className="live-badge__label">Ao vivo</span>
        <span className="live-badge__minute">
          {live.atHalfTime ? 'Intervalo' : `${live.minute}'`}
        </span>
      </span>

      <span className="live-badge__match">
        <span className="live-badge__side">
          <ClubCrest
            primary={opponentPrimary}
            secondary={opponentSecondary}
            name={opponent}
            className="mini-crest"
          />
          <span className="live-badge__name" title={opponent}>{opponentShort || opponent}</span>
        </span>

        <b className="live-badge__score">{score}</b>

        <span className="live-badge__side right">
          <span className="live-badge__name" title={mineName}>{mineShort || mineName}</span>
          <ClubCrest
            primary={minePrimary}
            secondary={mineSecondary}
            name={mineName}
            className="mini-crest"
          />
        </span>
      </span>
    </NavLink>
  );
};

export default LiveMatchBadge;
