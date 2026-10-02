import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useGameState } from '@/state';
import { MatchApi, SeasonApi, TacticsApi } from '@/api';
import type {
  TacticDto,
  TacticsBoardDto,
  TacticsSquadRowDto
} from '@/types';
import {
  positionLabel,
  attributeToneClass,
  energyTextClass,
  energyClass,
  energyPercent,
  initialsOf,
  starsToString
} from '@/services/formatters';
import { PlayerName, ClubName } from '@/components/Common/Names';
import { formOf, FormBadge, FORM_TITLE } from '@/components/Club/FormRun';
import PlayerStatusMarks from '@/components/Common/PlayerStatusMarks';

/** The eleven on the pitch and the seven beside it. The two numbers the Laws settle on. */
const STARTERS = 11;
const BENCH_SIZE = 7;

/**
 * The eight, with the short labels the squad table and the training sheet already use.
 *
 * <para>
 * Same list, same order, same abbreviations — a manager who has read "Cab" all season does
 * not have to learn "Cabeceio" on this screen. The labels travel with the wire positions
 * rather than the wire carrying them, so the backend can be reordered without the screen
 * being a second thing to keep in step.
 * </para>
 */
const ATTRIBUTE_LABELS = ['Vel', 'Fin', 'Dri', 'Cab', 'For', 'Gol', 'Ref', 'Est'] as const;

/**
 * The same eight, spelled out, for the window that has room for a word.
 *
 * <para>
 * The abbreviation is a bargain made in a strip eight numbers wide, where "Cabeceio" would
 * have made every row three lines tall. The detail window holds one man at a time with nothing
 * beside him, so the bargain buys nothing there and costs the one thing a manager is reading
 * for: a column of "Cab For Ref" tells him nothing about what he is being shown.
 * </para>
 */
const DETAIL_ATTRIBUTE_LABELS = [
  'Velocidade',
  'Finalização',
  'Drible',
  'Cabeceio',
  'Força',
  'Poder de goleiro',
  'Reflexos',
  'Resistência'
] as const;

/** The two goalkeeper numbers, whose place in the list is theirs and not a manager's. */
const GOALKEEPER_ATTRIBUTE_INDEXES = [5, 6] as const;

/**
 * The colour of a number: the squad table's bands, asked of the one function that owns them.
 *
 * <p>
 * A tag strip of eight numbers is unreadable without them: what a manager is looking for is
 * the one attribute that stands out, and eight equally flat numbers have nothing standing out.
 * </p>
 *
 * <p>
 * This used to carry its own copy of the rule — <c>under 8</c> red, <c>under 14</c> yellow,
 * the bands an attribute was written on when the scale was 1..20. Attributes are 1..100 and
 * have been for a long time, so on this board every attribute above 14 came out green and a
 * squad of fourteen and a squad of ninety were drawn identically. The rule lives in
 * <c>attributeToneClass</c> and a second copy of it is a second answer to the same question.
 * </p>
 */
const attributeTextClass = attributeToneClass;

/**
 * Why a man is off the board, in words, or nothing when he is fit.
 *
 * <p>
 * The board refuses a man who cannot play and greys his row, which stops a plan from quietly
 * losing two players. It used to say only "Fora", and a manager reading that has to leave the
 * screen to learn whether it is two matches of a suspension or a knock — the difference
 * between a plan that will be fine in a fortnight and one that needs somebody else now.
 * </p>
 *
 * <p>
 * It is asked of the two counters the backend sends rather than of anything worked out here.
 * A ban and a bandage are different facts with different lengths, and a screen that guessed
 * between them would be guessing about a man's availability.
 * </p>
 */
const absenceReason = (row: TacticsSquadRowDto): string => {
  if (row.isAvailable) return '';

  const matches = `${row.suspensionMatches} partida${row.suspensionMatches === 1 ? '' : 's'}`;

  if (row.injuryMatchesRemaining > 0) {
    return `Lesionado • ${row.injuryMatchesRemaining} jogo${row.injuryMatchesRemaining === 1 ? '' : 's'} de retorno`;
  }

  if (row.suspensionMatches > 0) {
    return `Suspenso • ${matches}`;
  }

  return 'Indisponível';
};

type Zone = 'starters' | 'bench' | 'squad';

/**
 * The manager's board.
 *
 * <para>
 * This screen replaces the eleven-picked-in-the-browser screen, and the whole reason is
 * visible in the sentence at the top of it: <b>the kickoff is not the manager's</b>. A match
 * is opened by the world's own calendar rather than by the act of opening a screen, so what
 * a manager decides here has to be <i>written down</i> to be used. A lineup composed in the
 * browser and posted at the whistle only exists on the day somebody is sitting in front of
 * it, which is the one day the world never waits for.
 * </para>
 *
 * <para>
 * So there is no "start match" button here. There is a shape, an eleven, a bench, and a
 * save. Everything else on the screen exists to make that one decision easier: who is out,
 * who the opponent is, how the two clubs have done against each other, and what this club
 * has been doing lately.
 * </para>
 *
 * <para>
 * <b>The two halves.</b> The board on the left is the decision and the column on the right is
 * the context for it — the fixture, the two clubs' record, and the last five results, which
 * stay where a manager can read them without losing his place in the eleven. The split is two
 * thirds and one third because the thing being decided is a column of names and the things
 * informing it are five rows of prose: a column that has to share its width with a form guide
 * gives eleven names about two hundred pixels each, and an eleven you have to scroll is an
 * eleven you cannot read at a glance.
 * </para>
 */
