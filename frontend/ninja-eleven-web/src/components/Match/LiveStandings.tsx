import { ClubName } from '@/components/Common/Names';
import { starsToString } from '@/services/formatters';
import type { StandingDto, TeamDto } from '@/types';
import React, { useMemo } from 'react';

interface LiveStandingsProps {
  standings: StandingDto[];
  matchId: string;
  homeTeamId: string;
  awayTeamId: string;
  homeScore: number;
  awayScore: number;
  homeTeamName: string;
  awayTeamName: string;
  matchStatus: string;
}

/**
 * Shows the league table with live projection based on the current match score.
 * Official standings only include finished matches; projected standings include
 * the current match's score if it's in progress.
 */
const LiveStandings: React.FC<LiveStandingsProps> = ({
  standings,
  matchId,
  homeTeamId,
  awayTeamId,
  homeScore,
  awayScore,
  homeTeamName,
  awayTeamName,
  matchStatus
}) => {
  const teamMap = useMemo(() => {
    const map: Record<string, TeamDto> = {};
    standings.forEach(s => {
      if (s.team) map[s.teamId] = s.team;
    });
    return map;
  }, [standings]);

  const isLive = matchStatus === 'InProgress' || matchStatus === 'HalfTime';

  // Calculate projected points for the current match
  const projectedPoints: Record<string, number> = useMemo(() => {
    const points: Record<string, number> = {};
    if (!isLive) return points;

    if (homeScore > awayScore) {
      points[homeTeamId] = 3;
    } else if (homeScore < awayScore) {
      points[awayTeamId] = 3;
    } else {
      points[homeTeamId] = 1;
      points[awayTeamId] = 1;
    }
    return points;
  }, [isLive, homeTeamId, awayTeamId, homeScore, awayScore]);

  // Calculate projected stats for each team
  const getProjectedStats = (standing: StandingDto) => {
    const extraPoints = projectedPoints[standing.teamId] ?? 0;
    const projectedPointsTotal = standing.points + extraPoints;
    let projectedGD = standing.goalDifference;
    let projectedGF = standing.goalsFor;
    let projectedPlayed = standing.played;
    let projectedWins = standing.wins;
    let projectedDraws = standing.draws;
    let projectedLosses = standing.losses;

    if (standing.teamId === homeTeamId) {
      projectedGD += homeScore - awayScore;
      projectedGF += homeScore;
      projectedPlayed += 1;
      if (isLive) {
        if (homeScore > awayScore) projectedWins += 1;
        else if (homeScore === awayScore) projectedDraws += 1;
        else projectedLosses += 1;
      }
    } else if (standing.teamId === awayTeamId) {
      projectedGD += awayScore - homeScore;
      projectedGF += awayScore;
      projectedPlayed += 1;
      if (isLive) {
        if (awayScore > homeScore) projectedWins += 1;
        else if (homeScore === awayScore) projectedDraws += 1;
        else projectedLosses += 1;
      }
    }

    return {
      points: projectedPointsTotal,
      played: projectedPlayed,
      wins: projectedWins,
      draws: projectedDraws,
      losses: projectedLosses,
      goalDifference: projectedGD,
      goalsFor: projectedGF,
      isProjected: extraPoints > 0
    };
  };

  // Build projected standings
  const projectedStandings = useMemo(() => {
    return [...standings].map(s => ({
      ...s,
      ...getProjectedStats(s)
    })).sort((a, b) => {
      // Sort by: points DESC, then goalDifference DESC, then goalsFor DESC
      const pointsDiff = b.points - a.points;
      if (pointsDiff !== 0) return pointsDiff;
      
      const gdDiff = b.goalDifference - a.goalDifference;
      if (gdDiff !== 0) return gdDiff;
      
      return b.goalsFor - a.goalsFor;
    });
  }, [standings, projectedPoints, homeTeamId, awayTeamId, homeScore, awayScore, isLive]);

  // Find position changes
  const officialPositions = useMemo(() => {
    const positions: Record<string, number> = {};
    [...standings].sort((a, b) =>
      b.points - a.points ||
      b.goalDifference - a.goalDifference ||
      b.goalsFor - a.goalsFor
    ).forEach((s, i) => { positions[s.teamId] = i + 1; });
    return positions;
  }, [standings]);

  const projectedPositions = useMemo(() => {
    const positions: Record<string, number> = {};
    projectedStandings.forEach((s, i) => { positions[s.teamId] = i + 1; });
    return positions;
  }, [projectedStandings]);

  if (standings.length === 0) {
    return <div className="league-empty">Nenhuma classificação disponível.</div>;
  }

  return (
    <div className="live-standings">
      {isLive && (
        <div className="live-badge" style={{ marginBottom: '8px', padding: '6px 10px', background: 'var(--accent)', color: 'var(--bg)', borderRadius: '4px', fontSize: '12px', fontWeight: 'bold', display: 'inline-flex', alignItems: 'center', gap: '6px' }}>
          <span style={{ animation: 'pulse 1s infinite' }}>●</span>
          Classificação ao vivo (projetada) — inclui o placar atual desta partida
        </div>
      )}

      {isLive && (
        <div style={{ fontSize: '11px', color: 'var(--muted)', marginBottom: '8px', padding: '8px', background: 'rgba(255, 193, 7, 0.1)', border: '1px solid var(--accent)', borderRadius: '4px' }}>
          <strong>⚠ Projeção baseada no placar atual:</strong> Times desta partida têm pontos, SG e GP projetados. 
          A classificação oficial só atualiza ao fim da partida.
        </div>
      )}

      <table className="standings">
        <thead>
          <tr>
            <th style={{ width: '36px' }}>#</th>
            <th>Time</th>
            <th style={{ width: '50px', textAlign: 'center' }}>★</th>
            <th style={{ width: '40px', textAlign: 'center' }}>P</th>
            <th style={{ width: '30px', textAlign: 'center' }}>J</th>
            <th style={{ width: '30px', textAlign: 'center' }}>V</th>
            <th style={{ width: '30px', textAlign: 'center' }}>E</th>
            <th style={{ width: '30px', textAlign: 'center' }}>D</th>
            <th style={{ width: '40px', textAlign: 'center' }}>SG</th>
            <th style={{ width: '40px', textAlign: 'center' }}>GP</th>
          </tr>
        </thead>
        <tbody>
          {projectedStandings.map((s, i) => {
            const team = teamMap[s.teamId] || s.team;
            if (!team) return null;

            const officialPos = officialPositions[s.teamId] ?? i + 1;
            const projectedPos = projectedPositions[s.teamId] ?? i + 1;
            const positionChange = officialPos - projectedPos;
            const isProjected = projectedPoints[s.teamId] > 0;
            const isHome = s.teamId === homeTeamId;
            const isAway = s.teamId === awayTeamId;

            const proj = getProjectedStats(s);

            let posIndicator = '';
            if (isLive && positionChange !== 0) {
              posIndicator = positionChange > 0
                ? <span className="pos-change up" title={`Subiu ${positionChange} posição(ões)`}>↑{positionChange}</span>
                : <span className="pos-change down" title={`Desceu ${Math.abs(positionChange)} posição(ões)`}>↓{Math.abs(positionChange)}</span>;
            }

            return (
              <tr
                key={s.teamId}
                className={[
                  isProjected ? 'projected' : '',
                  isHome || isAway ? 'current-match' : ''
                ].filter(Boolean).join(' ') || undefined}
                style={{ '--team-primary': team.primaryColor, '--team-secondary': team.secondaryColor } as React.CSSProperties}>
                <td>
                  <span className="position-cell">
                    {projectedPos}
                    {posIndicator}
                  </span>
                </td>
                <td>
                  <span className="team-dot" style={{ '--team-primary': team.primaryColor, '--team-secondary': team.secondaryColor }}></span>
                  <ClubName teamId={team.id}>{team.name}</ClubName>
                  {isHome && <span className="match-indicator home" title="Mandante nesta partida"> 🏠</span>}
                  {isAway && <span className="match-indicator away" title="Visitante nesta partida"> ✈️</span>}
                </td>
                <td style={{ textAlign: 'center', color: 'var(--accent)', fontWeight: 'bold' }}>
                  {starsToString(s.stars ?? team.stars ?? 0)}
                </td>
                <td style={{ textAlign: 'center' }}>
                  <b>{proj.points}</b>
                  {isProjected && (
                    <span className="points-delta" style={{ fontSize: '10px', color: 'var(--accent)', marginLeft: '4px' }}>
                      (+{projectedPoints[s.teamId]})
                    </span>
                  )}
                </td>
                <td style={{ textAlign: 'center' }}>{proj.played}</td>
                <td style={{ textAlign: 'center' }}>{proj.wins}</td>
                <td style={{ textAlign: 'center' }}>{proj.draws}</td>
                <td style={{ textAlign: 'center' }}>{proj.losses}</td>
                <td style={{ textAlign: 'center' }}>{proj.goalDifference}</td>
                <td style={{ textAlign: 'center' }}>{proj.goalsFor}</td>
              </tr>
            )
          })}
        </tbody>
      </table>

      {isLive && (
        <div className="standings-legend" style={{ marginTop: '10px', fontSize: '11px', color: 'var(--muted)', lineHeight: '1.6' }}>
          <strong>Legenda:</strong>
          <ul style={{ margin: '4px 0 0 16px', padding: 0 }}>
            <li>Linhas destacadas: times desta partida (mandante 🏠 / visitante ✈️)</li>
            <li>Pontos em <b>negrito</b> incluem o resultado projetado desta partida</li>
            <li><span className="pos-change up">↑</span>/<span className="pos-change down">↓</span> Mudança de posição vs. classificação oficial</li>
            <li>A classificação oficial só é atualizada quando a partida termina</li>
          </ul>
        </div>
      )}
    </div>
  );
};

export default LiveStandings;
