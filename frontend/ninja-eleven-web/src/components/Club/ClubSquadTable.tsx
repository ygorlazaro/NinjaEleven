import React, { useMemo, useState } from 'react';
import type { SquadPlayerDto } from '@/types';
import { positionLabel } from '@/services/formatters';
import { PlayerName } from '@/components/Common/Names';

/**
 * The columns a manager sorts a squad by. Each one is a number the engine decided, so
 * sorting is presentation: the value is never recomputed here, only the order of the rows
 * that already arrived.
 */
type SortKey =
  | 'position' | 'name' | 'age' | 'speed' | 'accuracy' | 'dribbling' | 'heading'
  | 'strength' | 'goalkeeperPower' | 'reflexes' | 'goals' | 'saves'
  | 'yellowCards' | 'redCards' | 'energy';

const POSITION_RANK: Record<string, number> = { GK: 0, DEF: 1, MID: 2, ATT: 3 };

interface Column {
  key: SortKey;
  label: string;
  className?: string;
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

const tallyColumns: Column[] = [
  { key: 'goals', label: 'Gols', className: 'num accent-col' },
  { key: 'saves', label: 'Defs', className: 'num accent-col' },
  { key: 'yellowCards', label: 'Ama', className: 'num' },
  { key: 'redCards', label: 'Verm', className: 'num' },
  { key: 'energy', label: 'Energia', className: 'num' }
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
                {player.injury !== 'None' && (
                  <span className="injury-mark" title={`Lesionado: ${player.injury}`}>🩹</span>
                )}
              </td>
              <td className="num">{player.age}</td>
              <td className="num">{player.speed}</td>
              <td className="num">{player.accuracy}</td>
              <td className="num">{player.dribbling}</td>
              <td className="num">{player.heading}</td>
              <td className="num">{player.strength}</td>
              {hasKeeper && (
                <td className="num">{player.position === 'GK' ? player.goalkeeperPower : '—'}</td>
              )}
              {hasKeeper && (
                <td className="num">{player.position === 'GK' ? player.reflexes : '—'}</td>
              )}
              {/* The season's own tallies, read from the same place the player profile
                  reads them, so a card and a profile cannot disagree. */}
              <td className="num accent">{player.goals}</td>
              <td className="num accent">{player.saves}</td>
              <td className="num">{player.yellowCards}</td>
              <td className="num">{player.redCards}</td>
              <td className="num">
                {player.energy}
                {player.suspensionMatches > 0 && (
                  <span className="history-note" title="Suspenso"> ⛔{player.suspensionMatches}</span>
                )}
              </td>
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
