import React from 'react';
import type { Position, SquadPlayerDto } from '@/types';

/**
 * What a manager is looking for when he filters a squad.
 *
 * Every field here is an empty string or a set of positions, and "no filter" is the state
 * every one of them starts and returns to. A filter panel that could not be cleared is a
 * panel a manager works around by reloading the page, and a reload is not a way of saying
 * "I did not mean that".
 */
export interface SquadFilter {
  /** The lines to show. Empty is every line: a manager filtering by position asks for one, not for none. */
  positions: Position[];

  /** The age band, inclusive, as the two ends a manager typed. */
  ageMin: string;
  ageMax: string;

  /** What he is worth, and what the club owes him — two different money questions. */
  valueMin: string;
  valueMax: string;
  salaryMin: string;
  salaryMax: string;

  /**
   * The attribute a minimum is set on, and the minimum itself.
   *
   * It is one attribute at a time rather than eight boxes, because "I want somebody who can
   * head it" is a question about one number, and eight boxes side by side is a form nobody
   * fills in twice.
   */
  attribute: string;
  attributeMin: string;

  /** Only the men available for the next match. */
  onlyAvailable: boolean;
}

export const emptyFilter: SquadFilter = {
  positions: [],
  ageMin: '',
  ageMax: '',
  valueMin: '',
  valueMax: '',
  salaryMin: '',
  salaryMax: '',
  attribute: 'speed',
  attributeMin: '',
  onlyAvailable: false
};

const LINES: { position: Position; label: string }[] = [
  { position: 'GK', label: 'Goleiros' },
  { position: 'DEF', label: 'Defesas' },
  { position: 'MID', label: 'Meios' },
  { position: 'ATT', label: 'Atacantes' }
];

/**
 * The attributes a minimum can be set on, and the word each is read as.
 *
 * The words are the ones the table's own column headers use, so a manager filtering by "defesa"
 * is filtering by the column he can see in the rows underneath. An attribute whose name only
 * exists in a dropdown is an attribute a manager cannot find in the table he is reading.
 */
const ATTRIBUTES: { key: keyof SquadPlayerDto; label: string }[] = [
  { key: 'speed', label: 'Velocidade' },
  { key: 'accuracy', label: 'Finalização' },
  { key: 'dribbling', label: 'Drible' },
  { key: 'heading', label: 'Cabeceio' },
  { key: 'strength', label: 'Força' },
  { key: 'stamina', label: 'Fôlego' },
  { key: 'potential', label: 'Potencial' },
  { key: 'goalkeeperPower', label: 'Gol (goleiro)' },
  { key: 'reflexes', label: 'Reflexos' }
];

interface SquadFilterBarProps {
  filter: SquadFilter;
  onChange: (filter: SquadFilter) => void;

  /** How many men the filter is leaving, said next to the twenty-three it started from. */
  matched: number;
  total: number;
}

