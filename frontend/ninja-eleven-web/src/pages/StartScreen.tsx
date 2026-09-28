import React, { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AuthApi, CompetitionApi, ManagerApi, SeasonApi, TeamApi } from '@/api';
import { API_BASE_URL } from '@/config/env';
import ClubCrest from '@/components/Club/ClubCrest';
import { PlayerName } from '@/components/Common/Names';
import { useGameState } from '@/state';
import { useAuthStore } from '@/state/auth';
import { divisionsOf } from '@/types';
import type { CompetitionEditionDto, SeasonDto, SquadPlayerDto, TeamDto } from '@/types';
import { TEAM_COLOR_PALETTES, pick, positionLabel, sortByPosition, starsToString } from '@/services/formatters';

const StartScreen: React.FC = () => {
  const navigate = useNavigate();
  const setSelectedTeam = useGameState((s) => s.setSelectedTeam);
  const setLeagueTeams = useGameState((s) => s.setLeagueTeams);
  const setSeasonInfo = useGameState((s) => s.setSeasonInfo);
  const setCompetitionInfo = useGameState((s) => s.setCompetitionInfo);
  const selectedCompetition = useGameState((s) => s.selectedCompetition);
  const setTeamId = useAuthStore((s) => s.setTeamId);
  const setCoachName = useAuthStore((s) => s.setCoachName);

  const [clubs, setClubs] = useState<TeamDto[]>([]);
  const [seasons, setSeasons] = useState<SeasonDto[]>([]);
  const [editions, setEditions] = useState<CompetitionEditionDto[]>([]);
  const [selectedClubId, setSelectedClubId] = useState<string | null>(null);
  const [selectedSeasonId, setSelectedSeasonId] = useState<string>('');
  const [selectedEditionId, setSelectedEditionId] = useState<string>('');
  const [loading, setLoading] = useState(true);
  const [connectionError, setConnectionError] = useState<string | null>(null);
  const [coachNameInput, setCoachNameInput] = useState('');
  const [coachError, setCoachError] = useState<string | null>(null);
  const [showCoachModal, setShowCoachModal] = useState(false);

  const divisions = useMemo(() => divisionsOf(editions), [editions]);

  useEffect(() => {
    const loadData = async () => {
      setLoading(true);
      setConnectionError(null);

      try {
        const [teamData, seasonData] = await Promise.all([
          TeamApi.list(),
          SeasonApi.list(),
        ]);

        setClubs(teamData);
        setSeasons(seasonData);

        // The current season first, and the newest first otherwise: seasons are counted from
        // one and a manager opening a new career wants the one in progress, not the oldest
        // one the backend still has.
        const ordered = [...seasonData].sort((a, b) => b.number - a.number);
        const current = ordered.find(season => season.status === 'InProgress') ?? ordered[0];

        if (current) setSelectedSeasonId(current.id);

        if (current) {
          const editionList = await CompetitionApi.listEditionsBySeason(current.id);
          setEditions(editionList);

          // The division the manager left on last time, when it is still one of this season's
          // divisions. Otherwise the top flight, because that is where a career starts.
          const remembered = divisionsOf(editionList).find(
            division => division.id === selectedCompetition?.id
          );
          const first = remembered ?? divisionsOf(editionList)[0] ?? editionList[0];

          if (first) setSelectedEditionId(first.id);
        }
      } catch (err: any) {
        // Never fake the world: a broken backend must be visible, not hidden behind
        // invented teams that cannot start a match.
        const code = err?.response?.status
          ? `HTTP ${err.response.status}`
          : err?.message || 'erro desconhecido';
        setConnectionError(code);
        setClubs([]);
        setSeasons([]);
        setEditions([]);
      } finally {
        setLoading(false);
      }
    };

    loadData();
  }, [selectedCompetition?.id]); // eslint-disable-line react-hooks/exhaustive-deps

  // The clubs of the chosen division. They are asked of the backend rather than filtered out
  // of the whole pyramid on the client: a club's division is a fact about its enrolment in an
  // edition, and two lists joined up in a browser is how a club ends up in the wrong one.
  const [divisionClubs, setDivisionClubs] = useState<TeamDto[]>([]);
  const [clubsLoading, setClubsLoading] = useState(false);

  useEffect(() => {
    if (!selectedEditionId) {
      setDivisionClubs([]);
      return;
    }

    let cancelled = false;
    setClubsLoading(true);

    CompetitionApi.listClubs(selectedEditionId)
      .then(loaded => {
        if (!cancelled) setDivisionClubs(loaded);
      })
      .catch(() => {
        if (!cancelled) setDivisionClubs([]);
      })
      .finally(() => {
        if (!cancelled) setClubsLoading(false);
      });

    return () => { cancelled = true; };
  }, [selectedEditionId]);

  // A club chosen in another division is not a club of this one.
  useEffect(() => {
    setSelectedClubId(null);
  }, [selectedEditionId]);

  /**
   * The squad of every club in the division, so the choice is made by reading men rather than
   * by reading a number of stars.
   *
   * Taking over a club is the one decision in the game that cannot be undone, and a manager
   * makes it by looking at the eleven he would inherit. So the whole division's squads are
   * asked for, all of them, at once — a card that shows the eleven a club would field and a
   * card that hides it behind a click are the same screen to one manager and not the same
   * screen to another, and a manager who has to click twelve times before he has compared
   * two clubs is a manager who compares them on the stars.
   *
   * They are asked of the backend, one call per club, against the season being chosen, because
   * a squad is a fact about a club in a season and not a list the client may build out of the
   * teams it already has.
   */
  const [squads, setSquads] = useState<Record<string, SquadPlayerDto[]>>({});
  const [squadsLoading, setSquadsLoading] = useState(false);

  useEffect(() => {
    if (!selectedSeasonId || divisionClubs.length === 0) {
      setSquads({});
      return;
    }

    let cancelled = false;
    setSquadsLoading(true);

    Promise.all(
      divisionClubs.map(club =>
        TeamApi.getSquad(club.id, selectedSeasonId)
          .then(squad => [club.id, sortByPosition(squad)] as const)
          // A club whose squad will not load still has to be choosable: the card says the squad
          // could not be read and the manager goes on to the club's own page, which can.
          .catch(() => [club.id, [] as SquadPlayerDto[]] as const)
      )
    )
      .then(entries => {
        if (cancelled) return;
        setSquads(Object.fromEntries(entries));
      })
      .finally(() => {
        if (!cancelled) setSquadsLoading(false);
      });

    return () => { cancelled = true; };
  }, [divisionClubs, selectedSeasonId]);

  if (loading) {
    return (
      <div className="card start">
        <p>Carregando...</p>
      </div>
    );
  }

  if (connectionError) {
    return (
      <div className="card start">
        <h1>Servidor indisponível</h1>
        <p>Não foi possível carregar os dados do campeonato.</p>
        <div className="conn-error">
          <h3>Não foi possível conectar ao servidor</h3>
          <p>Confirme que a API está rodando e que a URL abaixo está correta.</p>
          <code>{API_BASE_URL}</code>
          <p style={{ marginTop: '8px' }}>Detalhe: {connectionError}</p>
        </div>
      </div>
    );
  }

  if (clubs.length === 0) {
    return (
      <div className="card start">
        <h1>Nova carreira</h1>
        <p>Nenhuma equipe disponível. Rode a API com <code>--seed</code> para criar o mundo.</p>
      </div>
    );
  }

  const startPressed = async () => {
    const club = divisionClubs.find(team => team.id === selectedClubId);
    if (!club) return;

    // A manager can only be created once per user. If the backend already has one for this
    // club the coach-name gate is skipped — the club is taken over, not re-christened.
    setCoachError(null);
    let managerName = '';

    try {
      const existing = await ManagerApi.getByTeam(club.id);
      managerName = existing.name;
    } catch {
      // The modal that asks for a name is what follows only when there is no manager yet.
    }

    const proceed = () => {
      setSelectedTeam(club);
      setLeagueTeams(divisionClubs);
      setSeasonInfo(seasons.find(s => s.id === selectedSeasonId) || null);
      setCompetitionInfo(editions.find(e => e.id === selectedEditionId) || null);

      // The season and the division are queries of the screens behind this one, not a rewrite
      // of the url: the router owns the path, so navigation has to go through it. A career
      // opens on the club the manager just took over, because that squad is the first thing he
      // reads — and the division he is about to manage is named alongside it, because a club
      // with no division is a club with no table.
      navigate(`/team/${club.id}?season=${selectedSeasonId}&edition=${selectedEditionId}`);
    };

    if (managerName) {
      proceed();
      return;
    }

    // No manager yet: ask for a name and create one before the career opens. This is a modal
    // overlay so the StartScreen stays behind it, and pressing Escape or clicking outside
    // cancels back into the team picker.
    setShowCoachModal(true);
  };

  const canStart = selectedClubId !== null && selectedSeasonId !== '' && selectedEditionId !== '';

  const createManagerAndProceed = async () => {
    const club = divisionClubs.find(team => team.id === selectedClubId);
    if (!club) return;

    // The authenticated user is not logged in, so we link them to a newly created manager
    // rather than creating an unlinked one. The auth store is updated with the new team id
    // so the next check in AuthRoot navigates straight to the club screen.
    setCoachError(null);
    try {
      const manager = await AuthApi.linkManager({ teamId: club.id, coachName: coachNameInput.trim() });
      setShowCoachModal(false);
      setCoachNameInput('');

      setTeamId(club.id);
      setCoachName(manager.name);

      setSelectedTeam(club);
      setLeagueTeams(divisionClubs);
      setSeasonInfo(seasons.find(s => s.id === selectedSeasonId) || null);
      setCompetitionInfo(editions.find(e => e.id === selectedEditionId) || null);
      navigate(`/team/${club.id}?season=${selectedSeasonId}&edition=${selectedEditionId}`);
    } catch (err: any) {
      const code = err?.response?.data?.code || err?.response?.data?.message || err?.message;
      setCoachError(code || 'Não foi possível criar o técnico.');
    }
  };

  return (
    <>
      <div className="card start">
        <h1>Nova carreira</h1>
      <p>Escolha a divisão, escolha um dos clubes e coloque seu time em campo.</p>

      {seasons.length > 1 && (
        <div style={{ marginBottom: '12px' }}>
          <label style={{ fontSize: '13px', color: 'var(--muted)' }}>Temporada:</label>
          <select
            value={selectedSeasonId}
            onChange={(e) => setSelectedSeasonId(e.target.value)}
            style={selectStyle}
          >
            {seasons.map(s => (
              <option key={s.id} value={s.id}>{s.name}</option>
            ))}
          </select>
        </div>
      )}

      {divisions.length > 0 && (
        <div style={{ marginBottom: '12px' }}>
          <label style={{ fontSize: '13px', color: 'var(--muted)' }}>Divisão:</label>
          <select
            value={selectedEditionId}
            onChange={(e) => setSelectedEditionId(e.target.value)}
            style={selectStyle}
          >
            {divisions.map(division => (
              <option key={division.id} value={division.id}>{division.name}</option>
            ))}
          </select>
        </div>
      )}

      <div className="small" style={{ marginBottom: '8px' }}>
        {clubsLoading
          ? 'Carregando clubes...'
          : `${divisionClubs.length} clubes nesta divisão`}
        {squadsLoading && !clubsLoading ? ' • carregando elencos...' : ''}
      </div>

      <div className="team-grid team-grid--squad">
        {divisionClubs.map((club, i) => {
          const colors = club.primaryColor && club.secondaryColor
            ? { primary: club.primaryColor, secondary: club.secondaryColor }
            : pick(TEAM_COLOR_PALETTES);
          const squad = squads[club.id];

          return (
            <div
              key={club.id}
              className={`team-choice ${selectedClubId === club.id ? 'selected' : ''}`}
              onClick={() => setSelectedClubId(club.id)}
              data-i={i}
              style={{ '--team-primary': colors.primary, '--team-secondary': colors.secondary } as React.CSSProperties}
            >
              {/* The shield goes beside the name rather than above it: a card that carries a squad
                  is read by the manager in two glances — the club, then the men — and the badge
                  is how he tells the twelve apart before he has read a word of either. */}
              <div className="team-choice__head">
                <ClubCrest
                  primary={colors.primary}
                  secondary={colors.secondary}
                  name={club.name}
                  className="team-choice__crest"
                />
                <div className="team-choice__identity">
                  <h3>{club.name}</h3>
                  <div className="rating" style={{ color: colors.primary }}>
                    ELenco {starsToString(club.stars)}
                  </div>
                  {club.stadium && (
                    <div className="small">
                      {club.stadium.name} • {club.stadium.capacity.toLocaleString('pt-BR')} lugares
                    </div>
                  )}
                </div>
              </div>

              <ClubSquadPreview squad={squad} loading={squadsLoading} />
            </div>
          );
        })}
      </div>

      <button
        id="startBtn"
        className="primary"
        disabled={!canStart}
        onClick={startPressed}
      >
        Escolher clube e começar
      </button>
      </div>

      {showCoachModal && (
        <div className="modal-backdrop" onClick={() => setShowCoachModal(false)}>
          <div
            className="start-modal"
            onClick={e => e.stopPropagation()}
            onKeyDown={e => {
              if (e.key === 'Escape') setShowCoachModal(false);
            }}
          >
            <h2>Novo técnico</h2>
            <p>Dê um nome ao seu treinador para começar a carreira em {divisionClubs.find(t => t.id === selectedClubId)?.name || ''}.</p>
            <input
              className="ctrl"
              type="text"
              placeholder="Nome do técnico"
              value={coachNameInput}
              onChange={e => setCoachNameInput(e.target.value)}
              autoFocus
              onKeyDown={e => {
                if (e.key === 'Enter') createManagerAndProceed();
              }}
            />
            {coachError && <div className="error">{coachError}</div>}
            <div className="modal-actions">
              <button className="ctrl" onClick={() => setShowCoachModal(false)}>Cancelar</button>
              <button
                className="primary"
                disabled={!coachNameInput.trim()}
                onClick={createManagerAndProceed}
              >
                Criar técnico
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
};

const selectStyle: React.CSSProperties = {
  width: '100%',
  padding: '6px',
  background: '#0a1520',
  color: 'var(--text)',
  border: '1px solid var(--line)',
  borderRadius: '7px'
};

/**
 * A club's squad, read on the card, before the club is chosen.
 *
 * Every man, in the order the rest of the game reads a squad in — keepers, defenders,
 * midfielders, attackers — and with his line, his age and his stars, because those are the
 * three things a manager compares between two clubs. The list is complete and scrollable
 * rather than trimmed to a starting eleven: a card that showed eleven of twenty-three would
 * be a card about a match the manager has not agreed to play yet, and the two men left on the
 * bench are exactly what a manager taking a club over is buying.
 *
 * A name is a door, here as everywhere: the manager who wants to know who a striker is before
 * he takes the club asks, rather than taking the club and finding out. The name stops the
 * click so that reading a name never selects a club by accident.
 */
const ClubSquadPreview: React.FC<{ squad?: SquadPlayerDto[]; loading: boolean }> = ({
  squad,
  loading
}) => {
  if (loading && !squad) {
    return <div className="squad-preview squad-preview--empty">Carregando elenco...</div>;
  }

  if (!squad) {
    return <div className="squad-preview squad-preview--empty">Elenco indisponível.</div>;
  }

  if (squad.length === 0) {
    return <div className="squad-preview squad-preview--empty">Elenco indisponível.</div>;
  }

  return (
    <div className="squad-preview">
      <div className="squad-preview__title">Elenco ({squad.length})</div>
      <ul className="squad-preview__list">
        {squad.map(player => (
          <li className="squad-preview__row" key={player.id}>
            <span className="squad-preview__pos">{positionLabel(player.position)}</span>
            <PlayerName playerId={player.id} className="squad-preview__name">
              {player.name}
            </PlayerName>
            <span className="squad-preview__age">{player.age}a</span>
            <span className="squad-preview__stars">{starsToString(player.stars)}</span>
            {player.injuryMatchesRemaining > 0 && (
              <span className="squad-preview__flag" title="Lesionado">🩹</span>
            )}
          </li>
        ))}
      </ul>
    </div>
  );
};

export default StartScreen;
