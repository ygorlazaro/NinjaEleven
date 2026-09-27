import React, { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { SeasonApi, TeamApi } from '@/api';
import type { SquadPlayerDto, TeamDto, TeamMatchRecordDto } from '@/types';
import { ClubName } from '@/components/Common/Names';
import ClubSquadTable from '@/components/Club/ClubSquadTable';
import FormRun, { formOf } from '@/components/Club/FormRun';
import { useClubWindow } from '@/services/clubColors';

interface TeamProfileModalProps {
  teamId: string;
  onClose: () => void;
}

/** How many of the club's matches the form guide shows. The backend defaults to this too. */
const FORM_GUIDE_LENGTH = 10;

/** What a match meant for the club, and the one word that says it beside the colour. */
type Form = 'win' | 'draw' | 'loss';

const FORM_LABEL: Record<Form, string> = { win: 'V', draw: 'E', loss: 'D' };

/**
 * A club, the way a manager looks one up: what it is called, what it wears, who plays
 * for it and what those men are, and how the club has been doing.
 *
 * It is a modal because a club is looked up from inside something else — a scoreline, a
 * sentence in the feed, a player's profile — and the manager wants to see the club and go
 * back to where they were, not to be taken somewhere else entirely.
 */
const TeamProfileModal: React.FC<TeamProfileModalProps> = ({ teamId, onClose }) => {
  const navigate = useNavigate();

  const [team, setTeam] = useState<TeamDto | null>(null);
  const [squad, setSquad] = useState<SquadPlayerDto[]>([]);
  const [matches, setMatches] = useState<TeamMatchRecordDto[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      const [found, season] = await Promise.all([TeamApi.get(teamId), SeasonApi.current()]);

      // The squad and the form guide are two more questions about the same club, so they
      // travel together rather than one after the other: a manager opening a card is
      // looking for the whole club, and drawing it in two rounds of waiting reads as two.
      const [players, form] = await Promise.all([
        TeamApi.getSquad(teamId, season.id),
        TeamApi.getMatches(teamId, FORM_GUIDE_LENGTH),
      ]);

      if (cancelled) return;
      setTeam(found);
      setSquad(players);
      setMatches(form);
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

  const clubWindow = useClubWindow(team);

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div
        className="modal profile-modal club-modal"
        onClick={event => event.stopPropagation()}
        style={clubWindow}
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

        <ClubSquadTable squad={squad} />

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
            <ul className="form-list">
              {matches.map(record => {
                const form = formOf(record);

                return (
                  <li key={record.matchId} className={`form-item form-${form}`}>
                    <span className="form-result" title={FORM_LABEL[form]}>{FORM_LABEL[form]}</span>
                    <span className="form-score">
                      {record.goalsFor} <span className="form-sep">x</span> {record.goalsAgainst}
                    </span>
                    <span className="form-opponent">
                      {record.isHome ? 'vs' : 'fora'}{' '}
                      {/* A name is a door when we know which door. A backend that has not
                          sent the id yet leaves text rather than a button wired to nothing,
                          because a link that opens a broken card is worse than plain text. */}
                      {record.opponentTeamId ? (
                        <ClubName teamId={record.opponentTeamId}>{record.opponentName}</ClubName>
                      ) : (
                        record.opponentName
                      )}
                    </span>
                    <span className="form-round">R{record.roundNumber}</span>
                  </li>
                );
              })}
            </ul>
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
