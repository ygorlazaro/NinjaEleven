import React, { useEffect, useState } from 'react';
import { NavLink } from 'react-router-dom';
import type { LiveMatchDto, StandingDto, TeamDto, TeamMatchRecordDto } from '@/types';
import { ClubName } from '@/components/Common/Names';
import ClubCrest from '@/components/Club/ClubCrest';
import FormRun, { FORM_RUN_LENGTH } from '@/components/Club/FormRun';
import { TeamApi } from '@/api';
import type { NextFixture } from '@/hooks/useNextFixture';

interface ClubMatchCardProps {
  /** The club the manager is running: it is what says which side of the fixture is his. */
  team: TeamDto;
  /** The match his club has in front of it, live or not. */
  next: NextFixture | null;
  /** The match his club is playing right now, or null for the rest of a season's hours. */
  live: LiveMatchDto | null;
  /** The season the opponent's place in his division is counted over. */
  seasonId?: string;
}

/**
 * The match the manager is about to play, or the one he is playing right now.
 *
 * **These were two cards and they are one because the match is one.** A card saying "next
 * match" and a badge saying "ao vivo" stacked above each other meant a manager had to read the
 * column twice to work out which of the two facts was true of the game he is actually in, and
 * the two of them were never about different matches anyway: the backend counts a match in
 * progress as the club's next fixture, so while a game is being played `next` *is* that game.
 * One card reads the fixture either way and adds the score and the clock when there is one.
 *
 * **So nothing is lost by the merge.** The card still says who it is against, where it is
 * played, which side of the door the club is on, what the game is, the opponent's last five
 * results and the matchday — and while the match is being played it also says the score and
 * the minute, with the manager's own side named under the scoreline so the two numbers are
 * not just numbers. The old badge's two shields are the one thing that goes: the opponent's
 * shield is here, and the manager's own is the card above this one.
 *
 * **A championship fixture also says where the opponent stands.** His place and his points in
 * his own division, beside the run, are the two facts a manager picks an eleven by and the two
 * the old card left out — a form guide of five letters describes a shape and not a season. The
 * number is the club's own row in his division's table, read from the backend, and it is asked
 * only for a championship fixture: a cup tie has no table behind it, so a position printed over
 * it would be answering a question nobody asked.
 *
 * **The card is a door to two different screens, and it says which.** Before the whistle it is
 * a decision — pick the eleven — and it goes to the lineup. During the game it is a thing
 * being watched, and it goes to the match at the minute it is at.
 *
 * Every name and every number here is the backend's: the fixture carries both clubs and the
 * host's ground, the round number is the round's own, and the score and the clock come off the
 * registry that is playing the match. Nothing about the match is worked out in the browser.
 */
