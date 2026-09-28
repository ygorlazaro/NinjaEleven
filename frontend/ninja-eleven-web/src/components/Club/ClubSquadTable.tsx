import React, { useMemo, useState } from 'react';
import type { SquadPlayerDto } from '@/types';
import { positionLabel } from '@/services/formatters';
import { formatLimo } from '@/services/limo';
import { PlayerName } from '@/components/Common/Names';
import { starsToString } from '@/services/formatters';

/**
 * The columns a manager sorts a squad by. Each one is a number the engine decided, so
 * sorting is presentation: the value is never recomputed here, only the order of the rows
 * that already arrived.
 */
type SortKey =
  | 'position' | 'name' | 'age' | 'energy' | 'speed' | 'accuracy' | 'dribbling' | 'heading'
  | 'strength' | 'goalkeeperPower' | 'reflexes' | 'goals' | 'saves'
  | 'yellowCards' | 'redCards' | 'stars'
  | 'marketValue' | 'askingPrice' | 'salary' | 'seasonsLeft';

const POSITION_RANK: Record<string, number> = { GK: 0, DEF: 1, MID: 2, ATT: 3 };

interface Column {
  key: SortKey;
  label: string;
  className?: string;
}

/**
 * Returns CSS class for attribute color coding:
 * < 8: red (attr-red), < 14: yellow (attr-yellow), >= 14: green (attr-green)
 */
function attrClass(value: number): string {
  if (value < 8) return 'attr-red';
  if (value < 14) return 'attr-yellow';
  return 'attr-green';
}

/**
 * Returns CSS class for energy text color coding:
 * < 35: red (energy-red-text), < 70: yellow (energy-yellow-text), >= 70: green (energy-green-text)
 */
function energyTextClass(energy: number): string {
  if (energy < 35) return 'energy-red-text';
  if (energy < 70) return 'energy-yellow-text';
  return 'energy-green-text';
}

/**
 * The columns that are always there, and the two that belong to a goalkeeper.
 *
 * A keeper's numbers describe his job. Showing a striker's reflexes beside his name would
 * be a column of zeros pretending to be information, and showing the two columns only for
 * the keeper who uses them would break the grid the other twenty-two men are read on. So
 * they appear when the club has a keeper at all, and hold their place for everyone else.
 */
const commonColumns: Column[] = [
  { key: 'position', label: 'Pos' },
  { key: 'name', label: 'Jogador', className: 'squad-name' },
  { key: 'age', label: 'Idade', className: 'num' },
  { key: 'energy', label: 'Energia', className: 'num' },
  { key: 'stars', label: '★', className: 'num stars-col' },
  { key: 'speed', label: 'Vel', className: 'num' },
  { key: 'accuracy', label: 'Fin', className: 'num' },
  { key: 'dribbling', label: 'Dri', className: 'num' },
  { key: 'heading', label: 'Cab', className: 'num' },
  { key: 'strength', label: 'For', className: 'num' }
];

const keeperColumns: Column[] = [
  { key: 'goalkeeperPower', label: 'Gol', className: 'num' },
  { key: 'reflexes', label: 'Ref', className: 'num' }
];

/**
 * The money, as four numbers a manager negotiates with: what the player is worth, what a
 * rival would have to pay for him, what the club pays him and how much of his contract is
 * left.
 *
 * The value and the price are two columns because they are two questions. A man with two
 * seasons still to run is worth what he is worth and costs a fifth more, and a table with
 * one money column would have to pick one of the two — the one that reads as cheap, and so
 * the first offer a club makes. The fine is said on the price rather than hidden inside it,
 * so a manager can see that the extra exists and why.
 *
 * The amounts are limos and the unit is said once in the header rather than on every cell:
 * twenty-three rows repeating "L$" is twenty-three repetitions of a fact the column already
 * says, and the width it costs is the width a name does not have.
 */
