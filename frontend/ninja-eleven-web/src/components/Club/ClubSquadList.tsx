import React, { useMemo, useState } from 'react';
import type { SquadPlayerDto, TeamDto } from '@/types';
import { formatLimo } from '@/services/limo';
import PlayerBox from '@/components/Common/PlayerBox';
import ShirtNumberCell from '@/components/Club/ShirtNumberCell';

/**
 * The facts a manager orders a squad by, in the words the order control shows.
 *
 * Every one of them is a number the engine decided, so ordering is presentation: the value is
 * never recomputed here, only the order of the boxes that already arrived. That is why the
 * list still sorts at all now that the table's column heads are gone — the head was never the
 * sort, it was the sort's way of being reached.
 */
const SORT_OPTIONS: { key: SortKey; label: string }[] = [
  { key: 'position', label: 'Posição' },
  { key: 'shirtNumber', label: 'Número da camisa' },
  { key: 'name', label: 'Nome' },
  { key: 'age', label: 'Idade' },
  { key: 'stars', label: 'Estrelas' },
  { key: 'energy', label: 'Energia' },
  { key: 'speed', label: 'Velocidade' },
  { key: 'accuracy', label: 'Finalização' },
  { key: 'dribbling', label: 'Drible' },
  { key: 'heading', label: 'Cabeceio' },
  { key: 'strength', label: 'Força' },
  { key: 'goalkeeperPower', label: 'Gol (goleiro)' },
  { key: 'reflexes', label: 'Reflexos (goleiro)' },
  { key: 'goals', label: 'Gols' },
  { key: 'saves', label: 'Defesas' },
  { key: 'yellowCards', label: 'Amarelos' },
  { key: 'redCards', label: 'Vermelhos' },
  { key: 'marketValue', label: 'Valor' },
  { key: 'askingPrice', label: 'Preço' },
  { key: 'salary', label: 'Salário' },
  { key: 'seasonsLeft', label: 'Contrato restante' }
];

type SortKey =
  | 'shirtNumber' | 'position' | 'name' | 'age' | 'energy' | 'speed' | 'accuracy'
  | 'dribbling' | 'heading' | 'strength' | 'goalkeeperPower' | 'reflexes' | 'goals' | 'saves'
  | 'yellowCards' | 'redCards' | 'stars'
  | 'marketValue' | 'askingPrice' | 'salary' | 'seasonsLeft';

const POSITION_RANK: Record<string, number> = { GK: 0, DEF: 1, MID: 2, ATT: 3 };

interface ClubSquadListProps {
  squad: SquadPlayerDto[];

  /**
   * The club these men play for, so the shirt sits beside the name.
   *
   * A squad is a column of men in one colour, and the colour is half of what says whose club
   * this is — a manager reading another club's squad is reading names he will not see on
   * Saturday. Left out on a screen that has no club to show, it is simply a list of men.
   */
  team?: TeamDto | null;

  /**
   * When a box is something a manager picks, the box is the target. A screen that is choosing
   * an eleven is not reading a list, it is filling one in, and a screen that only offers
   * fifteen small targets for that is fifteen chances to miss the right man.
   *
   * Left out, the box is not a target: a thing that looks clickable and does nothing is
   * worse than a thing that is obviously not a thing.
   */
  onToggle?: (player: SquadPlayerDto) => void;

  /** The players already picked, who the box then says so. */
  selectedIds?: ReadonlySet<string>;

  /** The players picked for the other group, so a box can say where a man already is. */
  elsewhereIds?: ReadonlySet<string>;

  /** Said in the box's tooltip: why a man is out, in words. */
  describeAbsence?: (player: SquadPlayerDto) => string;

  /**
   * Ends the man's contract, paying the settlement. Left out, the control is not drawn: a
   * squad screen belongs to somebody else's club as often as to the manager's own, and a
   * screen that offered to fire half the league would be offering it to a manager who
   * cannot.
   */
  onRelease?: (player: SquadPlayerDto) => void;

  /**
   * Renews a contract, for as many seasons as the manager says and at whatever the wage is
   * today. Left out, the renewal control is not shown: a squad screen belongs to somebody
   * else's club as often as to the manager's own, and the renewal only matters for the
   * manager's own players.
   *
   * It is offered to every contracted man and not only to one in his last season, because
   * that is what the club may do: a renewal restates the deal from this season on, so a
   * manager who wants three more seasons of a man he has four seasons of does not have to
   * wait for the fourth to run out to say so.
   */
  onRenewContract?: (player: SquadPlayerDto) => void;