const TacticsScreen: React.FC = () => {
  const selectedTeam = useGameState((s) => s.selectedTeam);

  const [board, setBoard] = useState<TacticsBoardDto | null>(null);
  const [tactics, setTactics] = useState<TacticDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  /** The staff are being asked for an eleven, so the shape buttons stop answering. */
  const [suggesting, setSuggesting] = useState(false);

  /**
   * The man under the pointer, and the column he is over.
   *
   * <para>
   * Both are screen state rather than something read off the DOM because a drop has to know
   * which of three columns it landed in, and asking the element under the cursor on
   * <c>drop</c> gets the answer from whatever is on top — which during a drag is the thing
   * being dragged.
   * </para>
   */
  const [dragging, setDragging] = useState<string | null>(null);
  const [dropZone, setDropZone] = useState<Zone | null>(null);

  /**
   * The man the detail window is about.
   *
   * <para>
   * A card is too small to carry the eight attributes and far too small to carry the words
   * that say why a man is out, so twenty-three cards that each had all of it would be twenty-
   * three small things nobody could read. They carry four things instead — the line he plays,
   * the letters on his back, how good he is and how much he has left — and the rest of the
   * man is one click away in a window that stays where it is while the board is worked on.
   * </para>
   */
  const [inspected, setInspected] = useState<string | null>(null);

  // The order as the manager currently has it, which is not yet what is written down. Keeping
  // the two apart is what lets a manager walk away from a half-made change: nothing on the
  // screen has changed until he presses save.
  const [tacticCode, setTacticCode] = useState<string>('');
  /**
   * The eleven and the bench, held together.
   *
   * <para>
   * They are one state and not two because every single thing this screen does to them
   * touches both: a man moving to the eleven leaves the bench, a man arriving in a full
   * eleven pushes somebody else onto it, and a man dragged back to the squad leaves both. Two
   * <c>setState</c> calls and two intermediate arrangements in between, one of which a
   * re-render can catch — an eleven of eleven and a bench of eight, or a man in both — and the
   * fix is that the order is written once, in one place, and is never half-applied.
   * </para>
   */
  const [order, setOrder] = useState<{ starters: string[]; bench: string[] }>({
    starters: [],
    bench: []
  });

  const { starters, bench } = order;
  const [touched, setTouched] = useState(false);

  const teamId = selectedTeam?.id;

  const load = useCallback(async () => {
    if (!teamId) return;

    setLoading(true);
    setError(null);

    try {
      const season = await SeasonApi.current();
      const [boardResponse, catalogue] = await Promise.all([
        TacticsApi.board(teamId, season.id),
        MatchApi.getTactics()
      ]);

      setBoard(boardResponse);
      setTactics(catalogue);

      // The board's plan is loaded into the editor as it stands. A manager opening the
      // screen sees the order his club will actually go out in, not an empty form: an
      // editor that opened blank would be telling him to re-say what he already said.
      setTacticCode(boardResponse.plan?.tacticCode ?? '');
      setOrder({
        starters: boardResponse.plan?.starterIds ?? [],
        bench: boardResponse.plan?.benchIds ?? []
      });
      setTouched(false);
    } catch (failure) {
      setError(
        failure instanceof Error ? failure.message : 'Não foi possível montar o painel.'
      );
    } finally {
      setLoading(false);
    }
  }, [teamId]);

  useEffect(() => {
    void load();
  }, [load]);

  const squadById = useMemo(() => {
    const byId = new Map<string, TacticsSquadRowDto>();

    for (const row of board?.squad ?? []) {
      byId.set(row.playerId, row);
    }

    return byId;
  }, [board]);

  const chosen = useMemo(
    () => [...starters, ...bench],
    [starters, bench]
  );

  /**
   * A shape as the manager says it, out of the catalogue this screen already holds.
   *
   * <para>
   * A match remembers the code it kicked off in ("352"), and the code is not what anybody
   * reads: a column of codes is a column of numbers and the manager has to remember which is
   * which. The name is asked of the catalogue the screen was given for the ten buttons
   * rather than of a second endpoint, and a code the catalogue does not know is printed as
   * itself — a shape nobody has a name for is still a shape, and hiding it behind a dash
   * would claim the game never played it.
   * </para>
   *
   * <p>
   * And a match with no shape recorded is a dash, which is the difference between "this club
   * went out in nothing" and "nobody wrote this down".
   * </p>
   */
  const shapeOf = useMemo(() => {
    const byCode = new Map(tactics.map(tactic => [tactic.code.toLowerCase(), tactic.name]));

    return (code?: string | null): string => {
      if (!code) return '—';

      return byCode.get(code.toLowerCase()) ?? code;
    };
  }, [tactics]);

  /**
   * Puts a man somewhere, which is the only way anything moves on this screen.
   *
   * <para>
   * One function, and the buttons and the dragging both go through it. They are the same
   * decision — "this man is a starter now" — and two implementations of one decision is how a
   * drag onto a full eleven behaves differently from a click onto it: the drag pushes the
   * weakest man off, the click does nothing, and the manager is left arguing with a mouse.
   * </para>
   *
   * <para>
   * A full column takes the man anyway and puts the last of the others somewhere else. Refusing
   * is the worse answer on a board: the manager has dragged somebody in front of the eleven and
   * watching nothing happen teaches him the screen is broken. Which man leaves is the last of
   * the column, and the column is written best-first, so that is the one the club was least
   * </para>
   *
   * <para>
   * attached to.
   * </para>
   */
  const place = useCallback(
    (playerId: string, target: 'starters' | 'bench' | 'free') => {
      setTouched(true);

      setOrder(current => {
        const without = current.starters.filter(id => id !== playerId);
        const benchWithout = current.bench.filter(id => id !== playerId);

        if (target === 'free') {
          return { starters: without, bench: benchWithout };
        }

        if (target === 'bench') {
          if (benchWithout.length >= BENCH_SIZE) {
            return { starters: without, bench: benchWithout };
          }

          return { starters: without, bench: [...benchWithout, playerId] };
        }

        // Titulares. A full eleven takes the man anyway and the last of the others goes to
        // the bench, or leaves altogether if the bench is full too.
        if (without.length >= STARTERS) {
          const evicted = without[without.length - 1];

          return {
            starters: without.slice(0, -1).concat(playerId),
            bench:
              benchWithout.length < BENCH_SIZE
                ? [...benchWithout, evicted]
                : benchWithout
          };
        }

        return { starters: [...without, playerId], bench: benchWithout };
      });
    },
    []
  );

  /** Adds a man to whichever column he is not in yet, or takes him out if he is in both. */
  const toggle = useCallback(
    (playerId: string) => {
      if (starters.includes(playerId)) {
        place(playerId, bench.length < BENCH_SIZE ? 'bench' : 'free');
        return;
      }

      place(playerId, starters.length < STARTERS ? 'starters' : 'bench');
    },
    [starters, bench, place]
  );

  /** The one-click version, for the buttons and for anybody using a keyboard. */
  const swap = useCallback(
    (playerId: string, target: 'starters' | 'bench') => place(playerId, target),
    [place]
  );

  /** A man who cannot play today is not dragged anywhere: he is not a choice. */
  const canBeMoved = useCallback(
    (playerId: string) => squadById.get(playerId)?.isAvailable ?? false,
    [squadById]
  );

  const startDragging = useCallback((playerId: string) => {
    setDragging(playerId);
  }, []);

  const endDragging = useCallback(() => {
    setDragging(null);
    setDropZone(null);
  }, []);

  /**
   * The move a drop means, answered once.
   *
   * <para>
   * The id comes from the screen first and from the drag payload second. Screen state is the
   * honest one because it is the only one that knows a man cannot be moved today, and a drop
   * that had to be trusted to its payload would be trusting the browser to have refused to
   * drag a suspended player — which it will not, because <c>draggable</c> is not a permission.
   * </para>
   *
   * <para>
   * The squad is a target and not only a source: putting somebody back is the thing a
   * manager does when the eleven is full and the man in it has just been suspended, and making
   * him find an empty column first would be the screen rearranging his problem.
   * </para>
   */
  const dropOn = useCallback(
    (event: React.DragEvent, zone: Zone) => {
      event.preventDefault();

      const playerId = dragging ?? event.dataTransfer.getData('text/plain');
      if (!playerId || !canBeMoved(playerId)) return;

      place(playerId, zone === 'squad' ? 'free' : zone);
      endDragging();
    },
    [dragging, canBeMoved, place, endDragging]
  );

  /**
   * Lets a column be dropped on, and says so while it is being hovered.
   *
   * <p>
   * <c>preventDefault</c> on the dragover is the whole permission: without it the browser
   * refuses the drop and no amount of correct handling in <c>drop</c> is ever called.
   * </p>
   */
  /** The four things a column needs to be a drop target and to say so. */
  const dropTarget = (zone: Zone) => ({
    onDragOver: (event: React.DragEvent) => hoverOn(event, zone),
    onDragEnter: (event: React.DragEvent) => hoverOn(event, zone),
    onDrop: (event: React.DragEvent) => dropOn(event, zone)
  });

  const hoverOn = useCallback(
    (event: React.DragEvent, zone: Zone) => {
      event.preventDefault();
      event.dataTransfer.dropEffect = 'move';
      setDropZone(zone);
    },
    []
  );

  /**
   * Picks the shape and fills the board with the eleven the staff would pick for it.
   *
   * <para>
   * The eleven is asked of the backend rather than worked out here, and that is the whole
   * point of asking: the staff's answer weighs two things the screen cannot weigh on its own —
   * <i>how good the man is</i> and <i>how much he has left</i>. A striker on four is still the
   * best striker in the club, and the same answer the engine will use to fill a gap this plan
   * leaves is the answer that should be on the board to begin with. A screen with its own idea
   * of "best" would fill the eleven with the men who look best in a column and go out with
   * the men who are not.
   * </para>
   *
   * <para>
   * It is a starting point and not a decision. Everything it picks stays clickable, and
   * nothing it picks is saved until the manager presses the button — which is the difference
   * between a screen that helps a manager and one that plays his football for him.
   * </para>
   */
  const pickTactic = useCallback(
    async (code: string) => {
      if (!teamId || !board) return;

      setTouched(true);
      setTacticCode(code);
      setSuggesting(true);
      setError(null);

      try {
        const suggestion = await MatchApi.getSuggestedEleven(teamId, board.seasonId, code);

        setOrder({ starters: suggestion.starterIds, bench: suggestion.benchIds });
      } catch (failure) {
        // The shape is kept even when the eleven could not be filled: a manager who has just
        // chosen 4-3-3 does not want the choice undone because the staff could not be asked.
        setError(
          failure instanceof Error
            ? failure.message
            : 'Não foi possível montar o time para este esquema.'
        );
      } finally {
        setSuggesting(false);
      }
    },
    [teamId, board]
  );

  const save = useCallback(async () => {
    if (!teamId || !board) return;

    setSaving(true);
    setError(null);

    try {
      await TacticsApi.savePlan({
        teamId,
        seasonId: board.seasonId,
        tacticCode,
        starterIds: starters,
        benchIds: bench
      });

      // The board is re-read rather than patched, because the answer that matters is the
      // one the kick-off will get — and a screen that drew its own idea of what was saved
      // would be a second answer to "what does my club play".
      await load();
    } catch (failure) {
      setError(
        failure instanceof Error ? failure.message : 'Não foi possível salvar a ordem.'
      );
    } finally {
      setSaving(false);
    }
  }, [teamId, board, tacticCode, starters, bench, load]);

  if (!teamId) {
    return (
      <div className="page">
        <p className="empty">Escolha um clube para montar o time.</p>
      </div>
    );
  }

  if (loading && !board) {
    return (
      <div className="page">
        <p className="empty">Carregando o painel…</p>
      </div>
    );
  }

  const next = board?.next;
  const opponent = board?.opponent;
  const hasFixture = Boolean(next && opponent);

  const squad = board?.squad ?? [];
  const recentForm = board?.recentForm ?? [];
  const summary = board?.recentFormSummary;

  // Exactly one goalkeeper, and the count is read off the eleven rather than off the squad:
  // a team sheet with two names in that band is two goalkeepers on the pitch, because a
  // replacement for one of them comes from the line the man plays and a keeper's line has
  // nobody to replace it from. The backend refuses it, and a save button that offers a save
  // the backend will refuse is a control the server has already said no to.
  const keeperCount = starters.filter(playerId =>
    squad.some(player => player.playerId === playerId && player.position === 'GK')
  ).length;
  const completeEleven = starters.length === STARTERS;
  const oneGoalkeeper = keeperCount === 1;

  // The men the manager has named who cannot walk onto the pitch. The row says why beside his
  // name, but a mark beside a name is read in passing and this is the sentence that says it
  // outright — eleven names are on this panel and the manager is choosing a keeper in the middle
  // of them, not reading an absence list.
  const startersOutOfAction = starters
    .map(playerId => squad.find(player => player.playerId === playerId))
    .filter((row): row is TacticsSquadRowDto => !!row && !row.isAvailable)
    .map(row => `${row.name} (${absenceReason(row)})`);

  const canSave =
    completeEleven && oneGoalkeeper && !saving && (touched || starters.length > 0);

  const inspectedRow = inspected ? squadById.get(inspected) : undefined;

  return (
    <div className="page tactics-page">
      {/* The screen's own header, and it stays where it is.
          The order the manager leaves here is the one the kick-off reads, and the kick-off is
          not waiting for him: a save button at the foot of a page that is two screens tall is a
          button a manager changes an eleven and walks away from. So the button lives at the top
          and the top does not move — which also means the question "is this saved?" has to be
          answered up there, because a button that is always in view is a button that can be
          pressed without anybody having looked at what changed. */}
      <div className="tactics-bar">
        <h1>Táticas</h1>
        <span
          className={`tactics-bar__status ${touched ? 'tactics-bar__status--dirty' : ''}`}
        >
          {touched
            ? 'Alterações não salvas'
            : board?.plan
              ? `Salvo em ${new Date(board.plan.updatedAt).toLocaleString('pt-BR')}`
              : 'Nenhuma ordem salva ainda'}
        </span>
        <button
          type="button"
          className="primary"
          onClick={() => void save()}
          disabled={!canSave}
        >
          {saving ? 'Salvando…' : 'Salvar ordem'}
        </button>
      </div>

      <p className="page-subtitle">
        A ordem que o seu clube vai usar na próxima partida. Ela fica valendo até você
        trocar — a partida é aberta pelo calendário, não por aqui.
      </p>

      {error && <p className="error">{error}</p>}

      <div className="tactics-layout">
        {/* -------------------------------------------------- A decisão
            Two thirds of the screen, and everything in it is being decided: the shape, the
            eleven, the bench and the twenty-three men they are chosen out of. */}
        <div className="tactics-workspace">
          <section
            className={`tactics-panel tactics-squad ${dropZone === 'squad' ? 'tactics-panel--over' : ''}`}
          >
            <h2 className="tactics-panel__title">
              Elenco
              <span className="tactics-count">{squad.length}</span>
            </h2>

            {/* Every man in the club, as a card small enough to hold twenty-three of on one
                screen. The four things on it are the four a choice between men needs: which
                line he plays in, who he is, how good he is and how much he has left. A card
                carrying all eight attributes instead would be a card nobody could read at
                twenty-three to a page, and the attributes are one click away in the window
                that opens when a card is pressed. */}
            <ul className="tactics-cards" {...dropTarget('squad')}>
              {squad.map(row => (
                <li
                  key={row.playerId}
                  draggable={row.isAvailable}
                  onDragStart={event => {
                    event.dataTransfer.setData('text/plain', row.playerId);
                    startDragging(row.playerId);
                  }}
                  onDragEnd={endDragging}
                  role="button"
                  tabIndex={0}
                  aria-pressed={inspected === row.playerId}
                  title={row.name}
                  className={`tactics-card ${chosen.includes(row.playerId) ? 'tactics-card--chosen' : ''} ${row.isAvailable ? '' : 'tactics-card--out'} ${dragging === row.playerId ? 'tactics-card--dragging' : ''} ${inspected === row.playerId ? 'tactics-card--inspected' : ''}`}
                  onClick={() =>
                    setInspected(current => (current === row.playerId ? null : row.playerId))
                  }
                  onKeyDown={event => {
                    if (event.key !== 'Enter' && event.key !== ' ') return;
                    event.preventDefault();
                    setInspected(current =>
                      current === row.playerId ? null : row.playerId
                    );
                  }}
                >
                  <span className="tactics-card__pos">
                    {positionLabel(row.position)}
                  </span>
                  {/* The letters are a door like any other name: a manager who has twenty
                      pixels of a man still has to be able to ask who he is. */}
                  <PlayerName
                    playerId={row.playerId}
                    className="tactics-card__initials"
                  >
                    {initialsOf(row.name)}
                  </PlayerName>
                  <span
                    className="tactics-card__stars"
                    title={`${row.stars.toFixed(1)} estrelas`}
                  >
                    {starsToString(row.stars)}
                  </span>
                  {/* Energy at the foot of the card, where a bar reads as the ground a man
                      is standing on rather than as one more number in a column. */}
                  <span
                    className="tactics-card__energy"
                    title={`Energia ${row.energy}`}
                  >
                    <span
                      className={`tactics-card__energy-fill ${energyClass(row.energy)}`}
                      style={{ width: energyPercent(row.energy) }}
                    />
                  </span>
                </li>
              ))}
            </ul>
          </section>

          <div className="tactics-board">
            {/* The shape, listed. Beside the eleven and not above it: a manager picks a shape
                and an eleven together, and a catalogue sitting under the eleven made the order
                of the screen the opposite of the order of the thought. */}
            <section className="tactics-panel tactics-shapes">
              <h2 className="tactics-panel__title">
                Esquema
                {suggesting && <span className="tactics-count">montando…</span>}
              </h2>
              <div className="tactics-selector">
                {tactics.map(tactic => (
                  <button
                    key={tactic.code}
                    type="button"
                    className={`tactics-option ${tacticCode === tactic.code ? 'active' : ''}`}
                    onClick={() => void pickTactic(tactic.code)}
                    disabled={suggesting}
                  >
                    <span className="tactics-option__name">{tactic.name}</span>
                    <span className="tactics-option__shape">
                      {tactic.defenders}-{tactic.midfielders}-{tactic.attackers}
                    </span>
                  </button>
                ))}
              </div>
            </section>

            <section
              className={`tactics-panel ${dropZone === 'starters' ? 'tactics-panel--over' : ''}`}
            >
              <h2 className="tactics-panel__title">
                Titulares
                <span className="tactics-count">
                  {starters.length}/{STARTERS}
                </span>
              </h2>
              <ul
                className="tactics-list"
                {...dropTarget('starters')}
              >
                {starters.map(playerId => (
                  <TacticsRow
                    key={playerId}
                    row={squadById.get(playerId)}
                    playerId={playerId}
                    dragging={dragging === playerId}
                    onDragStart={startDragging}
                    onDragEnd={endDragging}
                    onRemove={() => toggle(playerId)}
                    onPromote={() => swap(playerId, 'bench')}
                    actionLabel="Reserva"
                  />
                ))}
              </ul>
              {starters.length < STARTERS && (
                <p className="tactics-hint">
                  Faltam {STARTERS - starters.length} para fechar o time.
                </p>
              )}
              {startersOutOfAction.length > 0 && (
                /* Not a refusal and not a red panel: naming him is refused by the backend, and a
                   control that saves an eleven the server will reject is a control the server has
                   already said no to. This says who and why, so the choice of the replacement is the
                   manager's before he presses anything. */
                <p className="tactics-hint tactics-hint--warning" role="status">
                  Não jogam: {startersOutOfAction.join(', ')}. O salvamento é recusado pelo servidor
                  enquanto eles estiverem escalados.
                </p>
              )}
              {completeEleven && !oneGoalkeeper && (
                <p className="tactics-hint">
                  {keeperCount === 0
                    ? 'Falta um goleiro no time.'
                    : `O time tem ${keeperCount} goleiros. Só um joga.`}
                </p>
              )}
            </section>

            <section
              className={`tactics-panel ${dropZone === 'bench' ? 'tactics-panel--over' : ''}`}
            >
              <h2 className="tactics-panel__title">
                Reservas
                <span className="tactics-count">
                  {bench.length}/{BENCH_SIZE}
                </span>
              </h2>
              <ul
                className="tactics-list"
                {...dropTarget('bench')}
              >
                {bench.map(playerId => (
                  <TacticsRow
                    key={playerId}
                    row={squadById.get(playerId)}
                    playerId={playerId}
                    dragging={dragging === playerId}
                    onDragStart={startDragging}
                    onDragEnd={endDragging}
                    onRemove={() => toggle(playerId)}
                    onPromote={() => swap(playerId, 'starters')}
                    actionLabel="Titular"
                  />
                ))}
              </ul>
            </section>
          </div>
        </div>

        {/* -------------------------------------------------- O contexto
            One third, and pinned where it stands while the board is worked on. The fixture
            the order is about, and the last five results — which are the reason one shape is
            right today and another was right three weeks ago. */}
        <aside className="tactics-aside">
          <section className="tactics-panel">
            <h2 className="tactics-panel__title">Próxima partida</h2>
            {hasFixture ? (
              <>
                <div className="tactics-next__fixture">
                  <span className="tactics-next__competition">
                    {next!.competitionName}
                    {next!.matchDayNumber ? ` · Dia ${next!.matchDayNumber}` : ''}
                    {next!.roundNumber ? ` · Rodada ${next!.roundNumber}` : ''}
                  </span>
                  <span className="tactics-next__teams">
                    {opponent!.isHome ? (
                      <>
                        <span>{board!.teamName}</span>
                        <span className="tactics-next__vs">×</span>
                        <ClubName teamId={opponent!.id}>{opponent!.name}</ClubName>
                      </>
                    ) : (
                      <>
                        <ClubName teamId={opponent!.id}>{opponent!.name}</ClubName>
                        <span className="tactics-next__vs">×</span>
                        <span>{board!.teamName}</span>
                      </>
                    )}
                  </span>
                  <span className="tactics-next__venue">
                    {opponent!.isHome ? 'Fora de casa' : 'Em casa'}
                  </span>
                </div>

                {board!.headToHead && board!.headToHead.played > 0 && (
                  <dl className="tactics-h2h">
                    <dt>Confrontos</dt>
                    <dd>{board!.headToHead.played}</dd>
                    <dt>Vitórias</dt>
                    <dd>{board!.headToHead.wins}</dd>
                    <dt>Empates</dt>
                    <dd>{board!.headToHead.draws}</dd>
                    <dt>Derrotas</dt>
                    <dd>{board!.headToHead.losses}</dd>
                    <dt>Saldo</dt>
                    <dd>{board!.headToHead.goalDifference}</dd>
                  </dl>
                )}

                {/* The window may already have gone. Saying so is the difference between an
                    order that will be used and one written after the whistle. */}
                {!next!.waveOpen && (
                  <p className="tactics-next__closed">
                    A janela desta partida já começou. A ordem vale a partir da próxima.
                  </p>
                )}
              </>
            ) : (
              <p className="empty">
                Não há próxima partida marcada para esta temporada. O elenco continua ao lado.
              </p>
            )}
          </section>

          {recentForm.length > 0 && (
            <section className="tactics-panel">
              <h2 className="tactics-panel__title">Últimos jogos</h2>

              {/* The five rows say which games; these five numbers say what they add up to,
                  and the backend counts them over the very rows underneath — so the header
                  and the list can never be about two different weeks. */}
              {summary && summary.played > 0 && (
                <div className="tactics-form__summary">
                  <span className="tactics-form__run">
                    <span className="tactics-form__run-item form-win">
                      <strong>{summary.wins}</strong> V
                    </span>
                    <span className="tactics-form__run-item form-draw">
                      <strong>{summary.draws}</strong> E
                    </span>
                    <span className="tactics-form__run-item form-loss">
                      <strong>{summary.losses}</strong> D
                    </span>
                  </span>
                  <span className="tactics-form__goals">
                    <span>
                      <strong>{summary.goalsFor}</strong> pró
                    </span>
                    <span>
                      <strong>{summary.goalsAgainst}</strong> contra
                    </span>
                    <span
                      className={
                        summary.goalDifference >= 0
                          ? 'tactics-form__diff--up'
                          : 'tactics-form__diff--down'
                      }
                    >
                      {summary.goalDifference >= 0 ? '+' : ''}
                      {summary.goalDifference} saldo
                    </span>
                  </span>
                </div>
              )}

              <ul className="tactics-form">
                {recentForm.map(match => (
                  <FormRow key={match.matchId} match={match} shapeOf={shapeOf} />
                ))}
              </ul>
            </section>
          )}
        </aside>
      </div>

      {/* The window that says the rest of the man. It stays where it is while the board is
          worked on, because reading a player and choosing between eleven are two halves of
          one decision and a window that jumped about would break the second one. */}
      {inspectedRow && (
        <PlayerDetail
          row={inspectedRow}
          starters={starters}
          bench={bench}
          onClose={() => setInspected(null)}
          onPlace={swap}
          onRemove={playerId => place(playerId, 'free')}
        />
      )}
    </div>
  );
};

