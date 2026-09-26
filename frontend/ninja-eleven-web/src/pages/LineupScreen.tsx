import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useGameState } from '@/state';
import { FixtureApi, MatchApi, SeasonApi, TeamApi } from '@/api';
import type { FixtureDto, Position, SquadPlayerDto, TacticDto } from '@/types';
import { positionLabel } from '@/services/formatters';

const STARTERS = 11;

/**
 * The order a table of players is read in: goalkeepers, defenders, midfielders and
 * attackers, alphabetically inside each group. It is the same order the API uses, so the
 * rows arrive already sorted and this only has to group them under a heading.
 */
const POSITION_ORDER: Position[] = ['GK', 'DEF', 'MID', 'ATT'];

const POSITION_LABELS: Record<Position, string> = {
  GK: 'Goleiros',
  DEF: 'Defesa',
  MID: 'Meio-campo',
  ATT: 'Ataque'
};

/** What the squad list can be ordered by. Age and every attribute the engine reads. */
type SortKey = 'position' | 'age' | 'speed' | 'accuracy' | 'dribbling' | 'heading' | 'strength' | 'goalkeeperPower' | 'reflexes' | 'energy';

const SORT_OPTIONS: { key: SortKey; label: string }[] = [
  { key: 'position', label: 'Posição' },
  { key: 'age', label: 'Idade' },
  { key: 'speed', label: 'Velocidade' },
  { key: 'accuracy', label: 'Finalização' },
  { key: 'dribbling', label: 'Drible' },
  { key: 'heading', label: 'Cabeceio' },
  { key: 'strength', label: 'Força' },
  { key: 'goalkeeperPower', label: 'Poder de goleiro' },
  { key: 'reflexes', label: 'Reflexos' },
  { key: 'energy', label: 'Energia' },
];

/** Every attribute a row shows on its second line, in reading order. */
const ATTRIBUTE_CHIPS: { key: keyof SquadPlayerDto; label: string }[] = [
  { key: 'speed', label: 'Vel' },
  { key: 'accuracy', label: 'Fin' },
  { key: 'dribbling', label: 'Dri' },
  { key: 'heading', label: 'Cab' },
  { key: 'strength', label: 'For' },
  { key: 'reflexes', label: 'Ref' },
  { key: 'goalkeeperPower', label: 'Gle' },
];

type SquadRow = SquadPlayerDto;

