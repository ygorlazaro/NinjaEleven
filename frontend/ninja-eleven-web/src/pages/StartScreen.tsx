import React, { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { CompetitionApi, SeasonApi, TeamApi } from '@/api';
import { API_BASE_URL } from '@/config/env';
import { useGameState } from '@/state';
import { divisionsOf } from '@/types';
import type { CompetitionEditionDto, SeasonDto, TeamDto } from '@/types';
import { TEAM_COLOR_PALETTES, pick, starsToString } from '@/services/formatters';

const StartScreen: React.FC = () => {
  const navigate = useNavigate();
  const setSelectedTeam = useGameState((s) => s.setSelectedTeam);
  const setLeagueTeams = useGameState((s) => s.setLeagueTeams);
  const setSeasonInfo = useGameState((s) => s.setSeasonInfo);
  const setCompetitionInfo = useGameState((s) => s.setCompetitionInfo);
  const selectedCompetition = useGameState((s) => s.selectedCompetition);

  const [clubs, setClubs] = useState<TeamDto[]>([]);
  const [seasons, setSeasons] = useState<SeasonDto[]>([]);
  const [editions, setEditions] = useState<CompetitionEditionDto[]>([]);
  const [selectedClubId, setSelectedClubId] = useState<string | null>(null);
  const [selectedSeasonId, setSelectedSeasonId] = useState<string>('');
  const [selectedEditionId, setSelectedEditionId] = useState<string>('');
  const [loading, setLoading] = useState(true);
  const [connectionError, setConnectionError] = useState<string | null>(null);

  const divisions = useMemo(() => divisionsOf(editions), [editions]);

  useEffect(() => {
    const loadData = async () => {
      setLoading(true);
      setConnectionError(null);

      try {
        const [teamData, seasonData] = await Promise.all([
          TeamApi.list(),
          SeasonApi.list(),
        ]);

        setClubs(teamData);
        setSeasons(seasonData);

        // The current season first, and the newest first otherwise: seasons are counted from
        // one and a manager opening a new career wants the one in progress, not the oldest
        // one the backend still has.
        const ordered = [...seasonData].sort((a, b) => b.number - a.number);
        const current = ordered.find(season => season.status === 'InProgress') ?? ordered[0];

        if (current) setSelectedSeasonId(current.id);

        if (current) {
          const editionList = await CompetitionApi.listEditionsBySeason(current.id);
          setEditions(editionList);

          // The division the manager left on last time, when it is still one of this season's
          // divisions. Otherwise the top flight, because that is where a career starts.
          const remembered = divisionsOf(editionList).find(
            division => division.id === selectedCompetition?.id
          );
          const first = remembered ?? divisionsOf(editionList)[0] ?? editionList[0];

          if (first) setSelectedEditionId(first.id);
        }
      } catch (err: any) {
        // Never fake the world: a broken backend must be visible, not hidden behind
        // invented teams that cannot start a match.
        const code = err?.response?.status
          ? `HTTP ${err.response.status}`
          : err?.message || 'erro desconhecido';
        setConnectionError(code);
        setClubs([]);
        setSeasons([]);
        setEditions([]);
      } finally {
        setLoading(false);
      }
    };

    loadData();
  }, [selectedCompetition?.id]); // eslint-disable-line react-hooks/exhaustive-deps

  // The clubs of the chosen division. They are asked of the backend rather than filtered out
  // of the whole pyramid on the client: a club's division is a fact about its enrolment in an
  // edition, and two lists joined up in a browser is how a club ends up in the wrong one.
  const [divisionClubs, setDivisionClubs] = useState<TeamDto[]>([]);
  const [clubsLoading, setClubsLoading] = useState(false);

  useEffect(() => {
    if (!selectedEditionId) {
      setDivisionClubs([]);
      return;
    }

    let cancelled = false;
    setClubsLoading(true);

    CompetitionApi.listClubs(selectedEditionId)
      .then(loaded => {
        if (!cancelled) setDivisionClubs(loaded);
      })
      .catch(() => {
        if (!cancelled) setDivisionClubs([]);
      })
      .finally(() => {
        if (!cancelled) setClubsLoading(false);
      });

    return () => { cancelled = true; };
  }, [selectedEditionId]);

  // A club chosen in another division is not a club of this one.
  useEffect(() => {
    setSelectedClubId(null);
  }, [selectedEditionId]);

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

  if (clubs.length === 0) {
    return (
      <div className="card start">
        <h1>Nova carreira</h1>
        <p>Nenhuma equipe disponível. Rode a API com <code>--seed</code> para criar o mundo.</p>
      </div>
    );
  }

  const startPressed = () => {
    const club = divisionClubs.find(team => team.id === selectedClubId);
    if (!club) return;

    setSelectedTeam(club);
    setLeagueTeams(divisionClubs);
    setSeasonInfo(seasons.find(s => s.id === selectedSeasonId) || null);
    setCompetitionInfo(editions.find(e => e.id === selectedEditionId) || null);

    // The season and the division are queries of the screens behind this one, not a rewrite
    // of the url: the router owns the path, so navigation has to go through it. A career
    // opens on the club the manager just took over, because that squad is the first thing he
    // reads — and the division he is about to manage is named alongside it, because a club
    // with no division is a club with no table.
    navigate(`/team/${club.id}?season=${selectedSeasonId}&edition=${selectedEditionId}`);
  };

  const canStart = selectedClubId !== null && selectedSeasonId !== '' && selectedEditionId !== '';

  return (
    <div className="card start">
      <h1>Nova carreira</h1>
      <p>Escolha a divisão, escolha um dos clubes e coloque seu time em campo.</p>

      {seasons.length > 1 && (
        <div style={{ marginBottom: '12px' }}>
          <label style={{ fontSize: '13px', color: 'var(--muted)' }}>Temporada:</label>
          <select
            value={selectedSeasonId}
            onChange={(e) => setSelectedSeasonId(e.target.value)}
            style={selectStyle}
          >
            {seasons.map(s => (
              <option key={s.id} value={s.id}>{s.name}</option>
            ))}
          </select>
        </div>
      )}

      {divisions.length > 0 && (
        <div style={{ marginBottom: '12px' }}>
          <label style={{ fontSize: '13px', color: 'var(--muted)' }}>Divisão:</label>
          <select
            value={selectedEditionId}
            onChange={(e) => setSelectedEditionId(e.target.value)}
            style={selectStyle}
          >
            {divisions.map(division => (
              <option key={division.id} value={division.id}>{division.name}</option>
            ))}
          </select>
        </div>
      )}

      <div className="small" style={{ marginBottom: '8px' }}>
        {clubsLoading
          ? 'Carregando clubes...'
          : `${divisionClubs.length} clubes nesta divisão`}
      </div>

      <div className="team-grid">
        {divisionClubs.map((club, i) => {
          const colors = club.primaryColor && club.secondaryColor
            ? { primary: club.primaryColor, secondary: club.secondaryColor }
            : pick(TEAM_COLOR_PALETTES);

          return (
            <div
              key={club.id}
              className={`team-choice ${selectedClubId === club.id ? 'selected' : ''}`}
              onClick={() => setSelectedClubId(club.id)}
              data-i={i}
              style={{ '--team-primary': colors.primary, '--team-secondary': colors.secondary } as React.CSSProperties}
            >
              <div
                className="crest"
                style={{ background: `linear-gradient(135deg, ${colors.primary}, ${colors.secondary})` }}
              >
                {club.shortName.split(' ').map(w => w[0]).slice(0, 2).join('')}
              </div>
              <h3>{club.name}</h3>
              <div className="rating" style={{ color: colors.primary }}>
                ELenco {starsToString(club.stars)}
              </div>
              {club.stadium && (
                <div className="small">
                  {club.stadium.name} • {club.stadium.capacity.toLocaleString('pt-BR')} lugares
                </div>
              )}
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

const selectStyle: React.CSSProperties = {
  width: '100%',
  padding: '6px',
  background: '#0a1520',
  color: 'var(--text)',
  border: '1px solid var(--line)',
  borderRadius: '7px'
};

export default StartScreen;
