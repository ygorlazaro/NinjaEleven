import React from 'react';
import { useNavigate } from 'react-router-dom';
import type { CupBracketClubDto, CupBracketDto, CupBracketTieDto } from '@/types';
import { ClubName } from '@/components/Common/Names';
import ClubCrest from '@/components/Club/ClubCrest';
import CupTrophy from '@/components/Cup/CupTrophy';

/** A leg's score from one club's side, or a dash while the leg is still to be played. */
const legScore = (goals?: number | null, conceded?: number | null) =>
  goals == null || conceded == null ? '–' : `${goals} x ${conceded}`;

/**
 * One cup, as a bracket: a column per round, oldest on the left.
 *
 * A cup is the one thing in the game that is not a table, and it is read the way a manager
 * reads it — the round of 16 on the left and the final on the right, each tie with the two
 * clubs that were in it, the two legs they played, and the aggregate they finished on.
 *
 * **Every number is a club's own.** A club's line says the goals it scored and the goals it let
 * in, in each leg, because the two legs of a tie swap ends: printing the legs as they were
 * played would put a club's second leg on the other side of the tie, and the tie it went
 * through on. A leg that has not been played is a dash rather than a 0 x 0, because a cup tie
 * decided by nobody is not a goalless draw.
 *
 * Only the rounds the cup has drawn are here. The round after a tie is decided does not exist
 * yet, so the bracket ends where the football has got to — which is the honest shape of a
 * knockout, and the reason the columns get wider as they go.
 */
const CupBracket: React.FC<{ bracket: CupBracketDto; userTeamId?: string | null }> = ({
  bracket,
  userTeamId
}) => {
  const navigate = useNavigate();

  if (bracket.rounds.length === 0) {
    return (
      <div className="cup-bracket-empty">
        <div className="league-empty">O chaveamento da copa ainda não foi sorteado.</div>
      </div>
    );
  }

  return (
    <div className="cup-bracket-scroll">
      <div className="cup-bracket" style={{ gridTemplateColumns: `repeat(${bracket.rounds.length}, minmax(186px, 1fr))` }}>
        {bracket.rounds.map(round => (
          <section key={round.roundNumber} className="cup-bracket__round">
            <h3 className="cup-bracket__title">{round.name}</h3>
            <div className="cup-bracket__ties">
              {round.ties.map(tie => (
                <TieCard key={tie.tieId} tie={tie} userTeamId={userTeamId} onNavigate={navigate} />
              ))}
            </div>
          </section>
        ))}
      </div>

      {bracket.championTeamName && (
        <div className="cup-champion">
          <CupTrophy size={28} className="cup-champion__trophy" title="Troféu da copa" />
          <span>
            Campeão da {bracket.competitionName}: <b>{bracket.championTeamName}</b>
            {bracket.runnerUpTeamName && <span className="cup-champion__runner"> · Vice: {bracket.runnerUpTeamName}</span>}
          </span>
        </div>
      )}
    </div>
  );
};

/** One tie: the two clubs, the two legs, and the aggregate. */
const TieCard: React.FC<{
  tie: CupBracketTieDto;
  userTeamId?: string | null;
  onNavigate: (path: string) => void;
}> = ({ tie, userTeamId, onNavigate }) => {
  const [home, away] = tie.clubs;

  const goToMatch = (matchId?: string | null) => {
    if (matchId) onNavigate(`/match/${matchId}`);
  };

  return (
    <div className={`cup-tie ${tie.clubs.some(club => club.isWinner) ? 'cup-tie--decided' : ''}`}>
      <ClubLine club={home} matchId={tie.firstLegMatchId} userTeamId={userTeamId} onGoToMatch={goToMatch} />
      <ClubLine club={away} matchId={tie.secondLegMatchId} userTeamId={userTeamId} onGoToMatch={goToMatch} />
      <div className="cup-tie__aggregate">
        {home?.aggregateGoals != null ? (
          <>
            <span>Agregado {home.aggregateGoals} x {home.aggregateConceded}</span>
            {home.penaltyGoals != null && (
              <span className="cup-tie__penalties">
                Pênaltis {home.penaltyGoals} x {away?.penaltyGoals}
              </span>
            )}
          </>
        ) : (
          <span className="cup-tie__pending">Em andamento</span>
        )}
        <div className="cup-tie__watch">
          {tie.firstLegMatchId && (
            <button
              type="button"
              className="cup-tie__watch-btn"
              onClick={() => goToMatch(tie.firstLegMatchId)}
              title="Assistir ao 1º jogo"
            >
              1º jogo
            </button>
          )}
          {tie.secondLegMatchId && (
            <button
              type="button"
              className="cup-tie__watch-btn"
              onClick={() => goToMatch(tie.secondLegMatchId)}
              title="Assistir ao 2º jogo"
            >
              2º jogo
            </button>
          )}
        </div>
      </div>
    </div>
  );
};

/**
 * One club's line of a tie. The winner is marked by the line, not by a trophy in it: a bracket
 * with two winners in it is a bracket nobody can read, and the club that goes through is the one
 * whose line carries the day.
 *
 * A leg's score is the door to that leg's match, for both clubs. The two legs swap ends, so the
 * away club's first leg is the same match the home club's first leg is — which is exactly why
 * the score is linked and not merely printed: it is the number a manager wants to click, and
 * the id it is wired to is the one the backend read it out of.
 */
const ClubLine: React.FC<{
  club?: CupBracketClubDto;
  matchId?: string | null;
  userTeamId?: string | null;
  onGoToMatch: (matchId?: string | null) => void;
}> = ({ club, matchId, userTeamId, onGoToMatch }) => {
  if (!club) return null;

  const isMine = !!userTeamId && club.teamId === userTeamId;

  return (
    <div
      className={[
        'cup-club',
        club.isWinner ? 'cup-club--winner' : '',
        club.isLoser ? 'cup-club--loser' : '',
        isMine ? 'cup-club--mine' : ''
      ]
        .filter(Boolean)
        .join(' ')}
    >
      <span className="cup-club__crest">
        <ClubCrest
          primary={club.primaryColor}
          secondary={club.secondaryColor}
          name={club.name}
          className="mini-crest"
        />
      </span>
      <span className="cup-club__name">
        <ClubName teamId={club.teamId}>{club.name}</ClubName>
        {isMine && <span className="cup-club__you" title="O seu clube">Você</span>}
      </span>
      <span className="cup-club__legs">
        <LegScore
          score={legScore(club.firstLegGoals, club.firstLegConceded)}
          title="Primeiro jogo"
          matchId={matchId}
          onGoToMatch={onGoToMatch}
        />
        <LegScore
          score={legScore(club.secondLegGoals, club.secondLegConceded)}
          title="Segundo jogo"
          matchId={matchId}
          onGoToMatch={onGoToMatch}
        />
      </span>
    </div>
  );
};

/**
 * One leg's score, which is a link when the leg has been played and plain text when it has not.
 *
 * A leg that was never played has no match to go to, so it prints a dash and offers nothing —
 * a button that goes nowhere is worse than a number that does not, because a manager presses it
 * expecting the whistle.
 */
const LegScore: React.FC<{
  score: string;
  title: string;
  matchId?: string | null;
  onGoToMatch: (matchId?: string | null) => void;
}> = ({ score, title, matchId, onGoToMatch }) => {
  if (!matchId) {
    return <span title={title}>{score}</span>;
  }

  return (
    <button
      type="button"
      className="cup-club__leg"
      title={`${title} — ver a partida`}
      onClick={() => onGoToMatch(matchId)}
    >
      {score}
    </button>
  );
};

export default CupBracket;
