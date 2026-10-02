import { SeasonApi, TeamApi, PlayerApi } from '@/api';
import type { AcademyPlayerDto, SeasonDto, TeamDto, SquadTrainingQuotesDto, PlayerAttribute } from '@/types';
import React, { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { attributeToneClass, energyTextClass, positionLabel, starsToString } from '@/services/formatters';
import { PlayerName } from '@/components/Common/Names';
import PlayerStatusMarks from '@/components/Common/PlayerStatusMarks';
import ClubCrest from '@/components/Club/ClubCrest';
import { useClubWindow } from '@/services/clubColors';
import { useGameState } from '@/state';
import { ApiProblemError } from '@/api/client';

const POSITION_RANK: Record<string, number> = {
  GK: 0,
  DEF: 1,
  MID: 2,
  ATT: 3,
};

/**
 * The columns of the base, in the order a manager reads them, and each one a door to an order.
 *
 * The header is the order, exactly as it is on the squad: a column that can be sorted says so
 * with a pointer and a hover, and the one that is sorted says which way with an arrow. This is
 * why the dropdown that used to sit above this table is gone — two controls doing one thing is a
 * screen where the manager has to work out which of them is the real one, and the answer would
 * be whichever he found first.
 */
type AcademySortKey =
  | 'name'
  | 'position'
  | 'age'
  | 'overallRating'
  | 'potential'
  | 'stars'
  | 'speed'
  | 'accuracy'
  | 'dribbling'
  | 'heading'
  | 'strength'
  | 'goalkeeperPower'
  | 'reflexes'
  | 'energy'
  | 'developmentRoom';

const COLUMNS: { key: AcademySortKey; label: string; head?: string }[] = [
  { key: 'name', label: 'Jogador', head: 'name-col' },
  { key: 'position', label: 'Pos' },
  { key: 'age', label: 'Idade' },
  { key: 'overallRating', label: 'Geral' },
  { key: 'potential', label: 'Pot' },
  { key: 'stars', label: '★', head: 'stars-col' },
  { key: 'speed', label: 'Vel' },
  { key: 'accuracy', label: 'Fin' },
  { key: 'dribbling', label: 'Dri' },
  { key: 'heading', label: 'Cab' },
  { key: 'strength', label: 'For' },
  { key: 'goalkeeperPower', label: 'Gol' },
  { key: 'reflexes', label: 'Ref' },
  { key: 'energy', label: 'Ene' },
  { key: 'developmentRoom', label: 'Dev' },
];

type SortKey = AcademySortKey;

const BaseScreen: React.FC = () => {
  const { teamId: paramTeamId } = useParams<{ teamId: string }>();
  const selectedTeam = useGameState((s) => s.selectedTeam);
  const navigate = useNavigate();
  const teamId = paramTeamId ?? selectedTeam?.id ?? '';

  const [team, setTeam] = useState<TeamDto | null>(null);
  const [seasonId, setSeasonId] = useState<string>('');
  const [seasons, setSeasons] = useState<SeasonDto[]>([]);
  const [players, setPlayers] = useState<AcademyPlayerDto[]>([]);
  const [trainingQuotes, setTrainingQuotes] = useState<Record<string, { sessionFee: number; attributes: { attribute: PlayerAttribute; value: number; cost: number | null }[] }>>({});
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sortKey, setSortKey] = useState<SortKey>('name');
  const [sortDir, setSortDir] = useState<'asc' | 'desc'>('asc');
  const [promotingPlayerId, setPromotingPlayerId] = useState<string | null>(null);
  const [trainingPlayerId, setTrainingPlayerId] = useState<string | null>(null);
  const [trainingAttribute, setTrainingAttribute] = useState<PlayerAttribute | null>(null);

  // The base is read in the club's own colours, and they are the colours of the club on screen
  // rather than of the club in the state: the two are the same club except when the manager has
  // gone looking at somebody else's, and a base painted in one club's wash while its name says
  // another is a screen contradicting itself.
  const clubWindow = useClubWindow(team);

  useEffect(() => {
    if (!teamId) {
      return;
    }

    let alive = true;

    TeamApi.get(teamId)
      .then(loaded => {
        if (alive) setTeam(loaded);
      })
      .catch(() => {
        if (alive) setError('Não foi possível carregar o clube.');
      });

    return () => {
      alive = false;
    };
  }, [teamId]);

  useEffect(() => {
    let alive = true;

    SeasonApi.list()
      .then(list => {
        if (!alive) return;
        setSeasons(list);
        const current =
          list.find(season => season.status === 'InProgress') ?? list[list.length - 1];
        if (current) {
          setSeasonId(current.id);
        }
      })
      .catch(() => {
        if (alive) setError('Não foi possível carregar as temporadas.');
      });

    return () => {
      alive = false;
    };
  }, []);

  useEffect(() => {
    if (!teamId || !seasonId) {
      return;
    }

    let alive = true;
    setLoading(true);
    setError(null);

    TeamApi.getAcademy(teamId, seasonId)
      .then(rows => {
        if (alive) setPlayers(rows);
      })
      .catch(() => {
        if (alive) setError('Não foi possível carregar a base.');
      });

    TeamApi.training(teamId, seasonId)
      .then(sheet => {
        if (alive) {
          const map: Record<string, { sessionFee: number; attributes: { attribute: PlayerAttribute; value: number; cost: number | null }[] }> = {};
          for (const q of sheet.players) {
            if (q.isAcademyPlayer) {
              map[q.playerId] = {
                sessionFee: q.sessionFee,
                attributes: q.attributes.map(a => ({
                  attribute: a.attribute as PlayerAttribute,
                  value: a.value,
                  cost: a.cost,
                })),
              };
            }
          }
          setTrainingQuotes(map);
        }
      })
      .catch(() => {
        // Non-fatal: training quotes are informational.
      })
      .finally(() => {
        if (alive) setLoading(false);
      });

    return () => {
      alive = false;
    };
  }, [teamId, seasonId]);

  const sortedPlayers = useMemo(() => {
    return [...players].sort((a, b) => {
      const aVal = a[sortKey];
      const bVal = b[sortKey];
      const aPos = typeof aVal === 'string' ? POSITION_RANK[aVal] ?? 99 : aVal;
      const bPos = typeof bVal === 'string' ? POSITION_RANK[bVal] ?? 99 : bVal;
      if (aPos < bPos) return sortDir === 'asc' ? -1 : 1;
      if (aPos > bPos) return sortDir === 'asc' ? 1 : -1;
      return 0;
    });
  }, [players, sortKey, sortDir]);

  /**
   * Ordering by a column, the way the squad's headers do it: a column that is already the order
   * flips it, and a column that is not becomes the order in the direction the manager was last
   * reading by — so moving from a name to an age does not silently reverse itself on him.
   */
  const toggleSort = (key: SortKey) => {
    if (key === sortKey) {
      setSortDir(d => (d === 'asc' ? 'desc' : 'asc'));
    } else {
      setSortKey(key);
      setSortDir('asc');
    }
  };

  const handlePromote = async (playerId: string) => {
    if (!teamId || !seasonId) {
      return;
    }

    setPromotingPlayerId(playerId);

    try {
      await TeamApi.promoteAcademyPlayer(teamId, playerId, seasonId);
      setPlayers(prev => prev.filter(p => p.playerId !== playerId));
    } catch (err) {
      if (err instanceof ApiProblemError) {
        setError(err.message);
      } else {
        setError('Não foi possível promover o jogador.');
      }
    } finally {
      setPromotingPlayerId(null);
    }
  };

  const handleTrain = async (playerId: string, attribute: PlayerAttribute) => {
    if (!seasonId) {
      return;
    }

    setTrainingPlayerId(playerId);
    setTrainingAttribute(attribute);

    try {
      await PlayerApi.train(playerId, attribute, seasonId);
    } catch (err) {
      if (err instanceof ApiProblemError) {
        setError(err.message);
      } else {
        setError('Não foi possível treinar o jogador.');
      }
    } finally {
      setTrainingPlayerId(null);
      setTrainingAttribute(null);
    }
  };

  const getTrainableAttributes = (playerId: string) => {
    const q = trainingQuotes[playerId];
    if (!q) return [];
    return q.attributes.filter(a => a.cost !== null);
  };

  if (!selectedTeam) {
    return (
      <div className="app">
        <div className="card match-header club-modal">
          <h2 className="profile-name">Base</h2>
          <p className="competition">Escolha um clube para ver a base.</p>
        </div>
      </div>
    );
  }

  const seasonName = seasons.find(season => season.id === seasonId)?.name;

  return (
    /* The same frame the club's own screen is drawn in, wrapper for wrapper. The base is a read
       of the same club by the same manager, and he moves between the two constantly — from the
       squad to the men coming up, and back when one of them is ready. A screen that reinvents the
       frame puts the crest, the name and the club's colours in a different place on every page,
       and a manager crossing between them has to find the club again on each.
       So this is `.app` → `.team-view-overlay` → `.card.team-view-card.club-modal`, the same three
       wrappers the squad screen uses, and the header below is its header rather than a second
       kind of header that happens to also have a crest in it. */
    <div className="app">
      <div className="team-view-overlay">
        <div className="card team-view-card club-modal" style={clubWindow}>
          {team && (
            <div className="squad-head team-view-summary">
              <div className="team-header-with-crest">
                {/* No className: the squad screen's crest takes its forty pixels from
                    `.team-header-with-crest .club-crest`, which is the rule that makes the two
                    screens' badges the same size. A class of its own here is how the two
                    crests drift apart. */}
                <ClubCrest
                  crest={team.crest}
                  primary={team.primaryColor || '#f2d34f'}
                  secondary={team.secondaryColor || '#f2d34f'}
                  name={team.name}
                />
                <div className="team-header-info">
                  <div className="team-header-main">
                    <h2 className="profile-name">{team.name}</h2>
                  </div>
                  {/* The same hint line as the squad's, saying the same kind of thing about a
                      list of men: how many there are and which season they belong to. */}
                  <p className="squad-hint">
                    {players.length} {players.length === 1 ? 'jogador na base' : 'jogadores na base'}
                    {seasonName ? ` • ${seasonName}` : ''}
                  </p>
                </div>
              </div>
            </div>
          )}

          {team && (
            <div className="club-colors">
              <span className="club-swatch" style={{ background: team.primaryColor || '#f2d34f' }} />
              <span className="club-swatch" style={{ background: team.secondaryColor || '#f2d34f' }} />
            </div>
          )}

          {/* Loading and error are the squad screen's own lines rather than cards of their own: a
              panel inside the club's card is a box inside a box, and the manager is reading the
              same club either way — only the content under the crest is missing for a moment. */}
          {loading && <p className="league-empty">Carregando a base…</p>}
          {error && <p className="squad-hint" style={{ color: 'var(--danger)' }}>{error}</p>}

          {!loading && !error && (
            /* An empty base is a sentence rather than a row. A row would have to span seventeen
               columns of a table whose headers are the only thing telling a manager what a youth
               is, and a header row above an empty body is a shape that looks like a page that
               failed to load rather than a club with nobody coming up. The squad's empty state is
               the same sentence for the same reason. */
            sortedPlayers.length === 0 ? (
              <p className="league-empty">Nenhum jogador na base.</p>
            ) : (
              /* The same table the squad's is: `.history-table.club-squad-table` inside the same
                 `.profile-history` box, so the two tables on two screens are one table. The rows
                 carry the same marks — a youth carrying a knock is dimmed as unavailable rather
                 than shown in the same ink as one who is fit, and the two goalkeeper columns print
                 an em dash for an outfielder instead of the number he will never use, which is
                 what the squad's does with the same two columns. */
              <div className="profile-history">
              <table className="history-table club-squad-table">
                <thead>
                  <tr>
                    {COLUMNS.map(column => (
                      <th
                        key={column.key}
                        className={[
                          column.head ?? 'num',
                          'sort-head',
                          sortKey === column.key ? 'sorted' : ''
                        ]
                          .filter(Boolean)
                          .join(' ')}
                        onClick={() => toggleSort(column.key)}
                        title={`Ordenar por ${column.label}`}
                      >
                        {column.label}
                        {sortKey === column.key && (
                          <span className="sort-arrow">{sortDir === 'asc' ? '▲' : '▼'}</span>
                        )}
                      </th>
                    ))}
                    <th className="num">Tre</th>
                    {/* The two decisions are not a column a manager sorts by, so the header says
                        what they are and nothing more — the same reason the squad's action column
                        carries no arrow. */}
                    <th className="actions-col">Ações</th>
                  </tr>
                </thead>
                <tbody>
                  {sortedPlayers.map(player => (
                    <tr
                      key={player.playerId}
                      className={[
                        'history-row',
                        player.injury !== 'None' ? 'injured' : '',
                        player.isAvailable ? '' : 'unavailable'
                      ]
                        .filter(Boolean)
                        .join(' ')}
                    >
                      <td className="squad-name">
                        <PlayerName playerId={player.playerId}>{player.name}</PlayerName>
                        {/* One rule for the three marks a man's name can carry, so a youth
                            carrying a knock is never printed as "Suspenso" for having been
                            unavailable — the red card he was never shown. */}
                        <PlayerStatusMarks
                          retiring={player.retiring}
                          injury={player.injury}
                          injuryMatchesRemaining={player.injuryMatchesRemaining}
                          suspensionMatches={player.suspensionMatches}
                        />
                      </td>
                      <td className="num">{positionLabel(player.position)}</td>
                      <td className="num">{player.age}</td>
                      <td className="num">{player.overallRating}</td>
                      <td className="num">{player.potential}</td>
                      <td className="num stars-col">{starsToString(player.stars)}</td>
                      <td className={`num ${attributeToneClass(player.speed)}`}>{player.speed}</td>
                      <td className={`num ${attributeToneClass(player.accuracy)}`}>{player.accuracy}</td>
                      <td className={`num ${attributeToneClass(player.dribbling)}`}>{player.dribbling}</td>
                      <td className={`num ${attributeToneClass(player.heading)}`}>{player.heading}</td>
                      <td className={`num ${attributeToneClass(player.strength)}`}>{player.strength}</td>
                      {/* The two goalkeeper numbers are only the goalkeeper's. An outfielder's is
                          not zero — it is a rating he will never be measured on — so the cell
                          says so rather than printing a number the manager might read as one. */}
                      <td className={`num ${player.position === 'GK' ? attributeToneClass(player.goalkeeperPower) : ''}`}>
                        {player.position === 'GK' ? player.goalkeeperPower : '—'}
                      </td>
                      <td className={`num ${player.position === 'GK' ? attributeToneClass(player.reflexes) : ''}`}>
                        {player.position === 'GK' ? player.reflexes : '—'}
                      </td>
                      <td className={`num ${energyTextClass(player.energy)}`}>{player.energy}</td>
                      <td className="num">{player.developmentRoom}</td>
                      {/* What a session with this youth costs. A dash rather than a zero when the
                          backend has not quoted him, for the same reason the rest of the screen
                          prints one: a zero is a price, and this is not one yet. */}
                      <td className="num">
                        {trainingQuotes[player.playerId] ? (
                          <span title={`Custo da sessão: ${trainingQuotes[player.playerId].sessionFee} L$`}>
                            {trainingQuotes[player.playerId].sessionFee}
                          </span>
                        ) : '—'}
                      </td>
                      <td className="actions-col">
                        <div className="squad-row__actions">
                          <select
                            className="promote-button"
                            value=""
                            onChange={e => {
                              const attr = e.target.value as PlayerAttribute;
                              if (attr) handleTrain(player.playerId, attr);
                            }}
                            disabled={trainingPlayerId === player.playerId}
                            title="Treinar atributo"
                          >
                            <option value="" disabled>
                              {trainingPlayerId === player.playerId ? '…' : 'Treinar'}
                            </option>
                            {getTrainableAttributes(player.playerId).map(attr => (
                              <option key={attr.attribute} value={attr.attribute}>
                                {attr.attribute} ({attr.cost})
                              </option>
                            ))}
                          </select>
                          <button
                            className="promote-button"
                            onClick={() => handlePromote(player.playerId)}
                            disabled={promotingPlayerId === player.playerId}
                            title="Promover para a equipe principal"
                          >
                            {promotingPlayerId === player.playerId ? '…' : 'Promover'}
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            )
          )}
        </div>
      </div>
    </div>
  );
};

export default BaseScreen;