const ClubMatchCard: React.FC<ClubMatchCardProps> = ({ team, next, live, seasonId }) => {
  // Nothing to play and nothing being played: the card says so rather than saying nothing,
  // because a column whose match box has simply vanished is a manager who has to go and look
  // for the fixture to find out there is no fixture.
  //
  // A live match is *not* that case, and the two are kept apart on purpose: the match in
  // progress is a fact of its own, read off the registry of matches being played, and a
  // fixture list that has not caught up — or a club whose fixture the calendar has not drawn
  // yet — must not be able to hide a match the manager is watching.
  if (!next && !live) {
    return (
      <div className="match-card">
        <span className="match-card__label">Próxima partida</span>
        <span className="match-card__none">
          Nenhum jogo marcado no calendário da temporada.
        </span>
      </div>
    );
  }

  const { fixture, matchDayNumber, competitionName, competitionType, playableNow, waitingFor } =
    next ?? {};

  // Which side of the door the club is on, asked of the fixture and, when there is no fixture
  // to ask, of the live match itself — which says the same thing, because it is answered for
  // the club the question was asked about.
  const isHome = live ? live.isHome : fixture?.homeTeamId === team.id;

  // The ground is the host's, and the host is whoever is at home — whichever club that is.
  // An away match is played in the other club's stadium, so a card that printed the manager's
  // own ground for an away game would be telling him the one thing he already knows wrong.
  const stadium = fixture?.homeTeam?.stadium?.name;

  // The opponent is the fixture's other club, and the fixture's own row when there is one: a
  // live match alone carries the name and the colours but no ground and no run, so the card
  // takes the shield from whichever of the two has it and prints what it has rather than
  // inventing the rest.
  const opponent = fixture
    ? (isHome ? fixture.awayTeam : fixture.homeTeam) ?? null
    : live
      ? {
          id: isHome ? live.awayTeamId : live.homeTeamId,
          name: isHome ? live.awayTeamName : live.homeTeamName,
          primaryColor: isHome ? live.awayPrimaryColor : live.homePrimaryColor,
          secondaryColor: isHome ? live.awaySecondaryColor : live.homeSecondaryColor
        }
      : null;

  // A live match wins over anything else on the card. Normally `next` *is* the match being
  // played, because a fixture is still to be played until the final whistle — but a card that
  // said "next: Fulano" while the manager was watching Fulano would be two cards' worth of
  // confusion in one, so the live match is drawn whatever the fixture says.
  const isLive = live != null;

  const score = live ? `${live.homeGoals} × ${live.awayGoals}` : '';
  const opponentId = opponent?.id;
  const isChampionship = competitionType === 'League';

  return (
    <NavLink
      className={`match-card${isLive ? ' match-card--live' : ''}`}
      to={live ? `/match/${live.matchId}` : '/tactics'}
    >
      <span className="match-card__head">
        {live ? (
          /* The pulse is the whole of the difference between the two states, and it is drawn
             rather than typed because a card that changed only its words would leave a manager
             deciding whether to click without looking. */
          <span className="match-card__live">
            <span className="match-card__pulse" aria-hidden="true" />
            Ao vivo
            <span className="match-card__minute">
              {live.atHalfTime ? 'Intervalo' : `${live.minute}'`}
            </span>
          </span>
        ) : (
          <span className="match-card__label">Próxima partida</span>
        )}

        {/* The round and the day of the season, said together: "R9" on its own is a number
            out of context, and the day is the thing a manager reads to know how far away it
            is. */}
        <span className="match-card__round">
          {matchDayNumber ? `Dia ${matchDayNumber}` : '—'}
        </span>
      </span>

      <span
        className="match-card__fixture"
        title={live ? `${live.homeTeamName} ${score} ${live.awayTeamName}` : undefined}
      >
        {/* C or F, and the words with it: a letter in a column is a label a manager has to
            know how to read, and the same letter in a sentence costs nothing. It is also what
            makes the score unambiguous — the card prints it home first, as every other
            scoreline in the game does, and the letter says which of the two numbers is his. */}
        <span
          className={`match-card__side${isHome ? ' match-card__side--home' : ''}`}
          title={isHome ? 'Em casa' : 'Fora de casa'}
        >
          {isHome ? 'C' : 'F'}
        </span>

        {opponentId ? (
          <>
            <ClubCrest
              primary={opponent?.primaryColor}
              secondary={opponent?.secondaryColor}
              name={opponent?.name ?? ''}
              className="mini-crest"
            />
            <span className="match-card__opponent">
              <ClubName teamId={opponentId}>{opponent?.name ?? ''}</ClubName>
            </span>
          </>
        ) : (
          <span className="match-card__opponent">Adversário a definir</span>
        )}

        {/* The score, and the manager's own side named under it. The old badge drew the two
            clubs' shields on either side of the score; here the opponent's shield is the one
            above and the manager's own club is the card higher up the column, so what the
            badge's second shield said is said in words instead: the two numbers are attached
            to a side, and the letter above already says which side is his. */}
        {live && (
          <span className="match-card__scoreline">
            <b>{score}</b>
            <span className="match-card__mine">
              {isHome ? live.homeShortName || live.homeTeamName : live.awayShortName || live.awayTeamName}
            </span>
          </span>
        )}
      </span>

      {/* The run and the place are asked about the club the card names, whether the whistle
          has gone or not: both are facts a manager picks an eleven by, and the second one only
          changes when the league says so, so there is nothing about a live match that would
          make them a question about the match rather than about the season.

          They ride on the fixture's own row, so a live match the calendar has not caught up
          with prints the score and the minute and no run — which is what is actually known
          about it, rather than a form guide drawn for a fixture that is not there. */}
      {fixture && opponent && <OpponentRun opponent={opponent} />}
      {fixture && opponentId && isChampionship && (
        <OpponentStanding opponentId={opponentId} seasonId={seasonId} />
      )}

      {/* The ground and the competition are the fixture's facts, and a match the calendar has
          not drawn yet has neither. The line is drawn anyway rather than left out, so the
          card does not lose its shape between the two states. */}
      <span className="match-card__where">
        <span className="match-card__stadium" title={stadium ?? 'Estádio a definir'}>
          {stadium ?? 'Estádio a definir'}
        </span>
        {/* The competition is the edition's own name, so a 1ª Divisão fixture says "1ª Divisão"
            and a cup tie says "Copa": "Campeonato Brasileiro" alone would be the same word for
            a match in the first division and a match in the third. */}
        <span className="match-card__competition">{competitionName || '—'}</span>
      </span>

      {/* The match is the club's next one and is not kick-off time yet. A matchday runs one
          wave at a time — championship, then cup — so a cup tie on a day whose league games
          are still being played is the next fixture and is unplayable. Saying that here is
          the difference between a manager who knows to wait and a manager who assembles an
          eleven for a game the server will refuse. */}
      {!isLive && !playableNow && (
        <span className="match-card__waiting">
          Depois do {waveName(waitingFor)} do dia {matchDayNumber}
        </span>
      )}
    </NavLink>
  );
};

