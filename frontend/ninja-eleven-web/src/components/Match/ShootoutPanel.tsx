import React, { useEffect, useMemo, useState } from 'react';
import { positionLabel } from '@/services/formatters';
import { PlayerName } from '@/components/Common/Names';
import type { MatchPlayerDto, ShootoutDto } from '@/types';

interface ShootoutPanelProps {
  shootout: ShootoutDto;
  /** The manager's club, or null when nobody is watching this match. */
  userTeamId: string | null;
  /** Short names for the strip, so the panel does not go and ask for them. */
  homeName: string;
  awayName: string;
  /** The eleven of each side, used to put a name on an id the order carries. */
  homePlayers: MatchPlayerDto[];
  awayPlayers: MatchPlayerDto[];
  busy?: boolean;
  onConfirmOrder: (takerIds: string[]) => void;
  error?: string | null;
}

/**
 * How many men a side may send to the spot: the number of kicks it is given, and never
 * more. A club that finished a man down has ten men who may take and still takes five of
 * them, and a screen that asked for eleven would be asking for a taker who does not exist.
 * The engine refuses it either way — this is about not asking.
 */
const KICKS_EACH = 5;

/**
 * A penalty shootout, as a manager stands at it.
 *
 * Two things are on this screen and they are not the same thing. The kicks are a fact the
 * engine has already decided: who took them, in what order, and whether each one went in.
 * The order of the manager's own club is not a fact yet — it is the one decision the
 * shootout is waiting for, and the clock is held for it exactly as it is held for the taker
 * of a penalty in open play.
 *
 * So the panel is written the way the moment is: the kicks are read, and the pick is made.
 * The men offered are the engine's pool — the eleven that finished the match, minus the
 * ones the Laws leave out — and the chance shown against each is the number the engine will
 * roll, so a manager ordering his five is deciding with the same information his takers have.
 */
