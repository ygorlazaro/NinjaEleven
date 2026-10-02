import React, { useCallback, useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { CompetitionApi, LeagueApi, SeasonApi } from '@/api';
import { useGameState } from '@/state';
import type {
  CompetitionEditionDto,
  CupBracketDto,
  CupPrizeDto,
  CupRulesDto,
  ScorerDto,
  SeasonDto,
  TopScorerPrizeListDto
} from '@/types';
import CupBracket from '@/components/Cup/CupBracket';
import CupPodium from '@/components/Cup/CupPodium';
import CupPrizeLegend from '@/components/Cup/CupPrizeLegend';
import CupRulesPanel from '@/components/Cup/CupRulesPanel';
import CupTrophy from '@/components/Cup/CupTrophy';
import ScorersList from '@/components/League/ScorersList';

/**
 * The four pages the cup is read on, and the one a manager lands on.
 *
 * The cup is a bracket, a ranking, a scoring chart and a purse, and they are read four different
 * ways: the bracket is a picture, the ranking is a column of every club in the competition, the
 * chart is a column of names to scan down, and the purse is a ladder of money. Stacked on one page
 * they competed for the same attention, and the bracket — the reason a manager came to a cup at
 * all — ended up below two panels of text and a standings list. A page each is what the division
 * already does, and a cup screen that is a second kind of tab is a screen the manager has to
 * learn the controls of twice.
 *
 * The ranking is a page of its own rather than sitting above the bracket because it is the answer
 * to a different question: the bracket shows how the ties got there, and on a closed season the
 * ranking is the only place that says where his club finished — but it is sixty-four clubs, which
 * is four pages of a pager, and a pager pushed between a manager and his bracket is a bracket he
 * has to scroll past.
 *
 * The tab travels in the query string rather than living in the component, for the same reason
 * it does on the table: a piece of state that exists only inside one screen cannot be pointed at
 * from outside it, so "the cup's ranking" is a link that opens on the ranking.
 */
type CupTab = 'bracket' | 'ranking' | 'scorers' | 'rules';

/**
 * The non-default tabs, as the backend-free names the URL carries. The default is absent from
 * the URL rather than written out, so a link to a cup is a link to its bracket with no query
 * string on it at all.
 */
const TAB_PARAM: Record<Exclude<CupTab, 'bracket'>, string> = {
  ranking: 'ranking',
  scorers: 'scorers',
  rules: 'rules'
};

/**
 * Whether the `tab` the URL carries is a tab this screen has.
 *
 * A name that is not one of them is not an error and not a blank screen: it is a link somebody
 * typed or an old page whose tab has since been renamed, and it lands on the bracket like a
 * screen that had never been given one.
 */
const isCupTab = (value: string | null): value is Exclude<CupTab, 'bracket'> =>
  value === 'ranking' || value === 'scorers' || value === 'rules';

/**
 * The cup, as a bracket.
 *
 * A cup runs once in a season while the championship runs three times, so this screen is not the
 * league table with a different filter: there is one bracket per season and it is the whole of
 * the competition, which is why it is a screen of its own under its own name in the column
 * rather than a third tab of the table.
 *
 * **The season travels in the query string**, like the division does on the table, so a bracket
 * somebody is looking at is a link they can send on — and a cup from three seasons ago is still a
 * bracket that can be read, with the runner-up still named. The page being read travels in it too,
 * as `tab`, so a link names a page of the cup and not only a cup.
 */
const CupScreen: React.FC = () => {
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const selectedTeam = useGameState((s) => s.selectedTeam);

  const [seasons, setSeasons] = useState<SeasonDto[]>([]);
  const [edition, setEdition] = useState<CompetitionEditionDto | null>(null);
  const [bracket, setBracket] = useState<CupBracketDto | null>(null);
  // What a cup run is paid. It is a rule of the game rather than a fact of a season, so it is
  // read once and it does not follow the filter: a manager reading the bracket of three seasons
  // ago is still reading a cup that pays what this cup pays.
  const [cupPrizes, setCupPrizes] = useState<CupPrizeDto[]>([]);
  // The cup's own rules, and read once for the same reason and with the same shape: how a tie is
  // decided is a property of the competition and not of the season, so a manager reading the cup
  // of three seasons ago is reading rules that are still the rules. They are asked of the backend
  // rather than written here because a screen that restated "são jogos de ida e volta" would be
  // promising a cup the game does not play — and this page is the one explaining the bracket, so
  // it is the last place a wrong rule would be read and believed.
  const [cupRules, setCupRules] = useState<CupRulesDto | null>(null);
  // The cup's own scoring chart, and not the season's: a striker with six goals of the season
  // and three of them in the cup has scored three here, and a chart that said six would be
  // reading him off the league. The pool is the size the list needs so that the manager's own
  // players are in it whatever their position in the cup's fifteen.
  const [cupScorers, setCupScorers] = useState<ScorerDto[]>([]);
  // The cup's artilharia, and the one part of the money that follows the season: the three men
  // being paid are the three of *this* cup, and what each takes is a share of the cup's own
  // champion's prize rather than of any division's — which is what lets a third-division forward
  // at the top of the cup's scoring be paid the same as a first-division one.
  const [scorerPrize, setScorerPrize] = useState<TopScorerPrizeListDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  // The season is read as a value rather than as the whole parameter object, because the object
  // is a new one on every navigation: a screen that re-fetches the bracket, the scoring chart
  // and the artilharia's purse because the manager pressed a tab is a screen that asks the
  // backend to recompute the cup three times while he looks for his own striker.
  const seasonParam = params.get('season') || '';

  const load = useCallback(async (seasonId: string) => {
    setError(null);
    setLoading(true);

    try {
      // A cup has no tier, so the edition is found by the kind of competition rather than by a
      // division list: a screen that picked a division would be picking a table.
      const editionList = await CompetitionApi.listEditionsBySeason(seasonId);
      const cup = editionList.find(candidate => candidate.type === 'Cup');

      if (!cup) {
        setEdition(null);
        setBracket(null);
        setError('Esta temporada não tem copa.');
        return;
      }

      setEdition(cup);

      const [bracketData, scorerData, prizeData] = await Promise.all([
        CompetitionApi.getBracket(cup.id),
        LeagueApi.getScorers(seasonId, 250, 'Cup'),
        CompetitionApi.getTopScorerPrizes(cup.id).catch(() => null)
      ]);

      setBracket(bracketData);
      setCupScorers(scorerData);
      setScorerPrize(prizeData);
    } catch (err: any) {
      const code = err?.response?.data?.code;
      setError(code ? `Erro ao carregar a copa (${code}).` : 'Erro ao carregar a copa.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    let alive = true;

    CompetitionApi.getCupPrizes()
      .then(list => alive && setCupPrizes(list))
      .catch(() => alive && setCupPrizes([]));

    CompetitionApi.getCupRules()
      .then(loaded => alive && setCupRules(loaded))
      .catch(() => alive && setCupRules(null));

    return () => {
      alive = false;
    };
  }, []);

  useEffect(() => {
    let alive = true;

    SeasonApi.list()
      .then(list => alive && setSeasons(list))
      .catch(() => alive && setError('Erro ao carregar as temporadas.'));

    return () => {
      alive = false;
    };
  }, []);

  useEffect(() => {
    let cancelled = false;

    const initialize = async () => {
      try {
        const seasonId = seasonParam || (await SeasonApi.current()).id;
        await load(seasonId);
      } catch {
        if (!cancelled) setError('Erro ao carregar as temporadas.');
      }
    };

    initialize();

    return () => {
      cancelled = true;
    };
  }, [seasonParam, load]);

  /**
   * Which of the three pages is open. The bracket is the one a manager lands on, and a `tab`
   * that names no page of this screen lands there too.
   */
  const requestedTab = params.get('tab');
  const activeTab: CupTab = isCupTab(requestedTab) ? requestedTab : 'bracket';

  /**
   * Opening a page is a navigation, not a state change: the tab is the URL, so it can be shared
   * and the back button works. The default is removed rather than written, so the bracket keeps
   * its short link.
   *
   * `replace` and not `push`, because a tab is not a place a manager has been: pressing back
   * from the artilharia should leave the cup, not walk back through the two tabs he glanced at
   * on the way there.
   */
  const setActiveTab = (tab: CupTab) => {
    setParams(
      current => {
        const next = new URLSearchParams(current);
        if (tab === 'bracket') next.delete('tab');
        else next.set('tab', TAB_PARAM[tab]);
        return next;
      },
      { replace: true }
    );
  };

  /** The open page as a query fragment, for the season dropdown that rebuilds the URL by hand. */
  const tabQuery = activeTab === 'bracket' ? '' : `&tab=${TAB_PARAM[activeTab]}`;

  // Switching season keeps the page open. A manager reading the cup's artilharia who drops into
  // last season's wants to keep reading artilharias, and a dropdown that put him back on the
  // bracket every time made it a way of losing his place.
  const changeSeason = (seasonId: string) => {
    if (!seasonId) return;
    setBracket(null);
    navigate(`/copa?season=${seasonId}${tabQuery}`, { replace: true });
  };

  return (
    <div className="card league-screen cup-screen">
      <div className="league-head">
        <div>
          <h2 className="cup-screen__title">
            <CupTrophy size={30} />
            {edition?.name ?? bracket?.competitionName ?? 'Copa'}
          </h2>
          {/* The cup's own rule, said once and said by the backend. It used to be written here in
              a sentence of the screen's own — which is the one thing this sentence must not be:
              a client restating "são dois jogos, pelo agregado" is a client promising a cup the
              game does not play, and it is wrong the day the cup is not two-legged, while the
              bracket underneath it goes on being the bracket the game drew. It is not printed
              until the rules arrive rather than filled in from memory in the meantime. */}
          <p>{cupRules ? cupRules.aggregateRule : ' '}</p>
          {seasons.length > 1 && (
            <div style={{ marginTop: '8px' }}>
              <label style={{ fontSize: '13px', color: 'var(--muted)' }}>Temporada:</label>{' '}
              <select
                value={seasonParam || seasons.find(season => season.status === 'InProgress')?.id || ''}
                onChange={e => changeSeason(e.target.value)}
                disabled={loading}
                aria-label="Temporada"
                className="cup-screen__select"
              >
                {seasons
                  .slice()
                  .sort((a, b) => b.number - a.number)
                  .map(season => (
                    <option key={season.id} value={season.id}>{season.name}</option>
                  ))}
              </select>
            </div>
          )}
        </div>
        {selectedTeam && (
          <div className="league-actions">
            <button className="ctrl" onClick={() => navigate('/league')}>
              Ver o campeonato
            </button>
          </div>
        )}
      </div>

      {error && <p className="squad-hint" style={{ color: 'var(--danger)' }}>{error}</p>}
      {loading && !bracket && <p className="squad-hint">Carregando o chaveamento...</p>}

      {/* The tab bar is the club's own: the same control the table is divided by, because a
          screen with two kinds of tab on it is a screen where the manager has to learn where the
          controls are twice.

          It is drawn outside the guard below, so it is on the screen while the bracket is still
          loading rather than appearing under it — and because the prizes are a rule of the game
          rather than of the season, the page that holds them is readable before a single fixture
          has come back. */}
      <div className="tabs league-tabs" role="tablist">
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === 'bracket'}
          className={`tab ${activeTab === 'bracket' ? 'active' : ''}`}
          onClick={() => setActiveTab('bracket')}
        >
          Chaveamento
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === 'ranking'}
          className={`tab ${activeTab === 'ranking' ? 'active' : ''}`}
          onClick={() => setActiveTab('ranking')}
        >
          Classificação
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === 'scorers'}
          className={`tab ${activeTab === 'scorers' ? 'active' : ''}`}
          onClick={() => setActiveTab('scorers')}
        >
          Artilharia
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === 'rules'}
          className={`tab ${activeTab === 'rules' ? 'active' : ''}`}
          onClick={() => setActiveTab('rules')}
        >
          Prêmios e Regras
        </button>
      </div>

      {activeTab === 'bracket' && bracket && !error && (
        <CupBracket bracket={bracket} userTeamId={selectedTeam?.id} />
      )}

      {activeTab === 'ranking' && bracket && !error && (
        /* The ranking is the bracket's answer to "and how did everyone else do", and it is a page
           of its own for the reason the bracket is: sixty-four clubs is four pages of a pager, and
           a pager standing between a manager and the bracket is a bracket he has to scroll past.
           It is also the one answer a cup has on a closed season — where his club finished — which
           the bracket above cannot give him unless he reads every tie in it. */
        <CupPodium ranking={bracket.ranking ?? []} userTeamId={selectedTeam?.id} />
      )}

      {activeTab === 'scorers' && bracket && !error && (
        /* The chart is a page of its own because it is read a different way — one name at a
           time, down a column of goals — and because it is the only panel that lists every club
           of the cup rather than one of them. It takes the screen's whole width for the same
           reason: a column of names squeezed into half a page is a column nobody scans down, and
           the chart is nothing but a column to scan.

           The cup is named in the title because the list below it is this cup's and not the
           season's: a striker with six goals of the season and three of them in the cup has
           scored three here.

           The guard is the bracket's and not the chart's own, because a chart drawn from an
           empty list while the cup is still loading would say "ainda não há gols na copa" —
           which is a claim about the cup, and the honest answer at that moment is that the
           numbers have not arrived. */
        <div className="league-panel">
          <h3 className="cup-screen__panel-title">
            <CupTrophy size={16} /> Artilharia da Copa
            {bracket.competitionName ? ` — ${bracket.competitionName}` : ''}
          </h3>
          <ScorersList
            scorers={cupScorers}
            userTeamId={selectedTeam?.id}
            userTeamName={selectedTeam?.name}
            emptyMessage="Ainda não há gols na copa."
            allLabel="Copa"
          />
        </div>
      )}

      {activeTab === 'rules' && (
        /* The purse and the rules are one page because they are the two halves of the same
           question: what is a cup run worth, and what is it that decides it. A manager deciding
           whether to throw a squad at this cup needs the consolation ladder and he needs to know
           that a tie is two legs decided on the aggregate — and neither of those is visible from
           the bracket, which shows who is in a tie and not how a tie works.

           The legend is drawn bare, with no `.league-panel` around it, because it already brings
           its own titled card. The league's TopScorerPrizePanel is deliberately absent for the
           same reason the legend is not wrapped: the legend already carries the cup's artilharia,
           so drawing that panel too would print the striker prizes twice under two headings.

           Neither block is guarded by the bracket, because neither is a fact of the season — the
           money and the rules of a cup are the same whichever cup is on screen, and both are
           readable before the bracket has finished loading. */
        <>
          <CupRulesPanel rules={cupRules} />

          <CupPrizeLegend prizes={cupPrizes} scorerPrize={scorerPrize} />
        </>
      )}
    </div>
  );
};

export default CupScreen;
