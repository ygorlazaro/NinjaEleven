import React, { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { PlayerApi, SeasonApi } from '@/api';
import type { PlayerProfileDto } from '@/types';
import { positionLabel } from '@/services/formatters';

interface PlayerProfileModalProps {
  playerId: string;
  onClose: () => void;
  /**
   * Opens a club from inside this modal. A player belongs to a club and the club name is
   * right there, so it has to lead somewhere rather than being the one dead end on screen.
   */
  onOpenTeam?: (teamId: string) => void;
}

/**
 * What the attributes are called. The boxes are the screen's whole point — seven numbers
 * a manager reads at a glance — so the two that belong to a goalkeeper only appear for a
 * goalkeeper, and a striker's card is not padded with zeroes for reflexes he will never
 * use.
 */
const OUTFIELD_ATTRIBUTES: { key: keyof PlayerProfileDto; label: string }[] = [
  { key: 'speed', label: 'Velocidade' },
  { key: 'accuracy', label: 'Finalização' },
  { key: 'dribbling', label: 'Drible' },
  { key: 'heading', label: 'Cabeceio' },
  { key: 'strength', label: 'Força' }
];

const KEEPER_ATTRIBUTES: { key: keyof PlayerProfileDto; label: string }[] = [
  { key: 'reflexes', label: 'Reflexos' },
  { key: 'goalkeeperPower', label: 'Mão' }
];

/**
 * One attribute as a number big enough to be read without glasses.
 *
 * The bar behind it is the same value the engine reads, scaled the way the engine scales
 * it, so a 15 here and a 15 on the team sheet are the same fifteen.
 */
const AttributeBox: React.FC<{ label: string; value: number }> = ({ label, value }) => (
  <div className="attr-box">
    <span className="attr-value">{value}</span>
    <span className="attr-bar" aria-hidden="true">
      <span className="attr-bar-fill" style={{ width: `${Math.min(100, (value / 20) * 100)}%` }} />
    </span>
    <span className="attr-label">{label}</span>
  </div>
);

/**
 * "14 (3) [2]": fourteen matches, three of them off the bench, two on the bench unused.
 *
 * The three are kept apart because a single number throws away the only thing a manager
 * really wants from an appearance count — whether the staff trusted this man to start,
 * and how often they left him on the bench without playing.
 */
const appearances = (line: { appearances: number; started: number; cameOn: number; benchUnused: number }) =>
  line.cameOn > 0 || line.benchUnused > 0
    ? `${line.appearances} (${line.cameOn})${line.benchUnused > 0 ? ` [${line.benchUnused}]` : ''}`
    : `${line.appearances}`;

const PlayerProfileModal: React.FC<PlayerProfileModalProps> = ({ playerId, onClose, onOpenTeam }) => {
  const navigate = useNavigate();
  const [profile, setProfile] = useState<PlayerProfileDto | null>(null);
  const [seasonId, setSeasonId] = useState<string>('');
  const [scope, setScope] = useState<'season' | 'total'>('total');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    SeasonApi.current()
      .then(season => {
        if (!cancelled) setSeasonId(season.id);
      })
      .catch(err => console.error('Failed to resolve the current season:', err));

    PlayerApi.getProfile(playerId)
      .then(loaded => {
        if (!cancelled) setProfile(loaded);
      })
      .catch(err => {
        console.error('Failed to load the player profile:', err);
        if (!cancelled) setError('Não foi possível carregar o perfil do jogador.');
      });

    return () => {
      cancelled = true;
    };
  }, [playerId]);

  // Escape closes it, and so does a click on the backdrop: a modal nobody can leave with
  // the keyboard is a modal a manager has to hunt for the close button on.
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose();
    };

    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);

  const matches = useMemo(() => {
    if (!profile) return [];

    return scope === 'season' && seasonId
      ? profile.history.filter(line => line.seasonId === seasonId)
      : profile.history;
  }, [profile, scope, seasonId]);

  const totals = scope === 'season' && seasonId ? profile?.season : profile?.total;

  if (error) {
    return (
      <div className="modal-backdrop" onClick={onClose}>
        <div className="modal profile-modal" onClick={event => event.stopPropagation()}>
          <p className="competition" style={{ color: 'var(--danger)' }}>{error}</p>
          <button className="btn" onClick={onClose}>Fechar</button>
        </div>
      </div>
    );
  }

  if (!profile) {
    return (
      <div className="modal-backdrop" onClick={onClose}>
        <div className="modal profile-modal" onClick={event => event.stopPropagation()}>
          <p className="competition">Carregando…</p>
        </div>
      </div>
    );
  }

  const isKeeper = profile.position === 'GK';
  const attributes = isKeeper
    ? [...KEEPER_ATTRIBUTES, ...OUTFIELD_ATTRIBUTES.filter(attribute => attribute.key !== 'strength')]
    : OUTFIELD_ATTRIBUTES;

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal profile-modal" onClick={event => event.stopPropagation()}>
        <header className="profile-head">
          <div>
            <h2 className="profile-name">{profile.name}</h2>
            <p className="profile-role">
              <span className="profile-position">{positionLabel(profile.position)}</span>
              <span className="profile-age">{profile.age} anos</span>
              {profile.teamId && (
                <button
                  className="profile-team link"
                  onClick={() => profile.teamId && onOpenTeam?.(profile.teamId)}
                >
                  {profile.teamName}
                </button>
              )}
            </p>
          </div>
          <button className="modal-close" onClick={onClose} aria-label="Fechar">×</button>
        </header>

        {profile.injury !== 'None' && (
          <p className="profile-injury">
            {profile.injury === 'Grave' ? 'Lesão grave' : 'Lesão leve'}
            {profile.injuryMatchesRemaining > 0 && ` (${profile.injuryMatchesRemaining})`}
          </p>
        )}

        <section className="profile-attrs">
          {attributes.map(attribute => (
            <AttributeBox
              key={attribute.key as string}
              label={attribute.label}
              value={profile[attribute.key] as number}
            />
          ))}
        </section>

        <section className="profile-totals">
          <div className="segmented">
            <button
              className={`segmented-item ${scope === 'season' ? 'active' : ''}`}
              onClick={() => setScope('season')}
            >
              Temporada
            </button>
            <button
              className={`segmented-item ${scope === 'total' ? 'active' : ''}`}
              onClick={() => setScope('total')}
            >
              Total
            </button>
          </div>

          {totals && (
            <div className="career-line">
              <div className="career-cell">
                <span className="career-value">{appearances(totals)}</span>
                <span className="career-label">Jogos (entradas)</span>
              </div>
              <div className="career-cell">
                <span className="career-value">{totals.goals}</span>
                <span className="career-label">Gols</span>
              </div>
              {isKeeper && (
                <div className="career-cell">
                  <span className="career-value">{totals.saves}</span>
                  <span className="career-label">Defesas</span>
                </div>
              )}
              <div className="career-cell">
                <span className="career-value">{totals.injuries}</span>
                <span className="career-label">Lesões</span>
              </div>
            </div>
          )}
        </section>

        <section className="profile-history">
          {matches.length === 0 ? (
            <p className="league-empty">Nenhuma partida registrada.</p>
          ) : (
            <table className="history-table">
              <thead>
                <tr>
                  <th>Rod.</th>
                  <th>Jogo</th>
                  <th>Placar</th>
                  <th>J</th>
                  <th>G</th>
                  {isKeeper && <th>Def</th>}
                  <th>Les</th>
                </tr>
              </thead>
              <tbody>
                {matches.map(line => {
                  const goalsFor = line.isHome ? line.homeGoals : line.awayGoals;
                  const goalsAgainst = line.isHome ? line.awayGoals : line.homeGoals;
                  const result = goalsFor > goalsAgainst ? 'v' : goalsFor < goalsAgainst ? 'd' : 'e';

                  return (
                    <tr
                      key={line.matchId}
                      className={`history-row result-${result} ${line.wasOnBenchUnused ? 'bench-unused' : ''}`}
                      onClick={() => navigate(`/match/${line.matchId}`)}
                    >
                      <td>{line.roundNumber}</td>
                      <td>
                        {line.isHome ? 'x ' : 'x '}
                        {line.opponentName}
                        {line.started ? <span className="history-note"> (titular)</span> : ''}
                        {line.cameOn && <span className="history-note"> (entrou)</span>}
                        {line.subbedOff && <span className="history-note"> (saiu)</span>}
                        {line.wasOnBenchUnused && <span className="history-note bench-unused-badge"> (banco)</span>}
                      </td>
                      <td className="history-score">{goalsFor} x {goalsAgainst}</td>
                      <td>{line.started ? 'T' : line.cameOn ? 'E' : line.wasOnBenchUnused ? 'B' : 'J'}</td>
                      <td>{line.goals > 0 ? line.goals : ''}</td>
                      {isKeeper && <td>{line.saves > 0 ? line.saves : ''}</td>}
                      <td>{line.wasInjured ? (line.injuredOff ? 'saiu' : 'leve') : ''}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </section>
      </div>
    </div>
  );
};

export default PlayerProfileModal;
