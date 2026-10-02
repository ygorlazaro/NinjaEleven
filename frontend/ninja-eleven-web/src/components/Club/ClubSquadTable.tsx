import React, { useMemo, useState } from 'react';
import type { SquadPlayerDto, TeamDto } from '@/types';
import { positionLabel, starsToString, attributeToneClass, energyTextClass } from '@/services/formatters';
import { formatLimo } from '@/services/limo';
import { PlayerName } from '@/components/Common/Names';
import PlayerStatusMarks from '@/components/Common/PlayerStatusMarks';
import KitChip from '@/components/Club/KitChip';
import ShirtNumberCell from '@/components/Club/ShirtNumberCell';

/**
 * The columns a manager sorts a squad by. Each one is a number the engine decided, so
 * sorting is presentation: the value is never recomputed here, only the order of the rows
 * that already arrived.
 */
type SortKey =
  | 'shirtNumber' | 'position' | 'name' | 'age' | 'energy' | 'speed' | 'accuracy'
  | 'dribbling' | 'heading' | 'strength' | 'goalkeeperPower' | 'reflexes' | 'goals' | 'saves'
  | 'yellowCards' | 'redCards' | 'stars'
  | 'marketValue' | 'askingPrice' | 'salary' | 'seasonsLeft';

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
  // The shirt leads the row, the way it leads the fixture list a manager reads on a Saturday.
  // It is the one number about a man that is a decision rather than a measurement.
  { key: 'shirtNumber', label: 'Nº', className: 'num shirt-col' },
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
   * The club these men play for, so the shirt sits beside the name.
   *
   * A squad table is a column of names in one colour, and the colour is half of what says
   * whose club this is — a manager reading another club's squad is reading names he will not
   * see on Saturday. Left out on a screen that has no club to show, the column is simply names.
   */
  team?: TeamDto | null;

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

  /**
   * Ends the man's contract, paying the settlement. Left out, the column is not there: a squad
   * screen belongs to somebody else's club as often as to the manager's own, and a table that
   * offered to fire half a league would be offering it to a manager who cannot.
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
   * Declares the man will retire at the end of the season, or takes the declaration back. The
   * news belongs to the club that owns him, so the same rule applies as for a release.
   */

  /**
   * Puts a man in a shirt. Left out, the number is a number and not a control: the squad
   * screen belongs to somebody else's club as often as to the manager's own, and a table
   * that offered to renumber the whole league would be offering it to a reader who cannot.
   */
  onEditShirtNumber?: (player: SquadPlayerDto, shirtNumber: number) => void;

  /**
   * What the server said about the last number asked for, by player id, so the row that was
   * refused is the row that says so. A refusal shown in the corner of the screen is a refusal
   * about somebody else's problem.
   */
  shirtRefusals?: Readonly<Record<string, string>>;

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
            {/* The club's own decisions are not a column a manager sorts by, so the header
                says what they are and nothing more. The column is drawn when either decision
                exists: the renewal is the manager's as much as the release, and nesting it
                inside the release's guard made it disappear with it. */}
            {(onRelease || onRenewContract) && <th className="actions-col">Ações</th>}
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
                <td className="num shirt-col">
                  <ShirtNumberCell
                    value={player.shirtNumber ?? null}
                    playerName={player.name}
                    onChange={
                      onEditShirtNumber
                        ? number => onEditShirtNumber(player, number)
                        : undefined
                    }
                    refusal={shirtRefusals?.[player.id] ?? null}
                  />
                </td>
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
                  <KitChip team={team} />
                  <PlayerName playerId={player.id}>{player.name}</PlayerName>
                  <PlayerStatusMarks
                    retiring={player.retiring}
                    injury={player.injury}
                    injuryMatchesRemaining={player.injuryMatchesRemaining}
                    suspensionMatches={player.suspensionMatches}
                  />
                </td>
                <td className="num">{player.age}</td>
                <td className={`num ${energyTextClass(player.energy)}`}>{player.energy}</td>
                <td className="num stars-col">{starsToString(player.stars)}</td>
                <td className={`num ${attributeToneClass(player.speed)}`}>{player.speed}</td>
                <td className={`num ${attributeToneClass(player.accuracy)}`}>{player.accuracy}</td>
                <td className={`num ${attributeToneClass(player.dribbling)}`}>{player.dribbling}</td>
                <td className={`num ${attributeToneClass(player.heading)}`}>{player.heading}</td>
                <td className={`num ${attributeToneClass(player.strength)}`}>{player.strength}</td>
                {hasKeeper && (
                  <td className={`num ${player.position === 'GK' ? attributeToneClass(player.goalkeeperPower) : ''}`}>{player.position === 'GK' ? player.goalkeeperPower : '—'}</td>
                )}
                {hasKeeper && (
                  <td className={`num ${player.position === 'GK' ? attributeToneClass(player.reflexes) : ''}`}>{player.position === 'GK' ? player.reflexes : '—'}</td>
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
                      : `${player.contractSeasons - player.seasonsLeft} de ${player.contractSeasons} temporadas de contrato ainda a correr. Rescindir agora custa ${formatLimo(player.releaseCost)}.`
                  }
                >
                  {formatLimo(player.askingPrice)}
                  {/* What letting him go costs, quoted by the rule that charges it. It used to
                      read "+20%", which is not a settlement anywhere in the game. */}
                  {!player.isInLastSeason && (
                    <span className="contract-fine" title="Custo de rescindir o contrato agora">
                      {formatLimo(player.releaseCost)}
                    </span>
                  )}
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
                {(onRelease || onRenewContract) && (
                  <td className="actions-col" onClick={event => event.stopPropagation()}>
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
                        makes, so the row that has no button is the row the rule has no answer
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
                  </td>
                )}
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
