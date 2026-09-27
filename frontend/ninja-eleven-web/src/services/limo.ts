/**
 * The Limo, the only money in the game.
 *
 * It is not the real and never was: a club's books are a fiction, and a fiction with a real
 * currency symbol on it borrows a scale the game does not have. So the money is the Limo and
 * it is written the same way everywhere — `L$ 1.000.000`, the way a Brazilian writes a
 * million — from one place, because a balance in one symbol and a movement in another is a
 * club whose own statement does not add up.
 *
 * The amounts are limos and, where the game divides money, cents of a limo. A gate is two
 * thirds of what a stand took and a wage is a hundredth of a price, so most lines carry
 * cents, and a ledger that rounded them away would print a total that does not add up to the
 * lines it is made of. Whole amounts print without decimals, because "L$ 1.000.000,00" is
 * noise.
 */
export const LIMO_SYMBOL = 'L$';

const amount = new Intl.NumberFormat('pt-BR', {
  minimumFractionDigits: 0,
  maximumFractionDigits: 2
});

/** What every club is given when a season's world is drawn. */
export const STARTING_BALANCE_LIMOS = 1_000_000;

/** An amount, in the game's money: `L$ 1.234.567`, or `L$ 3.943,33` when there are cents. */
export const formatLimo = (value: number): string => `${LIMO_SYMBOL} ${amount.format(value)}`;

/**
 * A movement, which is a sign as much as an amount: `+L$ 24.000` in, `-L$ 118.000` out. The
 * sign is written here rather than left to the column, so a ledger line says the same thing
 * whether it is read in a table, in a summary or aloud.
 */
export const formatSignedLimo = (value: number): string =>
  value > 0 ? `+${formatLimo(value)}` : value < 0 ? `-${formatLimo(-value)}` : formatLimo(0);
