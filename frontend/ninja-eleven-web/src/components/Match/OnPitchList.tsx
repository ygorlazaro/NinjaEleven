import React from 'react';
import type { MatchPlayerDto, TeamDto } from '@/types';
import type { KitSide } from '@/types';
import PlayerCard from './PlayerCard';

/**
 * The eleven on the pitch, in the order a manager reads them in: by line, and by where they
 * play in that line.
 *
 * <para>
 * The card is the one every list of players on this screen uses, and it is not a summary of it
 * — it is the same thing. A manager deciding a substitution reads the eleven under the scoreboard
 * and then reads the same eleven in the panel he is about to pick from, and two designs would
 * mean he is learning the squad twice.
 * </para>
 */

interface OnPitchListProps {
  players: MatchPlayerDto[];
  team?: TeamDto | null;
  side?: KitSide;
  onSelect?: (player: MatchPlayerDto) => void;
  /** The man with the ball, which the card marks so the eye finds it in a column. */
  ballCarrierId?: string | null;
}

const OnPitchList: React.FC<OnPitchListProps> = ({
  players,
  team,
  side = 'Home',
  onSelect,
  ballCarrierId,
}) => {
  if (players.length === 0) {
    return <p className="squad-empty">Ninguém em campo.</p>;
  }

  return (
    <div className="on-pitch-list">
      {players.map(p => {
        const isBallCarrier = !!ballCarrierId && ballCarrierId === p.playerId;
        // A man who cannot play is drawn as he is rather than left out: an eleven is a fact
        // about the match, and a card that is missing says the match is missing a player when
        // it has one who cannot touch the ball.
        const unavailable = p.injuredOff || p.redCard;

        return (
          <PlayerCard
            key={p.playerId}
            player={p}
            team={team}
            side={side}
            className={`on-pitch-card${isBallCarrier ? ' has-ball' : ''}${
              unavailable ? ' unavailable' : ''
            }`}
            title={
              isBallCarrier
                ? 'Com a bola'
                : p.injuredOff
                  ? 'Saiu lesionado'
                  : p.redCard
                    ? 'Expulso'
                    : undefined
            }
            onClick={onSelect ? () => onSelect(p) : undefined}
            badges={
              <span className="player-tile__icons">
                {/* Everything here happened in this match. The season's numbers belong on a
                    profile, not beside a man who has not scored today. */}
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
                  <span className="icon-own-goal" title={`${p.matchOwnGoals} gol(s) contra`}>
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
                {p.emergencyGK && (
                  <span className="icon-sub" title="Assumiu a meta sem goleiro">🧤</span>
                )}
              </span>
            }
          />
        );
      })}
    </div>
  );
};

export default OnPitchList;
