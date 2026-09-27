import React, { useEffect, useState } from 'react';
import type { FinanceLedgerDto, SeasonDto } from '@/types';
import { formatLimo, formatSignedLimo } from '@/services/limo';
import { SeasonApi, TeamApi } from '@/api';
import { useGameState } from '@/state';

/**
 * How many movements a page holds. A ledger of a whole season is a long read, and a screen
 * that shows all of it is a screen nobody scrolls to the end of: the point of a page is that
 * the last line is a few lines away, so the eye can keep its place.
 */
const PAGE_SIZE = 10;

/**
 * A movement, said the way a manager would say it: the mark, the words, and the sign.
 *
 * The key is the contract with the backend and the words are the screen's, exactly as the
 * match feed's event types are. A movement whose kind the ledger has never seen still gets a
 * row, a sign and a place in the column of money — it is the words that fall back to a mark,
 * because refusing to show a movement is not the same as not understanding it.
 */
const KINDS: Record<string, { label: string; icon: string }> = {
  GateRevenue: { label: 'Bilheteria', icon: '🎟' },
  // The one movement of the book that leaves the club on every matchday of the league, and
  // the only one a manager cannot do anything about in the ninety minutes: it is paid at the
  // whistle whether the result was a win, a draw or a defeat, which is why the row is said
  // apart from the rest of the column rather than as one more line in it.
  Wages: { label: 'Folha salarial', icon: '👥' },
  TransferIn: { label: 'Venda de atleta', icon: '📤' },
  TransferOut: { label: 'Contratação', icon: '📥' },
  Sponsorship: { label: 'Patrocínio', icon: '🤝' },
  TvRights: { label: 'Direitos de TV', icon: '📺' },
  PrizeMoney: { label: 'Premiação', icon: '🏅' },
  Infrastructure: { label: 'Estádio e estrutura', icon: '🏗' },
  Fine: { label: 'Multa', icon: '⚖' },
  Merchandising: { label: 'Merchandising', icon: '🛍' },
  // The two lines that are not movements of money. The capital a club is founded on and the
  // balance it is handed to open a season both state a balance rather than moving one, and
  // the screen draws them as a balance: a club that opened a season with nine hundred
  // thousand limos did not earn them that day and the line must not read as if it had.
  Seed: { label: 'Capital inicial', icon: '🏛' },
  CarryOver: { label: 'Saldo transportado', icon: '🏦' }
};

/**
 * The club's books: what it has, and every movement that took it there or away from it.
 *
 * The newest movement is the first line. A ledger read oldest-first is a history, and a
 * manager opening his club's money is not looking for how it began; the balance at the end of
 * the season is in the summary, and the line he wants is the one that happened last. Days,
 * not dates: the same rule the calendar is read by, because a movement happens on a day of
 * the season and not on a date the game does not act on.
 *
 * The balance on a line, the money in and the money out, the totals, the ordering and the
 * count of lines are all the backend's: a page of a ledger that starts halfway down the
 * history cannot work out what came before it, and a screen that summed a column of ten lines
 * and called it the season's income would be right on the first page and wrong on the last.
 */
