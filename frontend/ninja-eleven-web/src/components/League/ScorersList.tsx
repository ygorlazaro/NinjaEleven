import React, { useMemo, useState } from 'react';
import type { ScorerDto } from '@/types';
import { ClubName, PlayerName } from '@/components/Common/Names';
import ClubCrest from '@/components/Club/ClubCrest';

interface ScorersListProps {
  scorers: ScorerDto[];
  /** The club the manager commands: the list is about his own goalscorers by default. */
  userTeamId?: string;
  userTeamName?: string;
  /**
   * What an empty list says. The same list is the league's chart and the cup's, and "no goals in
   * the championship" under a bracket is a sentence about the wrong competition.
   */
  emptyMessage?: string;
  /**
   * What the whole-competition tab says, for the same reason. It is the championship's chart
   * and the cup's, and a tab reading "Campeonato" over a list of cup goals is a caption for the
   * wrong competition.
   */
  allLabel?: string;
  /**
   * How many names the table shows. Ten is a chart a manager reads whole — the top of a league
   * table, ten names — and a longer list is a league's whole season, which is a different page
   * with a different purpose. The cup keeps the longer default because a cup's scorers are
   * spread thin and a top of ten is often only a handful of men.
   */
  limit?: number;
}

type Scope = 'mine' | 'all';

/** How many names each view shows when the screen does not say. */
const LIMIT = 15;

/**
 * The scoring chart, opened on the manager's own club. A manager cares first about who is
 * scoring for him, so his players lead; the whole league is one click away for the rest,
 * and the toggle is remembered for as long as the screen is mounted.
 *
 * **The order is the one the backend sent, and the position is the one it decided.** A scorers
 * table is settled on goals, then fewest games, then fewest cards, then age — and three of the
 * artilharia prizes hang on that order, so a client that sorted its own rows by goals and age
 * would be publishing an order the game does not have and paying out of step with it. The
 * columns for games and cards are here so the order can be read rather than trusted: a manager
 * who sees a name above his own knows which of the three numbers settled it.
 *
 * The club view is the same table filtered to his men, so the number in the first column is the
 * place the man holds in the competition and not in the club. A row of one's own players reads
 * 1, 7, 19, and that is the truth: those are the places his men hold.
 */
const ScorersList: React.FC<ScorersListProps> = ({
  scorers,
  userTeamId,
  userTeamName,
  emptyMessage = 'Ainda não há gols no campeonato.',
  allLabel = 'Campeonato',
  limit = LIMIT
}) => {
  const [scope, setScope] = useState<Scope>('mine');

  const mine = useMemo(
    () => (userTeamId ? scorers.filter(s => s.teamId === userTeamId) : []),
    [scorers, userTeamId]
  );

  // The rows are the rows, in the order they came, cut to the number of names the chart
  // shows. The two numbers a client would have sorted by are the ones the backend settled on,
  // and a tie it could not break is carried on the line rather than invented here — so the cut
  // is a slice of a ranked list, never a re-ranking of the part that is left.
  const rows = useMemo(
    () => (scope === 'mine' ? mine : scorers).slice(0, limit),
    [scope, mine, scorers, limit]
  );

  const hasClub = !!userTeamId;

  if (scorers.length === 0) {
    return <div className="league-empty">{emptyMessage}</div>;
  }

  return (
    <>
      {hasClub && (
        <div className="player-team-tabs scorer-tabs">
          <button
            className={`player-team-tab ${scope === 'mine' ? 'active' : ''}`}
            onClick={() => setScope('mine')}
          >
            Meu time ({mine.length})
          </button>
          <button
            className={`player-team-tab ${scope === 'all' ? 'active' : ''}`}
            onClick={() => setScope('all')}
          >
            {allLabel} ({scorers.length})
          </button>
        </div>
      )}

      {rows.length === 0 ? (
        <div className="league-empty">
          {scope === 'mine'
            ? `${userTeamName ?? 'Seu time'} ainda não marcou nenhum gol.`
            : emptyMessage}
        </div>
      ) : (
        <table className="scorers">
          <thead>
            <tr>
              <th className="scorers__pos">#</th>
              <th>Jogador</th>
              <th className="scorers__num">Idade</th>
              <th className="scorers__num">Gols</th>
              <th className="scorers__num">Jogos</th>
              <th className="scorers__num">
                Cartões
                <span className="scorers__hint" title="Amarelo vale 1 ponto, vermelho vale 3 — o desempate da artilharia">
                  (A1 · V3)
                </span>
              </th>
              {scope === 'all' && <th>Time</th>}
            </tr>
          </thead>
          <tbody>
            {rows.map(s => (
              <tr key={s.playerId} className={s.teamId === userTeamId ? 'user-row' : undefined}>
                <td className="scorers__pos">
                  {s.position}
                  {s.tiedWith > 0 && (
                    <span className="scorers__tied" title={`Empatado com ${s.tiedWith} outro(s)`}>
                      =
                    </span>
                  )}
                </td>
                <td>
                  {/* The club beside the man, not only in the last column: on the manager's own
                      club there is no club column, and a name on its own says who scored
                      without saying for whom. */}
                  <span className="scorer-player">
                    {s.teamPrimaryColor && s.teamSecondaryColor && (
                      <ClubCrest
                        primary={s.teamPrimaryColor}
                        secondary={s.teamSecondaryColor}
                        name={s.teamName}
                        className="mini-crest"
                      />
                    )}
                    <PlayerName playerId={s.playerId}><b>{s.playerName}</b></PlayerName>
                  </span>
                </td>
                <td className="scorers__num">{s.age}</td>
                <td className="scorers__num"><b>{s.goals}</b></td>
                <td className="scorers__num">{s.appearances}</td>
                <td
                  className="scorers__num"
                  title={`${s.yellowCards} amarelo(s) e ${s.redCards} vermelho(s)`}
                >
                  {s.cardPoints}
                </td>
                {scope === 'all' && (
                  <td>
                    <ClubName teamId={s.teamId}>{s.teamName}</ClubName>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
};

export default ScorersList;
