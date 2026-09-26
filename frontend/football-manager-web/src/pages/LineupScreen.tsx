import React, { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useGameState } from '@/state';
import { FixtureApi, MatchApi, SeasonApi, TeamApi } from '@/api';
import type { FixtureDto, Position, SquadPlayerDto } from '@/types';
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

type SquadRow = SquadPlayerDto;

/**
 * Lineup screen. The manager picks the eleven here and the match starts with it. The
 * rules are only mirrored for feedback: the backend validates the eleven again and
 * refuses anything invalid, so the client can never force an illegal lineup.
 *
 * The whole squad is shown as a table, injuries and suspensions included: a manager
 * decides who plays knowing who cannot, not only what is on offer.
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

        const roster: SquadRow[] = (squadStates || [])
          .slice()
          .sort(
            (a, b) =>
              POSITION_ORDER.indexOf(a.position) - POSITION_ORDER.indexOf(b.position) ||
              a.name.localeCompare(b.name, 'pt-BR')
          );

        setSquad(roster);
        setSelected(pickSuggestedEleven(roster.filter((player) => player.isAvailable)));
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
   * The whole squad, one row per player, grouped under the heading of his position.
   */
  const rowsByPosition = useMemo(
    () =>
      POSITION_ORDER.map(position => ({
        position,
        label: POSITION_LABELS[position],
        players: squad.filter((player) => player.position === position)
      })).filter(group => group.players.length > 0),
    [squad]
  );

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
                <tr className="squad-group">
                  <th colSpan={8} scope="colgroup">
                    {group.label}
                  </th>
                </tr>

                {group.players.map(player => {
                  const isSelected = selected.has(player.id);
                  const isReserveGoalkeeper =
                    player.position === 'GK' && goalkeepersSelected > 0 && !isSelected;

                  return (
                    <tr
                      key={player.id}
                      className={[
                        'squad-row',
                        isSelected ? 'selected' : '',
                        isReserveGoalkeeper ? 'reserve' : '',
                        player.isAvailable ? '' : 'unavailable'
                      ]
                        .filter(Boolean)
                        .join(' ')}
                      title={absenceReason(player)}
                      onClick={() => toggle(player)}
                    >
                      <td className="col-pos">
                        <span className="pos-badge">{positionLabel(player.position)}</span>
                      </td>
                      <td className="col-name">
                        <strong>{player.name}</strong>
                        <span className="attrs">
                          Vel {player.speed} • Fin {player.accuracy} • Dri {player.dribbling} • For{' '}
                          {player.strength} • Cab {player.heading}
                          {player.position === 'GK'
                            ? ` • Ref ${player.reflexes} • Gle ${player.goalkeeperPower}`
                            : ''}
                        </span>
                      </td>
                      <td className="col-num">{player.age}</td>
                      <td className="col-num">{player.energy}%</td>
                      <td className="col-num">{player.goals}</td>
                      <td className="col-num">{player.yellowCards}</td>
                      <td className="col-num">{player.redCards}</td>
                      <td className="col-status">{situationOf(player)}</td>
                    </tr>
                  );
                })}
              </tbody>
            ))}
          </table>
        </div>

        <div style={{ display: 'flex', gap: '8px', justifyContent: 'flex-end', marginTop: '14px' }}>
          <button className="ctrl" onClick={() => navigate('/league')} disabled={starting}>
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

/**
 * Strongest available player per position, with the best goalkeeper in the eleven. Only
 * one goalkeeper is ever proposed: the other keepers of the roster are cover, and a
 * rating alone would rank them high because that is what a goalkeeper is for.
 */
function pickSuggestedEleven(squad: SquadRow[]): Set<string> {
  const rating = (p: SquadRow) =>
    p.speed + p.accuracy + p.dribbling + p.heading + p.strength + p.goalkeeperPower + p.reflexes;

  const goalkeepers = squad
    .filter(p => p.position === 'GK')
    .sort((a, b) => b.reflexes + b.goalkeeperPower - (a.reflexes + a.goalkeeperPower));
  const keeper = goalkeepers[0];

  const chosen = new Set<string>();
  if (keeper) chosen.add(keeper.id);

  [...squad]
    .filter(p => p.position !== 'GK')
    .sort((a, b) => rating(b) - rating(a))
    .slice(0, STARTERS - chosen.size)
    .forEach(p => chosen.add(p.id));

  // A squad short of outfield players is completed with a reserve goalkeeper rather than
  // left with ten names.
  if (chosen.size < STARTERS) {
    [...squad]
      .filter(p => !chosen.has(p.id))
      .sort((a, b) => rating(b) - rating(a))
      .slice(0, STARTERS - chosen.size)
      .forEach(p => chosen.add(p.id));
  }

  return chosen;
}

export default LineupScreen;
