import React, { useMemo, useState } from 'react';
import type { MatchPlayerDto } from '@/types';
import { energyClass, positionLabel, sortByPosition } from '@/services/formatters';

interface SubstitutionPanelProps {
  lineup: MatchPlayerDto[];
  bench: MatchPlayerDto[];
  /** Substitutions already used by the club: the league allows five. */
  used: number;
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
  lineup,
  bench,
  used,
  limit = 5,
  busy = false,
  onSubstitute,
}) => {
  const [playerOutId, setPlayerOutId] = useState<string | null>(null);
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

  const card = (player: MatchPlayerDto, kind: 'out' | 'in', disabled: boolean) => (
    <button
      key={`${kind}-${player.playerId}`}
      type="button"
      className={`player-card ${disabled ? 'disabled' : ''} ${
        (kind === 'out' ? playerOutId : playerInId) === player.playerId ? 'selected' : ''
      }`}
      disabled={disabled}
      onClick={() => {
        if (kind === 'out') {
          setPlayerOutId(playerOutId === player.playerId ? null : player.playerId);
        } else {
          setPlayerInId(playerInId === player.playerId ? null : player.playerId);
        }
      }}
    >
      <div className="player-top">
        <span className="player-pos">{positionLabel(player.position)}</span>
        <span className="player-card__name">{player.name}</span>
        <span className="player-energy">{player.energy}%</span>
      </div>
      <div className="player-stats">
        <span>Vel {player.speed}</span>
        <span>Fio {player.dribbling}</span>
        <span>For {player.strength}</span>
        {player.goals > 0 && <span>⚽ {player.goals}</span>}
        {player.matchYellowCards > 0 && <span>🟨 {player.matchYellowCards}</span>}
        {player.redCard && <span className="injury-mark">🟥</span>}
        {player.injuredOff && <span className="injury-mark" title="Saiu lesionado">🚑</span>}
        {player.emergencyGK && <span title="Assumiu a meta sem goleiro">🧤</span>}
        {player.subbedIn && <span>↩</span>}
      </div>
      <div className={`pc-energy-fill ${energyClass(player.energy)}`} />
    </button>
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
            {onBench.map(player => card(player, 'in', remaining === 0 || busy))}
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