const ShootoutPanel: React.FC<ShootoutPanelProps> = ({
  shootout,
  userTeamId,
  homeName,
  awayName,
  homePlayers,
  awayPlayers,
  busy = false,
  onConfirmOrder,
  error,
}) => {
  const namesOf = useMemo(() => {
    const index = new Map<string, MatchPlayerDto>();
    for (const player of [...homePlayers, ...awayPlayers]) {
      index.set(player.playerId, player);
    }
    return index;
  }, [homePlayers, awayPlayers]);

  const managerIsHome = userTeamId != null && userTeamId === shootout.homeTeamId;
  const managerIsAway = userTeamId != null && userTeamId === shootout.awayTeamId;
  const managerIsInIt = managerIsHome || managerIsAway;

  const candidates = shootout.candidates;
  const required = Math.min(KICKS_EACH, candidates.length);
  const [order, setOrder] = useState<string[]>([]);

  // A pool that changes must not leave a stale pick on screen: the manager is naming men
  // out of the list in front of him, not out of the one he was looking at a moment ago.
  useEffect(() => {
    setOrder([]);
  }, [candidates]);

  const toggle = (playerId: string) => {
    setOrder(current =>
      current.includes(playerId)
        ? current.filter(id => id !== playerId)
        : current.length >= required
          ? // The list is full: the man he clicks goes in and the first he named comes out,
            // so a manager who changes his mind is not told the panel is full.
            [...current.slice(1), playerId]
          : [...current, playerId]
    );
  };

  const canConfirm = managerIsInIt && shootout.awaitingOrder && order.length === required && !busy;

  const name = (playerId: string) => namesOf.get(playerId)?.name ?? '';
  const label = (teamId: string) => (teamId === shootout.homeTeamId ? homeName : awayName);

  /**
   * The kicks as a manager reads them: in rounds, one from each side, the side that goes
   * first on the left. After the five the rounds keep coming, and the panel says so rather
   * than pretending the shootout ended at five.
   */
  const rounds = useMemo(() => {
    const byRound: ({ home?: ShootoutDto['kicks'][number]; away?: ShootoutDto['kicks'][number] })[] = [];
    const first = shootout.homeTakesFirst ? 'home' : 'away';

    shootout.kicks.forEach(kick => {
      const side = kick.teamId === shootout.homeTeamId ? 'home' : 'away';
      const taken = shootout.kicks.filter(k =>
        (k.teamId === shootout.homeTeamId ? 'home' : 'away') === side
      ).length;
      const index = side === first ? taken - 1 : taken;

      const round = byRound[index] ?? (byRound[index] = {});
      round[side] = kick;
    });

    return byRound;
  }, [shootout.kicks, shootout.homeTeamId, shootout.homeTakesFirst]);

  const kickMarker = (kick: ShootoutDto['kicks'][number] | undefined, teamId: string) => {
    if (!kick) {
      return <span className="shootout-cell shootout-cell--empty" aria-label="ainda não cobrou" />;
    }

    const isNext =
      !shootout.isComplete &&
      shootout.nextTeamId === teamId &&
      shootout.nextTakerId === kick.takerId;

    return (
      <span
        className={`shootout-cell ${kick.scored ? 'shootout-cell--goal' : 'shootout-cell--miss'}`}
        title={`${name(kick.takerId)} — ${kick.scored ? 'convertido' : 'perdeu'}`}
      >
        <PlayerName playerId={kick.takerId}>{name(kick.takerId)}</PlayerName>
        <b aria-hidden>{kick.scored ? '⚽' : '✕'}</b>
        {isNext && <i className="shootout-next" title="Próximo a cobrar" />}
      </span>
    );
  };

  const managerOrder = managerIsHome ? shootout.homeTakers : shootout.awayTakers;
  const opponentOrder = managerIsHome ? shootout.awayTakers : shootout.homeTakers;
  const opponentTeamId = managerIsHome ? shootout.awayTeamId : shootout.homeTeamId;

  const orderList = (ids: string[]) => (
    <ol>
      {ids.map(id => (
        <li key={id}>
          <PlayerName playerId={id}>{name(id)}</PlayerName>
        </li>
      ))}
      {ids.length === 0 && <li className="shootout__empty">—</li>}
    </ol>
  );

  return (
    <section className="shootout" aria-label="Disputa de pênaltis">
      <header className="shootout__head">
        <h3>⚽ Disputa de pênaltis</h3>
        {shootout.isSuddenDeath && <span className="shootout__sudden">Morte súbita</span>}
      </header>

      <div className="shootout__score">
        <span className="shootout__side">{homeName}</span>
        <b>
          {shootout.homeGoals} <i>×</i> {shootout.awayGoals}
        </b>
        <span className="shootout__side shootout__side--away">{awayName}</span>
      </div>

      <div className="shootout__taken">
        {rounds.map((round, index) => (
          <div className="shootout__round" key={index}>
            <span className="shootout__round-number">{index < KICKS_EACH ? `${index + 1}ª` : 'S/S'}</span>
            {/* The side the coin sent first is on the left in every round, so the two
                columns of a round are the same two clubs all the way down. */}
            {shootout.homeTakesFirst ? (
              <>
                {kickMarker(round.home, shootout.homeTeamId)}
                {kickMarker(round.away, shootout.awayTeamId)}
              </>
            ) : (
              <>
                {kickMarker(round.away, shootout.awayTeamId)}
                {kickMarker(round.home, shootout.homeTeamId)}
              </>
            )}
          </div>
        ))}
        {rounds.length === 0 && (
          <p className="shootout__hint">
            Nenhuma cobrança ainda — {shootout.homeTakesFirst ? homeName : awayName} vai primeiro.
          </p>
        )}
      </div>

      <p className="shootout__hint">
        {shootout.isComplete
          ? `${label(shootout.winnerTeamId ?? '')} vence na disputa.`
          : shootout.nextTeamId
            ? `Agora é a vez de ${label(shootout.nextTeamId)}.`
            : ''}
      </p>

      <div className="shootout__orders">
        {/* The other club's order is shown, not edited: the engine chose it and the Laws do
            not ask a manager twice. */}
        {managerIsInIt && (
          <div className="shootout__order shootout__order--opponent">
            <h4>Ordem de {label(opponentTeamId)}</h4>
            {orderList(opponentOrder)}
          </div>
        )}

        {managerIsInIt ? (
          <div className="shootout__order">
            <h4>
              Sua ordem
              {shootout.awaitingOrder && (
                <span className="selection-count">
                  {order.length}/{required}
                </span>
              )}
            </h4>

            {shootout.awaitingOrder ? (
              <>
                <p className="shootout__hint">
                  Escolha {required} homens, na ordem em que vão cobrar. O relógio espera.
                </p>
                <div className="shootout__candidates">
                  {candidates.map(player => {
                    const place = order.indexOf(player.playerId);
                    // Not a <button>: the name inside it is one — the door to the player's
                    // profile — and a browser will not honour a button nested in a button.
                    // The role, the tab stop and the key handling are what make the card
                    // pressable, and the name stops the click so reading a player never
                    // changes the order of the five kicks.
                    return (
                      <span
                        key={player.playerId}
                        role="button"
                        tabIndex={busy ? -1 : 0}
                        aria-disabled={busy}
                        className={`shootout__candidate ${place >= 0 ? 'selected' : ''}`}
                        onClick={() => !busy && toggle(player.playerId)}
                        onKeyDown={event => {
                          if (busy) return;
                          if (event.key !== 'Enter' && event.key !== ' ') return;
                          event.preventDefault();
                          toggle(player.playerId);
                        }}
                      >
                        <span className="shootout__place">{place >= 0 ? place + 1 : ''}</span>
                        <span className="shootout__candidate-name">
                          <PlayerName playerId={player.playerId}>{player.name}</PlayerName>
                        </span>
                        <span className="shootout__candidate-meta">
                          {positionLabel(player.position)}
                          {player.penaltyChance != null && ` • ${Math.round(player.penaltyChance * 100)}%`}
                        </span>
                      </span>
                    );
                  })}
                  {candidates.length === 0 && (
                    <span className="shootout__empty">Nenhum jogador elegível.</span>
                  )}
                </div>
                {error && (
                  <p className="shootout__error" role="alert">
                    {error}
                  </p>
                )}
                <button
                  type="button"
                  className="primary"
                  disabled={!canConfirm}
                  onClick={() => onConfirmOrder(order)}
                >
                  {busy ? 'Confirmando...' : 'Confirmar ordem'}
                </button>
              </>
            ) : (
              orderList(managerOrder)
            )}
          </div>
        ) : (
          <div className="shootout__order shootout__order--opponent">
            <h4>Ordens</h4>
            {orderList(shootout.homeTakers)}
            {orderList(shootout.awayTakers)}
          </div>
        )}
      </div>
    </section>
  );
};

export default ShootoutPanel;