/**
 * The opponent's last results, asked for the club the card names and not for the club in the
 * column, so the run under the name is the one that belongs to it.
 *
 * The prop is the two facts the run is drawn from rather than a whole club row: a live match
 * the calendar has not caught up with carries the opponent's name and nothing else, and asking
 * for a full `TeamDto` here would be asking the run to need the whole row in order to print
 * five letters.
 *
 * The card is a card and not a screen, so this is one small read rather than a second one in
 * the browser: the run is drawn off the backend's own classification of each match, which is
 * the same chain the standings and the club page are settled on.
 */
const OpponentRun: React.FC<{ opponent: { id: string; name: string } }> = ({ opponent }) => {
  const [matches, setMatches] = useState<TeamMatchRecordDto[]>([]);

  useEffect(() => {
    let cancelled = false;

    TeamApi.getMatches(opponent.id, FORM_RUN_LENGTH)
      .then(records => {
        if (!cancelled) setMatches(records);
      })
      .catch(err => {
        console.error('Failed to load the opponent\'s recent matches:', err);
        if (!cancelled) setMatches([]);
      });

    return () => {
      cancelled = true;
    };
  }, [opponent.id]);

  return (
    <span className="match-card__form" title={`Últimos ${FORM_RUN_LENGTH} jogos do ${opponent.name}`}>
      <span className="form-run">
        <FormRun matches={matches} emptyLabel="sem jogos" />
      </span>
    </span>
  );
};

/**
 * Where the opponent stands, in his own division, and with how many points.
 *
 * **It is asked of the backend, and only for a championship fixture.** A cup tie has no table
 * behind it, so the position of the opponent's club in his league division says nothing about
 * the tie being played — it is the same number beside a different sentence, and a card that
 * printed "3º" over a cup match would be answering a question nobody asked. The number itself
 * is the club's own row in his division's table, which is the table the game settles on and
 * not a position worked out in the column.
 *
 * It sits beside the run rather than under the name because it is the same kind of fact: a
 * column of five letters saying the shape of the club, and one line saying where it is in the
 * season. A manager choosing an eleven is reading both together.
 */
const OpponentStanding: React.FC<{ opponentId: string; seasonId?: string }> = ({
  opponentId,
  seasonId
}) => {
  const [row, setRow] = useState<StandingDto | null>(null);

  useEffect(() => {
    if (!seasonId) {
      setRow(null);
      return undefined;
    }

    let cancelled = false;

    TeamApi.getStanding(opponentId, seasonId)
      .then(standing => { if (!cancelled) setRow(standing.row ?? null); })
      .catch(err => {
        console.error('Failed to load the opponent\'s place in the table:', err);
        if (!cancelled) setRow(null);
      });

    return () => {
      cancelled = true;
    };
  }, [opponentId, seasonId]);

  // No row is not a club with no place: it is a division that has not been drawn yet, and the
  // card says nothing rather than saying a position of zero.
  if (!row) return null;

  return (
    <span className="match-card__place" title={`${row.position}º lugar com ${row.points} pontos`}>
      {row.position}º <span className="match-card__points">{row.points} pts</span>
    </span>
  );
};

/**
 * The wave a matchday is in, in the words the rest of the game uses for it. The backend
 * sends the kind of competition, and the sentence is this card's: "depois do campeonato"
 * is what a manager reads, and a raw enum would be the one place in the column he has to
 * translate.
 */
const waveName = (wave: string | null | undefined): string => {
  switch (wave) {
    case 'SuperCup':
      return 'jogo da Supercopa';
    case 'League':
      return 'campeonato';
    case 'Cup':
      return 'jogo da Copa';
    default:
      return 'jogo do dia';
  }
};

export default ClubMatchCard;