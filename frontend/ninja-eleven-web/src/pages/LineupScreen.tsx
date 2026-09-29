import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useGameState } from '@/state';
import { FixtureApi, MatchApi, RoundApi, SeasonApi, TeamApi } from '@/api';
import type { FixtureDto, Position, RoundDto, SquadPlayerDto, TacticDto } from '@/types';
import { positionLabel } from '@/services/formatters';
import ClubSquadTable from '@/components/Club/ClubSquadTable';
import { useClubWindow } from '@/services/clubColors';
import NextMatchPanel from '@/components/Club/NextMatchPanel';
import RoundContextPanel from '@/components/Club/RoundContextPanel';

const STARTERS = 11;
const BENCH_SIZE = 7;

/**
 * The order a table of players is read in: goalkeepers, defenders, midfielders and
 * attackers, alphabetically inside each group. It is the same order the API uses, so the
 * rows arrive already sorted and this only has to group them under a heading.
 */
const POSITION_ORDER: Position[] = ['GK', 'DEF', 'MID', 'ATT'];

type SquadRow = SquadPlayerDto;

/**
 * Lineup screen. The manager picks the eleven and the bench here and the match starts with it.
 * The rules are only mirrored for feedback: the backend validates the eleven again and
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
  const [selectedBench, setSelectedBench] = useState<Set<string>>(new Set());
  const [error, setError] = useState<string | null>(null);
  const [starting, setStarting] = useState(false);
  const [positionFilter, setPositionFilter] = useState<Position | 'ALL'>('ALL');
  const [tactics, setTactics] = useState<TacticDto[]>([]);
  const [tacticCode, setTacticCode] = useState<string | undefined>(undefined);
  /**
   * The season this eleven belongs to, kept by the screen.
   *
   * It used to be read from the shared store, which is a value nothing ever writes: the
   * suggestion silently did nothing, and a button that does nothing with no message is the one
   * bug a manager cannot argue with. The season the squad was read for is the season the
   * eleven is picked in, and this is the one the screen already resolved for that read.
   */
  const [seasonId, setSeasonId] = useState('');
  const selectedCompetition = useGameState((s) => s.selectedCompetition);

  /**
   * The eleven and bench the staff would put out, asked of the backend for the shape the manager
   * has ordered. Asking is the point: the screen used to work the eleven out itself, by
   * its own idea of who is good, and it could propose a reserve goalkeeper and an eleven
   * with no shape in it at all — the same eleven the engine would never have picked.
   *
   * The season arrives as an argument rather than being read from a store, so the first ask
   * and every later one — the button, the tactic dropdown — are the same question asked the
   * same way, and a failure says so on the screen instead of leaving the eleven as it was.
   */
  const suggestFromStaff = useCallback(
    async (season: string, code?: string) => {
      if (!selectedTeam) return;

      setError(null);

      try {
        const suggestion = await MatchApi.getSuggestedEleven(selectedTeam.id, season, code);
        setSelected(new Set(suggestion.starterIds));
        setSelectedBench(new Set(suggestion.benchIds));
      } catch (err) {
        console.error('Failed to ask for a suggested eleven:', err);
        setError('A comissão técnica não respondeu. Tente de novo em instantes.');
      }
    },
    [selectedTeam?.id]
  );

  /**
   * The staff's eleven for a shape, asked with the season this screen resolved. A button that
   * cannot answer says why it is blocked rather than doing nothing at all: a control which
   * silently refuses is the one thing a manager cannot argue with.
   */
  const askStaffFor = useCallback(
    (code?: string) => {
      if (!selectedTeam || !seasonId) {
        setError('O elenco ainda está sendo carregado.');
        return;
      }

      suggestFromStaff(seasonId, code);
    },
    [selectedTeam?.id, seasonId, suggestFromStaff]
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
        setSeasonId(season.id);
        // The staff are asked once the season is known, because the suggestion is a question
        // about a season's squad: asked without one there is no answer to show.
        await suggestFromStaff(season.id);
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

  const goalkeepersOnBench = useMemo(
    () => squad.filter((p) => selectedBench.has(p.id) && p.position === 'GK').length,
    [squad, selectedBench]
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
      return `Faltam ${missing} ${missing === 1 ? 'jogador' : 'jogadores'} para completar o time titular.`;
    }

    if (goalkeepersSelected === 0) {
      return 'Escolha um goleiro: o time só pode entrar em campo com um.';
    }

    if (goalkeepersSelected > 1) {
      return 'Só pode haver um goleiro em campo. Clique no goleiro que deve sair para trocá-lo.';
    }

    if (selectedBench.size > BENCH_SIZE) {
      return `O banco de reservas pode ter no máximo ${BENCH_SIZE} jogadores.`;
    }

    // If bench is partially filled, validate it has a GK when possible
    if (selectedBench.size > 0 && goalkeepersOnBench === 0 && goalkeepersAvailable > (goalkeepersSelected > 0 ? 1 : 0)) {
      return 'O banco de reservas precisa de pelo menos um goleiro.';
    }

    return null;
  }, [selected.size, goalkeepersSelected, selectedBench.size, goalkeepersOnBench, goalkeepersAvailable]);

  const opponent = useMemo(() => {
    if (!fixture || !selectedTeam) return null;
    return fixture.homeTeamId === selectedTeam.id ? fixture.awayTeam : fixture.homeTeam;
  }, [fixture, selectedTeam]);

  /**
   * The squad under the filter. The order is the table's own: it opens by position and the
   * manager reorders it by clicking a column, which is the same table a club's squad is
   * read from on the club's own screen. The filter narrows what is on offer and never
   * changes who is in the eleven, so filtering a position out never quietly drops a
   * selected player from the count.
   */
  const visibleSquad = useMemo(
    () =>
      positionFilter === 'ALL' ? squad : squad.filter(player => player.position === positionFilter),
    [squad, positionFilter]
  );

  // The bench is everybody available who is not in the eleven.
  const availableForBench = useMemo(
    () => squad.filter(player => player.isAvailable && !selected.has(player.id)),
    [squad, selected]
  );

  const toggle = (player: SquadRow) => {
    setError(null);

    if (!player.isAvailable) {
      setError(absenceReason(player));
      return;
    }

    const isInStarters = selected.has(player.id);
    const isInBench = selectedBench.has(player.id);

    // If already selected, remove from wherever they are
    if (isInStarters) {
      setSelected(prev => {
        const next = new Set(prev);
        next.delete(player.id);
        return next;
      });
      return;
    }

    if (isInBench) {
      setSelectedBench(prev => {
        const next = new Set(prev);
        next.delete(player.id);
        return next;
      });
      return;
    }

    // Not selected yet - add to starters if space, otherwise bench
    if (selected.size < STARTERS) {
      setSelected(prev => {
        const next = new Set(prev);

        // Choosing a goalkeeper replaces the one in the eleven instead of adding a second:
        if (player.position === 'GK') {
          squad
            .filter(other => other.position === 'GK' && next.has(other.id))
            .forEach(other => next.delete(other.id));
        }

        if (next.size >= STARTERS) {
          setError(`Escolha exatamente ${STARTERS} jogadores titulares.`);
          return next;
        }

        next.add(player.id);
        return next;
      });
      return;
    }

    // Starters full, add to bench
    if (selectedBench.size < BENCH_SIZE) {
      setSelectedBench(prev => {
        const next = new Set(prev);
        next.add(player.id);
        return next;
      });
      return;
    }

    // Both full
    setError(`Elenco completo: ${STARTERS} titulares e ${BENCH_SIZE} reservas.`);
  };

  const startMatch = async () => {
    if (selected.size !== STARTERS) {
      setError(`Escolha exatamente ${STARTERS} jogadores titulares.`);
      return;
    }

    if (goalkeepersSelected !== 1) {
      setError('A escalação precisa de exatamente um goleiro.');
      return;
    }

    if (selectedBench.size > BENCH_SIZE) {
      setError(`O banco de reservas pode ter no máximo ${BENCH_SIZE} jogadores.`);
      return;
    }

    // Validate bench has GK if possible
    if (selectedBench.size > 0 && goalkeepersOnBench === 0 && goalkeepersAvailable > 1) {
      setError('O banco de reservas precisa de pelo menos um goleiro.');
      return;
    }

    setStarting(true);
    setError(null);

    try {
      const result = await MatchApi.start(fixtureId, selectedTeam?.id, [...selected], [...selectedBench], tacticCode);
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

  // The matchday this eleven is for. The screen was opened for a fixture, so the round is
  // that fixture's own round — asked once, because it is the one thing about a fixture that
  // the fixture itself does not carry. A screen opened for a matchday already played is
  // still described correctly by this, which "the next fixture" would not be.
  const [thisRound, setThisRound] = useState<RoundDto | null>(null);

  useEffect(() => {
    if (!fixture?.roundId) return undefined;

    let cancelled = false;

    RoundApi.get(fixture.roundId)
      .then(round => {
        if (!cancelled) setThisRound(round);
      })
      .catch(err => console.error('Failed to load the matchday:', err));

    return () => {
      cancelled = true;
    };
  }, [fixture?.roundId]);

  // The manager's own colours, measured the same way as everywhere else a club is read.
  // It is a hook and not an inline style so the lineup, the club's screen and the club's
  // modal are given the same two colours by the same measure rather than by three.
  const clubWindow = useClubWindow(selectedTeam);

  return (
    <div className="app">
      <div className="card match-header club-modal club-squad-picker" style={clubWindow}>
        <h2 className="profile-name">Escalação</h2>
        <p className="competition">
          {selectedTeam?.name} {opponent ? `x ${opponent.name}` : ''}
        </p>
        <p className="competition">
          {selected.size}/{STARTERS} titulares • {selectedBench.size}/{BENCH_SIZE} reservas • {goalkeepersSelected} goleiro(s) em campo •{' '}
          {goalkeepersAvailable} goleiro(s) no elenco
        </p>

        {thisRound && selectedTeam && fixture && (
          <NextMatchPanel
            fixture={fixture}
            round={thisRound}
            managerTeam={selectedTeam}
            competitionName={selectedCompetition?.name ?? ''}
            /* The round knows which edition it belongs to, and that is the edition the
               table is addressed by — one less thing that has to be remembered correctly. */
            competitionSeasonId={thisRound.competitionSeasonId}
          />
        )}

        {thisRound && selectedTeam && fixture && opponent && (
          <RoundContextPanel
            fixture={fixture}
            round={thisRound}
            managerTeam={selectedTeam}
            competitionSeasonId={thisRound.competitionSeasonId}
          />
        )}

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
                askStaffFor(code);
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

        </div>

        {/* The same table a club's squad is read from on the club's own screen, twice: once
            for the eleven and once for the bench. The row is the pick, because a manager
            filling in a team sheet is not reading a table, he is filling one in. */}
        <ClubSquadTable
          squad={visibleSquad}
          caption={`Titulares (${selected.size}/${STARTERS})`}
          selectedIds={selected}
          elsewhereIds={selectedBench}
          onToggle={toggle}
          describeAbsence={absenceReason}
        />

        <ClubSquadTable
          squad={availableForBench}
          caption={`Banco de Reservas (${selectedBench.size}/${BENCH_SIZE})`}
          selectedIds={selectedBench}
          elsewhereIds={selected}
          onToggle={toggle}
          describeAbsence={absenceReason}
        />

        <div className="squad-actions">
          <button className="ctrl" onClick={() => navigate('/league')} disabled={starting}>
            Voltar
          </button>
          {/* The same question the tactic dropdown asks, so one button and one dropdown do not
              drift into two different advices. It is not disabled while the squad loads: a
              control that is greyed out with no reason is the one thing a manager cannot argue
              with, and the screen says what is wrong instead. */}
          <button className="ctrl" onClick={() => askStaffFor(tacticCode)} disabled={starting}>
            🧠 Pedir à comissão técnica
          </button>
          <button
            className="primary"
            onClick={startMatch}
            disabled={starting || selected.size !== STARTERS || goalkeepersSelected !== 1 || selectedBench.size > BENCH_SIZE}
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

export default LineupScreen;