const moneyColumns: Column[] = [
  { key: 'marketValue', label: 'Valor L$', className: 'num money' },
  { key: 'askingPrice', label: 'Preço L$', className: 'num money' },
  { key: 'salary', label: 'Salário L$', className: 'num money' },
  { key: 'seasonsLeft', label: 'Contrato', className: 'num contract-cell' }
];

const tallyColumns: Column[] = [
  { key: 'goals', label: 'Gols', className: 'num accent-col' },
  { key: 'saves', label: 'Defs', className: 'num accent-col' },
  { key: 'yellowCards', label: 'Ama', className: 'num' },
  { key: 'redCards', label: 'Verm', className: 'num' }
];

interface ClubSquadTableProps {
  squad: SquadPlayerDto[];

  /**
   * When a row is something a manager picks, the row is the button. A screen that is
   * choosing an eleven is not reading a table, it is filling one in, and a screen that only
   * offers fifteen small targets for that is fifteen chances to miss the right man.
   *
   * Left out, the row is not a target: a row that looks clickable and does nothing is worse
   * than a row that is obviously a row.
   */
  onToggle?: (player: SquadPlayerDto) => void;

  /** The players already picked, who the row then says so. */
  selectedIds?: ReadonlySet<string>;

  /** The players picked for the other group, so a row can say where a man already is. */
  elsewhereIds?: ReadonlySet<string>;

  /** Said in the row's tooltip: why a man is out, in words. */
  describeAbsence?: (player: SquadPlayerDto) => string;

  /** The caption above the table. Nothing when there is no caption to give. */
  caption?: string;
}

/**
 * A club's players, as a manager reads them: one row per man, every column a number he can
 * sort by, and the goalkeeper's two columns where they belong.
 *
 * This is one component and not two similar ones. A squad looked up in a modal and the same
 * squad on the club's own screen are the same question, and two tables answering it
 * differently is how the keeper's saves end up on one screen and not the other.
 */
