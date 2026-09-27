import React from 'react';
import type { StandingDto, TeamDto } from '@/types';
import { ClubName } from '@/components/Common/Names';
import { starsToString } from '@/services/formatters';

interface StandingsTableProps {
  standings: StandingDto[];
  userId?: string;
  teams: TeamDto[];
}

const StandingsTable: React.FC<StandingsTableProps> = ({ standings, userId, teams }) => {
  const teamMap = teams.reduce((acc, t) => {
    acc[t.id] = t;
    return acc;
  }, {} as Record<string, TeamDto>);

  const sorted = [...standings].sort((a, b) =>
    b.points - a.points ||
    (b.goalsFor - b.goalsAgainst) - (a.goalsFor - a.goalsAgainst) ||
    b.goalsFor - a.goalsFor
  );

  return (
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
        {sorted.map((s, i) => {
          const team = teamMap[s.teamId] || s.team;
          if (!team) return null;
          const isUser = team.id === userId;
          const colors = team.primaryColor || '#f2d34f';
          // Use the stars from the standing if available, otherwise from the team
          const stars = s.stars ?? team.stars ?? 0;
          return (
            <tr key={s.teamId} className={isUser ? 'user-row' : ''} style={{ '--team-primary': colors, '--team-secondary': team.secondaryColor || '#f2d34f' } as React.CSSProperties}>
              <td>{i + 1}</td>
              <td>
                <span className="team-dot" style={{ '--team-primary': colors, '--team-secondary': team.secondaryColor || '#f2d34f' } as React.CSSProperties}></span>
                <ClubName teamId={team.id}>{team.name}</ClubName>
              </td>
              <td style={{ color: 'var(--accent)', fontWeight: 'bold' }}>{starsToString(stars)}</td>
              <td><b>{s.points}</b></td>
              <td>{s.played}</td>
              <td>{s.wins}</td>
              <td>{s.draws}</td>
              <td>{s.losses}</td>
              <td>{s.goalDifference}</td>
              <td>{s.goalsFor}</td>
              <td>{s.yellowCards}</td>
              <td>{s.redCards}</td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
};

export default StandingsTable;