/**
 * Lineup screen. The manager picks the eleven here and the match starts with it. The
 * rules are only mirrored for feedback: the backend validates the eleven again and
 * refuses anything invalid, so the client can never force an illegal lineup.
 *
 * The whole squad is shown as a table, injuries and suspensions included: a manager
 * decides who plays knowing who cannot, not only what is on offer. Every player takes two
 * lines, the first with the season numbers and the second with his attributes, so a long
 * name and seven numbers never fight for the same cell.
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
  const [positionFilter, setPositionFilter] = useState<Position | 'ALL'>('ALL');
  const [sortKey, setSortKey] = useState<SortKey>('position');
  const [tactics, setTactics] = useState<TacticDto[]>([]);
  const [tacticCode, setTacticCode] = useState<string | undefined>(undefined);
  const seasonId = useGameState((s) => s.selectedSeason?.id);

  /**
   * The eleven the staff would put out, asked of the backend for the shape the manager
   * has ordered. Asking is the point: the screen used to work the eleven out itself, by
   * its own idea of who is good, and it could propose a reserve goalkeeper and an eleven
   * with no shape in it at all — the same eleven the engine would never have picked.
   */
  const suggestFromStaff = useCallback(
    async (code?: string) => {
      if (!selectedTeam || !seasonId) return;
      try {
        const ids = await MatchApi.getSuggestedEleven(selectedTeam.id, seasonId, code);
        setSelected(new Set(ids));
      } catch (err) {
        console.error('Failed to ask for a suggested eleven:', err);
      }
    },
    [selectedTeam?.id, seasonId]
  );

  useEffect(() => {
    MatchApi.getTactics()
      .then(setTactics)
      .catch(err => console.error('Failed to load the tactics:', err));
  }, []);

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

        const roster: SquadRow[] = (squadStates || [])
          .slice()
          .sort(
            (a, b) =>
              POSITION_ORDER.indexOf(a.position) - POSITION_ORDER.indexOf(b.position) ||
              a.name.localeCompare(b.name, 'pt-BR')
          );

        setSquad(roster);
        await suggestFromStaff();
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

  const goalkeepersAvailable = useMemo(
    () => squad.filter((p) => p.position === 'GK' && p.isAvailable).length,
    [squad]
  );

  /**
   * Why the button is blocked, said out loud. A disabled button with no reason is the
   * one thing a manager cannot argue with, so the rule is spelled on the screen instead
   * of only in the validation that follows.
   */
  const blockingReason = useMemo(() => {
    if (selected.size !== STARTERS) {
      const missing = STARTERS - selected.size;
      return `Faltam ${missing} ${missing === 1 ? 'jogador' : 'jogadores'} para completar o time.`;
    }

    if (goalkeepersSelected === 0) {
      return 'Escolha um goleiro: o time só pode entrar em campo com um.';
    }

    if (goalkeepersSelected > 1) {
      return 'Só pode haver um goleiro em campo. Clique no goleiro que deve sair para trocá-lo.';
    }

    return null;
  }, [selected.size, goalkeepersSelected]);

  const opponent = useMemo(() => {
    if (!fixture || !selectedTeam) return null;
    return fixture.homeTeamId === selectedTeam.id ? fixture.awayTeam : fixture.homeTeam;
  }, [fixture, selectedTeam]);

  /**
   * The squad under the filter and the order the manager asked for. The position filter
   * narrows what is on offer; the sort decides how it is read. Neither ever changes who is
   * in the eleven, so filtering a position out never quietly drops a selected player from
   * the count.
   */
  const visibleSquad = useMemo(() => {
    const filtered =
      positionFilter === 'ALL' ? squad : squad.filter(player => player.position === positionFilter);

    if (sortKey === 'position') {
      return filtered;
    }

    return [...filtered].sort((a, b) => {
      const left = a[sortKey] as number;
      const right = b[sortKey] as number;

      // Best first on every number, and the name settles a tie so the list never jitters.
      return right - left || a.name.localeCompare(b.name, 'pt-BR');
    });
  }, [squad, positionFilter, sortKey]);

  /**
   * The whole squad under the heading of each position. Grouping only makes sense while
   * the list is grouped by position: a list sorted by finishing has no groups to head.
   */
  const rowsByPosition = useMemo(() => {
    if (sortKey !== 'position') {
      return [{ position: positionFilter, label: '', players: visibleSquad }];
    }

    return POSITION_ORDER.map(position => ({
      position,
      label: POSITION_LABELS[position],
      players: visibleSquad.filter(player => player.position === position)
    })).filter(group => group.players.length > 0);
  }, [visibleSquad, positionFilter, sortKey]);

  const toggle = (player: SquadRow) => {
    setError(null);

    if (!player.isAvailable) {
      setError(absenceReason(player));
      return;
    }

    setSelected(previous => {
      const next = new Set(previous);

      if (next.has(player.id)) {
        next.delete(player.id);
        return next;
      }

      // Choosing a goalkeeper replaces the one in the eleven instead of adding a second:
      // the other keepers exist to be swapped in, not to share the pitch.
      if (player.position === 'GK') {
        squad
          .filter(other => other.position === 'GK' && next.has(other.id))
          .forEach(other => next.delete(other.id));
        next.add(player.id);
        return next;
      }

      if (next.size >= STARTERS) {
        setError(`Escolha exatamente ${STARTERS} jogadores.`);
        return next;
      }

      next.add(player.id);
      return next;
    });
  };

  /**
   * The technical staff picks the eleven, for the shape currently ordered. It is the
   * same advice a manager would get: the best goalkeeper, the shape the manager asked for
   * filled with the men who are best at each line's job, and nobody injured or suspended.
   * The manager can still change every name.
   */
  const askTheCommission = () => {
    setError(null);
    suggestFromStaff(tacticCode);
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
      const result = await MatchApi.start(fixtureId, selectedTeam?.id, [...selected], tacticCode);
      if (result.matchId) {
        // Either the match just started, or this fixture is already being played: both
        // cases are watched on the match screen, keyed by the matchId the backend gave
        // back. A refused eleven carries no match, so it stays an error here.
        navigate(`/match/${result.matchId}`);
        return;
      }

      setError(result.errorMessage || 'O backend recusou a escalação.');
      setStarting(false);
    } catch (err: any) {
      const code = err?.response?.data?.code;
      setError(code ? `${code}: ${err.response.data.detail}` : 'Falha ao iniciar a partida.');
      setStarting(false);
    }
  };

  return (
    <div className="app">
      <div className="card match-header">
        <h2>Escalação</h2>
        <p className="competition">
          {selectedTeam?.name} {opponent ? `x ${opponent.name}` : ''}
        </p>
        <p className="competition">
          {selected.size}/{STARTERS} escolhidos • {goalkeepersSelected} goleiro(s) •{' '}
          {goalkeepersAvailable} goleiro(s) no elenco
        </p>

        {error && <p className="competition" style={{ color: 'var(--danger)' }}>{error}</p>}

        {blockingReason && (
          <p className="competition" style={{ color: 'var(--orange)' }}>{blockingReason}</p>
        )}

        <div className="squad-toolbar">
          <label className="squad-filters">
            <span className="squad-toolbar-label">Tática</span>
            <select
              value={tacticCode ?? ''}
              onChange={event => {
                const code = event.target.value || undefined;
                setTacticCode(code);
                suggestFromStaff(code);
              }}
            >
              <option value="">Do elenco</option>
              {tactics.map(tactic => (
                <option key={tactic.code} value={tactic.code}>
                  {tactic.name} ({tactic.defenders}-{tactic.midfielders}-{tactic.attackers})
                </option>
              ))}
            </select>
          </label>

          <div className="squad-filters">
            <span className="squad-toolbar-label">Posição</span>
            <div className="segmented">
              <button
                className={`segmented-item ${positionFilter === 'ALL' ? 'active' : ''}`}
                onClick={() => setPositionFilter('ALL')}
              >
                Todas
              </button>
              {POSITION_ORDER.map(position => (
                <button
                  key={position}
                  className={`segmented-item ${positionFilter === position ? 'active' : ''}`}
                  onClick={() => setPositionFilter(position)}
                >
                  {positionLabel(position)}
                </button>
              ))}
            </div>
          </div>

          <label className="squad-filters">
            <span className="squad-toolbar-label">Ordenar por</span>
            <select value={sortKey} onChange={event => setSortKey(event.target.value as SortKey)}>
              {SORT_OPTIONS.map(option => (
                <option key={option.key} value={option.key}>{option.label}</option>
              ))}
            </select>
          </label>
        </div>

        <div className="squad-table-wrap">
          <table className="squad-table">
            <thead>
              <tr>
                <th className="col-pos">Pos</th>
                <th className="col-name">Jogador</th>
                <th className="col-num">Idade</th>
                <th className="col-num">Energia</th>
                <th className="col-num">Gols</th>
                <th className="col-num">Amarelos</th>
                <th className="col-num">Vermelhos</th>
                <th className="col-status">Situação</th>
              </tr>
            </thead>

            {rowsByPosition.map(group => (
              <tbody key={group.position}>
                {group.label && (
                  <tr className="squad-group">
                    <th colSpan={8} scope="colgroup">
                      {group.label}
                    </th>
                  </tr>
                )}

                {group.players.map(player => (
                  <PlayerRows
                    key={player.id}
                    player={player}
                    isSelected={selected.has(player.id)}
                    isReserveGoalkeeper={
                      player.position === 'GK' && goalkeepersSelected > 0 && !selected.has(player.id)
                    }
                    onToggle={() => toggle(player)}
                  />
                ))}
              </tbody>
            ))}
          </table>

          {rowsByPosition.length === 0 && (
            <div className="league-empty">Nenhum jogador nesta posição.</div>
          )}
        </div>

        <div className="squad-actions">
          <button className="ctrl" onClick={() => navigate('/league')} disabled={starting}>
            Voltar
          </button>
          <button className="ctrl" onClick={askTheCommission} disabled={starting}>
            🧠 Pedir à comissão técnica
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

interface PlayerRowsProps {
  player: SquadRow;
  isSelected: boolean;
  isReserveGoalkeeper: boolean;
  onToggle: () => void;
}

/**
 * One player, two lines. The first is everything a manager reads at a glance — name, age,
 * energy, cards and situation; the second is his attributes, which are the reason a name is
 * on the list at all. Both lines belong to the same player, so both are clickable and both
 * light up together.
 */
const PlayerRows: React.FC<PlayerRowsProps> = ({ player, isSelected, isReserveGoalkeeper, onToggle }) => {
  const classes = [
    'squad-row',
    isSelected ? 'selected' : '',
    isReserveGoalkeeper ? 'reserve' : '',
    player.isAvailable ? '' : 'unavailable'
  ]
    .filter(Boolean)
    .join(' ');

  return (
    <>
      <tr className={classes} title={absenceReason(player)} onClick={onToggle}>
        <td className="col-pos">
          <span className="pos-badge">{positionLabel(player.position)}</span>
        </td>
        <td className="col-name">
          <strong>{player.name}</strong>
        </td>
        <td className="col-num">{player.age}</td>
        <td className="col-num">{player.energy}%</td>
        <td className="col-num">{player.goals}</td>
        <td className="col-num">{player.yellowCards}</td>
        <td className="col-num">{player.redCards}</td>
        <td className="col-status">{situationOf(player)}</td>
      </tr>

      <tr className={`squad-attrs-row ${classes}`} title={absenceReason(player)} onClick={onToggle}>
        <td className="col-pos" />
        <td className="squad-attrs" colSpan={7}>
          {ATTRIBUTE_CHIPS.map(chip => {
            // A goalkeeper's own numbers and an outfielder's are the ones the engine reads
            // for him, so a keeper is never judged on a heading he will never head.
            const relevant =
              player.position === 'GK'
                ? chip.key === 'speed' || chip.key === 'reflexes' || chip.key === 'goalkeeperPower'
                : chip.key === 'reflexes' || chip.key === 'goalkeeperPower'
                  ? false
                  : true;

            if (!relevant) return null;

            const value = player[chip.key] as number;

            return (
              <span key={chip.label as string} className="attr-chip">
                {chip.label} <b>{value}</b>
              </span>
            );
          })}
        </td>
      </tr>
    </>
  );
};

/**
 * What keeps a player out, in the words a manager would use. A player with nothing against
 * him is simply available.
 */
function absenceReason(player: SquadRow): string {
  if (player.suspensionMatches > 0) {
    const matches = player.suspensionMatches;
    return `Suspenso: ainda cumpre ${matches} ${matches === 1 ? 'jogo' : 'jogos'}.`;
  }

  if (player.injury !== 'None') {
    const remaining = player.injuryMatchesRemaining;
    const severity = player.injury === 'Grave' ? 'Lesão grave' : 'Lesão leve';
    return remaining > 0
      ? `${severity}: fora por mais ${remaining} ${remaining === 1 ? 'jogo' : 'jogos'}.`
      : `${severity}: indisponível.`;
  }

  if (player.isAvailable) {
    return 'Disponível';
  }

  return 'Indisponível';
}

/**
 * The short version of the situation, for the table cell.
 */
function situationOf(player: SquadRow): string {
  if (player.suspensionMatches > 0) {
    return `Suspenso (${player.suspensionMatches})`;
  }

  if (player.injury !== 'None') {
    const remaining = player.injuryMatchesRemaining;
    const severity = player.injury === 'Grave' ? 'Grave' : 'Leve';
    return remaining > 0 ? `${severity} (${remaining})` : severity;
  }

  return 'Apto';
}

export default LineupScreen;
