import React from 'react';
import type { MatchdayReportDto } from '@/types';

interface MatchdayReportProps {
  report: MatchdayReportDto | null;
  userTeamId?: string;
  onSelect: (matchId: string) => void;
}

/**
 * A played round, told back: the scoreline, and underneath it the account the match gave
 * of itself.
 *
 * The summary is the goals' own words, read back from the events the match recorded, and
 * the screen only decides where to put them. A table of results is the shape of a league
 * and says nothing about any of it — "2 x 1" does not say who was waiting for the ball in
 * the area at the 30th minute, and the match already said. This panel is that saying,
 * kept.
 */
const MatchdayReportPanel: React.FC<MatchdayReportProps> = ({ report, userTeamId, onSelect }) => {
  if (!report || report.entries.length === 0) {
    return null;
  }

  return (
    <div className="matchday-report">
      <h4 className="matchday-report-title">Como foi a rodada {report.roundNumber}</h4>

      {report.entries.map(entry => {
        const isUser = userTeamId === entry.homeTeamId || userTeamId === entry.awayTeamId;

        return (
          <button
            key={entry.matchId}
            className={`matchday-report-entry ${isUser ? 'user-entry' : ''}`}
            onClick={() => onSelect(entry.matchId)}
          >
            <span className="matchday-report-score">
              <b>{entry.homeShortName || entry.homeTeamName}</b>
              <span className="matchday-report-goals">
                {entry.homeGoals}&nbsp;<i>x</i>&nbsp;{entry.awayGoals}
              </span>
              <b>{entry.awayShortName || entry.awayTeamName}</b>
            </span>
            <span className="matchday-report-summary">{entry.summary}</span>
          </button>
        );
      })}
    </div>
  );
};

export default MatchdayReportPanel;
