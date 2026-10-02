import { SeasonApi, TeamApi, ManagerApi, TransferApi, PlayerApi } from '@/api';
import { ApiProblemError } from '@/api/client';
import ClubCrest from '@/components/Club/ClubCrest';
import ClubSquadTable from '@/components/Club/ClubSquadTable';
import TrainingPanel from '@/components/Club/TrainingPanel';
import SquadFilterBar, {
  applySquadFilter,
  emptyFilter,
  type SquadFilter
} from '@/components/Club/SquadFilterBar';
import FormRun, { formOf } from '@/components/Club/FormRun';
import { ClubName, PlayerName } from '@/components/Common/Names';
import DivisionTrophy from '@/components/League/DivisionTrophy';
import { useClubWindow } from '@/services/clubColors';
import { formatLimo } from '@/services/limo';
import { starsToString } from '@/services/formatters';
import { useGameState } from '@/state';
import type { ClubStandingDto, SquadPlayerDto, TeamDto, TeamMatchRecordDto, ClubTransferHistoryDto, TransferHistoryLineDto, TransferStatus, SquadTrainingQuotesDto, PlayerAttribute } from '@/types';
import React, { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';

const STATUS_COLOR: Record<TransferStatus, string> = {
  Pending: 'pending',
  Accepted: 'accepted',
  Rejected: 'rejected',
  Completed: 'completed',
  Expired: 'expired'
};

const STATUS_LABELS: Record<TransferStatus, string> = {
  Pending: 'Pendente',
  Accepted: 'Aceita',
  Rejected: 'Recusada',
  Completed: 'Concluída',
  Expired: 'Expirada'
};

/**
 * The wire name of an attribute and the field the squad row carries it in.
 *
 * The backend's enum is spelled in Pascal case because that is how enums travel; the DTO is
 * camel case because that is how JSON fields are. Both are right and neither is derivable
 * from the other by a rule a screen should be guessing at, so the eight are written out.
 * Writing it out is also what makes the compiler complain when an attribute is added and
 * this table is not — which is the moment to be told, rather than the moment a manager
 * watches a column stop moving.
 */
const ATTRIBUTE_FIELD: Partial<Record<PlayerAttribute, keyof SquadPlayerDto>> = {
  Speed: 'speed',
  Accuracy: 'accuracy',
  Dribbling: 'dribbling',
  Heading: 'heading',
  Strength: 'strength',
  GoalkeeperPower: 'goalkeeperPower',
  Reflexes: 'reflexes',
  Stamina: 'stamina'
};

const TeamViewScreen: React.FC<{ teamId?: string }> = ({ teamId: propTeamId }) => {
  const teams = useGameState((s) => s.leagueTeams);
  const selectedTeam = useGameState((s) => s.selectedTeam);
  const [team, setTeam] = useState<TeamDto | null>(null);
  const [players, setPlayers] = useState<SquadPlayerDto[]>([]);
  const [matches, setMatches] = useState<TeamMatchRecordDto[]>([]);
  const [h2hMatches, setH2hMatches] = useState<TeamMatchRecordDto[]>([]);
  const [coachName, setCoachName] = useState<string | null>(null);
  // The division the club is in this season and the line it holds there. It is the backend's
  // answer rather than something worked out here: a club's division is its enrolment for the
  // season, so a screen that guessed from the season's list of divisions would be a screen
  // printing a tier the club does not play in.
  const [standing, setStanding] = useState<ClubStandingDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busyPlayerId, setBusyPlayerId] = useState<string | null>(null);
  const [transferHistory, setTransferHistory] = useState<ClubTransferHistoryDto | null>(null);

  /**
   * The club's training sheet: every man, every attribute, and what a session on each costs.
   *
   * It is its own state and not derived from `players` because the prices are not on the
   * squad row and must not be worked out here — the cost is a product of the attribute, the
   * potential, the stamina and a curve, and a client computing it would be a second
   * implementation of a rule the backend owns.
   */
  const [filter, setFilter] = useState<SquadFilter>({ ...emptyFilter });
  const [shirtRefusals, setShirtRefusals] = useState<Record<string, string>>({});
  const [training, setTraining] = useState<SquadTrainingQuotesDto | null>(null);
  const [trainingLoading, setTrainingLoading] = useState(false);
  const [trainingError, setTrainingError] = useState<string | null>(null);

  const FORM_GUIDE_LENGTH = 10;
  const H2H_LENGTH = 5;

  const navigate = useNavigate();
  const { teamId: routeTeamId = '' } = useParams();
  const [params, setParams] = useSearchParams();
  const urlTeamId = propTeamId || routeTeamId;
  const seasonId = params.get('season') || '';

  /**
   * Which of the club's two screens is being read: the men, or what the manager is doing
   * about them. A training column inside the squad table would have made the table a place
   * where every row is a decision, and a table of twenty-three decisions is not a table any
   * more.
   *
   * The answer is read from the URL and not held here, because the sidebar has a door to
   * training and a door that goes nowhere is a door nobody uses: a piece of state that only
   * exists inside one component cannot be pointed at from outside it. So `?tab=training` is
   * what the link says and what the screen obeys, and the tab buttons write the same
   * parameter rather than a variable — which is also why the back button works.
   */
  const activeTab: 'squad' | 'training' = params.get('tab') === 'training' ? 'training' : 'squad';

  const setActiveTab = (tab: 'squad' | 'training') => {
    setParams(
      current => {
        const next = new URLSearchParams(current);
        if (tab === 'training') next.set('tab', 'training');
        else next.delete('tab');
        return next;
      },
      { replace: true }
    );
  };

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

    // Load manager name
    ManagerApi.getByTeam(urlTeamId)
      .then(manager => {
        if (!cancelled) setCoachName(manager.name);
      })
      .catch(() => {
        // Club might not have a manager yet
        if (!cancelled) setCoachName(null);
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

      // The squad and the club's place in the season are read together: they are two halves of
      // the same page, and a club's own place is asked of the club. A club in no division
      // answers with nothing in it, and the screen says so rather than inventing a tier.
      const [squadData, place] = await Promise.all([
        TeamApi.getSquad(urlTeamId, season),
        TeamApi.getStanding(urlTeamId, season).catch(() => null),
      ]);
      if (cancelled) return;

      setPlayers(squadData);
      setMatches(form);
      setH2hMatches(h2h);
      setStanding(place);
    };

    load().catch(error => {
      if (!cancelled) console.error('Failed to load the club data:', error);
    });

    return () => {
      cancelled = true;
    };
  }, [urlTeamId, seasonId, selectedTeam?.id]);

  /**
   * The training sheet, read when — and only when — the manager opens the tab.
   *
   * <para>
   * It is not read with the squad, on purpose. A club page is opened far more often than a
   * training page, and twenty-three men times eight prices is a payload paid on every visit
   * to read a screen most visits never look at. Reading it on the tab's own opening is the
   * difference between a club card that loads as fast as it always did and one that does not.
   * </para>
   */
  useEffect(() => {
    // The club is the manager's own, and the sheet is a decision only a manager may make —
    // so the same test that gates the button gates the read, and a spectator never pulls
    // twenty-three price lists he has no use for. It is written out rather than reusing the
    // `isOwnTeam` further down, which is declared after the early returns.
    const ownsClub = Boolean(selectedTeam && selectedTeam.id === urlTeamId);

    if (activeTab !== 'training' || !urlTeamId || !ownsClub) {
      return undefined;
    }

    let cancelled = false;

    setTrainingLoading(true);
    setTrainingError(null);

    TeamApi.training(urlTeamId, seasonId || undefined)
      .then(quotes => {
        if (!cancelled) setTraining(quotes);
      })
      .catch(err => {
        console.error('Failed to load the training sheet:', err);
        if (!cancelled) setTrainingError('Não foi possível carregar o treino.');
      })
      .finally(() => {
        if (!cancelled) setTrainingLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [activeTab, urlTeamId, seasonId, selectedTeam?.id]);

  /**
   * Loads the club's transfer history for the current and previous season.
   * The backend returns pending, accepted and completed transfers.
   */
  useEffect(() => {
    if (!urlTeamId || !seasonId) {
      setTransferHistory(null);
      return;
    }

    let cancelled = false;

    SeasonApi.list()
      .then(seasons => {
        const currentSeason = seasons.find(s => s.id === seasonId);
        if (!currentSeason) return;

        // Get current and previous season numbers
        const seasonNumbers = [currentSeason.number];
        const previousSeason = seasons.find(s => s.number === currentSeason.number - 1);
        if (previousSeason) {
          seasonNumbers.push(previousSeason.number);
        }

        TransferApi.getClubHistory(urlTeamId, seasonNumbers)
          .then(history => {
            if (!cancelled) setTransferHistory(history);
          })
          .catch(() => {
            if (!cancelled) setTransferHistory(null);
          });
      })
      .catch(() => {});

    return () => { cancelled = true; };
  }, [urlTeamId, seasonId]);

  /**
   * The twenty-three men the manager is looking at, which is not always the twenty-three he
   * has. It is worked out here rather than inside the table, because the lineup screen draws
   * the same table and a manager picking eleven is not narrowing a list — he is choosing.
   */
  const filteredPlayers = useMemo(
    () => applySquadFilter(players, filter),
    [players, filter]
  );

  /**
   * Rereads the squad after a decision the manager just took.
   *
   * The list is not edited here: a release ends a contract, and that is a fact the world holds.
   * Patching the row would leave a screen that believes a player was let go while the club's
   * books still carry him.
   */
  const reloadSquad = async () => {
    const season = seasonId || (await SeasonApi.current()).id;
    setPlayers(await TeamApi.getSquad(urlTeamId, season));
  };

  /**
   * Puts a man in a shirt, and says nothing at all when the server agrees.
   *
   * A successful renumbering is not news: it is the state the manager asked for, and a
   * notice for it would train him to read past the notices that are. The one row that
   * changes is patched from the answer, so a manager who renumbers five men in a row sees
   * five rows change and is not waiting on twenty-three.
   */
  const handleShirtNumber = async (player: SquadPlayerDto, shirtNumber: number) => {
    if (!urlTeamId) return;

    setBusyPlayerId(player.id);
    setNotice(null);
    setError(null);
    setShirtRefusals(current => {
      const next = { ...current };
      delete next[player.id];
      return next;
    });

    try {
      const result = await TeamApi.updateShirtNumber(urlTeamId, player.id, shirtNumber);

      // The number painted is the one the backend answered with, not the one that was asked
      // for: a client that trusted its own input would put 10 on a row whose change had been
      // refused, and the refusal would only surface on the next reload.
      setPlayers(current =>
        current.map(row =>
          row.id === result.playerId ? { ...row, shirtNumber: result.shirtNumber } : row
        )
      );
    } catch (err) {
      // The refusal is the rule's own sentence rather than a message written here: the
      // backend knows whether it was the range or the clash with another man, and a screen
      // that guessed would be guessing twice — once wrong here, and once in whatever the
      // backend changes the rule to tomorrow.
      setShirtRefusals(current => ({
        ...current,
        [player.id]:
          err instanceof ApiProblemError && err.detail
            ? err.detail
            : 'Não foi possível alterar o número.'
      }));
    } finally {
      setBusyPlayerId(null);
    }
  };

  const handleRelease = async (player: SquadPlayerDto) => {
    if (!urlTeamId) return;

    if (!window.confirm(
      `Rescindir o contrato de ${player.name}?\n\n` +
      `O clube paga a multa: as rodadas restantes desta temporada e as temporadas que faltam ` +
      `do contrato, metades do salário por rodada.`
    )) {
      return;
    }

    setBusyPlayerId(player.id);
    setNotice(null);
    setError(null);
    try {
      const result = await TransferApi.release(player.id, urlTeamId);
      await reloadSquad();
      // The settlement is a number the rules decided; the way it is written is this screen's,
      // because every other amount in the game is written by the one formatter.
      setNotice(
        `${result.playerName} foi dispensado por ${formatLimo(result.releaseCost)}` +
        (result.withdrawnOffers > 0
          ? `, e ${result.withdrawnOffers} proposta(s) pela venda dele caíram.`
          : '.')
      );
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Não foi possível rescindir o contrato.');
    } finally {
      setBusyPlayerId(null);
    }
  };

/**
   * Renews a man's deal for as many seasons as the manager says.
   *
   * <para>
   * The season the renewal is signed in is the season being played, and not whatever the
   * address bar happens to carry: the screen is reached as <c>/team/{id}</c> as often as it is
   * reached as <c>/team/{id}?season=…</c>, and a control that quietly did nothing on the
   * first of those was a renewal button that only worked on some of the doors.
   * </para>
   */
  const handleRenewContract = async (player: SquadPlayerDto) => {
    if (!urlTeamId) return;

    // The wage is quoted by the rule that charges it, and the settlement is what the same
    // release would cost — both arrive on the row rather than being worked out here.
    const seasons = window.prompt(
      `Renovar ${player.name} por quantas temporadas?\n\n` +
      `Nova wage por temporada: ${formatLimo(player.wageOnRenewal ?? 0)}\n` +
      `O novo contrato conta a partir desta temporada.\n` +
      `Entre 1 e 5 temporadas.`,
      '3'
    );

    if (!seasons) return;

    const n = parseInt(seasons, 10);
    if (isNaN(n) || n < 1 || n > 5) {
      setError('Número inválido de temporadas. Escolha entre 1 e 5.');
      return;
    }

    setBusyPlayerId(player.id);
    setNotice(null);
    setError(null);
    try {
      const season = seasonId || (await SeasonApi.current()).id;
      await TeamApi.renewContract(urlTeamId, {
        playerId: player.id,
        seasons: n,
        seasonId: season,
      });
      await reloadSquad();
      setNotice(`${player.name} renovou por ${n} temporada(s).`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Não foi possível renovar o contrato.');
    } finally {
      setBusyPlayerId(null);
    }
  };

  const clubWindow = useClubWindow(team);

  /**
   * One session, and both halves of what it did.
   *
   * <para>
   * The sheet and the squad table are patched from the same answer rather than refetched.
   * The backend's response already carries the attribute either side of the session and the
   * energy either side of it, so the screen knows every number that changed without asking
   * again — and asking again would be a way of being wrong: a refetch landing between a
   * session and its next would show the manager a price that is no longer the price.
   * </para>
   *
   * <para>
   * The squad is patched as well as the sheet because they are the same men. A manager who
   * trained somebody and went back to the elenco to find his energy unchanged would be
   * looking at one screen that spent something and another that says nothing was spent, and
   * the second one is the one a manager would believe.
   * </para>
   */
  const handleTrain = async (playerId: string, attribute: PlayerAttribute) => {
    setBusyPlayerId(playerId);

    try {
      const result = await PlayerApi.train(playerId, attribute, seasonId || undefined);

      setTraining(current => {
        if (!current) return current;

        const players = current.players.map(player => {
          if (player.playerId !== playerId) return player;

          return {
            ...player,
            energy: result.energyLeft,
            attributes: player.attributes.map(cell =>
              cell.attribute === attribute
                ? { ...cell, value: result.attributeAfter }
                : cell
            )
          };
        });

        return {
          ...current,
          squadEnergy: current.squadEnergy - result.energySpent,
          players
        };
      });

      setPlayers(current =>
        current.map(player => {
          if (player.id !== playerId) return player;

          // The attribute arrives under its wire name ("Speed") and the squad row's field is
          // camel case ("speed"). Spreading the wire name in would have written a second,
          // unread key onto the row and left the column a manager is looking at unchanged —
          // a session that visibly did nothing on the very screen they came back to.
          const field = ATTRIBUTE_FIELD[attribute];

          return field ? { ...player, energy: result.energyLeft, [field]: result.attributeAfter } : player;
        })
      );

      setNotice(
        `${result.attribute} de ${result.attributeBefore} para ${result.attributeAfter}, ` +
        `gastando ${result.energySpent} de energia e ${formatLimo(result.fee)} do clube.`
      );
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Não foi possível treinar o jogador.');
    } finally {
      setBusyPlayerId(null);
    }
  };

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

  /**
   * The club's strength, as the season's own squad is measured: the average of every man on the
   * books, which is the number the division's table seeds a club with. The club row itself
   * carries no strength of its own — a club is a name and two colours — so the one that arrives
   * with the club's place in the season is the one read here, and the club's own value is the
   * fallback for a season that could not be read at all.
   */
  const clubStars = standing?.squadStars ?? team.stars;

  /**
   * The club's season, said in one line: the division it is in, where it stands in it, and the
   * table the line belongs to.
   *
   * The position is the one the division's own table holds, and the link carries the edition with
   * it — a club that is seventh in the fourth division opens the fourth division's table, not the
   * first one the screen happens to show. A club with no division this season says so, because
   * "not enrolled" is a fact about the world and a made-up tier would not be.
   */
  const divisionLine = standing?.competitionSeasonId ? (
    <p className="club-division">
      {standing.tier != null && <DivisionTrophy tier={standing.tier} size={18} className="club-division__trophy" />}
      <button
        type="button"
        className="club-division__name"
        onClick={() => navigate(
          `/league?season=${standing.seasonId}&edition=${standing.competitionSeasonId}`
        )}
        title="Abrir a tabela desta divisão"
      >
        {standing.divisionName}
      </button>
      {standing.row && (
        <span className="club-division__place">
          {standing.row.position}º de {standing.clubsInDivision ?? '—'}
          {' • '}
          {standing.row.points} pts
          {' • '}
          {standing.row.played} jogos
        </span>
      )}
    </p>
  ) : (
    <p className="club-division club-division--none">Sem divisão nesta temporada</p>
  );

  return (
    <div className="app">
      <div className="team-view-overlay">
        <div className="card team-view-card club-modal" style={clubWindow}>
        <div className="squad-head team-view-summary">
          <div className="team-header-with-crest">
            <ClubCrest
              crest={team.crest}
              primary={team.primaryColor || '#f2d34f'}
              secondary={team.secondaryColor || '#f2d34f'}
              name={team.name}
            />
            <div className="team-header-info">
              <div className="team-header-main">
                <h2 className="profile-name">{team.name}</h2>
                <span className="team-stars" title="Força do elenco">{starsToString(clubStars)}</span>
              </div>
              <p className="squad-hint">
                {players.length} jogadores
                {team.stadium && ` • ${team.stadium.name} • ${team.stadium.capacity.toLocaleString('pt-BR')} lugares`}
              </p>
              {divisionLine}
              {coachName && (
                <p className="coach-name">Técnico: {coachName}</p>
              )}
            </div>
          </div>
          <button
            className="ctrl"
            onClick={() => navigate(
              standing?.competitionSeasonId
                ? `/league?season=${standing.seasonId}&edition=${standing.competitionSeasonId}`
                : '/league'
            )}
          >
            Tabela e jogos
          </button>
        </div>

        <div className="club-colors">
          <span className="club-swatch" style={{ background: team.primaryColor || '#f2d34f' }} />
          <span className="club-swatch" style={{ background: team.secondaryColor || '#f2d34f' }} />
        </div>

        {notice && <p className="league-notice">{notice}</p>}
        {busyPlayerId && <p className="league-empty">Registrando a decisão…</p>}

        {/*
          The two screens of a club. The tab only exists for the manager's own club: a
          spectator has no decision to make about these twenty-three men, and a tab that
          opened onto a list of prices he may not pay would be a control the server has
          already said no to.
        */}
        {isOwnTeam && (
          <div className="tabs club-tabs">
            <button
              type="button"
              className={`tab ${activeTab === 'squad' ? 'active' : ''}`}
              onClick={() => setActiveTab('squad')}
            >
              Elenco
            </button>
            <button
              type="button"
              className={`tab ${activeTab === 'training' ? 'active' : ''}`}
              onClick={() => setActiveTab('training')}
            >
              Treino
            </button>
          </div>
        )}

        {activeTab === 'training' && isOwnTeam ? (
          <TrainingPanel
            team={team}
            quotes={training}
            loading={trainingLoading}
            error={trainingError}
            busyPlayerId={busyPlayerId}
            onTrain={handleTrain}
          />
        ) : (
          <>
            {/* The filters are offered on any club's roster, not only the manager's own,
                because narrowing a list he is reading is not a decision about the world: a
                manager scouting a rival wants the twenty-three-year-old defenders and has
                no way to say so without this. What stays behind isOwnTeam is the one thing
                that changes anything — the number on the man's shirt. */}
            <SquadFilterBar
              filter={filter}
              onChange={setFilter}
              matched={filteredPlayers.length}
              total={players.length}
            />
            <ClubSquadTable
              squad={filteredPlayers}
              team={team}
              onRelease={isOwnTeam ? handleRelease : undefined}
              onRenewContract={isOwnTeam ? handleRenewContract : undefined}
              onEditShirtNumber={isOwnTeam ? handleShirtNumber : undefined}
              shirtRefusals={shirtRefusals}
            />
          </>
        )}

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
                      <td className="form-score">
                        <button
                          type="button"
                          className="form-score__btn"
                          onClick={() => navigate(`/match/${record.matchId}`)}
                          title="Assistir à partida"
                        >
                          {record.goalsFor} x {record.goalsAgainst}
                        </button>
                      </td>
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
                        <button
                          type="button"
                          className="form-score__btn"
                          onClick={() => navigate(`/match/${m.matchId}`)}
                          title="Assistir à partida"
                        >
                          {m.isHome ? m.goalsFor : m.goalsAgainst} x {m.isHome ? m.goalsAgainst : m.goalsFor}
                        </button>
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

        {/*
          The club's live transfer business for this season and the one before it: a bid still
          on the table, a bid agreed and a player who arrived. A refused or expired offer is not
          on it — neither moves a player, and a season of three rejected bids for one man is
          not a season of the club's business. The section is drawn even when the list is
          empty, so "this club did not do business" is a fact on the page and not a missing
          table.
        */}
        {transferHistory && (
          <section className="club-form">
            <h4 className="club-form__title">Transferências das temporadas {transferHistory.seasonNumbers.join(' e ')}</h4>
            {transferHistory.transfers.length === 0 ? (
              <p className="league-empty">Nenhuma transferência nestas temporadas.</p>
            ) : (
              <table className="form-table">
                <thead>
                  <tr>
                    <th>Jogador</th>
                    <th>De</th>
                    <th>Para</th>
                    <th className="num money">Valor</th>
                    <th className="num">Temp.</th>
                    <th>Status</th>
                    <th>Data</th>
                  </tr>
                </thead>
                <tbody>
                  {transferHistory.transfers.map((line, i) => (
                    <tr key={`${line.sellingClubId ?? 'livre'}-${line.buyingClubId}-${i}`} className={`history-row status-${STATUS_COLOR[line.status]}`}>
                      <td>
                        <PlayerName playerId={line.playerId}>{line.playerName}</PlayerName>
                      </td>
                      <td>
                        {line.sellingClubId ? (
                          <ClubName teamId={line.sellingClubId}>{line.sellingClubName}</ClubName>
                        ) : (
                          <span className="free-agent">{line.sellingClubName}</span>
                        )}
                      </td>
                      <td>
                        <ClubName teamId={line.buyingClubId}>{line.buyingClubName}</ClubName>
                      </td>
                      <td className="num money">{line.fee ? formatLimo(line.fee) : '—'}</td>
                      <td className="num">{line.proposalSeasonNumber} → {line.arrivalSeasonNumber}</td>
                      <td>
                        <span className={`status-badge status-${STATUS_COLOR[line.status]}`}>
                          {STATUS_LABELS[line.status]}
                        </span>
                      </td>
                      <td className="num">{line.proposedAt ? new Date(line.proposedAt).toLocaleDateString('pt-BR') : '—'}</td>
                    </tr>
                  ))}
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
