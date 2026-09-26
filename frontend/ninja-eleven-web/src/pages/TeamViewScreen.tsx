import React, { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { SeasonApi, TeamApi } from '@/api';
import { useGameState } from '@/state';
import type { Position, SquadPlayerDto, TeamDto } from '@/types';
import { positionLabel } from '@/services/formatters';
import EnergyBar from '@/components/Match/EnergyBar';
import { PlayerName } from '@/components/Common/Names';

const POSITION_ORDER: Position[] = ['GK', 'DEF', 'MID', 'ATT'];

const POSITION_LABELS: Record<Position, string> = {
  GK: 'Goleiros',
  DEF: 'Defesa',
  MID: 'Meio-campo',
  ATT: 'Ataque'
};

/**
 * What keeps a player out of the squad, said in the words a manager uses. An injury is
 * only meaningful with the number of matches it still costs him.
 */
function availabilityOf(player: SquadPlayerDto): string {
  if (player.suspensionMatches > 0) {
    return `🚫 Suspenso (${player.suspensionMatches})`;
  }

  if (player.injury !== 'None') {
    const severity = player.injury === 'Grave' ? 'Lesão grave' : 'Lesão leve';
    return player.injuryMatchesRemaining > 0
      ? `🩹 ${severity} (${player.injuryMatchesRemaining})`
      : `🩹 ${severity}`;
  }

  return 'Apto';
}

const TeamViewScreen: React.FC<{ teamId?: string }> = ({ teamId: propTeamId }) => {
  const teams = useGameState((s) => s.leagueTeams);
  const [team, setTeam] = useState<TeamDto | null>(null);
  const [players, setPlayers] = useState<SquadPlayerDto[]>([]);
  const [error, setError] = useState<string | null>(null);

  const navigate = useNavigate();
  const { teamId: routeTeamId = '' } = useParams();
  const [params] = useSearchParams();
  const urlTeamId = propTeamId || routeTeamId;
  const seasonId = params.get('season') || '';

  useEffect(() => {
    const teamObj = teams.find(t => t.id === urlTeamId);
    if (teamObj) setTeam(teamObj);
  }, [teams, urlTeamId]);

  // The club list is remembered in this browser, but the root of the game is now this
  // screen, so a manager can arrive here by typing the address with nothing remembered. The
  // club is then asked for by name rather than drawn as a blank page.
  useEffect(() => {
    if (!urlTeamId || teams.some(t => t.id === urlTeamId)) return undefined;

    let cancelled = false;

    TeamApi.get(urlTeamId)
      .then(loaded => {
        if (!cancelled) setTeam(loaded);
      })
      .catch(err => {
        console.error('Failed to load the club:', err);
        if (!cancelled) setError('Não foi possível carregar o clube.');
      });

    return () => {
      cancelled = true;
    };
  }, [urlTeamId, teams]);

  useEffect(() => {
    if (!urlTeamId) return undefined;

    // The squad belongs to a season. The screen can be opened without the query, so
    // the current season is what it falls back to.
    let cancelled = false;

    const load = async () => {
      const season = seasonId || (await SeasonApi.current()).id;
      if (cancelled) return;
      setPlayers(await TeamApi.getSquad(urlTeamId, season));
    };

    load().catch(error => {
      if (!cancelled) console.error('Failed to load the squad:', error);
    });

    return () => {
      cancelled = true;
    };
  }, [urlTeamId, seasonId]);

  /**
   * The squad as a manager reads it: by position, and by name inside the position.
   */
  const rowsByPosition = useMemo(
    () =>
      POSITION_ORDER.map(position => ({
        position,
        label: POSITION_LABELS[position],
        players: players
          .filter(player => player.position === position)
          .sort((a, b) => a.name.localeCompare(b.name, 'pt-BR'))
      })).filter(group => group.players.length > 0),
    [players]
  );

  if (error) {
    return (
      <div className="card team-view-card">
        <p className="competition" style={{ color: 'var(--danger)' }}>{error}</p>
        <button className="ctrl" onClick={() => navigate('/')}>Voltar</button>
      </div>
    );
  }

  if (!team) return null;

  return (
    <div className="team-view-overlay">
      <div className="card team-view-card">
        <div className="squad-head team-view-summary">
          <div>
            <h2>{team.name}</h2>
            <p>Força {team.rating} • {players.length} jogadores</p>
          </div>
          <button className="ctrl" onClick={() => navigate('/league')}>Tabela e jogos</button>
        </div>

        <div className="selection-bar team-view-summary">
          <div>
            <b>{team.name} • Elenco completo</b>
            <div className="squad-hint">Acompanhe energia, atributos, gols, cartões e disponibilidade.</div>
          </div>
          <div
            className="team-color-sample"
            style={{
              '--team-primary': team.primaryColor,
              '--team-secondary': team.secondaryColor,
            } as React.CSSProperties}
          />
        </div>

        <div className="squad-table-wrap">
          <table className="squad-table team-view-table">
            <thead>
              <tr>
                <th>Pos</th>
                <th>Jogador</th>
                <th>Idade</th>
                <th>Energia</th>
                <th>Atributos</th>
                <th>Temporada</th>
              </tr>
            </thead>
            {rowsByPosition.map(group => (
              <tbody key={group.position}>
                <tr className="squad-group">
                  <th colSpan={6} scope="colgroup">
                    {group.label}
                  </th>
                </tr>

                {group.players.map(p => (
                  <tr key={p.id} className={p.isAvailable ? 'squad-row' : 'squad-row unavailable'}>
                    <td className="col-pos">
                      <span className="pos-badge">{positionLabel(p.position)}</span>
                    </td>
                    <td className="col-name">
                      <PlayerName playerId={p.id}>
                        <b>{p.name}</b>
                      </PlayerName>
                    </td>
                    <td className="col-num">{p.age}</td>
                    <td className="col-num">
                      <EnergyBar value={p.energy} compact />
                      <span style={{ fontSize: '10px' }}>{Math.round(p.energy)}%</span>
                    </td>
                    <td style={{ fontSize: '10px', color: 'var(--muted)' }}>
                      {p.position === 'GK'
                        ? `Gol ${p.goalkeeperPower} • Ref ${p.reflexes} • Vel ${p.speed}`
                        : `Vel ${p.speed} • Des ${p.accuracy} • Dri ${p.dribbling} • Cab ${p.heading} • For ${p.strength}`}
                    </td>
                    <td className="col-status">
                      {p.goals} gol{p.goals !== 1 ? 's' : ''} • CA {p.yellowCards}/3
                      {p.redCards > 0 && ` • 🟥 ${p.redCards}`}
                      <br />
                      {availabilityOf(p)}
                    </td>
                  </tr>
                ))}
              </tbody>
            ))}
          </table>
        </div>
      </div>
    </div>
  );
};

export default TeamViewScreen;