/**
 * One of the last few matches, with the shape each side went out in.
 *
 * <para>
 * The colour is the result and the letter says it again, because one of the two reaching a
 * manager is not guaranteed and the other one is a guess — and the two classes are the game's
 * own, so a form guide here is drawn exactly as the club's page draws the same five games.
 * </para>
 *
 * <p>
 * Both shapes are on the line because a scoreline does not say what it was played with: 2-1 is
 * the same evening against 4-4-2 and against 3-5-2, and the manager picking a shape for
 * Saturday is reading these five lines for exactly that. The club's own shape is the one in
 * the highlight, so nobody has to work out which of the two is his.
 * </p>
 */
const FormRow: React.FC<{
  match: TacticsBoardDto['recentForm'][number];
  shapeOf: (code?: string | null) => string;
}> = ({ match, shapeOf }) => {
  const form = formOf(match);

  return (
    <li
      className={`tactics-form__row form-${form}`}
      title={`${FORM_TITLE[form]} ${match.goalsFor} x ${match.goalsAgainst} — ${match.opponentName}`}
    >
      <FormBadge form={form} />
      <div className="tactics-form__body">
        <div className="tactics-form__head">
          <span className="tactics-form__comp">
            {match.competitionName ?? `Rodada ${match.roundNumber}`}
          </span>
          <span className="tactics-form__score">
            {match.goalsFor} × {match.goalsAgainst}
          </span>
        </div>
        <div className="tactics-form__fixture">
          <ClubName teamId={match.opponentTeamId}>{match.opponentName}</ClubName>
          <span className="tactics-form__venue">
            {match.isHome ? 'Casa' : 'Fora'}
          </span>
        </div>
        <div className="tactics-form__shapes">
          <span className="tactics-form__shape tactics-form__shape--mine">
            {shapeOf(match.tacticCode)}
          </span>
          <span className="tactics-form__vs">×</span>
          <span className="tactics-form__shape">{shapeOf(match.opponentTacticCode)}</span>
          <Link className="tactics-form__link" to={`/match/${match.matchId}`}>
            Resumo
          </Link>
        </div>
      </div>
    </li>
  );
};

