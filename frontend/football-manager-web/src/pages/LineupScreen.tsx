import React, { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useGameState } from '@/state';
import { FixtureApi, MatchApi, SeasonApi, TeamApi } from '@/api';
import type { FixtureDto, SquadPlayerDto } from '@/types';
import { positionLabel } from '@/services/formatters';

const STARTERS = 11;

type SquadRow = SquadPlayerDto;

/**
 * Lineup screen. The manager picks the eleven here and the match starts with it. The
 * rules are only mirrored for feedback: the backend validates the eleven again and
 * refuses anything invalid, so the client can never force an illegal lineup.
 */
const LineupScreen: React.FC = () => {
  const { fixtureId = '' } = useParams();
  const navigate = useNavigate();
  const selectedTeam = useGameState((s) => s.selectedTeam);

  const [squad, setSquad] = useState<SquadRow[]>([]);
  const [fixture, setFixture] = useState<FixtureDto | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [error, setError] = useState<string | null>(null);
  const [starting, setStarting] = useState(false);

  useEffect(() => {
    const load = async () => {
      if (!fixtureId) return;

      try {
        const found = await FixtureApi.list();
        const current = (found || []).find((f: FixtureDto) => f.id === fixtureId) || null;
        setFixture(current);

        if (!selectedTeam) return;

        const season = await SeasonApi.current();
        const squadStates = await TeamApi.getSquad(selectedTeam.id, season.id);

        const roster: SquadRow[] = squadStates.filter((state: SquadPlayerDto) => state.isAvailable);

        setSquad(roster);
        setSelected(pickSuggestedEleven(roster));
      } catch (err) {
        console.error('Failed to load the squad:', err);
        setError('Não foi possível carregar o elenco.');
      }
    };

    load();
  }, [fixtureId, selectedTeam?.id]);

  const goalkeepersSelected = useMemo(
    () => squad.filter((p) => selected.has(p.id) && p.position === 'GK').length,
    [squad, selected]
  );

  const opponent = useMemo(() => {
    if (!fixture || !selectedTeam) return null;
    return fixture.homeTeamId === selectedTeam.id ? fixture.awayTeam : fixture.homeTeam;
  }, [fixture, selectedTeam]);

  const toggle = (playerId: string) => {
    setError(null);
    setSelected(previous => {
      const next = new Set(previous);

      if (next.has(playerId)) {
        next.delete(playerId);
        return next;
      }

      if (next.size >= STARTERS) {
        setError(`Escolha exatamente ${STARTERS} jogadores.`);
        return next;
      }

      next.add(playerId);
      return next;
    });
  };

  const startMatch = async () => {
    if (selected.size !== STARTERS) {
      setError(`Escolha exatamente ${STARTERS} jogadores.`);
      return;
    }

    if (goalkeepersSelected !== 1) {
      setError('A escalação precisa de exatamente um goleiro.');
      return;
    }

    setStarting(true);
    setError(null);

    try {
      const result = await MatchApi.start(fixtureId, selectedTeam?.id, [...selected]);
      if (!result.accepted) {
        setError(result.errorMessage || 'O backend recusou a escalação.');
        setStarting(false);
        return;
      }

      navigate(`/match/${result.matchId}`);
    } catch (err: any) {
      const code = err?.response?.data?.code;
      setError(code ? `${code}: ${err.response.data.detail}` : 'Falha ao iniciar a partida.');
      setStarting(false);
    }
  };

  const grouped = useMemo(() => {
    const order: Record<string, number> = { GK: 0, DEF: 1, MID: 2, ATT: 3 };
    return [...squad].sort(
      (a, b) => (order[a.position] ?? 4) - (order[b.position] ?? 4) || a.name.localeCompare(b.name)
    );
  }, [squad]);

  return (
    <div className="app">
      <div className="card match-header">
        <h2>Escalação</h2>
        <p className="competition">
          {selectedTeam?.name} {opponent ? `x ${opponent.name}` : ''}
        </p>
        <p className="competition">
          {selected.size}/{STARTERS} escolhidos • {goalkeepersSelected} goleiro(s)
        </p>

        {error && <p className="competition" style={{ color: 'var(--danger)' }}>{error}</p>}

        <div className="lineup-grid">
          {grouped.map(player => {
            const isSelected = selected.has(player.id);
            return (
              <div
                key={player.id}
                className={`lineup-card ${isSelected ? 'selected' : ''}`}
                onClick={() => toggle(player.id)}
              >
                <div className="pos">{positionLabel(player.position)}</div>
                <div className="who">
                  <strong>{player.name}</strong>
                  <span>
                    Vel {player.speed} • Dri {player.dribbling} • For {player.strength} • En{' '}
                    {player.energy}%
                  </span>
                </div>
              </div>
            );
          })}
        </div>

        <div style={{ display: 'flex', gap: '8px', justifyContent: 'flex-end', marginTop: '14px' }}>
          <button className="ctrl" onClick={() => navigate('/')} disabled={starting}>
            Voltar
          </button>
          <button
            className="primary"
            onClick={startMatch}
            disabled={starting || selected.size !== STARTERS || goalkeepersSelected !== 1}
          >
            {starting ? 'Iniciando...' : 'Iniciar partida'}
          </button>
        </div>
      </div>
    </div>
  );
};

/** Strongest available player per position, with the best goalkeeper in the eleven. */
function pickSuggestedEleven(squad: SquadRow[]): Set<string> {
  const rating = (p: SquadRow) =>
    p.speed + p.accuracy + p.dribbling + p.heading + p.strength + p.goalkeeperPower + p.reflexes;

  const keeper = squad
    .filter(p => p.position === 'GK')
    .sort((a, b) => rating(b) - rating(a))[0];

  const chosen = new Set<string>();
  if (keeper) chosen.add(keeper.id);

  [...squad]
    .filter(p => p.id !== keeper?.id)
    .sort((a, b) => rating(b) - rating(a))
    .slice(0, STARTERS - chosen.size)
    .forEach(p => chosen.add(p.id));

  return chosen;
}

export default LineupScreen;
