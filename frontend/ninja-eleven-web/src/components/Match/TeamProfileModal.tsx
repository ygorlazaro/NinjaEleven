import React, { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { SeasonApi, TeamApi } from '@/api';
import type { SquadPlayerDto, TeamDto } from '@/types';
import { positionLabel } from '@/services/formatters';
import { PlayerName } from '@/components/Common/Names';

interface TeamProfileModalProps {
  teamId: string;
  onClose: () => void;
}

/**
 * A club, the way a manager looks one up: what it is called, what it wears, how many
 * players it has, and who they are.
 *
 * It is a modal because a club is looked up from inside something else — a scoreline, a
 * sentence in the feed, a player's profile — and the manager wants to see the club and go
 * back to where they were, not to be taken somewhere else entirely.
 */
const TeamProfileModal: React.FC<TeamProfileModalProps> = ({ teamId, onClose }) => {
  const navigate = useNavigate();

  const [team, setTeam] = useState<TeamDto | null>(null);
  const [squad, setSquad] = useState<SquadPlayerDto[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      const [found, season] = await Promise.all([TeamApi.get(teamId), SeasonApi.current()]);
      const players = await TeamApi.getSquad(teamId, season.id);

      if (cancelled) return;
      setTeam(found);
      setSquad(players);
    };

    load().catch(err => {
      console.error('Failed to load the club profile:', err);
      if (!cancelled) setError('Não foi possível carregar o clube.');
    });

    return () => {
      cancelled = true;
    };
  }, [teamId]);

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose();
    };

    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);

  const byPosition = useMemo(() => {
    const order: Record<string, number> = { GK: 0, DEF: 1, MID: 2, ATT: 3 };

    return [...squad].sort(
      (a, b) => order[a.position] - order[b.position] || a.name.localeCompare(b.name, 'pt-BR')
    );
  }, [squad]);

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div
        className="modal profile-modal"
        onClick={event => event.stopPropagation()}
        style={team ? ({
          '--team-primary': team.primaryColor || '#f2d34f',
          '--team-secondary': team.secondaryColor || '#f2d34f'
        } as React.CSSProperties) : undefined}
      >
        <header className="profile-head">
          <div>
            <h2 className="profile-name">{team?.name ?? 'Carregando…'}</h2>
            {team && (
              <p className="profile-role">
                <span className="profile-position">{team.shortName}</span>
                <span className="profile-age">Força {team.rating}</span>
                <span className="profile-age">{squad.length} jogadores</span>
              </p>
            )}
          </div>
          <button className="modal-close" onClick={onClose} aria-label="Fechar">×</button>
        </header>

        {team && (
          <div className="club-colors">
            <span className="club-swatch" style={{ background: team.primaryColor || '#f2d34f' }} />
            <span className="club-swatch" style={{ background: team.secondaryColor || '#f2d34f' }} />
          </div>
        )}

        {error && <p className="competition" style={{ color: 'var(--danger)' }}>{error}</p>}

        <section className="profile-history">
          {byPosition.length === 0 ? (
            <p className="league-empty">Nenhum jogador no elenco.</p>
          ) : (
            <table className="history-table">
              <thead>
                <tr>
                  <th>Posição</th>
                  <th>Jogador</th>
                  <th>Idade</th>
                  <th>Energia</th>
                </tr>
              </thead>
              <tbody>
                {byPosition.map(player => (
                  <tr key={player.id} className="history-row">
                    <td>{positionLabel(player.position)}</td>
                    <td>
                      <PlayerName playerId={player.id}>{player.name}</PlayerName>
                    </td>
                    <td>{player.age}</td>
                    <td>{player.energy}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>

        <footer className="profile-footer">
          <button
            className="btn"
            onClick={() => {
              onClose();
              navigate(`/team/${teamId}`);
            }}
          >
            Ver elenco completo
          </button>
        </footer>
      </div>
    </div>
  );
};

export default TeamProfileModal;