/**
 * One man, whole, in a window that does not move.
 *
 * <para>
 * Everything a card could not carry: the eight attributes by name, how old he is, how good he
 * is and what is keeping him off the pitch. It is also where the three moves are made, because
 * a card seventy pixels wide has no room for the buttons that let a manager put a man in the
 * eleven without dragging him — and a drag is unreachable from a keyboard, so the buttons
 * cannot live only where there is room for them.
 * </para>
 */
const PlayerDetail: React.FC<{
  row: TacticsSquadRowDto;
  starters: string[];
  bench: string[];
  onClose: () => void;
  onPlace: (playerId: string, target: 'starters' | 'bench') => void;
  onRemove: (playerId: string) => void;
}> = ({ row, starters, bench, onClose, onPlace, onRemove }) => {
  const isKeeper = row.position === 'GK';
  const inEleven = starters.includes(row.playerId);
  const onBench = bench.includes(row.playerId);

  return (
    <aside className="tactics-detail" aria-label={`Ficha de ${row.name}`}>
      <header className="tactics-detail__head">
        <span className="tactics-detail__pos">{positionLabel(row.position)}</span>
        <PlayerName playerId={row.playerId} className="tactics-detail__name">
          {row.name}
        </PlayerName>
        <button
          type="button"
          className="tactics-detail__close"
          onClick={onClose}
          title="Fechar a ficha"
          aria-label="Fechar a ficha"
        >
          ✕
        </button>
      </header>

      <div className="tactics-detail__meta">
        <span className="tactics-detail__stars" title={`${row.stars.toFixed(1)} estrelas`}>
          {starsToString(row.stars)}
        </span>
        <span className={energyTextClass(row.energy)}>{row.energy} de energia</span>
        <span>{row.age} anos</span>
        <PlayerStatusMarks
          suspensionMatches={row.suspensionMatches}
          injuryMatchesRemaining={row.injuryMatchesRemaining}
        />
      </div>

      {!row.isAvailable && (
        <p className="tactics-detail__out">{absenceReason(row)}</p>
      )}

      <ul className="tactics-detail__attrs">
        {DETAIL_ATTRIBUTE_LABELS.map((label, index) => {
          const value = row.attributes[index];

          if (value === undefined) return null;

          // A centre back's goalkeeper numbers are zero because he is not a goalkeeper, and two
          // red zeroes on the window would read as "the worst keeper in the country" rather than
          // as "not his job".
          if (GOALKEEPER_ATTRIBUTE_INDEXES.includes(index as 5 | 6) && !isKeeper) return null;

          return (
            <li key={label} className={`tactics-detail__attr ${attributeTextClass(value)}`}>
              <span className="tactics-detail__attr-label">{label}</span>
              <span className="tactics-detail__attr-bar" aria-hidden="true">
                <span
                  className={`tactics-detail__attr-fill ${attributeTextClass(value)}`}
                  style={{ width: `${Math.max(0, Math.min(100, value))}%` }}
                />
              </span>
              <span className="tactics-detail__attr-value">{value}</span>
            </li>
          );
        })}
      </ul>

      {/* A control that cannot be pressed rather than one that saves an eleven the server will
          refuse: an unavailable man is not named, and a full column has no place for him. */}
      <div className="tactics-detail__actions">
        <button
          type="button"
          className="tactics-detail__action"
          onClick={() => onPlace(row.playerId, 'starters')}
          disabled={!row.isAvailable || inEleven || starters.length >= STARTERS}
        >
          Titular
        </button>
        <button
          type="button"
          className="tactics-detail__action"
          onClick={() => onPlace(row.playerId, 'bench')}
          disabled={!row.isAvailable || onBench || bench.length >= BENCH_SIZE}
        >
          Reserva
        </button>
        <button
          type="button"
          className="tactics-detail__action"
          onClick={() => onRemove(row.playerId)}
          disabled={!inEleven && !onBench}
        >
          Remover
        </button>
      </div>
    </aside>
  );
};

