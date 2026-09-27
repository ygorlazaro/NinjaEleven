import React from 'react';
import type { FeedEvent } from '@/state';
import { ClubName } from '@/components/Common/Names';
import { useNavigate } from 'react-router-dom';

/** Live score of one match of the round, as pushed by the backend. */
export interface MatchScore {
  roundId: string;
  matchId: string;
  fixtureId: string;
  homeTeamId: string;
  homeTeamName: string;
  homeShortName: string;
  awayTeamId: string;
  awayTeamName: string;
  awayShortName: string;
  homeGoals: number;
  awayGoals: number;
  minute: number;
  half: string;
  status: string;
  isFinished: boolean;
  /**
   * What the match produced besides goals. A scoreboard that can only say 1 x 0 does not
   * tell a manager whether the side in front of him won a game or survived one, and these
   * are the numbers that answer it.
   */
  homeOwnGoals: number;
  awayOwnGoals: number;
  homeYellowCards: number;
  awayYellowCards: number;
  homeRedCards: number;
  awayRedCards: number;
  homeInjuries: number;
  awayInjuries: number;
}

interface MatchdayScoreboardProps {
  scores: MatchScore[];
  /** Match the client is watching in full; it is shown apart from the others. */
  currentMatchId: string;
  userTeamId?: string;
  /**
   * The last beats of each other match, by match id. They come from the same events the
   * match told its own followers, so a goal next door is worded the way that match worded
   * it rather than in a summary written here.
   */
  eventsByMatch?: Record<string, FeedEvent[]>;
}

/**
 * One match of the round, as a line: the two clubs, the score, and everything the match
 * did besides scoring. A mark is only drawn when the number behind it is not zero, so a
 * quiet match looks quiet instead of decorated.
 */
const Mark: React.FC<{ icon: string; value: number; title: string }> = ({ icon, value, title }) =>
  value > 0 ? (
    <span className="mark" title={title}>
      {icon}
      {value > 1 ? value : ''}
    </span>
  ) : null;

/**
 * One match of the round, as a line: the two clubs, the score, and everything the match
 * did besides scoring. A mark is only drawn when the number behind it is not zero, so a
 * quiet match looks quiet instead of decorated.
 */
const MatchdayScoreboard: React.FC<MatchdayScoreboardProps> = ({
  scores,
  currentMatchId,
  userTeamId,
  eventsByMatch = {},
}) => {
  const navigate = useNavigate();
  const others = scores.filter(score => score.matchId !== currentMatchId);
  if (others.length === 0) return null;

  const handleMatchClick = (matchId: string) => {
    if (matchId) {
      navigate(`/match/${matchId}`);
    }
  };

  return (
    <div className="matchday">
      {others.map(score => {
        const involvesUser =
          !!userTeamId && (score.homeTeamId === userTeamId || score.awayTeamId === userTeamId);
        const minute = score.isFinished ? 'FIM' : `${score.minute}'`;
        const beats = eventsByMatch[score.matchId] ?? [];
        // The last thing the match said, and not the last goal: a manager watching his own
        // match would rather read that the game is quiet than that a team is 1 x 0 up with
        // nothing since the fortieth minute.
        const last = beats[beats.length - 1];

        return (
          <div
            key={score.matchId}
            className={`matchday-row ${score.isFinished ? 'played' : 'live'} ${involvesUser ? 'user-fixture' : ''} ${score.matchId ? 'clickable' : ''}`}
            onClick={() => handleMatchClick(score.matchId)}
            style={{ cursor: score.matchId ? 'pointer' : 'default' }}
          >
            <div className="matchday-teams">
              <span className="dot" aria-hidden="true" />
              <span className="name" title={score.homeTeamName}>
                <ClubName teamId={score.homeTeamId}>{score.homeTeamName}</ClubName>
              </span>
              <span className="name right" title={score.awayTeamName}>
                <ClubName teamId={score.awayTeamId}>{score.awayTeamName}</ClubName>
              </span>
            </div>

            <div className="matchday-score">
              {score.homeGoals} <b>×</b> {score.awayGoals}
              <span className="minute">{minute}</span>
            </div>

            <div className="matchday-marks">
              <div className="marks home">
                <Mark icon="🔴" value={score.homeOwnGoals} title={`${score.homeOwnGoals} gol(s) contra`} />
                <Mark icon="🟨" value={score.homeYellowCards} title={`${score.homeYellowCards} cartão(s) amarelo(s)`} />
                <Mark icon="🟥" value={score.homeRedCards} title={`${score.homeRedCards} expulsão(ões)`} />
                <Mark icon="🚑" value={score.homeInjuries} title={`${score.homeInjuries} lesão(ões)`} />
              </div>
              <div className="marks away">
                <Mark icon="🔴" value={score.awayOwnGoals} title={`${score.awayOwnGoals} gol(s) contra`} />
                <Mark icon="🟨" value={score.awayYellowCards} title={`${score.awayYellowCards} cartão(s) amarelo(s)`} />
                <Mark icon="🟥" value={score.awayRedCards} title={`${score.awayRedCards} expulsão(ões)`} />
                <Mark icon="🚑" value={score.awayInjuries} title={`${score.awayInjuries} lesão(ões)`} />
              </div>
            </div>

            {last && (
              <div className={`matchday-beat${last.isGoal ? ' goal' : ''}`} title={last.description}>
                <span className="matchday-beat-minute">{last.minute}'</span>
                <span className="matchday-beat-text">{last.description}</span>
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
};

export default MatchdayScoreboard;
