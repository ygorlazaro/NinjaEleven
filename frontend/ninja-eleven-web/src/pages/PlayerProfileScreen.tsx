import React, { useEffect, useMemo, useState } from 'react';
import { useParams } from 'react-router-dom';
import { PlayerApi, SeasonApi, TeamApi, TransferApi } from '@/api';
import { useGameState } from '@/state';
import { useOffer } from '@/state/OfferProvider';
import type { PlayerProfileDto } from '@/types';
import { positionLabel } from '@/services/formatters';
import { formatLimo } from '@/services/limo';
import { starsToString, attributeBarWidth, attributeToneClass, matchRatingClass, matchRatingText } from '@/services/formatters';
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
              {/* The shirt he wears for this club, beside his name rather than in a line of
                  its own. A manager looking a striker up wants to know the number he will be
                  asked for on Saturday in the same glance as the name, and the number is a
                  fact about the contract — so a man who has just moved clubs shows the new
                  one, which is the whole reason it does not live on the player. */}
              {profile.shirtNumber != null && (
                <span
                  className="shirt-badge"
                  title={`Número ${profile.shirtNumber} no ${profile.teamName}`}
                >
                  <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true">
                    <path
                      fill="currentColor"
                      d="M9 2 4 4v5c0 1.4.8 2.6 2 3.2V22h12v-9.8c1.2-.6 2-1.8 2-3.2V4l-5-2-1.2 1.6a4.4 4.4 0 0 1-5.6 0L9 2Z"
                    />
                  </svg>
                  <span className="shirt-badge__number">{profile.shirtNumber}</span>
                </span>
              )}
              {profile.injury !== 'None' && <span className="injury-badge" title={profile.injury === 'Grave' ? 'Lesão grave' : 'Lesão leve'}>🩹</span>}
              {profile.retiring && (
                <span className="retiring-badge" title="Aposentadoria declarada ao final da temporada">🏁</span>
              )}
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
                  : `Contrato até ${profile.contractSeasons} temporada${profile.contractSeasons > 1 ? 's' : ''}, ${profile.seasonsLeft} restante${profile.seasonsLeft > 1 ? 's' : ''}. Rescindir agora custa ${formatLimo(profile.releaseCost)}.`
              }
            >
              <span className="career-value">
                {formatLimo(profile.askingPrice)}
                {/* The settlement, not a percentage. "Metade do que o clube ainda deve" is the
                    rule, and it was printed here as a flat +20% that the engine has never
                    charged — a number a manager budgets against. */}
                {!profile.isInLastSeason && (
                  <span className="contract-fine" title="Custo de rescindir o contrato agora">
                    {formatLimo(profile.releaseCost)}
                  </span>
                )}
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

        {profile.retiring && (
          <section className="profile-retirement-warning">
            <span className="retiring-badge">🏁</span>
            <strong>{profile.name}</strong> anunciou que este é o último ano de carreira.
            O contrato não será renovado ao final da temporada.
          </section>
        )}

        {profile.isInLastSeason && profile.contractSeasons > 0 && profile.teamId && (
          <section className="profile-contract-actions">
            {!profile.retiring && (
              <button
                className="ctrl renew"
                onClick={async () => {
                  const seasons = window.prompt(
                    `Renovar ${profile.name} por quantas temporadas?\n\n` +
                    `Nova wage por temporada: ${formatLimo(profile.wageOnRenewal ?? 0)}\n` +
                    `Entre 1 e 5 temporadas.`,
                    '3'
                  );
                  if (!seasons) return;
                  const n = parseInt(seasons, 10);
                  if (isNaN(n) || n < 1 || n > 5) {
                    window.alert('Escolha entre 1 e 5 temporadas.');
                    return;
                  }
                  try {
                    await TeamApi.renewContract(profile.teamId!, {
                      playerId: profile.playerId,
                      seasons: n,
                    });
                    window.location.reload();
                  } catch (err: unknown) {
                    const msg = err instanceof Error ? err.message : 'Não foi possível renovar o contrato.';
                    window.alert(msg);
                  }
                }}
                title="Renovar o contrato do jogador"
              >
                Renovar contrato ({formatLimo(profile.wageOnRenewal ?? 0)}/temp.)
              </button>
            )}
            {selectedTeamId && profile.teamId === selectedTeamId && (
              <button
                className="ctrl release"
                onClick={async () => {
                  if (!window.confirm(`Rescindir o contrato de ${profile.name}?`)) return;
                  try {
                    const result = await TransferApi.release(profile.playerId, profile.teamId!);
                    window.alert(
                      `${result.playerName} foi dispensado por ${formatLimo(result.releaseCost)}.` +
                      (result.withdrawnOffers > 0
                        ? `, e ${result.withdrawnOffers} proposta(s) pela venda dele caíram.`
                        : '.')
                    );
                    window.location.reload();
                  } catch (err: unknown) {
                    const msg = err instanceof Error ? err.message : 'Não foi possível rescindir o contrato.';
                    window.alert(msg);
                  }
                }}
                title="Rescindir o contrato e pagar a multa"
              >
                Rescindir
              </button>
            )}
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
              <div className="career-cell">
                <span className="career-value">{totals.assists}</span>
                <span className="career-label">Assistências</span>
              </div>
              {/* The average of the matches he was actually rated for, and not of every match
                  he was on the sheet for. A four-minute substitute is a cameo; averaging him
                  in would pull this number towards six by a different amount for every
                  player, which is one figure meaning two things. */}
              <div className="career-cell">
                <span className={`career-value ${matchRatingClass(totals.averageRating)}`}>
                  {totals.averageRating !== null && totals.averageRating !== undefined
                    ? totals.averageRating.toFixed(1)
                    : '—'}
                </span>
                <span className="career-label">
                  {totals.ratedMatches > 0 ? `Nota média (${totals.ratedMatches})` : 'Nota média'}
                </span>
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

        {/*
          One row per season, in the shirt he wore for it — the question a manager signing a
          player asks, which is not "how has he been lately" but "what did a season of him
          look like". The rows come from the backend, summed from the same lines the match
          table below is drawn from, so the two tables cannot disagree about a season's goals.
          It is shown for any player's profile, not only the manager's own: the club he is
          reading is usually somebody else's.

          The heading sits outside the scroll box rather than inside it: a title that scrolls
          away with the rows is a title a manager has to scroll back to find.
        */}
        <h3 className="squad-section-title">Histórico por temporada</h3>
        <section className="profile-history">
          {profile.seasons.length === 0 ? (
            <p className="league-empty">Nenhuma temporada registrada.</p>
          ) : (
            <table className="history-table">
              <thead>
                <tr>
                  <th>Temporada</th>
                  <th>Time</th>
                  <th className="num">J</th>
                  <th className="num">G</th>
                  <th className="num" title="Passes que resultaram em gol">A</th>
                  <th className="num" title="Defesas, para goleiros">Def</th>
                  <th className="num">Ama</th>
                  <th className="num">Verm</th>
                  <th className="num">Les</th>
                  <th className="num">V</th>
                  <th className="num">E</th>
                  <th className="num">D</th>
                </tr>
              </thead>
              <tbody>
                {profile.seasons.map(season => (
                  <tr key={`${season.seasonId ?? 'sem-temporada'}-${season.teamId}`} className="history-row">
                    <td className="form-season">{season.seasonName || '—'}</td>
                    <td className="form-opponent">
                      <ClubName teamId={season.teamId}>{season.teamName || '—'}</ClubName>
                    </td>
                    <td className="num">{appearances(season.line)}</td>
                    <td className="num">{season.line.goals}</td>
                    <td className="num">{season.line.assists}</td>
                    {/* A keeper's saves and nobody else's. The column is there for the whole
                        table rather than only on a keeper's row, so the grid the outfielders
                        are read on does not change shape halfway down. */}
                    <td className="num">{isKeeper ? season.line.saves : '—'}</td>
                    <td className="num">{season.line.yellowCards}</td>
                    <td className="num">{season.line.redCards}</td>
                    <td className="num">{season.line.injuries}</td>
                    <td className="num">{season.wins}</td>
                    <td className="num">{season.draws}</td>
                    <td className="num">{season.losses}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>

        <h3 className="squad-section-title">Partidas</h3>
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
                    <th title="Passes que resultaram em gol">A</th>
                    {isKeeper && <th>Def</th>}
                    <th>Nota</th>
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
                        <td>{line.assists > 0 ? line.assists : ''}</td>
                        {isKeeper && <td>{line.saves > 0 ? line.saves : ''}</td>}
                        {/* A dash rather than a blank, and a dash rather than a number for a
                            man who did not play five minutes: he has a cameo on this row and
                            not a match, and "—" says that where a zero would say he was
                            terrible. */}
                        <td className={matchRatingClass(line.rating, line.ratingBand)}>
                          {matchRatingText(line.rating)}
                        </td>
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