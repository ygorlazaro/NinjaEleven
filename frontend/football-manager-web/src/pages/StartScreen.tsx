import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { TeamApi, SeasonApi, CompetitionApi } from '@/api';
import { API_BASE_URL } from '@/config/env';
import { useGameState } from '@/state';
import type { TeamDto, SeasonDto, CompetitionDto } from '@/types';
import { TEAM_COLOR_PALETTES, pick } from '@/services/formatters';

const StartScreen: React.FC = () => {
  const navigate = useNavigate();
  const setSelectedTeam = useGameState((s) => s.setSelectedTeam);
  const setLeagueTeams = useGameState((s) => s.setLeagueTeams);
  const setSeasonInfo = useGameState((s) => s.setSeasonInfo);
  const setCompetitionInfo = useGameState((s) => s.setCompetitionInfo);

  const [teams, setTeams] = useState<TeamDto[]>([]);
  const [seasons, setSeasons] = useState<SeasonDto[]>([]);
  const [competitions, setCompetitions] = useState<CompetitionDto[]>([]);
  const [selectedIndex, setSelectedIndex] = useState<number | null>(null);
  const [selectedSeasonId, setSelectedSeasonId] = useState<string>('');
  const [selectedCompetitionId, setSelectedCompetitionId] = useState<string>('');
  const [loading, setLoading] = useState(true);
  const [connectionError, setConnectionError] = useState<string | null>(null);

  useEffect(() => {
    const loadData = async () => {
      setLoading(true);
      setConnectionError(null);
      try {
        const [teamsData, seasonsData] = await Promise.all([
          TeamApi.list(),
          SeasonApi.list(),
        ]);
        setTeams(teamsData);
        setSeasons(seasonsData);

        let compList: CompetitionDto[] = [];
        if (seasonsData.length > 0) {
          compList = await CompetitionApi.listBySeason(seasonsData[0].id);
        }
        setCompetitions(compList);
        if (compList.length > 0) {
          setSelectedCompetitionId(compList[0].id);
        }
        if (seasonsData.length > 0) {
          setSelectedSeasonId(seasonsData[0].id);
        }
      } catch (err: any) {
        // Never fake the world: a broken backend must be visible, not hidden behind
        // invented teams that cannot start a match.
        const code = err?.response?.status ? `HTTP ${err.response.status}` : err?.message || 'erro desconhecido';
        setConnectionError(code);
        setTeams([]);
        setSeasons([]);
        setCompetitions([]);
      } finally {
        setLoading(false);
      }
    };

    loadData();
  }, []);

  if (loading) {
    return (
      <div className="card start">
        <p>Carregando...</p>
      </div>
    );
  }

  if (connectionError) {
    return (
      <div className="card start">
        <h1>Servidor indisponível</h1>
        <p>Não foi possível carregar os dados do campeonato.</p>
        <div className="conn-error">
          <h3>Não foi possível conectar ao servidor</h3>
          <p>Confirme que a API está rodando e que a URL abaixo está correta.</p>
          <code>{API_BASE_URL}</code>
          <p style={{ marginTop: '8px' }}>Detalhe: {connectionError}</p>
        </div>
      </div>
    );
  }

  if (teams.length === 0) {
    return (
      <div className="card start">
        <h1>Nova carreira</h1>
        <p>Nenhuma equipe disponível. Crie equipes no backend primeiro.</p>
      </div>
    );
  }

  const startPressed = () => {
    if (selectedIndex === null) return;
    const team = { ...teams[selectedIndex] };
    setSelectedTeam(team);
    setLeagueTeams(teams);
    setSeasonInfo(seasons.find(s => s.id === (selectedSeasonId || seasons[0]?.id)) || null);
    setCompetitionInfo(competitions.find(c => c.id === (selectedCompetitionId || competitions[0]?.id)) || null);

    // The season is a query of the championship screen, not a rewrite of the url: the
    // router owns the path, so navigation has to go through it.
    const seasonId = selectedSeasonId || seasons[0]?.id || '';
    const competitionId = selectedCompetitionId || competitions[0]?.id || '';

    navigate(`/league?season=${seasonId}&competition=${competitionId}`);
  };

  const canStart = selectedIndex !== null && seasons.length > 0 && competitions.length > 0;

  return (
    <div className="card start">
      <h1>Nova carreira</h1>
      <p>Escolha um dos clubes e coloque seu time em campo.</p>

      {competitions.length > 0 && (
        <div style={{ marginBottom: '12px' }}>
          <label style={{ fontSize: '11px', color: 'var(--muted)' }}>Competição:</label>
          <select
            value={selectedCompetitionId}
            onChange={(e) => setSelectedCompetitionId(e.target.value)}
            style={{ width: '100%', padding: '6px', background: '#0a1520', color: 'var(--text)', border: '1px solid var(--line)', borderRadius: '7px' }}
          >
            {competitions.map(c => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </select>
        </div>
      )}

      {seasons.length > 0 && (
        <div style={{ marginBottom: '12px' }}>
          <label style={{ fontSize: '11px', color: 'var(--muted)' }}>Temporada:</label>
          <select
            value={selectedSeasonId || seasons[0]?.id || ''}
            onChange={(e) => setSelectedSeasonId(e.target.value)}
            style={{ width: '100%', padding: '6px', background: '#0a1520', color: 'var(--text)', border: '1px solid var(--line)', borderRadius: '7px' }}
          >
            {seasons.map(s => (
              <option key={s.id} value={s.id}>{s.name}</option>
            ))}
          </select>
        </div>
      )}

      <div className="team-grid">
        {teams.map((team, i) => {
          const colors = team.primaryColor && team.secondaryColor
            ? { primary: team.primaryColor, secondary: team.secondaryColor }
            : pick(TEAM_COLOR_PALETTES);
          return (
            <div
              key={team.id}
              className={`team-choice ${selectedIndex === i ? 'selected' : ''}`}
              onClick={() => setSelectedIndex(i)}
              data-i={i}
              style={{ '--team-primary': colors.primary, '--team-secondary': colors.secondary } as React.CSSProperties}
            >
              <div
                className="crest"
                style={{
                  background: `linear-gradient(135deg, ${colors.primary}, ${colors.secondary})`,
                }}
              >
                {team.shortName.split(' ').map(w => w[0]).slice(0, 2).join('')}
              </div>
              <h3>{team.name}</h3>
              <div className="small">Campeonato • {teams.length} equipes</div>
              <div className="rating" style={{ color: colors.primary }}>
                FORÇA DO ELENCO {team.rating}
              </div>
            </div>
          );
        })}
      </div>

      <button
        id="startBtn"
        className="primary"
        disabled={!canStart}
        onClick={startPressed}
      >
        Escolher clube e começar
      </button>
    </div>
  );
};

export default StartScreen;
