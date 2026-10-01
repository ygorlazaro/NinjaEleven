import React, { useEffect, useRef, useState } from 'react';

/**
 * The number a man wears, and the one decision a manager takes about it.
 *
 * The cell is a number until it is clicked and an input until it is not, because a squad
 * list of twenty-three editable fields is a spreadsheet, and the man who most wants to change
 * a number is not the one reading a column of twenty-three of them.
 */
interface ShirtNumberCellProps {
  /** The number he wears, or null when he wears none. */
  value: number | null;

  /**
   * Puts a man in a shirt. Left out, the number is not a target: a squad screen belongs to
   * somebody else's club as often as to the manager's own, and a table that offered to
   * renumber half a league would be offering it to a manager who cannot.
   */
  onChange?: (shirtNumber: number) => void;

  /** The man's name, said in the label so the input is not an unnamed box on a row of names. */
  playerName: string;

  /**
   * What the server said about the last number this cell asked for, or null when it has not
   * been refused. It arrives from the caller because the refusal is caught by the screen that
   * made the request, and the row that has to say so is this one.
   */
  refusal?: string | null;
}

const LOWEST = 1;
const HIGHEST = 99;

/**
 * A shirt with the number on it.
 *
 * It is drawn once and is a button on the manager's own club and a fact on anybody else's,
 * rather than a button that is disabled where it cannot be used: a disabled control still
 * looks like something to press, and a squad table full of things that look pressable and do
 * nothing is a table a manager learns to distrust.
 *
 * The number is drawn rather than typed because a bare number at the head of a row of numbers
 * is read as a rating, and this is the one number on the row that is not one.
 */
interface ShirtProps {
  value: number | null;
  playerName: string;
  onClick?: () => void;
}

const Shirt: React.FC<ShirtProps> = ({ value, playerName, onClick }) => {
  const shape = (
    <>
      <svg viewBox="0 0 24 24" className="shirt-number__icon" aria-hidden="true">
        <path
          fill="currentColor"
          d="M9 2 4 4v5c0 1.4.8 2.6 2 3.2V22h12v-9.8c1.2-.6 2-1.8 2-3.2V4l-5-2-1.2 1.6a4.4 4.4 0 0 1-5.6 0L9 2Z"
        />
      </svg>
      <span className="shirt-number__value">{value ?? '—'}</span>
    </>
  );

  const title =
    value === null
      ? `${playerName} ainda não tem número`
      : `${playerName} veste o número ${value}`;

  if (!onClick) {
    return <span className={`shirt-number shirt-number--static ${value === null ? 'shirt-number--empty' : ''}`} title={title}>{shape}</span>;
  }

  return (
    <button
      type="button"
      className={`shirt-number ${value === null ? 'shirt-number--empty' : ''}`}
      title={`${title} — clique para alterar`}
      onClick={onClick}
    >
      {shape}
    </button>
  );
};

const ShirtNumberCell: React.FC<ShirtNumberCellProps> = ({
  value,
  onChange,
  playerName,
  refusal
}) => {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState('');
  const input = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (editing) {
      input.current?.focus();
      input.current?.select();
    }
  }, [editing]);

  const open = () => {
    if (!onChange) return;
    setDraft(value === null ? '' : String(value));
    setEditing(true);
  };

  const close = () => {
    setEditing(false);
    setDraft('');
  };

  const commit = () => {
    if (!onChange) return;

    const asked = Number.parseInt(draft, 10);

    // An empty cell is not a number between one and ninety-nine, and a cell that silently
    // refused to close would leave a manager wondering whether the change went through.
    if (!Number.isInteger(asked) || asked < LOWEST || asked > HIGHEST) {
      close();
      return;
    }

    if (asked === value) {
      close();
      return;
    }

    close();
    onChange(asked);
  };

  return (
    <span className="shirt-cell">
      {editing ? (
        <input
          ref={input}
          className="shirt-input"
          type="number"
          min={LOWEST}
          max={HIGHEST}
          value={draft}
          onChange={event => setDraft(event.target.value)}
          onKeyDown={event => {
            if (event.key === 'Enter') commit();
            if (event.key === 'Escape') close();
          }}
          onBlur={commit}
          aria-label={`Número de camisa de ${playerName}`}
        />
      ) : (
        <Shirt value={value} playerName={playerName} onClick={onChange ? open : undefined} />
      )}
      {refusal && (
        <span className="shirt-refusal" role="alert">
          {refusal}
        </span>
      )}
    </span>
  );
};

export default ShirtNumberCell;
