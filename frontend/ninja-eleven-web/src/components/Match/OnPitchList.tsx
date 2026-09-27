import React, { useMemo } from 'react';
import type { Guid, MatchPlayerDto } from '@/types';
import { positionLabel, sortByPosition, starsToString } from '@/services/formatters';
import EnergyBar from '@/components/Match/EnergyBar';
import { PlayerName } from '@/components/Common/Names';

interface OnPitchListProps {
  /** Only the club the manager commands: the list is his team sheet, not both. */
  players: MatchPlayerDto[];
  /** False once the match is over, and while the five substitutions are spent. */
  onSelect?: (player: MatchPlayerDto) => void;
  /**
   * The player of this club who has the ball right now, when it is theirs. It is a
   * statement about where the ball is and not about how the player is performing, so the
   * card is lit rather than rated.
   */
  ballCarrierId?: Guid | null;
}

/**
 * The team sheet under the scoreboard, and the way into a substitution during the match.
 *
 * It shows the manager's own eleven, in the order a team sheet is read — goalkeepers,
 * defenders, midfielders, attackers, and by name inside each group — because a manager
 * looks for his own men, not for the other club. Every card carries what happened to its
 * player so far, and clicking one opens the substitution screen with that player already
 * picked to come off.
 */
const OnPitchList: React.FC<OnPitchListProps> = ({ players, onSelect, ballCarrierId }) => {
  const ordered = useMemo(() => sortByPosition(players), [players]);

  if (ordered.length === 0) {
    return <div className="league-empty">Nenhum jogador em campo.</div>;
  }

  return (
    <div className="on-pitch-list">
      {ordered.map(p => {
        const unavailable = p.redCard || p.injuredOff;
        const clickable = !!onSelect && !unavailable;
        const hasBall = !!ballCarrierId && p.playerId === ballCarrierId;

        return (
          <div
            key={p.playerId}
            className={[
              'on-pitch-card',
              unavailable ? 'unavailable' : '',
              p.emergencyGK ? 'emergency' : '',
              hasBall ? 'has-ball' : '',
              clickable ? 'clickable' : ''
            ]
              .filter(Boolean)
              .join(' ')}
            title={unavailable ? 'Não está mais em campo' : 'Clique para fazer uma substituição'}
            onClick={clickable ? () => onSelect?.(p) : undefined}
          >
            <span className="pc-pos">{positionLabel(p.position)}</span>
            <span className="pc-name">
              <PlayerName playerId={p.playerId}>{p.name}</PlayerName>
              <span className="pc-stars" style={{ color: 'var(--accent)', marginLeft: '6px' }}>{starsToString(p.stars)}</span>
            </span>
            <span className="on-pitch-icons">
              {/* The card under the scoreboard counts this match, not the season: a striker
                  with nine for the year who has not scored today has scored nothing here. */}
              {p.matchGoals > 0 && (
                <span className="icon-goal" title={`${p.matchGoals} gol(s) na partida`}>
                  ⚽{p.matchGoals > 1 ? p.matchGoals : ''}
                </span>
              )}
              {p.matchSaves > 0 && (
                <span className="icon-save" title={`${p.matchSaves} defesa(s) na partida`}>
                  🧤{p.matchSaves > 1 ? p.matchSaves : ''}
                </span>
              )}
              {p.matchOwnGoals > 0 && (
                <span className="icon-own-goal" title={`${p.matchOwnGoals} gol(s) contra na partida`}>
                  🔴{p.matchOwnGoals > 1 ? p.matchOwnGoals : ''}
                </span>
              )}
              {p.matchYellowCards > 0 && (
                <span className="icon-yellow" title={`${p.matchYellowCards} cartão(s) amarelo(s)`}>
                  🟨{p.matchYellowCards > 1 ? p.matchYellowCards : ''}
                </span>
              )}
              {p.redCard && <span className="icon-red" title="Expulso">🟥</span>}
              {p.injuredOff && <span className="icon-injury" title="Saiu lesionado">🚑</span>}
              {p.subbedIn && <span className="icon-sub" title="Entrou em campo">↩</span>}
              {p.emergencyGK && <span className="icon-sub" title="Assumiu a meta sem goleiro">🧤</span>}
            </span>
            <EnergyBar value={p.energy} compact />
          </div>
        );
      })}
    </div>
  );
};

export default OnPitchList;
