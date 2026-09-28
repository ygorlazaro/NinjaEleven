import { SeasonApi, TeamApi } from '@/api';
import ClubCrest from '@/components/Club/ClubCrest';
import ClubSquadTable from '@/components/Club/ClubSquadTable';
import FormRun, { formOf } from '@/components/Club/FormRun';
import { ClubName } from '@/components/Common/Names';
import { useClubWindow } from '@/services/clubColors';
import { starsToString } from '@/services/formatters';
import { useGameState } from '@/state';
import type { SquadPlayerDto, TeamDto, TeamMatchRecordDto } from '@/types';
import React, { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';

const TeamViewScreen: React.FC<{ teamId?: string }> = ({ teamId: propTeamId }) => {
  const teams = useGameState((s) => s.leagueTeams);
  const selectedTeam = useGameState((s) => s.selectedTeam);
  const [team, setTeam] = useState<TeamDto | null>(null);
  const [players, setPlayers] = useState<SquadPlayerDto[]>([]);
  const [matches, setMatches] = useState<TeamMatchRecordDto[]>([]);
  const [h2hMatches, setH2hMatches] = useState<TeamMatchRecordDto[]>([]);
  const [error, setError] = useState<string | null>(null);

  const FORM_GUIDE_LENGTH = 10;
  const H2H_LENGTH = 5;

  const navigate = useNavigate();
  const { teamId: routeTeamId = '' } = useParams();
  const [params] = useSearchParams();
  const urlTeamId = propTeamId || routeTeamId;
  const seasonId = params.get('season') || '';

  useEffect(() => {
    const teamObj = teams.find(t => t.id === urlTeamId);
    if (teamObj) setTeam(teamObj);
  }, [teams, urlTeamId]);

  useEffect(() => {
    if (!urlTeamId) return undefined;

    let cancelled = false;

    TeamApi.get(urlTeamId)
      .then(loaded => {
        if (!cancelled) setTeam(loaded);
      })
      .catch(err => {
        console.error('Failed to load the club:', err);
        if (!cancelled) setError('Não foi possível carregar o clube.');
      });

    return () => {
      cancelled = true;
    };
  }, [urlTeamId]);

  useEffect(() => {
    if (!urlTeamId) return undefined;

    let cancelled = false;

    const load = async () => {
      const [season, form, h2h] = await Promise.all([
        seasonId || (await SeasonApi.current()).id,
        TeamApi.getMatches(urlTeamId, FORM_GUIDE_LENGTH),
        selectedTeam && selectedTeam.id !== urlTeamId
          ? TeamApi.getHeadToHead(urlTeamId, selectedTeam.id, H2H_LENGTH).catch(() => [] as TeamMatchRecordDto[])
          : Promise.resolve([] as TeamMatchRecordDto[]),
      ]);

      if (cancelled) return;
      setPlayers(await TeamApi.getSquad(urlTeamId, season));
      setMatches(form);
      setH2hMatches(h2h);
    };

    load().catch(error => {
      if (!cancelled) console.error('Failed to load the club data:', error);
    });

    return () => {
      cancelled = true;
    };
  }, [urlTeamId, seasonId, selectedTeam?.id]);

  const clubWindow = useClubWindow(team);

  const getH2HResultFromHumanPerspective = useMemo(() => (match: TeamMatchRecordDto): 'win' | 'draw' | 'loss' => {
    const humanGoals = match.isHome ? match.goalsAgainst : match.goalsFor;
    const viewedTeamGoals = match.isHome ? match.goalsFor : match.goalsAgainst;

    if (humanGoals > viewedTeamGoals) return 'win';
    if (humanGoals < viewedTeamGoals) return 'loss';
    return 'draw';
  }, []);

  if (error) {
    return (
      <div className="card team-view-card">
        <p className="competition" style={{ color: 'var(--danger)' }}>{error}</p>
        <button className="ctrl" onClick={() => navigate('/')}>Voltar</button>
      </div>
    );
  }

  if (!team) return null;

  const isOwnTeam = selectedTeam && selectedTeam.id === urlTeamId;

  return (
    <div className="app">
      <div className="team-view-overlay">
        <div className="card team-view-card club-modal" style={clubWindow}>
        <div className="squad-head team-view-summary">
          <div className="team-header-with-crest">
            <ClubCrest
              primary={team.primaryColor || '#f2d34f'}
              secondary={team.secondaryColor || '#f2d34f'}
              name={team.name}
            />
            <div className="team-header-info">
              <div className="team-header-main">
                <h2 className="profile-name">{team.name}</h2>
                <span className="team-stars">{starsToString(team.stars)}</span>
              </div>
              <p className="squad-hint">
                {players.length} jogadores
                {team.stadium && ` • ${team.stadium.name} • ${team.stadium.capacity.toLocaleString('pt-BR')} lugares`}
              </p>
            </div>
          </div>
          <button className="ctrl" onClick={() => navigate('/league')}>Tabela e jogos</button>
        </div>

        <div className="club-colors">
          <span className="club-swatch" style={{ background: team.primaryColor || '#f2d34f' }} />
          <span className="club-swatch" style={{ background: team.secondaryColor || '#f2d34f' }} />
        </div>

        <ClubSquadTable squad={players} />

        <section className="club-form">
          <div className="club-form__head">
            <h4 className="club-form__title">Forma recente</h4>
            <div className="form-run" title="Os resultados mais recentes, do mais antigo ao mais novo">
              <FormRun matches={matches} length={FORM_GUIDE_LENGTH} />
            </div>
          </div>

          <h4 className="club-form__title">Últimas {FORM_GUIDE_LENGTH} partidas</h4>

          {matches.length === 0 ? (
            <p className="league-empty">Este clube ainda não jogou.</p>
          ) : (
            <table className="form-table">
              <thead>
                <tr>
                  <th>Temp.</th>
                  <th>Camp.</th>
                  <th>Rodada/Fase</th>
                  <th>Local</th>
                  <th>Estádio</th>
                  <th>Adversário</th>
                  <th>Placar</th>
                  <th>Público</th>
                </tr>
              </thead>
              <tbody>
                {matches.map(record => {
                  const form = formOf(record);
                  return (
                    <tr key={record.matchId} className={`form-${form}`}>
                      <td className="form-season">{record.seasonName || '—'}</td>
                      <td className="form-comp">{record.competitionName || '—'}</td>
                      <td className="form-phase">{record.phaseName || `Rodada ${record.roundNumber}`}</td>
                      <td className="form-venue">{record.isHome ? '🏠' : '✈️'}</td>
                      <td className="form-stadium">{record.stadiumName || '—'}</td>
                      <td className="form-opponent">
                        {record.opponentTeamId ? (
                          <ClubName teamId={record.opponentTeamId}>{record.opponentName}</ClubName>
                        ) : (
                          record.opponentName
                        )}
                      </td>
                      <td className="form-score">{record.goalsFor} x {record.goalsAgainst}</td>
                      <td className="form-attendance">{record.attendance ? record.attendance.toLocaleString('pt-BR') : '—'}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </section>

        {selectedTeam && selectedTeam.id !== urlTeamId && (
          <section className="club-form">
            <h4 className="club-form__title">Confrontos diretos vs <ClubName teamId={selectedTeam.id}>{selectedTeam.name}</ClubName></h4>
            {h2hMatches.length === 0 ? (
              <p className="league-empty">Nenhum confronto anterior entre os times</p>
            ) : (
              <table className="round-context__h2h-table">
                <thead>
                  <tr>
                    <th>Temporada</th>
                    <th>Campeonato</th>
                    <th>Fase/Rodada</th>
                    <th>Local</th>
                    <th>Estádio</th>
                    <th>Placar</th>
                    <th>Público</th>
                  </tr>
                </thead>
                <tbody>
                  {h2hMatches.map((m) => {
                    const result = getH2HResultFromHumanPerspective(m);
                    return (
                      <tr key={m.matchId}>
                        <td className="round-context__h2h-season">{m.seasonName || '—'}</td>
                        <td className="round-context__h2h-comp">{m.competitionName || '—'}</td>
                        <td className="round-context__h2h-phase">{m.phaseName || `Rodada ${m.roundNumber}`}</td>
                        <td className="round-context__h2h-venue">{m.isHome ? '🏠' : '✈️'}</td>
                        <td className="round-context__h2h-stadium">{m.stadiumName || '—'}</td>
                        <td className={`round-context__h2h-score h2h-${result}`}>
                          {m.isHome ? m.goalsFor : m.goalsAgainst} x {m.isHome ? m.goalsAgainst : m.goalsFor}
                        </td>
                        <td className="round-context__h2h-attendance">{m.attendance ? m.attendance.toLocaleString('pt-BR') : '—'}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )}
          </section>
        )}
      </div>
    </div>
    </div>
  );
};

export default TeamViewScreen;