/**
 * The eight attributes, as a strip beside a name.
 *
 * <para>
 * The decision this screen exists for is a comparison between men, and a comparison needs
 * both sides of it on screen. Stars say how good somebody is and energy says how much he has
 * left, and neither one says <i>at what</i> — so two identical rows would have been two
 * identical rows, and the manager would be choosing by name, which is the one thing he does
 * not know and the whole reason for having a board.
 * </para>
 *
 * <p>
 * A number is shown with its label rather than on its own, and the labels are the squad
 * table's. On a strip this narrow the bare digit would be a column of numbers with no
 * headings, and a manager reading "17" next to a name has learned nothing.
 * </p>
 */
const AttributeStrip: React.FC<{
  values?: number[];
  position?: string;
}> = ({ values, position }) => {
  if (!values || values.length === 0) return null;

  // A centre back's goalkeeper numbers are zero because he is not a goalkeeper, and two red
  // zeros on every outfielder's row would read as "the worst keeper in the country" rather
  // than "not his job". They are left off, which is the same convention the player's own line
  // of attributes uses — a keeper is shown his agility and his reflexes and his feet, and
  // nobody is shown six tags about a job they do not have.
  const keeper = position === 'GK';

  return (
    <span className="tactics-attrs">
      {ATTRIBUTE_LABELS.map((label, index) => {
        const value = values[index];

        if (value === undefined) return null;

        const isGoalkeeperTag = index === 5 || index === 6;
        if (isGoalkeeperTag && !keeper) return null;

        return (
          <span key={label} className={`tactics-attr ${attributeTextClass(value)}`}>
            <span className="tactics-attr__label">{label}</span>
            {value}
          </span>
        );
      })}
    </span>
  );
};

