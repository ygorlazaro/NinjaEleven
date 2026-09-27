import React from 'react';
import type { StandingDto, TableZone, TeamDto } from '@/types';
import { ClubName } from '@/components/Common/Names';
import StarRating from '@/components/Common/StarRating';

interface StandingsTableProps {
  standings: StandingDto[];
  userId?: string;
  teams: TeamDto[];
}

/**
 * The classification table, in the order the backend put it in.
 *
 * There is no sort here. The order is the promotion rule — points, goal difference, goals
 * scored, the head-to-head, the cards, and finally the squad — and a screen that sorted the
 * lines again would be applying its own subset of it: the same clubs, the same numbers, a
 * different order, and a manager who is told his club is sixth by one and seventh by the other.
 *
 * The position is shown as it arrives rather than as the index of the row, so a table that
 * ever arrived out of order would look out of order instead of quietly looking right.
 *
 * The green and red bands are tagged by the backend and only painted here: the promotion race
 * is the top four and the relegation battle the bottom four, and a screen that guessed the
 * bands would be guessing a rule it has no copy of to disagree with the season's end. The zone
 * is highlighted, not recomputed.
 */
const StandingsTable: React.FC<StandingsTableProps> = ({ standings, userId, teams }) => {
  const teamMap = teams.reduce<Record<string, TeamDto>>((acc, team) => {
    acc[team.id] = team;
    return acc;
  }, {});

  return (
    <>
      <table className="standings">
        <thead>
          <tr>
            <th>#</th>
            <th>Time</th>
            <th>★</th>
            <th>P</th>
            <th>J</th>
            <th>V</th>
            <th>E</th>
            <th>D</th>
            <th>SG</th>
            <th>GP</th>
            <th>CA</th>
            <th>CV</th>
          </tr>
        </thead>
        <tbody>
          {standings.map(row => {
            const team = teamMap[row.teamId] || row.team;
            if (!team) return null;

            const isUser = team.id === userId;
            const colors = team.primaryColor || '#f2d34f';
            const stars = row.stars ?? team.stars ?? 0;
            const zone: TableZone = row.zone ?? 'None';

            const rowClass = [
              isUser ? 'user-row' : '',
              zone === 'Promotion' ? 'promotion' : '',
              zone === 'Relegation' ? 'relegation' : '',
            ]
              .filter(Boolean)
              .join(' ');

            return (
              <tr
                key={row.teamId}
                className={rowClass || undefined}
                style={{
                  '--team-primary': colors,
                  '--team-secondary': team.secondaryColor || '#f2d34f'
                } as React.CSSProperties}
              >
                <td>{row.position}</td>
                <td>
                  <span
                    className="team-dot"
                    style={{
                      '--team-primary': colors,
                      '--team-secondary': team.secondaryColor || '#f2d34f'
                    } as React.CSSProperties}
                  ></span>
                  <ClubName teamId={team.id}>{team.name}</ClubName>
                </td>
                <td>
                  <StarRating stars={stars} />
                </td>
                <td><b>{row.points}</b></td>
                <td>{row.played}</td>
                <td>{row.wins}</td>
                <td>{row.draws}</td>
                <td>{row.losses}</td>
                <td>{row.goalDifference}</td>
                <td>{row.goalsFor}</td>
                <td>{row.yellowCards}</td>
                <td>{row.redCards}</td>
              </tr>
            );
          })}
        </tbody>
      </table>

      {standings.length > 0 && (
        <div className="standings-legend">
          <strong>Legenda:</strong>
          <span className="zone-promotion">4 primeiras — zona de promoção</span>
          <span className="zone-relegation">4 últimas — zona de descenso</span>
        </div>
      )}
    </>
  );
};

export default StandingsTable;
