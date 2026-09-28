import React from 'react';
import type { TopScorerPrizeListDto } from '@/types';
import { formatLimo } from '@/services/limo';
import { ClubName, PlayerName } from '@/components/Common/Names';
import ClubCrest from '@/components/Club/ClubCrest';
import CupTrophy from '@/components/Cup/CupTrophy';

/**
 * The three places of a competition's artilharia, and what each is paid.
 *
 * **The rows are in one place because the money is in one place.** A division's panel and the
 * cup's legend are the same three lines said twice, and a copy is a rule that can drift: the
 * cup's legend used to carry the consolation ladder and the division's panel the artilharia,
 * which left the cup's three men to be drawn a second time, in a second file, with a second
 * copy of what a place is worth. The legend of a cup now says what the cup pays and what its
 * artilharia is worth in the same list, and both say it through these rows.
 *
 * **A row does not name the division behind the money**, because there is no division behind
 * it: a competition's artilharia is a share of that competition's own title, and a cup striker
 * is paid the cup's money whether his club plays in the first division or the third.
 *
 * **The place and the prize are two numbers.** Two men level on goals, games, cards and age are
 * both second, both take the second prize, and the third prize is paid to nobody. The row shows
 * both and says "empatado" rather than inventing an order between them.
 */
const TopScorerPrizeRows: React.FC<{ prize: TopScorerPrizeListDto }> = ({ prize }) => {
  if (prize.winners.length === 0) {
    return <p className="league-empty">Ninguém marcou gol ainda nesta competição.</p>;
  }

  return (
    <div className="top-scorer-prize__rows">
      {prize.winners.map(winner => (
        <div
          key={winner.playerId}
          className={`prize-legend__row top-scorer-prize__row ${
            winner.prizeSlot === 1 ? 'prize-legend__row--champion' : ''
          }`}
        >
          <span className="prize-legend__position top-scorer-prize__position">
            {winner.prizeSlot === 1 && <CupTrophy size={15} />}
            {shareLabel(winner.prizeSlot)}
            {winner.tiedWith > 0 && <span className="top-scorer-prize__tied">empatado</span>}
          </span>
          <span className="prize-legend__amount">
            {winner.amount == null ? '—' : formatLimo(winner.amount)}
          </span>
          <span className="top-scorer-prize__man">
            {winner.teamName && winner.teamPrimaryColor && winner.teamSecondaryColor && (
              <ClubCrest
                primary={winner.teamPrimaryColor}
                secondary={winner.teamSecondaryColor}
                name={winner.teamName}
                className="mini-crest"
              />
            )}
            <PlayerName playerId={winner.playerId}>
              <b>{winner.playerName}</b>
            </PlayerName>
            {winner.teamId && (
              <ClubName teamId={winner.teamId}>{winner.teamName}</ClubName>
            )}
            <span className="top-scorer-prize__figures">
              {winner.goals} gols em {winner.appearances} jogos
              {winner.cardPoints > 0 && ` • ${winner.cardPoints} de cartão`}
            </span>
          </span>
        </div>
      ))}
    </div>
  );
};

/** "1º artilheiro", "2º artilheiro", "3º artilheiro". */
export const shareLabel = (place: number) => `${place}º artilheiro`;

/** A share of something, said the way a manager would say it: "10%". */
export const formatRate = (rate: number) => `${Math.round(rate * 100)}%`;

export default TopScorerPrizeRows;
