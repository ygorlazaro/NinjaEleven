import React, { useMemo, useState } from 'react';
import type { MatchPlayerDto, TeamDto } from '@/types';
import { sortByPosition } from '@/services/formatters';
import PlayerCard from '@/components/Match/PlayerCard';

interface SubstitutionPanelProps {
  /**
   * The club the men belong to and the shirt it is playing in, so the two grids are two
   * columns of names in two colours rather than one list of men to sort through at the
   * interval.
   */
  team?: TeamDto;
  kitSide?: 'Home' | 'Away';
  lineup: MatchPlayerDto[];
  bench: MatchPlayerDto[];
  /** Substitutions already used by the club: the league allows five. */
  used: number;
  /** A player the manager already picked to come off, before opening this panel. */
  preselectOut?: string | null;
  limit?: number;
  busy?: boolean;
  onSubstitute: (playerOutId: string, playerInId: string) => void;
}

/**
 * Substitutions of the club the user manages. The manager picks who leaves and who
 * comes in; the backend validates the pair again and applies it, so an illegal
 * substitution is refused there and never here.
 */
const SubstitutionPanel: React.FC<SubstitutionPanelProps> = ({
  team,
  kitSide,
  lineup,
  bench,
  used,
  preselectOut = null,
  limit = 5,
  busy = false,
  onSubstitute,
}) => {
  const [playerOutId, setPlayerOutId] = useState<string | null>(preselectOut);
  const [playerInId, setPlayerInId] = useState<string | null>(null);

  const remaining = Math.max(0, limit - used);

  const outgoing = useMemo(
    () => lineup.find(player => player.playerId === playerOutId) || null,
    [lineup, playerOutId]
  );
  const incoming = useMemo(
    () => bench.find(player => player.playerId === playerInId) || null,
    [bench, playerInId]
  );

  const canConfirm = !!outgoing && !!incoming && !busy && remaining > 0;

  // Both lists are read the way a team sheet is: by position, and by name inside it.
  const onPitch = useMemo(() => sortByPosition(lineup), [lineup]);
  const onBench = useMemo(() => sortByPosition(bench), [bench]);

  // A goalkeeper cannot be replaced by an outfielder: the club would be left with no
  // one in goal, which the backend refuses.
  const wouldBreakGoalkeeper =
    !!outgoing &&
    outgoing.position === 'GK' &&
    !!incoming &&
    incoming.position !== 'GK' &&
    !incoming.emergencyGK;

  const goalkeeperWarning = wouldBreakGoalkeeper ? (
    <p className="squad-hint" style={{ color: 'var(--danger)' }}>
      {incoming?.name} não é goleiro e {outgoing?.name} é o único goleiro em campo.
    </p>
  ) : null;

  const confirm = () => {
    if (!canConfirm || !outgoing || !incoming) return;
    onSubstitute(outgoing.playerId, incoming.playerId);
    setPlayerOutId(null);
    setPlayerInId(null);
  };

  // The card a manager picks a change from is the card he reads the eleven in, and the card
  // the bench is drawn in. A substitution decided against a different design from the one he
  // just studied is a decision made on a stranger.
  const card = (player: MatchPlayerDto, kind: 'out' | 'in', disabled: boolean) => (
    <PlayerCard
      key={`${kind}-${player.playerId}`}
      player={player}
      team={team}
      side={kitSide}
      disabled={disabled}
      selected={(kind === 'out' ? playerOutId : playerInId) === player.playerId}
      onClick={() => {
        if (kind === 'out') {
          setPlayerOutId(playerOutId === player.playerId ? null : player.playerId);
        } else {
          setPlayerInId(playerInId === player.playerId ? null : player.playerId);
        }
      }}
      detail={
        <div className="player-stats">
          <span>Vel {player.speed}</span>
          <span>Fio {player.dribbling}</span>
          <span>For {player.strength}</span>
          {player.matchGoals > 0 && <span title="Gols na partida">⚽ {player.matchGoals}</span>}
          {player.matchSaves > 0 && <span title="Defesas na partida">🧤 {player.matchSaves}</span>}
          {player.matchOwnGoals > 0 && <span title="Gols contra na partida">🔴 {player.matchOwnGoals}</span>}
          {player.matchYellowCards > 0 && <span title="Cartões amarelos">🟨 {player.matchYellowCards}</span>}
          {player.redCard && <span className="injury-mark" title="Expulso">🟥</span>}
          {player.injuredOff && <span className="injury-mark" title="Saiu lesionado">🚑</span>}
          {player.emergencyGK && <span title="Assumiu a meta sem goleiro">🧤</span>}
          {player.subbedIn && <span title="Entrou em campo">↩</span>}
          {/* A substitute is spent: he came off, so the screen says so rather than offering
              a change the backend is going to refuse. */}
          {player.subbedOff && (
            <span className="injury-mark" title="Já saiu de campo — não pode voltar">⇤</span>
          )}
        </div>
      }
    />
  );

  return (
    <div className="sub-panel">
      <div className="sub-panel__head">
        <b>Substituições</b>
        <span className="selection-count">
          {used}/{limit} usadas
        </span>
      </div>

      {remaining === 0 ? (
        <p className="squad-hint">Você já usou todas as substituições.</p>
      ) : (
        <p className="squad-hint">
          {outgoing && incoming
            ? `${outgoing.name} sai, ${incoming.name} entra.`
            : 'Escolha quem sai e quem entra.'}
        </p>
      )}

      <div className="sub-columns">
        <div>
          <h4>Em campo</h4>
          <div className="player-grid">
            {onPitch.map(player =>
              card(player, 'out', player.redCard || remaining === 0 || busy)
            )}
          </div>
        </div>

        <div>
          <h4>Banco</h4>
          <div className="player-grid">
            {bench.length === 0 && <p className="squad-hint">Banco vazio.</p>}
            {/* A man who has already been taken off is spent, so the panel does not offer
                him: naming him again is the same change twice, and the backend refuses it. */}
            {onBench.map(player =>
              card(player, 'in', remaining === 0 || busy || player.subbedOff || player.redCard)
            )}
          </div>
        </div>
      </div>

      {goalkeeperWarning}

      <button className="primary sub-confirm" disabled={!canConfirm || wouldBreakGoalkeeper} onClick={confirm}>
        {busy ? 'Confirmando...' : 'Confirmar substituição'}
      </button>
    </div>
  );

};

export default SubstitutionPanel;
