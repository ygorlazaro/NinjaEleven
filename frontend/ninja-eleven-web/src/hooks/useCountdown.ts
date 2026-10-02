import { useEffect, useState } from 'react';

/**
 * How many seconds are left until a moment the backend named, or null when there is no
 * such moment.
 *
 * <para>
 * The deadline is the server's and the count here is only its picture: the screen redraws
 * once a second so the number moves under the manager's eye, and the engine closes the
 * window when it is over whether this hook agrees with it or not. That is why the return is
 * a number of seconds and never a flag — a client that decided for itself when the wait was
 * over would be running a second clock over a decision that is not its own.
 * </para>
 *
 * <para>
 * Zero is a real answer and not "soon": it is drawn as 0 for the second or two between the
 * deadline passing and the state that ends the window arriving, because that gap is the
 * backend finishing the decision, and a number that said "—" there would read as a number
 * nobody had.
 * </para>
 */
export function useCountdown(endsAt: string | null | undefined): number | null {
  const [seconds, setSeconds] = useState<number | null>(null);

  useEffect(() => {
    if (!endsAt) {
      setSeconds(null);
      return undefined;
    }

    const deadline = new Date(endsAt).getTime();

    if (Number.isNaN(deadline)) {
      setSeconds(null);
      return undefined;
    }

    const tick = () => setSeconds(Math.max(0, Math.ceil((deadline - Date.now()) / 1000)));

    tick();
    const timer = window.setInterval(tick, 1000);

    return () => window.clearInterval(timer);
  }, [endsAt]);

  return seconds;
}