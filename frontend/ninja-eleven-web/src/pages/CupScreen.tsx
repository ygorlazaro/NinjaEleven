import React, { useCallback, useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { CompetitionApi, LeagueApi, SeasonApi } from '@/api';
import { useGameState } from '@/state';
import type {
  CompetitionEditionDto,
  CupBracketDto,
  CupPrizeDto,
  ScorerDto,
  SeasonDto,
  TopScorerPrizeListDto
} from '@/types';
import CupBracket from '@/components/Cup/CupBracket';
import CupPodium from '@/components/Cup/CupPodium';
import CupPrizeLegend from '@/components/Cup/CupPrizeLegend';
import CupTrophy from '@/components/Cup/CupTrophy';
import ScorersList from '@/components/League/ScorersList';

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
 * bracket that can be read, with the runner-up still named.
 */
const CupScreen: React.FC = () => {
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const selectedTeam = useGameState((s) => s.selectedTeam);

  const [seasons, setSeasons] = useState<SeasonDto[]>([]);
  const [seasonsLoading, setSeasonsLoading] = useState(false);
  const [edition, setEdition] = useState<CompetitionEditionDto | null>(null);
  const [bracket, setBracket] = useState<CupBracketDto | null>(null);
  // What a cup run is paid. It is a rule of the game rather than a fact of a season, so it is
  // read once and it does not follow the filter: a manager reading the bracket of three seasons
  // ago is still reading a cup that pays what this cup pays.
  const [cupPrizes, setCupPrizes] = useState<CupPrizeDto[]>([]);
  // The cup's own scoring chart, and not the season's: a striker with six goals of the season
  // and three of them in the cup has scored three here, and a chart that said six would be
  // reading him off the league. The pool is the size the list needs so that the manager's own
  // players are in it whatever their position in the cup's fifteen.
  const [cupScorers, setCupScorers] = useState<ScorerDto[]>([]);
  // The cup's artilharia. Unlike the consolation ladder above it, this is a fact of the season
  // and it does follow the filter: the three men being paid are the three of this cup, and the
  // amount each is paid is a share of the title of the division his own club is in.
  const [scorerPrize, setScorerPrize] = useState<TopScorerPrizeListDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

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

    return () => {
      alive = false;
    };
  }, []);

  useEffect(() => {
    let cancelled = false;

    const initialize = async () => {
      try {
        const seasonId = params.get('season') || (await SeasonApi.current()).id;
        const seasonList = await SeasonApi.list();

        if (cancelled) return;
        setSeasons(seasonList);

        await load(seasonId);
      } catch {
        if (!cancelled) setError('Erro ao carregar as temporadas.');
      }
    };

    initialize();

    return () => {
      cancelled = true;
    };
  }, [params, load]);

  const changeSeason = async (seasonId: string) => {
    setSeasonsLoading(true);
    setBracket(null);
    navigate(`/copa?season=${seasonId}`, { replace: true });
    await load(seasonId);
    setSeasonsLoading(false);
  };

  return (
    <div className="card league-screen cup-screen">
      <div className="league-head">
        <div>
          <h2 className="cup-screen__title">
            <CupTrophy size={30} />
            {edition?.name ?? bracket?.competitionName ?? 'Copa'}
          </h2>
          <p>Cada confronto é jogado em dois jogos, e o agregado é o que decide quem passa.</p>
          {seasons.length > 1 && (
            <div style={{ marginTop: '8px' }}>
              <label style={{ fontSize: '13px', color: 'var(--muted)' }}>Temporada:</label>{' '}
              <select
                value={params.get('season') || seasons.find(season => season.status === 'InProgress')?.id || ''}
                onChange={e => changeSeason(e.target.value)}
                disabled={seasonsLoading}
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

      {bracket && !error && (
        <>
          {/* What the cup pays and who has scored in it, above the bracket: both are read
              before it, because "how far does this pay" and "who is scoring for me in it" are
              the two questions a manager opens a knockout screen with. */}
          <div className="cup-top-grid">
            <div className="cup-prize-legend">
              <CupPrizeLegend prizes={cupPrizes} scorerPrize={scorerPrize} />
            </div>
            <div className="league-panel">
              <h3 className="cup-screen__panel-title"><CupTrophy size={16} /> Artilheiros da Copa</h3>
              <ScorersList
                scorers={cupScorers}
                userTeamId={selectedTeam?.id}
                userTeamName={selectedTeam?.name}
                emptyMessage="Ainda não há gols na copa."
                allLabel="Copa"
              />
            </div>
          </div>

          {/* The ranking is above the bracket rather than below it: on a closed season it is the
              answer a manager came for — where his club finished in the cup — and the bracket
              below is how the ties got there. */}
          <CupPodium ranking={bracket.ranking ?? []} userTeamId={selectedTeam?.id} />

          <CupBracket bracket={bracket} userTeamId={selectedTeam?.id} />
        </>
      )}
    </div>
  );
};

export default CupScreen;
