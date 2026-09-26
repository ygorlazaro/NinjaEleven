import React, { useEffect, useState } from 'react';
import { useGameState } from '@/state';
import type { PlayerDto, PlayerSeasonStateDto, TeamDto } from '@/types';
import { positionLabel, attrLine, teamAvailabilityLabel, isPlayerUnavailable } from '@/services/formatters';

const SquadScreen: React.FC = () => {
  const selectedTeam = useGameState((s) => s.selectedTeam);
  const leagueTeams = useGameState((s) => s.leagueTeams);
  const selectedSeasonId = useGameState((s) => s.selectedTeam);

  const [squad, setSquad] = useState<(PlayerDto & { state?: PlayerSeasonStateDto })[]>([]);
  const [selectedStarters, setSelectedStarters] = useState<Set<string>>(new Set());
  const [selectedSeason, setSelectedSeason] = useState<string>('');

  useEffect(() => {
    const loadSquad = async () => {
      if (selectedTeam?.id && selectedSeason) {
        const resp = await fetch(`/api/team/${selectedTeam.id}/squad/${selectedSeason}`);
        if (resp.ok) {
          const states: PlayerSeasonStateDto[] = await resp.json();
          const players = await fetch('/api/player').then(r => r.json());
          const playerMap = players.reduce((acc: Record<string, PlayerDto>, p: PlayerDto) => {
            acc[p.id] = p;
            return acc;
          }, {});

          const squadList = states
            .filter(s => s.teamId === selectedTeam.id || s.playerId in playerMap)
            .map(s => ({
              ...playerMap[s.playerId],
              ...s,
              state: s,
            }))
            .filter(p => p.id);

          setSquad(squadList);
        }
      }
    };

    loadSquad();
  }, [selectedTeam?.id, selectedSeason]);

  const handleSelect = (playerId: string) => {
    const newSelected = new Set(selectedStarters);
    if (newSelected.has(playerId)) {
      newSelected.delete(playerId);
    } else {
      if (newSelected.size >= 11) return;
      newSelected.add(playerId);
    }
    setSelectedStarters(newSelected);
  };

  const sortedSquad = [...squad].sort((a, b) => {
    const order: Record<string, number> = { GK: 0, DEF: 1, MID: 2, ATT: 3 };
    const pa = order[a.position] || 4;
    const pb = order[b.position] || 4;
    return pa - pb || a.name.localeCompare(b.name);
  });

  const selectedCount = selectedStarters.size;
  const canConfirm = selectedCount === 11;

  const countGKs = squad.filter(p => p.position === 'GK').length;
  const countSelectedGKs = squad.filter(p => p.position === 'GK' && selectedStarters.has(p.id)).length;

  return (
    <div className="card squad-screen">
      <div className="squad-head">
        <div>
          <h2>Escalação</h2>
          <p>
            <span className="user-highlight">{selectedTeam?.name}</span>
            {' • Selecione exatamente 11 jogadores para começar.'}
          </p>
        </div>
        <div style={{ textAlign: 'right' }}>
          <label style={{ fontSize: '11px', color: 'var(--muted)' }}>Temporada:</label>
          <select
            value={selectedSeason}
            onChange={(e) => setSelectedSeason(e.target.value)}
            style={{ padding: '4px 6px', background: '#0a1520', color: 'var(--text)', border: '1px solid var(--line)', borderRadius: '7px' }}
          >
            <option value="">-- Selecione --</option>
            <option value="current">Atual</option>
          </select>
        </div>
      </div>

      <div className="selection-bar">
        <div>
          <span className="selection-count">{selectedCount}/11</span> jogadores selecionados
          <div className="squad-hint">
            Você pode montar qualquer combinação. Ex.: 8 DEF + 1 MEI + 1 ATA = 8-1-1. É obrigatório ter exatamente 1 goleiro.
          </div>
        </div>
        <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', justifyContent: 'flex-end' }}>
          <button className="ctrl">🧠 Pedir à comissão técnica selecionar</button>
          <button
            className="primary"
            disabled={!canConfirm}
            onClick={() => console.log('Confirm', Array.from(selectedStarters))}
          >
            Confirmar escalação
          </button>
        </div>
      </div>

      <div className="squad-table-wrap">
        <table className="squad-table">
          <thead>
            <tr>
              <th>Pos</th>
              <th>Jogador</th>
              <th>Idade</th>
              <th>Energia</th>
              <th>Atributos</th>
            </tr>
          </thead>
          <tbody id="squadTableBody">
            {sortedSquad.map(p => {
              const energy = p.state?.energy ?? 100;
              const isSelected = selectedStarters.has(p.id);
              const isUnavailable = isPlayerUnavailable({
                ...p,
                energy: p.state?.energy ?? 100,
                position: p.position as any,
                injury: p.state?.injury,
                injuryRoundsRemaining: 0,
                suspensionRounds: p.state?.suspensionMatches,
                redCard: (p.state?.redCards ?? 0) > 0,
              });
              const isDisabled = isUnavailable || (isSelected && false);
              const status = teamAvailabilityLabel({
                ...p,
                energy: p.state?.energy ?? 100,
                position: p.position as any,
                injury: p.state?.injury,
                injuryRoundsRemaining: 0,
                suspensionRounds: p.state?.suspensionMatches,
                redCard: (p.state?.redCards ?? 0) > 0,
              });

              return (
                <tr key={p.id}>
                  <td>{positionLabel(p.position)}</td>
                  <td>
                    <b>{p.name}</b>
                    {' '}
                    <span style={{ fontSize: '10px', color: 'var(--muted)' }}>
                      {status !== 'Disponível' && status}
                    </span>
                  </td>
                  <td>{p.age}</td>
                  <td className="squad-energy">{energy}%</td>
                  <td>
                    <div className="team-view-attrs">
                      {attrLine(p as any).split(' • ').map(attr => (
                        <span key={attr} className="attr-chip">{attr}</span>
                      ))}
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default SquadScreen;
