import React, { useEffect, useMemo, useState } from 'react';
import { useParams } from 'react-router-dom';
import { PlayerApi, SeasonApi } from '@/api';
import { useGameState } from '@/state';
import { useOffer } from '@/state/OfferProvider';
import type { PlayerProfileDto } from '@/types';
import { positionLabel } from '@/services/formatters';
import { formatLimo } from '@/services/limo';
import { starsToString, attributeBarWidth, attributeToneClass } from '@/services/formatters';
import { PlayerFace } from '@/components/Common/PlayerFace';
import StarRating from '@/components/Common/StarRating';
import ClubCrest from '@/components/Club/ClubCrest';
import { ClubName } from '@/components/Common/Names';

/**
 * An attribute as the card draws it: the raw reading, the stars the backend read off it, and
 * the abbreviation the column carries. `starsKey` is named rather than derived because the
 * star belongs to the backend — deriving it here is the bug this list was rebuilt to remove.
 */
type AttributeEntry = {
  key: 'speed' | 'accuracy' | 'dribbling' | 'heading' | 'strength' | 'reflexes' | 'goalkeeperPower';
  starsKey: 'speedStars' | 'accuracyStars' | 'dribblingStars' | 'headingStars' | 'strengthStars' | 'reflexesStars' | 'goalkeeperPowerStars';
  label: string;
};

const OUTFIELD_ATTRIBUTES: AttributeEntry[] = [
  { key: 'speed', starsKey: 'speedStars', label: 'Vel' },
  { key: 'accuracy', starsKey: 'accuracyStars', label: 'Fin' },
  { key: 'dribbling', starsKey: 'dribblingStars', label: 'Dri' },
  { key: 'heading', starsKey: 'headingStars', label: 'Cab' },
  { key: 'strength', starsKey: 'strengthStars', label: 'For' }
];

const KEEPER_ATTRIBUTES: AttributeEntry[] = [
  { key: 'reflexes', starsKey: 'reflexesStars', label: 'Ref' },
  { key: 'goalkeeperPower', starsKey: 'goalkeeperPowerStars', label: 'Mão' }
];

const appearances = (line: { appearances: number; started: number; cameOn: number; benchUnused: number }) =>
  line.cameOn > 0 || line.benchUnused > 0
    ? `${line.appearances} (${line.cameOn})${line.benchUnused > 0 ? ` [${line.benchUnused}]` : ''}`
    : `${line.appearances}`;

const PAGESIZE = 20;

