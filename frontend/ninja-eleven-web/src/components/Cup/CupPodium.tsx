import React, { useEffect, useState } from 'react';
import type { CupRankingRowDto } from '@/types';
import { ClubName } from '@/components/Common/Names';
import ClubCrest from '@/components/Club/ClubCrest';
import { formatLimo } from '@/services/limo';

/** How many clubs a page of the podium holds. Eight is a screen of names and no more. */
const PageSize = 8;

/**
 * The cup as a ranking: every club it drew, the one that got furthest first.
 *
 * **It is the bracket's answer to "and how did everyone else do".** A knockout is sixty-four
 * clubs and one result, and the bracket below shows the ties; this shows what the run was worth,
 * which on a closed season is the only place a manager can read that his club took the field in
 * a tournament at all.
 *
 * **The order is the backend's and is not worked out here.** A club's step is the round it
 * reached — the champion above the final, because he was never in a round he could lose — and
 * inside one step the championship's own chain decides: points, goal difference, goals scored,
 * the head-to-head of the clubs still level, then the cards. A screen that sorted these lines a
 * second way would be a second answer to "which of these two had the better cup", and it would
 * be the second one to disagree with the table at the top of the league page.
 *
 * **Eight to a page, and the turner says where it is.** Sixty-four clubs is eight pages, and a
 * list that shows all sixty-four at once is a list nobody reads to the end — which is exactly
 * where a club knocked out in the first round is. The pager carries the count, because a screen
 * showing eight of sixty-four with no count is indistinguishable from a cup of eight.
 */
const CupPodium: React.FC<{
  ranking: CupRankingRowDto[];
  userTeamId?: string | null;
}> = ({ ranking, userTeamId }) => {
  const [page, setPage] = useState(1);

  const totalPages = Math.max(1, Math.ceil(ranking.length / PageSize));

  // A cup that has not drawn as many clubs as the page held — a new edition, or a cup part way
  // through its first round — must not leave the turner on a page that does not exist.
  useEffect(() => {
    if (page > totalPages) setPage(totalPages);
  }, [page, totalPages]);

  if (ranking.length === 0) return null;

  const shown = ranking.slice((page - 1) * PageSize, page * PageSize);

  return (
    <section className="cup-podium">
      <h3 className="cup-podium__title">Classificação da copa</h3>

      <table className="cup-podium__table">
        <thead>
          <tr>
            <th className="cup-podium__pos">#</th>
            <th className="cup-podium__club">Clube</th>
            <th className="cup-podium__round">Fase</th>
            <th title="Pontos, saldo de gols, gols pró e confronto direto: o desempate do campeonato">P</th>
            <th title="Vitórias, empates e derrotas">V</th>
            <th title="Gols pró">GP</th>
            <th title="Gols contra">GC</th>
            <th title="Saldo de gols">SG</th>
            <th className="cup-podium__money">Prêmio</th>
          </tr>
        </thead>
        <tbody>
          {shown.map(row => (
            <tr
              key={row.teamId}
              className={[
                'cup-podium__row',
                row.isChampion ? 'cup-podium__row--champion' : '',
                row.isRunnerUp ? 'cup-podium__row--runner-up' : '',
                userTeamId && row.teamId === userTeamId ? 'cup-podium__row--mine' : ''
              ]
                .filter(Boolean)
                .join(' ')}
            >
              <td className="cup-podium__pos">{row.position}</td>
              <td className="cup-podium__club">
                <span className="cup-podium__crest">
                  <ClubCrest
                    primary={row.primaryColor}
                    secondary={row.secondaryColor}
                    name={row.name}
                    className="mini-crest"
                  />
                </span>
                <ClubName teamId={row.teamId}>{row.name}</ClubName>
                {userTeamId && row.teamId === userTeamId && (
                  <span className="cup-club__you" title="O seu clube">Você</span>
                )}
              </td>
              <td className="cup-podium__round">
                {row.isChampion ? 'Campeão' : row.isRunnerUp ? 'Vice' : row.roundName}
              </td>
              <td>{row.points}</td>
              <td>{row.wins}</td>
              <td>{row.goalsFor}</td>
              <td>{row.goalsAgainst}</td>
              <td>{row.goalDifference > 0 ? `+${row.goalDifference}` : row.goalDifference}</td>
              {/* What a club still in the cup will be paid is not a number yet. It is a dash and
                  not a zero: a zero is what a cup pays nobody, and this club has not been paid
                  nothing — it has not finished. */}
              <td className="cup-podium__money">{row.prize != null ? formatLimo(row.prize) : '—'}</td>
            </tr>
          ))}
        </tbody>
      </table>

      {totalPages > 1 && (
        <div className="squad-actions finance-pager">
          <button className="ctrl" onClick={() => setPage(1)} disabled={page === 1}>
            « Início
          </button>
          <button className="ctrl" onClick={() => setPage(Math.max(1, page - 1))} disabled={page === 1}>
            ‹ Anterior
          </button>
          <span className="finance-pager__status">
            Página {page} de {totalPages} • {ranking.length} clubes
          </span>
          <button
            className="ctrl"
            onClick={() => setPage(page + 1)}
            disabled={page >= totalPages}
          >
            Seguinte ›
          </button>
          <button className="ctrl" onClick={() => setPage(totalPages)} disabled={page >= totalPages}>
            Fim »
          </button>
        </div>
      )}
    </section>
  );
};

export default CupPodium;