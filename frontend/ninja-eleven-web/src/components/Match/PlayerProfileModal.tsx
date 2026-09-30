import React, { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { PlayerApi, SeasonApi } from '@/api';
import type { PlayerProfileDto } from '@/types';
import { positionLabel, attributeBarWidth } from '@/services/formatters';
import { formatLimo } from '@/services/limo';
import { PlayerFace } from '@/components/Common/PlayerFace';
import StarRating from '@/components/Common/StarRating';

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
 * The bar behind it is the same value the engine reads, on the same 1..100 scale, so a 15
 * here and a 15 on the team sheet are the same fifteen. It used to divide by twenty, which
 * was the scale the attributes left behind.
 */
const AttributeBox: React.FC<{ label: string; value: number }> = ({ label, value }) => (
  <div className="attr-box">
    <span className="attr-value">{value}</span>
    <span className="attr-bar" aria-hidden="true">
      <span className="attr-bar-fill" style={{ width: attributeBarWidth(value) }} />
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

    // The season is asked for first and the profile is asked for with it, because a profile
    // without a season has no season state to read and answers with a salary of zero and a
    // price of nothing: the card would look like a card about a player nobody would sign.
    // Two calls in sequence rather than one, because the second depends on the answer of the
    // first and a card that loaded without its money would be a card half loaded.
    const load = async () => {
      try {
        const season = await SeasonApi.current();
        if (cancelled) return;

        setSeasonId(season.id);

        const loaded = await PlayerApi.getProfile(playerId, season.id);
        if (cancelled) return;

        setProfile(loaded);
      } catch (err) {
        console.error('Failed to load the player profile:', err);
        if (!cancelled) setError('Não foi possível carregar o perfil do jogador.');
      }
    };

    load();

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
          {/*
            The face comes first, before the name. It is how a manager recognises the man he
            clicked on among twenty-three of them, and a name is a label he has to read while
            a face is a thing he already knows. A player with no face renders nothing here,
            so the header looks exactly as it did before — the face is not allowed to cost a
            name its place.
          */}
          <PlayerFace face={profile.face} size={92} />
          <div className="profile-id">
            {/* The rating beside the name, and not inside the role line under it: what a
                manager looks for when he clicks a man is what the man is, and it should be
                in the same glance as the name rather than a line below. */}
            <h2 className="profile-name">
              {profile.name}
              <StarRating
                stars={profile.stars}
                label="Classificação geral"
                title={`Classificação geral: ${profile.stars} de 5 estrelas`}
              />
            </h2>
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

        {/* The attributes in a line, read across rather than down: seven numbers a manager
            compares with each other belong side by side, and a column of them is a list he
            has to walk. */}
        <section className="profile-attrs">
          {attributes.map(attribute => (
            <AttributeBox
              key={attribute.key as string}
              label={attribute.label}
              value={profile[attribute.key] as number}
            />
          ))}
        </section>

        {/* And below them the rating those attributes add up to, which is a different question
            from any of them: one speed does not make a striker, and a man with four good
            numbers and one bad one is not a four-star man. */}
        <section className="profile-stars">
          <StarRating stars={profile.stars} size="full" label="Classificação geral" />
        </section>

        {/* The money, which is the other half of a card about a player. Four numbers a
            manager negotiates with: what the man is worth, what a rival would have to pay
            for him, what this club owes him for the season, and how much of the contract is
            left to run. The price is what makes the first one mean anything — a player with
            two seasons still to run is worth what he is worth and costs a fifth more, and a
            card that showed only the worth would have a manager offering the price. */}
        {profile.contractSeasons > 0 && (
          <section className="profile-money">
            <div className="profile-money__cell">
              <span className="career-value">{formatLimo(profile.marketValue)}</span>
              <span className="career-label">Valor de mercado</span>
            </div>
            <div
              className={`profile-money__cell ${profile.isInLastSeason ? '' : 'under-contract'}`}
              title={
                profile.isInLastSeason
                  ? 'Última temporada de contrato: um rival paga o valor, sem multa'
                  : `Contrato até ${profile.contractSeasons} temporada${profile.contractSeasons > 1 ? 's' : ''}, ${profile.seasonsLeft} restante${profile.seasonsLeft > 1 ? 's' : ''}: multa de 20% para tirar o jogador`
              }
            >
              <span className="career-value">
                {formatLimo(profile.askingPrice)}
                {!profile.isInLastSeason && <span className="contract-fine">+20%</span>}
              </span>
              <span className="career-label">Preço de compra</span>
            </div>
            <div className="profile-money__cell">
              <span className="career-value">{formatLimo(profile.salary)}</span>
              <span className="career-label">Salário por temporada</span>
            </div>
            <div className="profile-money__cell">
              <span className="career-value">
                {profile.seasonsLeft}/{profile.contractSeasons}
              </span>
              <span className="career-label">
                {profile.isInLastSeason ? 'Última temporada' : 'Contrato restante'}
              </span>
            </div>
          </section>
        )}

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