const FinanceiroScreen: React.FC = () => {
  const selectedTeam = useGameState((s) => s.selectedTeam);
  const teamId = selectedTeam?.id;

  const [seasons, setSeasons] = useState<SeasonDto[]>([]);
  const [seasonId, setSeasonId] = useState('');
  const [page, setPage] = useState(1);
  const [ledger, setLedger] = useState<FinanceLedgerDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      try {
        // Newest first: a manager opening the books is asking about the season in progress.
        const list = await SeasonApi.list();
        if (cancelled) return;

        setSeasons([...list].sort((a, b) => b.number - a.number));
      } catch (err) {
        console.error('Failed to load the seasons:', err);
        if (!cancelled) setError('Não foi possível carregar as temporadas.');
      }
    };

    load();

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (!teamId) return undefined;

    let cancelled = false;

    const load = async () => {
      setLoading(true);
      setError(null);

      try {
        const book = await TeamApi.getFinance(
          teamId,
          seasonId || undefined,
          page,
          PAGE_SIZE
        );

        if (cancelled) return;

        setLedger(book);
      } catch (err) {
        console.error('Failed to load the club\'s books:', err);
        if (!cancelled) setError('Não foi possível carregar o financeiro do clube.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    };

    load();

    return () => {
      cancelled = true;
    };
  }, [teamId, seasonId, page]);

  /**
   * The season is a filter because a career's books are a long read and a manager asking
   * "how did last season go" is asking about one of them, not about the sum of five. It
   * opens on the whole career, which is where the club's balance is the one it has; turning to
   * another season starts the book again at its first page, because page four of last season
   * is not where anybody is standing when they ask about this one.
   */
  const changeSeason = (id: string) => {
    setSeasonId(id);
    setPage(1);
  };

  // The page being shown is the one the backend answered with, not the one that was asked
  // for: a filter can leave fewer lines than the page asked for, and the backend clamps to
  // the last one that has lines rather than sending an empty book.
  const shownPage = ledger ? Math.min(Math.max(ledger.page, 1), Math.max(ledger.totalPages, 1)) : 1;
  const totalPages = Math.max(ledger?.totalPages ?? 1, 1);

  if (!selectedTeam) {
    return (
      <div className="app">
        <div className="card match-header club-modal">
          <h2 className="profile-name">Financeiro</h2>
          <p className="competition">Escolha um clube para ver os livros dele.</p>
        </div>
      </div>
    );
  }

  return (
    <div className="app">
      <div className="card match-header club-modal">
        <h2 className="profile-name">Financeiro</h2>
        <p className="competition">{selectedTeam.name}</p>

        <div className="squad-toolbar">
          <label className="squad-filters">
            <span className="squad-toolbar-label">Temporada</span>
            <select value={seasonId} onChange={event => changeSeason(event.target.value)}>
              <option value="">Toda a carreira</option>
              {seasons.map(season => (
                <option key={season.id} value={season.id}>
                  {season.name}
                </option>
              ))}
            </select>
          </label>
        </div>

        {/* Three numbers, and the balance is the one that is said twice as large: what the
            club has, and the two streams that decided it. */}
        <div className="finance-summary">
          <div className="finance-summary__main">
            <span className="finance-summary__label">Saldo em caixa</span>
            <span className="finance-summary__balance">{formatLimo(ledger?.balance ?? 0)}</span>
          </div>
          <div className="finance-summary__side">
            <span className="finance-summary__label">Entradas</span>
            <span className="finance-in">{formatLimo(ledger?.income ?? 0)}</span>
          </div>
          <div className="finance-summary__side">
            <span className="finance-summary__label">Saídas</span>
            <span className="finance-out">-{formatLimo(ledger?.expenses ?? 0)}</span>
          </div>
        </div>

        <div className="calendar-list finance-list">
          {error ? (
            <p className="league-empty">{error}</p>
          ) : loading && !ledger ? (
            <p className="league-empty">Carregando os livros do clube…</p>
          ) : ledger && ledger.movements.length === 0 ? (
            <p className="league-empty">Nenhuma movimentação no período.</p>
          ) : (
            <table className="history-table finance-table">
              <thead>
                <tr>
                  <th className="calendar-day">Dia</th>
                  <th>Movimentação</th>
                  <th className="num finance-amount">Valor</th>
                  <th className="num">Saldo</th>
                </tr>
              </thead>
              <tbody>
                {ledger?.movements.map(movement => {
                  const kind = KINDS[movement.kind] ?? { label: movement.kind, icon: '•' };
                  // A line that states a balance is drawn in the colour of a balance: it is
                  // money the club had, not money that arrived, and a green plus on it would
                  // count the same limos twice in the reader's head.
                  const tone = movement.statesABalance
                    ? 'finance-balance'
                    : movement.amount > 0
                      ? 'finance-in'
                      : 'finance-out';

                  return (
                    <tr
                      key={movement.id}
                      className={[
                        'history-row',
                        'finance-row',
                        movement.statesABalance ? 'finance-row--balance' : '',
                        movement.kind === 'Wages' ? 'finance-row--wages' : ''
                      ]
                        .filter(Boolean)
                        .join(' ')}
                    >
                      <td className="calendar-day">
                        {movement.matchDayNumber === null ? '—' : `Dia ${movement.matchDayNumber}`}
                      </td>
                      <td>
                        <span className="finance-kind">
                          <span className="finance-kind__icon">{kind.icon}</span>
                          <span className="finance-kind__label">{kind.label}</span>
                        </span>
                        <span className="finance-description">{movement.description}</span>
                      </td>
                      <td className={`num finance-amount ${tone}`}>
                        {movement.statesABalance
                          ? formatLimo(movement.balanceAfter)
                          : formatSignedLimo(movement.amount)}
                      </td>
                      <td className="num finance-balance">{formatLimo(movement.balanceAfter)}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>

        {/* The page turner says where it is in the book, because a ledger that shows ten
            lines with no count is a list, and a manager cannot tell a list from a whole. */}
        <div className="squad-actions finance-pager">
          <button className="ctrl" onClick={() => setPage(1)} disabled={shownPage === 1}>
            « Início
          </button>
          <button
            className="ctrl"
            onClick={() => setPage(Math.max(1, shownPage - 1))}
            disabled={shownPage === 1}
          >
            ‹ Anterior
          </button>
          <span className="finance-pager__status">
            Página {shownPage} de {totalPages} • {ledger?.totalItems ?? 0}{' '}
            {ledger?.totalItems === 1 ? 'movimentação' : 'movimentações'}
          </span>
          <button
            className="ctrl"
            onClick={() => setPage(shownPage + 1)}
            disabled={shownPage >= totalPages}
          >
            Seguinte ›
          </button>
          <button
            className="ctrl"
            onClick={() => setPage(totalPages)}
            disabled={shownPage >= totalPages}
          >
            Fim »
          </button>
        </div>
      </div>
    </div>
  );
};

export default FinanceiroScreen;