const ClubSquadTable: React.FC<ClubSquadTableProps> = ({
  squad,
  onToggle,
  selectedIds,
  elsewhereIds,
  describeAbsence,
  caption
}) => {
  const [sort, setSort] = useState<{ key: SortKey; ascending: boolean }>({
    key: 'position',
    ascending: true
  });

  const hasKeeper = useMemo(() => squad.some(player => player.position === 'GK'), [squad]);

  const columns = useMemo(
    () => [
      ...commonColumns,
      ...(hasKeeper ? keeperColumns : []),
      ...moneyColumns,
      ...tallyColumns
    ],
    [hasKeeper]
  );

  const sorted = useMemo(() => {
    const direction = sort.ascending ? 1 : -1;

    const valueOf = (player: SquadPlayerDto, key: SortKey): number | string => {
      if (key === 'position') return POSITION_RANK[player.position] ?? 9;
      if (key === 'name') return player.name;
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

  const toggleSort = (key: SortKey) => {
    setSort(current =>
      current.key === key
        ? { key, ascending: !current.ascending }
        : { key, ascending: true }
    );
  };

  if (squad.length === 0) {
    return <p className="league-empty">Nenhum jogador no elenco.</p>;
  }

  const table = (
    <div className="profile-history">
      <table className="history-table club-squad-table">
        <thead>
          <tr>
            {/* Every column is a door to an order. The arrow says which way, because a
                sorted column with no direction is half the information. */}
            {columns.map(column => (
              <th
                key={column.key}
                className={`${column.className ?? ''} sort-head ${sort.key === column.key ? 'sorted' : ''}`}
                onClick={() => toggleSort(column.key)}
                title={`Ordenar por ${column.label}`}
              >
                {column.label}
                {sort.key === column.key && (
                  <span className="sort-arrow">{sort.ascending ? '▲' : '▼'}</span>
                )}
              </th>
            ))}
          </tr>
        </thead>
<tbody>
            {sorted.map(player => (
              <tr
                key={player.id}
                className={[
                  'history-row',
                  player.injury !== 'None' ? 'injured' : '',
                  player.isAvailable ? '' : 'unavailable',
                  onToggle ? 'picking' : '',
                  selectedIds?.has(player.id) ? 'picked' : '',
                  elsewhereIds?.has(player.id) ? 'picked-elsewhere' : ''
                ]
                  .filter(Boolean)
                  .join(' ')}
                title={describeAbsence?.(player)}
                onClick={onToggle ? () => onToggle(player) : undefined}
              >
                <td>{positionLabel(player.position)}</td>
                {/* The name is the door, and only the name — unless the whole row is a pick,
                    in which case the row is the door and the name is part of it. */}
                <td className="squad-name">
                  {selectedIds?.has(player.id) && (
                    <span className="pick-mark" title="Selecionado">✓</span>
                  )}
                  {!selectedIds?.has(player.id) && elsewhereIds?.has(player.id) && (
                    <span className="pick-mark" title="Está no outro grupo">⇤</span>
                  )}
                   <PlayerName playerId={player.id}>{player.name}</PlayerName>
                   {player.retiring && (
                     <span className="retiring-mark" title="Aposentadoria declarada">🏁</span>
                   )}
                   {player.injury !== 'None' && (
                    <span className="injury-mark" title={`Lesionado: ${player.injury}`}>🩹</span>
                  )}
                </td>
                <td className="num">{player.age}</td>
                <td className={`num ${energyTextClass(player.energy)}`}>{player.energy}</td>
                <td className="num stars-col">{starsToString(player.stars)}</td>
                <td className={`num ${attrClass(player.speed)}`}>{player.speed}</td>
                <td className={`num ${attrClass(player.accuracy)}`}>{player.accuracy}</td>
                <td className={`num ${attrClass(player.dribbling)}`}>{player.dribbling}</td>
                <td className={`num ${attrClass(player.heading)}`}>{player.heading}</td>
                <td className={`num ${attrClass(player.strength)}`}>{player.strength}</td>
                {hasKeeper && (
                  <td className={`num ${player.position === 'GK' ? attrClass(player.goalkeeperPower) : ''}`}>{player.position === 'GK' ? player.goalkeeperPower : '—'}</td>
                )}
                {hasKeeper && (
                  <td className={`num ${player.position === 'GK' ? attrClass(player.reflexes) : ''}`}>{player.position === 'GK' ? player.reflexes : '—'}</td>
                )}
                {/* The money a manager negotiates with. The price carries the fine as a mark
                    rather than as a second number, so the two are read as one figure with a
                    reason attached and not as two prices to choose between. */}
                <td className="num money">{formatLimo(player.marketValue)}</td>
                <td
                  className={`num money ${player.isInLastSeason ? '' : 'under-contract'}`}
                  title={
                    player.isInLastSeason
                      ? 'Última temporada de contrato: sem multa'
                      : `${player.contractSeasons - player.seasonsLeft} de ${player.contractSeasons} temporadas de contrato ainda a correr: multa de 20%`
                  }
                >
                  {formatLimo(player.askingPrice)}
                  {!player.isInLastSeason && <span className="contract-fine">+20%</span>}
                </td>
                <td className="num money">{formatLimo(player.salary)}</td>
                <td className="num contract-cell">
                  {player.contractSeasons > 0
                    ? `${player.seasonsLeft}/${player.contractSeasons}`
                    : '—'}
                </td>
                {/* The season's own tallies, read from the same place the player profile
                    reads them, so a card and a profile cannot disagree. */}
                <td className="num accent">{player.goals}</td>
                <td className="num accent">{player.saves}</td>
                <td className="num">{player.yellowCards}</td>
                <td className="num">{player.redCards}</td>
              </tr>
            ))}
          </tbody>
      </table>
    </div>
  );

  return caption ? (
    <section className="squad-section">
      <h3 className="squad-section-title">{caption}</h3>
      {table}
    </section>
  ) : (
    table
  );
};

export default ClubSquadTable;
