import React, { useEffect, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { SeasonApi, TeamApi } from '@/api';
import { useGameState } from '@/state';
import type { SquadPlayerDto, TeamDto } from '@/types';
import { starsToString } from '@/services/formatters';
import ClubSquadTable from '@/components/Club/ClubSquadTable';
import { useClubWindow } from '@/services/clubColors';

const TeamViewScreen: React.FC<{ teamId?: string }> = ({ teamId: propTeamId }) => {
  const teams = useGameState((s) => s.leagueTeams);
  const [team, setTeam] = useState<TeamDto | null>(null);
  const [players, setPlayers] = useState<SquadPlayerDto[]>([]);
  const [error, setError] = useState<string | null>(null);

  const navigate = useNavigate();
  const { teamId: routeTeamId = '' } = useParams();
  const [params] = useSearchParams();
  const urlTeamId = propTeamId || routeTeamId;
  const seasonId = params.get('season') || '';

  useEffect(() => {
    const teamObj = teams.find(t => t.id === urlTeamId);
    if (teamObj) setTeam(teamObj);
  }, [teams, urlTeamId]);

  // The club list is remembered in this browser, but the root of the game is now this
  // screen, so a manager can arrive here by typing the address with nothing remembered. The
  // club is then asked for by name rather than drawn as a blank page.
  useEffect(() => {
    if (!urlTeamId || teams.some(t => t.id === urlTeamId)) return undefined;

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
  }, [urlTeamId, teams]);

  useEffect(() => {
    if (!urlTeamId) return undefined;

    // The squad belongs to a season. The screen can be opened without the query, so
    // the current season is what it falls back to.
    let cancelled = false;

    const load = async () => {
      const season = seasonId || (await SeasonApi.current()).id;
      if (cancelled) return;
      setPlayers(await TeamApi.getSquad(urlTeamId, season));
    };

    load().catch(error => {
      if (!cancelled) console.error('Failed to load the squad:', error);
    });

    return () => {
      cancelled = true;
    };
  }, [urlTeamId, seasonId]);

  // Every hook is above every early return. A hook called after one is a hook that
  // sometimes is not called, and React counts: the render where the error clears would
  // reach a hook the failed render never did, and the screen would fall over on the very
  // recovery it was written to allow.
  const clubWindow = useClubWindow(team);

  if (error) {
    return (
      <div className="card team-view-card">
        <p className="competition" style={{ color: 'var(--danger)' }}>{error}</p>
        <button className="ctrl" onClick={() => navigate('/')}>Voltar</button>
      </div>
    );
  }

  if (!team) return null;

  return (
    <div className="team-view-overlay">
      {/* The club's own screen and the modal somebody else opened are the same question, so
          they are the same table in the same colours: one component, not two layouts that
          happen to agree today. */}
      <div className="card team-view-card club-modal" style={clubWindow}>
        <div className="squad-head team-view-summary">
          <div>
            <h2 className="profile-name">{team.name}</h2>
            <p className="squad-hint">
              Força {team.rating} • {players.length} jogadores • Elenco: {starsToString(team.stars)}
            </p>
          </div>
          <button className="ctrl" onClick={() => navigate('/league')}>Tabela e jogos</button>
        </div>

        <div className="club-colors">
          <span className="club-swatch" style={{ background: team.primaryColor || '#f2d34f' }} />
          <span className="club-swatch" style={{ background: team.secondaryColor || '#f2d34f' }} />
        </div>

        <ClubSquadTable squad={players} />
      </div>
    </div>
  );
};

export default TeamViewScreen;