const SquadFilterBar: React.FC<SquadFilterBarProps> = ({ filter, onChange, matched, total }) => {
  const set = <K extends keyof SquadFilter>(key: K, value: SquadFilter[K]) =>
    onChange({ ...filter, [key]: value });

  const togglePosition = (position: Position) =>
    set(
      'positions',
      filter.positions.includes(position)
        ? filter.positions.filter(p => p !== position)
        : [...filter.positions, position]
    );

  const filtering =
    filter.positions.length > 0 ||
    filter.ageMin !== '' || filter.ageMax !== '' ||
    filter.valueMin !== '' || filter.valueMax !== '' ||
    filter.salaryMin !== '' || filter.salaryMax !== '' ||
    filter.attributeMin !== '' ||
    filter.onlyAvailable;

  return (
    <section className="squad-filters">
      <div className="squad-filters__row">
        <span className="squad-filters__label">Linha</span>
        {LINES.map(line => (
          <button
            key={line.position}
            type="button"
            className={`filter-chip ${filter.positions.includes(line.position) ? 'active' : ''}`}
            onClick={() => togglePosition(line.position)}
          >
            {line.label}
          </button>
        ))}

        <label className="filter-check">
          <input
            type="checkbox"
            checked={filter.onlyAvailable}
            onChange={event => set('onlyAvailable', event.target.checked)}
          />
          <span>Só os disponíveis</span>
        </label>
      </div>

      <div className="squad-filters__row">
        <label className="filter-range">
          <span>Idade</span>
          <input
            type="number"
            min={16}
            max={45}
            placeholder="de"
            value={filter.ageMin}
            onChange={event => set('ageMin', event.target.value)}
          />
          <input
            type="number"
            min={16}
            max={45}
            placeholder="até"
            value={filter.ageMax}
            onChange={event => set('ageMax', event.target.value)}
          />
        </label>

        <label className="filter-range">
          <span>Valor L$</span>
          <input
            type="number"
            min={0}
            placeholder="de"
            value={filter.valueMin}
            onChange={event => set('valueMin', event.target.value)}
          />
          <input
            type="number"
            min={0}
            placeholder="até"
            value={filter.valueMax}
            onChange={event => set('valueMax', event.target.value)}
          />
        </label>

        <label className="filter-range">
          <span>Salário L$</span>
          <input
            type="number"
            min={0}
            placeholder="de"
            value={filter.salaryMin}
            onChange={event => set('salaryMin', event.target.value)}
          />
          <input
            type="number"
            min={0}
            placeholder="até"
            value={filter.salaryMax}
            onChange={event => set('salaryMax', event.target.value)}
          />
        </label>

        <label className="filter-range filter-range--attribute">
          <span>Atributo</span>
          <select
            value={filter.attribute}
            onChange={event => set('attribute', event.target.value)}
          >
            {ATTRIBUTES.map(attribute => (
              <option key={attribute.key} value={attribute.key}>
                {attribute.label}
              </option>
            ))}
          </select>
          <input
            type="number"
            min={1}
            max={100}
            placeholder="mín."
            value={filter.attributeMin}
            onChange={event => set('attributeMin', event.target.value)}
          />
        </label>
      </div>

      <div className="squad-filters__foot">
        <span className="squad-filters__count">
          {matched === total
            ? `${total} jogadores`
            : `${matched} de ${total} jogadores`}
        </span>
        {filtering && (
          <button type="button" className="filter-clear" onClick={() => onChange({ ...emptyFilter })}>
            Limpar filtros
          </button>
        )}
      </div>
    </section>
  );
};

export default SquadFilterBar;

/**
 * The players a filter leaves, and the arithmetic of a filter.
 *
 * It is here and not in the screen because the screen has one job and this one has another:
 * the screen asks the table for what it wants to show, and this decides what that is. A
 * filter that lived in the table would also run on the lineup screen, where a manager
 * choosing eleven is not narrowing a list — he is picking men.
 */
export function applySquadFilter(squad: SquadPlayerDto[], filter: SquadFilter): SquadPlayerDto[] {
  const within = (value: number, min: string, max: string) =>
    (min === '' || value >= Number(min)) && (max === '' || value <= Number(max));

  return squad.filter(player => {
    if (filter.positions.length > 0 && !filter.positions.includes(player.position)) {
      return false;
    }

    if (filter.onlyAvailable && !player.isAvailable) {
      return false;
    }

    if (!within(player.age, filter.ageMin, filter.ageMax)) {
      return false;
    }

    if (!within(player.marketValue, filter.valueMin, filter.valueMax)) {
      return false;
    }

    if (!within(player.salary, filter.salaryMin, filter.salaryMax)) {
      return false;
    }

    if (filter.attributeMin !== '') {
      const attribute = player[filter.attribute as keyof SquadPlayerDto];

      // A minimum on a keeper's reflexes is a question about keepers; asked of a striker the
      // number is zero and the man is silently dropped, which is a filter that hides men
      // because of where they stand rather than because of what they can do.
      if (typeof attribute !== 'number' || attribute < Number(filter.attributeMin)) {
        return false;
      }
    }

    return true;
  });
}
