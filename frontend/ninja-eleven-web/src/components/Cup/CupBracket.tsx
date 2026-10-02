import React from 'react';
import { useNavigate } from 'react-router-dom';
import type { CupBracketClubDto, CupBracketDto, CupBracketTieDto } from '@/types';
import { ClubName } from '@/components/Common/Names';
import ClubCrest from '@/components/Club/ClubCrest';
import CupTrophy from '@/components/Cup/CupTrophy';

/** A leg's score from the first-named club's side, or a dash while the leg is still to be played. */
const legScore = (goals?: number | null, conceded?: number | null) =>
  goals == null || conceded == null ? '–' : `${goals} x ${conceded}`;

/**
 * One cup, as a bracket read downwards, most recent round first.
 *
 * **The latest round is on top.** A manager opens the cup to find out where his club is, and the
 * round that answers that is the last one played — the one whose tie, if any, is still being
 * watched. The bracket arrives oldest first, because that is the order the cup was played in, and
 * it is reversed here rather than in the service: the order a thing was read is a fact, the order
 * a manager wants to read it in is a screen's decision, and reversing it in the service would
 * make the API answer a different question from the one the answer is for.
 *
 * **It goes down the page and not across it.** The bracket was a row of columns, which is the
 * shape the sport is drawn in and the worst possible shape for a screen: six rounds of sixteen,
 * eight, four, two and one ties laid side by side is a wall of a thousand pixels wide, and a
 * manager reads it by dragging a horizontal scrollbar to reach the final. Stacked, the same
 * bracket is a column the page already scrolls, and a tie card is as wide as the window instead
 * of being squeezed into 186 pixels to fit six of them on a laptop. The cards are thicker for it
 * and the club name may wrap — which is a better trade than a name cut off with an ellipsis.
 *
 * **Four ties to a line.** Two was a column of empty space the width of a card and a half, and
 * five leaves a card narrower than the club name it has to carry. Four is what two cards and a
 * gap need on the narrowest window this game is read on, and it reflows to one on a phone.
 *
 * **Each game is printed once.** The two legs swap ends, so the two clubs' own numbers are the
 * same two matches read from opposite sides: a card carrying both printed `3 x 1` and `1 x 3`
 * twice, and a manager could not tell a second goal from a second game. The games are read off
 * the first-named club and labelled as the first and the second, so every match on the card is a
 * match nobody has read yet. The aggregate below them is the one number both clubs share, and it
 * is printed once.
 *
 * Only the rounds the cup has drawn are here. The round after a tie is decided does not exist
 * yet, so the bracket ends where the football has got to.
 */
const CupBracket: React.FC<{ bracket: CupBracketDto; userTeamId?: string | null }> = ({
  bracket,
  userTeamId
}) => {
  const navigate = useNavigate();

  // The most recent round first: the round a manager opens the cup to see is the last one played.
  const rounds = [...bracket.rounds].sort((left, right) => right.roundNumber - left.roundNumber);

  if (rounds.length === 0) {
    return (
      <div className="cup-bracket-empty">
        <div className="league-empty">O chaveamento da copa ainda não foi sorteado.</div>
      </div>
    );
  }

  return (
    <div className="cup-bracket-scroll">
      <div className="cup-bracket">
        {rounds.map(round => (
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

/**
 * One tie: the two clubs, the two games, and the aggregate.
 *
 * The winner is marked by the club line, not by a trophy in the card: a bracket with two winners
 * in it is a bracket nobody can read.
 */
const TieCard: React.FC<{
  tie: CupBracketTieDto;
  userTeamId?: string | null;
  onNavigate: (path: string) => void;
}> = ({ tie, userTeamId, onNavigate }) => {
  const [home, away] = tie.clubs;

  const goToMatch = (matchId?: string | null) => {
    if (matchId) onNavigate(`/match/${matchId}`);
  };

  // The games are read off one club so that each of them is printed once. Which club is that is
  // the tie's own first-named side, so a card always reads top to bottom in the same order the
  // two club lines above it do.
  const firstLeg = {
    goals: home?.firstLegGoals,
    conceded: home?.firstLegConceded
  };
  const secondLeg = {
    goals: home?.secondLegGoals,
    conceded: home?.secondLegConceded
  };

  return (
    <div className={`cup-tie ${tie.clubs.some(club => club.isWinner) ? 'cup-tie--decided' : ''}`}>
      <ClubLine club={home} userTeamId={userTeamId} />
      <ClubLine club={away} userTeamId={userTeamId} />

      {/* The two games, each printed once and each a door to its own match. A game that was
          never played has no match to go to, so it prints a dash and offers nothing — a button
          that goes nowhere is worse than a number that does not. */}
      <div className="cup-tie__games">
        <GameScore
          label="1º jogo"
          score={legScore(firstLeg.goals, firstLeg.conceded)}
          matchId={tie.firstLegMatchId}
          isLive={tie.firstLegLive}
          onGoToMatch={goToMatch}
        />
        <GameScore
          label="2º jogo"
          score={legScore(secondLeg.goals, secondLeg.conceded)}
          matchId={tie.secondLegMatchId}
          isLive={tie.secondLegLive}
          onGoToMatch={goToMatch}
        />
      </div>

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
        ) : tie.firstLegLive || tie.secondLegLive ? (
          <span className="cup-tie__live">Ao vivo</span>
        ) : (
          <span className="cup-tie__pending">Em andamento</span>
        )}
      </div>
    </div>
  );
};

/**
 * One club's line of a tie: its shield, its name, and nothing else.
 *
 * The numbers that used to sit here — the club's own two legs — are in the tie's games line now,
 * because printed on both lines they said the same two matches twice.
 */
const ClubLine: React.FC<{
  club?: CupBracketClubDto;
  userTeamId?: string | null;
}> = ({ club, userTeamId }) => {
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
    </div>
  );
};

/**
 * One game of a tie, which is a link when it has been played or is live, and plain text when it
 * has not.
 */
const GameScore: React.FC<{
  label: string;
  score: string;
  matchId?: string | null;
  isLive?: boolean;
  onGoToMatch: (matchId?: string | null) => void;
}> = ({ label, score, matchId, isLive, onGoToMatch }) => {
  if (!matchId) {
    return (
      <span className="cup-tie__game cup-tie__game--pending" title={label}>
        {label} {score}
      </span>
    );
  }

  return (
    <button
      type="button"
      className={`cup-tie__game${isLive ? ' cup-tie__game--live' : ''}`}
      title={`${label}${isLive ? ' — ao vivo' : ' — ver a partida'}`}
      onClick={() => onGoToMatch(matchId)}
    >
      {label} {score}
      {isLive && <span className="cup-club__live-badge" title="Ao vivo" />}
    </button>
  );
};

export default CupBracket;