import React, { useEffect, useState } from 'react';
import { TeamApi, SeasonApi, CompetitionApi } from '@/api';
import { useGameState } from '@/state';
import type { TeamDto, SeasonDto, CompetitionDto } from '@/types';
import { TEAM_COLOR_PALETTES, pick } from '@/services/formatters';

const StartScreen: React.FC = () => {
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

  useEffect(() => {
    const loadData = async () => {
      setLoading(true);
      try {
        const [teamsData, seasonsData] = await Promise.all([
          TeamApi.list(),
          SeasonApi.list(),
        ]);
        setTeams(teamsData);
        setSeasons(seasonsData);

        let compList: CompetitionDto[] = [];
        if (seasonsData.length > 0) {
          compList = await CompetitionApi.list(seasonsData[0].id);
        }
        setCompetitions(compList);
        if (compList.length > 0) {
          setSelectedCompetitionId(compList[0].id);
        }
      } catch {
        // Generate demo teams if API not available
        const demoTeams = generateDemoTeams(8);
        setTeams(demoTeams);
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
    const seasonId = selectedSeasonId;
    const competitionId = selectedCompetitionId;

    window.location.hash = '#/league';
    window.location.search = `?season=${seasonId}&competition=${competitionId}`;
  };

  const canStart = selectedIndex !== null;

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

function generateDemoTeams(count: number): TeamDto[] {
  const TEAM_NAMES = ['Atlético do Vale', 'União Esportiva', 'Real Horizonte', 'Tigres FC', 'Aurora', 'Nacional Azul', 'Porto Verde', 'Estrela do Sul'];
  const selected = TEAM_NAMES.slice(0, count);
  return selected.map((name, i) => {
    const colors = TEAM_COLOR_PALETTES[i % TEAM_COLOR_PALETTES.length];
    return {
      id: crypto.randomUUID(),
      name,
      shortName: name.split(' ').map(w => w[0]).join(''),
      primaryColor: colors.primary,
      secondaryColor: colors.secondary,
      rating: 50 + Math.floor(Math.random() * 15),
      birthDate: '',
      age: 0,
      position: '',
      speed: 0,
      accuracy: 0,
      dribbling: 0,
      heading: 0,
      strength: 0,
      goalkeeperPower: 0,
      reflexes: 0,
      energy: 0,
      goals: 0,
      yellowCards: 0,
      redCards: 0,
      suspensionMatches: 0,
      injury: '',
      isUnavailable: false,
    };
  });
}

export default StartScreen;