  /**
   * Puts a man in a shirt. Left out, the number is a number and not a control: the squad
   * screen belongs to somebody else's club as often as to the manager's own, and a screen
   * that offered to renumber the whole league would be offering it to a reader who cannot.
   */
  onEditShirtNumber?: (player: SquadPlayerDto, shirtNumber: number) => void;

  /**
   * What the server said about the last number asked for, by player id, so the man whose
   * number was refused is the one who says so. A refusal shown in the corner of the screen is
   * a refusal about somebody else's problem.
   */
  shirtRefusals?: Readonly<Record<string, string>>;

  /** The caption above the list. Nothing when there is no caption to give. */
  caption?: string;
}

/**
 * A club's players, as a manager reads them: one box per man, each with his face, his age, his
 * attributes, his club, his money and his energy, and every one of them orderable.
 *
 * This is one component and not two similar ones. A squad looked up in a modal and the same
 * squad on the club's own screen are the same question, and two lists answering it
 * differently is how the keeper's saves end up on one screen and not on the other.
 */
const ClubSquadList: React.FC<ClubSquadListProps> = ({
  squad,
  team,
  onToggle,
  selectedIds,
  elsewhereIds,
  describeAbsence,
  onRelease,
  onEditShirtNumber,
  onRenewContract,
  shirtRefusals,
  caption
}) => {
  const [sort, setSort] = useState<{ key: SortKey; ascending: boolean }>({
    key: 'position',
    ascending: true
  });

  const sorted = useMemo(() => {
    const direction = sort.ascending ? 1 : -1;

    const valueOf = (player: SquadPlayerDto, key: SortKey): number | string => {
      if (key === 'position') return POSITION_RANK[player.position] ?? 9;
      if (key === 'name') return player.name;

      // A man who wears none is not number zero, so he sits below every man who wears one
      // in both directions rather than sorting above the whole squad as "—0" would.
      if (key === 'shirtNumber') return player.shirtNumber ?? Number.MAX_SAFE_INTEGER;
      return player[key] as number;
    };

    return [...squad].sort((a, b) => {
      const left = valueOf(a, sort.key);
      const right = valueOf(b, sort.key);

      if (typeof left === 'string' || typeof right === 'string') {
        return String(left).localeCompare(String(right), 'pt-BR') * direction;
      }

      // A tie is broken by name, so a column of equal numbers still reads as an order
      // rather than as whatever the database happened to return.
      return ((left as number) - (right as number)) * direction
        || a.name.localeCompare(b.name, 'pt-BR');
    });
  }, [squad, sort]);

  if (squad.length === 0) {
    return <p className="league-empty">Nenhum jogador no elenco.</p>;
  }

  const hasDecisions = Boolean(onRelease || onRenewContract);

  const list = (
    <>
      {/* The order the boxes are read in. A table said it with twenty clickable column heads;
          a grid of boxes has no columns, so it says it with one control — and one control for
          twenty-one orders is easier to use than twenty-one heads are to aim at. */}
      <div className="squad-sort">
        <label htmlFor="squad-sort">Ordenar por</label>
        <select
          id="squad-sort"
          className="ctrl"
          value={sort.key}
          onChange={e => {
            const key = e.target.value as SortKey;
            setSort(current => (current.key === key ? { key, ascending: !current.ascending } : { key, ascending: true }));
          }}
        >
          {SORT_OPTIONS.map(option => (
            <option key={option.key} value={option.key}>
              {option.label}
              {sort.key === option.key ? (sort.ascending ? ' ▲' : ' ▼') : ''}
            </option>
          ))}
        </select>
      </div>

      <div className="player-box-grid">
        {sorted.map(player => {
          const picked = Boolean(selectedIds?.has(player.id));
          const elsewhere = !picked && Boolean(elsewhereIds?.has(player.id));

          return (
            <div
              key={player.id}
              className={`squad-box ${onToggle ? 'squad-box--target' : ''}`}
              onClick={onToggle ? () => onToggle(player) : undefined}
            >
              <PlayerBox
                playerId={player.id}
                name={player.name}
                face={player.face}
                age={player.age}
                position={player.position}
                stars={player.stars}
                attributes={{
                  speed: player.speed,
                  accuracy: player.accuracy,
                  dribbling: player.dribbling,
                  heading: player.heading,
                  strength: player.strength,
                  goalkeeperPower: player.goalkeeperPower,
                  reflexes: player.reflexes
                }}
                keeper={player.position === 'GK'}
                injury={player.injury}
                injuryMatchesRemaining={player.injuryMatchesRemaining}
                suspensionMatches={player.suspensionMatches}
                retiring={player.retiring}
                energy={player.energy}
                team={team ? { teamId: team.id, name: team.name, primaryColor: team.primaryColor, secondaryColor: team.secondaryColor } : null}
                money={{
                  value: player.marketValue,
                  price: player.askingPrice,
                  salary: player.salary
                }}
                className={[
                  player.injury !== 'None' ? 'injured' : '',
                  player.isAvailable ? '' : 'unavailable',
                  picked ? 'picked' : '',
                  elsewhere ? 'picked-elsewhere' : ''
                ]
                  .filter(Boolean)
                  .join(' ')}
                title={describeAbsence?.(player)}
              >
                {/* What the table's own columns said, kept: the shirt, the season's tallies,
                    the contract, and the decisions this club may take on him. A box that
                    dropped them would have been a prettier screen showing less, which is the
                    trade this change was not asked to make.

                    The shirt is the number and not the kit: every box on this screen belongs
                    to the same club and the box already says which one, so twenty-three
                    repetitions of one shirt is twenty-three repetitions of a fact. */}
                <span className="shirt-col">
                  <ShirtNumberCell
                    value={player.shirtNumber ?? null}
                    playerName={player.name}
                    onChange={onEditShirtNumber ? number => onEditShirtNumber(player, number) : undefined}
                    refusal={shirtRefusals?.[player.id] ?? null}
                  />
                </span>

                {picked && <span className="pick-mark" title="Selecionado">✓</span>}
                {elsewhere && <span className="pick-mark" title="Está no outro grupo">⇤</span>}

                <span title="Gols na temporada">
                  <span className="player-box__tag">Gols</span> {player.goals}
                </span>
                <span title="Defesas na temporada">
                  <span className="player-box__tag">Defs</span> {player.saves}
                </span>
                <span>
                  <span className="player-box__tag">Cartões</span> {player.yellowCards}🟨 {player.redCards}🟥
                </span>
                <span title={
                  player.contractSeasons > 0
                    ? `${player.seasonsLeft} de ${player.contractSeasons} temporadas de contrato a correr`
                    : 'Sem contrato'
                }>
                  <span className="player-box__tag">Contrato</span>{' '}
                  {player.contractSeasons > 0 ? `${player.seasonsLeft}/${player.contractSeasons}` : '—'}
                </span>

                {/* The settlement, quoted by the rule that charges it. The table used to read
                    "+20%", which is not a settlement anywhere in the game, and a manager
                    budgeting against it found out at the moment he pressed the button. */}
                {!player.isInLastSeason && player.contractSeasons > 0 && (
                  <span
                    className="contract-fine"
                    title={`Custo de rescindir o contrato agora. ${player.contractSeasons - player.seasonsLeft} de ${player.contractSeasons} temporadas ainda a correr.`}
                  >
                    Rescisão {formatLimo(player.releaseCost)}
                  </span>
                )}

                {hasDecisions && (
                  <span className="actions-col" onClick={event => event.stopPropagation()}>
                    {onRelease && (
                      <button
                        className="ctrl btn-sm reject"
                        title="Rescindir o contrato e pagar a multa"
                        onClick={() => onRelease(player)}
                      >
                        Rescindir
                      </button>
                    )}
                    {/* A renewal is offered to any man the club holds on a contract and refused
                        to a man who announced his retirement — the same refusal the server
                        makes, so the box that has no button is the box the rule has no answer
                        for. */}
                    {onRenewContract && player.contractSeasons > 0 && !player.retiring && (
                      <button
                        className="ctrl btn-sm renew"
                        title={
                          'Renovar o contrato a partir desta temporada. ' +
                          `Nova wage: ${formatLimo(player.wageOnRenewal ?? 0)}`
                        }
                        onClick={() => onRenewContract(player)}
                      >
                        Renovar
                      </button>
                    )}
                  </span>
                )}
              </PlayerBox>
            </div>
          );
        })}
      </div>
    </>
  );

  return caption ? (
    <section className="squad-section">
      <h3 className="squad-section-title">{caption}</h3>
      {list}
    </section>
  ) : (
    list
  );
};

export default ClubSquadList;
