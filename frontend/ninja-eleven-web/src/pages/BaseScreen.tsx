import { SeasonApi, TeamApi, PlayerApi } from '@/api';
import type { AcademyPlayerDto, SeasonDto, TeamDto, SquadTrainingQuotesDto, PlayerAttribute } from '@/types';
import React, { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { attributeToneClass, starsToString } from '@/services/formatters';
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

const SORT_KEYS = ['name', 'age', 'overallRating', 'potential', 'position', 'energy'] as const;
type SortKey = (typeof SORT_KEYS)[number];

const SORT_LABELS: Record<SortKey, string> = {
  name: 'Nome',
  age: 'Idade',
  overallRating: 'Geral',
  potential: 'Potencial',
  position: 'Posição',
  energy: 'Energia',
};

const BaseScreen: React.FC = () => {
  const { teamId: paramTeamId } = useParams<{ teamId: string }>();
  const selectedTeam = useGameState((s) => s.selectedTeam);
  const clubWindow = useClubWindow(selectedTeam);
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

  return (
    <div className="club-page" style={clubWindow}>
      {team && (
        <>
          <header className="team-header-with-crest">
            <div className="team-header__crest-wrapper">
              <ClubCrest
                crest={team.crest}
                primary={team.primaryColor}
                secondary={team.secondaryColor}
                name={team.name}
                className="team-header__crest"
              />
            </div>
            <div className="team-header__info">
              <h1 className="team-header__name">{team.name}</h1>
              <p className="team-header__section-title">Base de {seasonId ? seasons.find(s => s.id === seasonId)?.name ?? '' : ''}</p>
            </div>
          </header>
        </>
      )}

      {loading && (
        <div className="card">
          <p>Carregando a base…</p>
        </div>
      )}

      {error && (
        <div className="card">
          <p className="error">{error}</p>
        </div>
      )}

      {!loading && !error && (
        <>
          <div className="squad-controls">
            <div className="squad-controls__sort">
              <label>Ordenar:</label>
              <select
                value={sortKey}
                onChange={e => setSortKey(e.target.value as SortKey)}
              >
                {SORT_KEYS.map(key => (
                  <option key={key} value={key}>{SORT_LABELS[key]}</option>
                ))}
              </select>
              <button
                className="sort-dir-button"
                onClick={() => setSortDir(d => d === 'asc' ? 'desc' : 'asc')}
                title={sortDir === 'asc' ? 'Crescente' : 'Decrescent'}
              >
                {sortDir === 'asc' ? '↑' : '↓'}
              </button>
            </div>
          </div>

          <table className="academy-table">
            <thead>
              <tr>
                <th className="name-col">Jogador</th>
                <th className="num">Pos</th>
                <th className="num">Idade</th>
                <th className="num">Geral</th>
                <th className="num">Pot</th>
                <th className="num stars-col">★</th>
                <th className="num">Vel</th>
                <th className="num">Fin</th>
                <th className="num">Dri</th>
                <th className="num">Cab</th>
                <th className="num">For</th>
                <th className="num">Gol</th>
                <th className="num">Ref</th>
                <th className="num">Ene</th>
              <th className="num">Dev</th>
              <th className="num">Tre</th>
              <th className="action-col">Ação</th>
              </tr>
            </thead>
            <tbody>
              {sortedPlayers.length === 0 ? (
                <tr>
                  <td colSpan={16} className="empty-cell">
                    Nenhum jogador na base.
                  </td>
                </tr>
              ) : (
                sortedPlayers.map(player => (
                  <tr key={player.playerId}>
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
                    <td className="num">{player.position}</td>
                    <td className="num">{player.age}</td>
                    <td className="num">{player.overallRating}</td>
                    <td className="num">{player.potential}</td>
                    <td className="num stars-col">{starsToString(player.stars)}</td>
                    <td className={`num ${attributeToneClass(player.speed)}`}>{player.speed}</td>
                    <td className={`num ${attributeToneClass(player.accuracy)}`}>{player.accuracy}</td>
                    <td className={`num ${attributeToneClass(player.dribbling)}`}>{player.dribbling}</td>
                    <td className={`num ${attributeToneClass(player.heading)}`}>{player.heading}</td>
                    <td className={`num ${attributeToneClass(player.strength)}`}>{player.strength}</td>
                    <td className="num">{player.goalkeeperPower}</td>
                    <td className="num">{player.reflexes}</td>
                    <td className="num">{player.energy}</td>
                    <td className="num">{player.developmentRoom}</td>
                    <td className="num">
                      {trainingQuotes[player.playerId] ? (
                        <span title={`Custo da sessão: ${trainingQuotes[player.playerId].sessionFee} L$`}>
                          {trainingQuotes[player.playerId].sessionFee}
                        </span>
                      ) : '—'}
                    </td>
                    <td className="action-col">
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
                ))
              )}
            </tbody>
          </table>
        </>
      )}
    </div>
  );
};

export default BaseScreen;