const PlayerProfileScreen: React.FC = () => {
  const { playerId } = useParams<{ playerId: string }>();
  const selectedTeamId = useGameState((s) => s.selectedTeam?.id);
  const { openOffer } = useOffer();
  const [profile, setProfile] = useState<PlayerProfileDto | null>(null);
  const [seasonId, setSeasonId] = useState<string>('');
  const [scope, setScope] = useState<'season' | 'total'>('total');
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(0);
  const [opponentFilter, setOpponentFilter] = useState('');
  const [teamFilter, setTeamFilter] = useState('');

  useEffect(() => {
    if (!playerId) return;

    let cancelled = false;

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

  const allMatches = useMemo(() => {
    if (!profile) return [];

    return scope === 'season' && seasonId
      ? profile.history.filter(line => line.seasonId === seasonId)
      : profile.history;
  }, [profile, scope, seasonId]);

  const totals = scope === 'season' && seasonId ? profile?.season : profile?.total;

  const filteredMatches = useMemo(() => {
    if (!allMatches.length) return [];
    return allMatches.filter(line => {
      const oppMatch = !opponentFilter || line.opponentName.toLowerCase().includes(opponentFilter.toLowerCase());
      const teamMatch = !teamFilter || line.teamName?.toLowerCase().includes(teamFilter.toLowerCase());
      return oppMatch && teamMatch;
    });
  }, [allMatches, opponentFilter, teamFilter]);

  const totalPages = Math.ceil(filteredMatches.length / PAGESIZE);
  const paginatedMatches = useMemo(() => {
    return filteredMatches.slice(page * PAGESIZE, (page + 1) * PAGESIZE);
  }, [filteredMatches, page]);

  const isKeeper = profile?.position === 'GK';

  const attributes = useMemo(() => {
    if (!profile) return [];
    if (isKeeper) {
      return [...KEEPER_ATTRIBUTES, ...OUTFIELD_ATTRIBUTES.filter(a => a.key !== 'strength')];
    }
    return OUTFIELD_ATTRIBUTES;
  }, [profile, isKeeper]);

  const stats = useMemo(() => {
    if (!totals) return null;
    // Compute wins/draws/losses from all matches in scope (not just filtered)
    let wins = 0, draws = 0, losses = 0;
    allMatches.forEach(line => {
      const gf = line.isHome ? line.homeGoals : line.awayGoals;
      const ga = line.isHome ? line.awayGoals : line.homeGoals;
      if (gf > ga) wins++;
      else if (gf < ga) losses++;
      else draws++;
    });
    return { wins, draws, losses, goals: totals.goals, saves: totals.saves, yellowCards: totals.yellowCards, redCards: totals.redCards };
  }, [totals, allMatches]);

  if (error) {
    return (
      <div className="app">
        <div className="card profile-page">
          <p className="competition" style={{ color: 'var(--danger)' }}>{error}</p>
          <button className="ctrl" onClick={() => window.history.back()}>Voltar</button>
        </div>
      </div>
    );
  }

  if (!profile) {
    return (
      <div className="app">
        <div className="card profile-page">
          <p className="competition">Carregando…</p>
        </div>
      </div>
    );
  }

  return (
    <div className="app">
      <div className="card profile-page">
        <header className="profile-head">
          <PlayerFace face={profile.face} size={92} />
          <div className="profile-id">
            <h2 className="profile-name">
              {profile.name}
              {profile.injury !== 'None' && <span className="injury-badge" title={profile.injury === 'Grave' ? 'Lesão grave' : 'Lesão leve'}>🩹</span>}
              <StarRating
                stars={profile.stars}
                label="Classificação geral"
                title={`Classificação geral: ${profile.stars} de 5 estrelas`}
              />
            </h2>
            <div className="team-header-with-crest">
              {profile.teamId && profile.teamPrimaryColor && profile.teamSecondaryColor && (
                <ClubCrest
                  primary={profile.teamPrimaryColor}
                  secondary={profile.teamSecondaryColor}
                  name={profile.teamName}
                />
              )}
              <div className="team-header-info">
                <div className="team-header-main">
                  {profile.teamId ? (
                    <ClubName teamId={profile.teamId} className="profile-team">
                      {profile.teamName}
                    </ClubName>
                  ) : (
                    <span className="profile-team">Sem clube</span>
                  )}
                  {/*
                    A profile is where a manager decides about a player, so it is where the
                    offer is offered. The card answers "who is this man" and the market answers
                    "what does he cost and who else wants him", and a manager who has just read
                    a striker's attributes should not have to go and find him in a list of eight
                    hundred to act on what he has just read.

                    The offer is a window of the game's own — the same one the market opens on the
                    same row — rather than a page to be sent to and a form to be filled in there.
                    A manager decides here, and the thing he decided is made here; "Ver no
                    mercado" is in the window for when he wants the rest of the market instead.
                  */}
                  {selectedTeamId && profile.teamId !== selectedTeamId && (
                    <button
                      type="button"
                      className="ctrl profile-offer-btn"
                      title="Abrir a proposta por este jogador"
                      onClick={() => openOffer({ playerId: profile.playerId })}
                    >
                      🔄 Fazer proposta
                    </button>
                  )}
                </div>
                <p className="profile-role">
                  <span className="profile-position">{positionLabel(profile.position)}</span>
                  <span className="profile-age">{profile.age} anos</span>
                </p>
              </div>
            </div>
          </div>
        </header>

        {profile.injury !== 'None' && (
          <p className="profile-injury">
            {profile.injury === 'Grave' ? 'Lesão grave' : 'Lesão leve'}
            {profile.injuryMatchesRemaining > 0 && ` (${profile.injuryMatchesRemaining} jogo${profile.injuryMatchesRemaining > 1 ? 's' : ''})`}
          </p>
        )}

        <section className="profile-attrs">
          {attributes.map(attribute => {
            const value = profile[attribute.key];
            const stars = profile[attribute.starsKey];
            return (
              <div key={attribute.key} className="attr-box">
                <span className={`attr-value ${attributeToneClass(value)}`}>{value}</span>
                <span className="attr-stars">{starsToString(stars)}</span>
                <span className="attr-bar" aria-hidden="true">
                  <span className="attr-bar-fill" style={{ width: attributeBarWidth(value) }} />
                </span>
                <span className="attr-label">{attribute.label}</span>
              </div>
            );
          })}
        </section>

        <section className="profile-stars">
          <StarRating stars={profile.stars} size="full" label="Classificação geral" />
        </section>

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
              onClick={() => { setScope('season'); setPage(0); }}
            >
              Temporada
            </button>
            <button
              className={`segmented-item ${scope === 'total' ? 'active' : ''}`}
              onClick={() => { setScope('total'); setPage(0); }}
            >
              Total
            </button>
          </div>

          {totals && stats && (
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
              <div className="career-cell">
                <span className="career-value">{totals.yellowCards}</span>
                <span className="career-label">Amarelos</span>
              </div>
              <div className="career-cell">
                <span className="career-value">{totals.redCards}</span>
                <span className="career-label">Vermelhos</span>
              </div>
              <div className="career-cell">
                <span className="career-value">{stats.wins}</span>
                <span className="career-label">Vitórias</span>
              </div>
              <div className="career-cell">
                <span className="career-value">{stats.draws}</span>
                <span className="career-label">Empates</span>
              </div>
              <div className="career-cell">
                <span className="career-value">{stats.losses}</span>
                <span className="career-label">Derrotas</span>
              </div>
            </div>
          )}
        </section>

        <section className="profile-history">
          <div className="profile-history__filters">
            <input
              type="text"
              placeholder="Filtrar adversário..."
              value={opponentFilter}
              onChange={e => { setOpponentFilter(e.target.value); setPage(0); }}
              className="filter-input"
            />
            <input
              type="text"
              placeholder="Filtrar time..."
              value={teamFilter}
              onChange={e => { setTeamFilter(e.target.value); setPage(0); }}
              className="filter-input"
            />
          </div>

          {filteredMatches.length === 0 ? (
            <p className="league-empty">Nenhuma partida registrada.</p>
          ) : (
            <>
              <table className="history-table">
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
                    <th>J</th>
                    <th>G</th>
                    {isKeeper && <th>Def</th>}
                    <th>Les</th>
                  </tr>
                </thead>
                <tbody>
                  {paginatedMatches.map(line => {
                    const goalsFor = line.isHome ? line.homeGoals : line.awayGoals;
                    const goalsAgainst = line.isHome ? line.awayGoals : line.homeGoals;
                    const result = goalsFor > goalsAgainst ? 'v' : goalsFor < goalsAgainst ? 'd' : 'e';

                    return (
                      <tr
                        key={line.matchId}
                        className={`history-row result-${result} ${line.wasOnBenchUnused ? 'bench-unused' : ''}`}
                        onClick={() => window.location.href = `/match/${line.matchId}`}
                      >
                        <td className="form-season">{line.seasonName || '—'}</td>
                        <td className="form-comp">{line.competitionName || '—'}</td>
                        <td className="form-phase">{line.phaseName || `Rodada ${line.roundNumber}`}</td>
                        <td className="form-venue">{line.isHome ? '🏠' : '✈️'}</td>
                        <td className="form-stadium">{line.stadiumName || '—'}</td>
                        <td className="form-opponent">
                          <span className="history-opponent">
                            {line.opponentTeamPrimaryColor && line.opponentTeamSecondaryColor && (
                              <ClubCrest
                                primary={line.opponentTeamPrimaryColor}
                                secondary={line.opponentTeamSecondaryColor}
                                name={line.opponentName}
                                className="history-opponent-crest"
                              />
                            )}
                            <span className="history-opponent__name">{line.opponentName}</span>
                          </span>
                          {line.started ? <span className="history-note"> (titular)</span> : ''}
                          {line.cameOn && <span className="history-note"> (entrou)</span>}
                          {line.subbedOff && <span className="history-note"> (saiu)</span>}
                          {line.wasOnBenchUnused && <span className="history-note bench-unused-badge"> (banco)</span>}
                        </td>
                        <td className="history-score form-score">{goalsFor} x {goalsAgainst}</td>
                        <td className="form-attendance">{line.attendance ? line.attendance.toLocaleString('pt-BR') : '—'}</td>
                        <td>{line.started ? 'T' : line.cameOn ? 'E' : line.wasOnBenchUnused ? 'B' : 'J'}</td>
                        <td>{line.goals > 0 ? line.goals : ''}</td>
                        {isKeeper && <td>{line.saves > 0 ? line.saves : ''}</td>}
                        <td>{line.wasInjured ? (line.injuredOff ? 'saiu' : 'leve') : ''}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>

              {totalPages > 1 && (
                <div className="pagination">
                  <button
                    className="pagination-btn"
                    onClick={() => setPage(p => Math.max(0, p - 1))}
                    disabled={page === 0}
                  >
                    ‹ Anterior
                  </button>
                  <span className="pagination-info">
                    Página {page + 1} de {totalPages} ({filteredMatches.length} jogos)
                  </span>
                  <button
                    className="pagination-btn"
                    onClick={() => setPage(p => Math.min(totalPages - 1, p + 1))}
                    disabled={page >= totalPages - 1}
                  >
                    Próxima ›
                  </button>
                </div>
              )}
            </>
          )}
        </section>
      </div>
    </div>
  );
};

export default PlayerProfileScreen;