/**
 * One man on the board, in the eleven or on the bench.
 *
 * <p>
 * A man who is no longer in the club's squad is still drawn, by id and with no name: the
 * plan names him, the plan is the manager's, and quietly dropping him would turn a saved
 * order into a shorter one without anybody deciding it.
 * </p>
 */
const TacticsRow: React.FC<{
  row?: TacticsSquadRowDto;
  playerId: string;
  /** Whether this is the row under the pointer, drawn at half so the drag is visible. */
  dragging: boolean;
  onDragStart: (playerId: string) => void;
  onDragEnd: () => void;
  onRemove: () => void;
  onPromote: () => void;
  actionLabel: string;
}> = ({
  row,
  playerId,
  dragging,
  onDragStart,
  onDragEnd,
  onRemove,
  onPromote,
  actionLabel
}) => (
  <li
    className={`tactics-row tactics-row--picked ${dragging ? 'tactics-row--dragging' : ''}`}
    draggable
    onDragStart={event => {
      event.dataTransfer.setData('text/plain', playerId);
      onDragStart(playerId);
    }}
    onDragEnd={onDragEnd}
  >
    {row ? (
      <>
        <span className="tactics-row__pos">{positionLabel(row.position)}</span>
        <div className="tactics-row__line">
          <span className="tactics-row__name" title={absenceReason(row) || undefined}>
            <PlayerName playerId={playerId}>{row.name}</PlayerName>
            <PlayerStatusMarks
              suspensionMatches={row.suspensionMatches}
              injuryMatchesRemaining={row.injuryMatchesRemaining}
            />
          </span>
          <span className="tactics-row__meta">
            <span className={energyTextClass(row.energy)}>{row.energy}</span>
            <span>{row.stars.toFixed(1)}★</span>
          </span>
        </div>
        <span className="tactics-row__buttons">
          <button
            type="button"
            className="tactics-row__move"
            onClick={onPromote}
            title={actionLabel}
          >
            {actionLabel === 'Titular' ? 'T' : 'R'}
          </button>
          <button
            type="button"
            className="tactics-row__move"
            onClick={onRemove}
            title="Remover"
          >
            ✕
          </button>
        </span>
        <AttributeStrip values={row.attributes} position={row.position} />
      </>
    ) : (
      <>
        <span className="tactics-row__pos">—</span>
        <div className="tactics-row__line">
          <span className="tactics-row__name tactics-row__gone">
            Jogador fora do elenco
          </span>
        </div>
        <span className="tactics-row__buttons">
          <button
            type="button"
            className="tactics-row__move"
            onClick={onRemove}
            title="Remover"
          >
            ✕
          </button>
        </span>
      </>
    )}
  </li>
);

export default TacticsScreen;