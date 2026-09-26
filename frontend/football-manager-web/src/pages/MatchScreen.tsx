import React, { useEffect, useState, useCallback, useRef } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useGameState } from '@/state';
import { MatchApi } from '@/api';
import MatchHubClient from '@/signalr/MatchHubClient';
import type {
  MatchEngineEventDto,
  MatchStateDto,
  MatchLineupDto,
  MatchResult
} from '@/types';
import { convertToFeedEvent, energyClass, energyPercent, positionLabel } from '@/services/formatters';
import MatchFeed from '@/components/Match/MatchFeed';
import MatchControls from '@/components/Match/MatchControls';
import MatchStats from '@/components/Match/MatchStats';
import PlayerStats from '@/components/Match/PlayerStats';
import LineupView from '@/components/Match/LineupView';
import TacticsView from '@/components/Match/TacticsView';
import EndModal from '@/components/Modals/EndModal';
import HalfTimeModal from '@/components/Modals/HalfTimeModal';
import PenaltyModal from '@/components/Modals/PenaltyModal';
import SubstitutionModal from '@/components/Modals/SubstitutionModal';
import MatchdayScoreboard, { type MatchScore } from '@/components/Match/MatchdayScoreboard';
import { FixtureApi } from '@/api';

const MatchScreen: React.FC<{ matchId?: string }> = ({ matchId: propMatchId }) => {
  const navigate = useNavigate();
  const { matchId: routeMatchId = '' } = useParams();
  const urlMatchId = propMatchId || routeMatchId;
  const currentMatch = useGameState((s) => s.currentMatch);
  const userTeam = useGameState((s) => s.selectedTeam);
  const setCurrentMatch = useGameState((s) => s.setCurrentMatch);
  const setFeed = useGameState((s) => s.setFeed);
  const addFeedEvent = useGameState((s) => s.addFeedEvent);
  const setIsPaused = useGameState((s) => s.setIsPaused);
  const setMatchScreen = useGameState((s) => s.setMatchScreen);
  const matchScreen = useGameState((s) => s.matchScreen);
  const isPaused = useGameState((s) => s.isPaused);
  const feed = useGameState((s) => s.feed);
  const lastEvents = useGameState((s) => s.lastEvents);

  const [lineup, setLineup] = useState<MatchLineupDto | null>(null);
  const [state, setState] = useState<MatchStateDto | null>(null);
  const [showEndModal, setShowEndModal] = useState(false);
  const [showHalfTimeModal, setShowHalfTimeModal] = useState(false);
  const [matchResult, setMatchResult] = useState<MatchResult | null>(null);
  const [selectedStarters, setSelectedStarters] = useState<Set<string>>(new Set());
  const [substitution, setSubstitution] = useState<{ out: string; in: string } | null>(null);
  const [showSubstitutionModal, setShowSubstitutionModal] = useState(false);
  const [substituting, setSubstituting] = useState(false);
  const [substitutionError, setSubstitutionError] = useState<string | null>(null);
  const [penaltyError, setPenaltyError] = useState<string | null>(null);
  const [matchdayScores, setMatchdayScores] = useState<MatchScore[]>([]);
  const [roundId, setRoundId] = useState<string>('');

  // Last sequence the feed already holds. The hub stream can leave a hole when the
  // browser tab is busy or a subscription is replaced, and the sequence is how that
  // hole is detected and filled from the persisted log.
  const lastSequence = useRef(0);

  const matchId = urlMatchId || currentMatch?.matchId || '';

  // The club the manager is watching is sent to the API: it is what tells the backend
  // which side of the team sheet is his, and a lineup read without it looks the same for
  // a home manager and an away one.
  const loadLineup = useCallback(async () => {
    if (!matchId) return;
    const lineupData = await MatchApi.getLineup(matchId, userTeam?.id);
    setLineup(lineupData);
  }, [matchId, userTeam?.id]);

  const loadState = useCallback(async () => {
    if (!matchId) return;
    const stateData = await MatchApi.getState(matchId);
    setState(stateData);
    setIsPaused(stateData.isPaused);
    if (stateData.isFinished) {
      setShowEndModal(true);
      // A finished match has no live session any more, so its result is read from
      // the persisted row.
      MatchApi.getResult(matchId).then(setMatchResult).catch(() => undefined);
    }
  }, [matchId]);

  useEffect(() => {
    if (!matchId) return;

    // Everything the effect starts, it also stops. React runs an effect twice in
    // development, so a listener left behind would double every event of the feed.
    let disposed = false;

    loadLineup();
    loadState();

    // The round of this fixture is what the matchday scoreboard follows. It comes from
    // the fixture list, because the match itself does not carry the round.
    FixtureApi.list()
      .then(fixtures => {
        const fixture = fixtures.find(item => item.matchId === matchId);
        if (fixture && !disposed) setRoundId(fixture.roundId);
      })
      .catch(() => undefined);

    // The persisted event log is the source of the feed: it is what a finished match
    // shows, and what fills the gap when a live client reconnects.
    lastSequence.current = 0;
    MatchApi.getEvents(matchId)
      .then(events => {
        if (disposed) return;
        lastSequence.current = events.length > 0 ? events[events.length - 1].sequence : 0;
        setFeed(events.map(convertToFeedEvent));
      })
      .catch(error => console.error('Failed to load the match events:', error));

    // The match is driven by the server: we subscribe and render whatever it pushes.
    // No client side clock and no polling loop.
    const offState = MatchHubClient.onState(next => {
      setState(next);
      setIsPaused(next.isPaused);

      // The server stops the loop by itself at the interval; the client owns the
      // decision to leave it.
      if (next.isHalfTime) {
        setShowHalfTimeModal(true);
      }

      if (next.isFinished) {
        setShowEndModal(true);
      }

    });

    const offEvent = MatchHubClient.onEvent(async events => {
      if (disposed || events.length === 0) return;

      const newest = events[events.length - 1].sequence;
      const previous = lastSequence.current;

      // A hole in the sequence means an event never reached this client: the
      // persisted log is the source of truth, so the missing ones are fetched before
      // the new ones are appended.
      if (previous > 0 && events[0].sequence > previous + 1) {
        try {
          const missing = await MatchApi.getEvents(matchId, previous);
          if (disposed) return;

          const fresh = missing.filter(e => e.sequence > previous && e.sequence < events[0].sequence);
          if (fresh.length > 0) {
            const currentFeed = useGameState.getState().feed;
            setFeed([...currentFeed, ...fresh.map(convertToFeedEvent)]);
          }
        } catch (error) {
          console.error('Failed to recover the missed match events:', error);
        }
      }

      if (disposed) return;

      const known = useGameState.getState().feed;
      const knownSequence = known.length > 0 ? known[known.length - 1].sequence : 0;
      const fresh = events.filter(e => e.sequence > knownSequence);
      if (fresh.length === 0) return;

      lastSequence.current = newest;
      setFeed([...known, ...fresh.map(convertToFeedEvent)]);
    });

    const offResult = MatchHubClient.onResult(result => {
      setMatchResult(result);
      setShowEndModal(true);
    });

    // The score of every match of the round. The four matches of a matchday are
    // simulated together by the backend, so this is the same clock as the match being
    // watched, not a second guess at it.
    const offScore = MatchHubClient.onScore(score => {
      setMatchdayScores(previous => {
        const rest = previous.filter(item => item.matchId !== score.matchId);
        return [...rest, score];
      });
    });

    // The lease the connection hands back is what releases it, so a cleanup that
    // arrives late cannot close the connection the next mount just opened.
    const lease = MatchHubClient.connect(matchId);

    return () => {
      disposed = true;
      offState();
      offEvent();
      offResult();
      offScore();
      MatchHubClient.disconnect(lease);
    };
  }, [matchId]);

  // The matchday subscription follows the round, which is only known once the fixture
  // list answered.
  useEffect(() => {
    if (!roundId) return;
    MatchHubClient.subscribeMatchday(roundId).catch(error =>
      console.error('Failed to subscribe to the round scores:', error)
    );
  }, [roundId]);

  // A substitution changes who is on the pitch, so the lineup is read again.
  useEffect(() => {
    const last = feed[feed.length - 1];
    if (last?.type === 'SubstitutionMade') {
      loadLineup();
    }
  }, [feed, loadLineup]);

  const pause = async () => {
    if (!matchId) return;
    await MatchHubClient.pause(matchId);
  };

  const resume = async () => {
    if (!matchId) return;
    await MatchHubClient.resume(matchId);
  };

  const changeSpeed = async (speed: number) => {
    if (!matchId) return;
    if (state) {
      setState({ ...state, speed });
    }
    await MatchHubClient.changeSpeed(matchId, speed);
  };

  const continueSecondHalf = async () => {
    if (!matchId) return;
    await MatchHubClient.continueSecondHalf(matchId);
    setShowHalfTimeModal(false);
  };

  /**
   * Asks the backend for the substitution. The backend is the one that decides whether
   * the pair is legal; a refusal comes back as a message the manager can read.
   */
  const substitute = async (playerOutId: string, playerInId: string) => {
    if (!matchId || !lineup) return;

    const teamId = userTeamId;
    if (!teamId) return;

    setSubstituting(true);
    setSubstitutionError(null);

    try {
      const result = await MatchApi.substitute(matchId, teamId, playerOutId, playerInId);
      if (!result.accepted) {
        setSubstitutionError(result.errorMessage || 'A substituição não foi aceita.');
        return;
      }

      await loadLineup();
    } catch (err: any) {
      const code = err?.response?.data?.code;
      setSubstitutionError(
        code ? `${code}: ${err.response.data.detail}` : 'Não foi possível fazer a substituição.'
      );
    } finally {
      setSubstituting(false);
    }
  };

  // Everything below reads these, including the command handlers, so they are derived
  // before them. They are null only while the first request is still in flight.
  const homeTeam = lineup?.homeTeam ?? null;
  const awayTeam = lineup?.awayTeam ?? null;
  const homeColor = homeTeam?.primaryColor || '#f2d34f';
  const awayColor = awayTeam?.primaryColor || '#57a6ff';
  // Which team the manager commands comes from the server when the match is live, and
  // from the club he picked otherwise: a finished match has no manager attached to it.
  const managedTeamId = state?.userTeamId ?? userTeam?.id;
  const userTeamIdx = managedTeamId
    ? lineup?.homeTeam.id === managedTeamId ? 0 : 1
    : lineup?.userTeamIndex ?? 0;
  const userLineup = userTeamIdx === 0 ? lineup?.homeLineup ?? [] : lineup?.awayLineup ?? [];
  const userBench = userTeamIdx === 0 ? lineup?.homeBench ?? [] : lineup?.awayBench ?? [];
  const userTeamId = managedTeamId;
  const userSubstitutionsUsed = userTeamIdx === 0
    ? state?.substitutionsUsedHome ?? 0
    : state?.substitutionsUsedAway ?? 0;
  const score = `${state?.homeScore ?? 0} × ${state?.awayScore ?? 0}`;
  const minute = state ? `${state.minute.toString().padStart(2, '0')}:${state.second.toString().padStart(2, '0')}` : '00:00';
  const halfLabel = state?.currentHalf === 'Second' ? ' (2º)' : '';
  const canSubstitute = !!lineup && !!state && !state.isFinished;

  if (!lineup || !homeTeam || !awayTeam) {
    return (
      <div className="card match-header">
        <p className="competition">Carregando partida...</p>
      </div>
    );
  }

  return (
    <div>
      <div className="card match-header">
        <div className="competition">
          <span>DIVISÃO 3 • JOGO-TREINO</span>
          <span style={{ float: 'right' }}>
            90 MIN + <span id="stoppageLabel">{state?.stoppageTimeMinutes ?? 3}</span>
            {' • ~3 min em 1×'}
          </span>
        </div>

        <div className="scoreline">
          <div className={`team-name ${userTeamIdx === 0 ? 'user-team team-colored' : 'team-colored'}`}>
            {homeTeam.name}
          </div>
          <div>
            <div className="score" id="score">{score}</div>
            <div className="clock" id="clock">
              {minute}
              {halfLabel}
            </div>
          </div>
          <div className={`team-name away ${userTeamIdx === 1 ? 'user-team team-colored' : 'team-colored'}`}>
            {awayTeam.name}
          </div>
        </div>

        <div id="possessionPlayers" className="possession-lines">
          <div className="possession-line">
            {lineup.homeLineup.map(p => (
              <div
                key={p.playerId}
                className={`possession-card ${p.redCard ? 'sent-off' : ''} ${p.emergencyGK ? 'has-ball' : ''}`}
                style={{ '--energy': energyPercent(p.energy) } as React.CSSProperties}
              >
                <span className="pc-pos">{positionLabel(p.position)}</span>
                <span className="pc-name">{p.name.split(' ')[0]}</span>
                <span className="pc-status">{p.goals > 0 && `⚽${p.goals}`}</span>
                <span className="pc-goals">{p.matchStats?.goals && p.matchStats.goals > 0 && `⚽${p.matchStats.goals}`}</span>
                <div className={`pc-energy-fill ${energyClass(p.energy)}`}></div>
              </div>
            ))}
          </div>
          <div className="possession-line">
            {lineup.awayLineup.map(p => (
              <div
                key={p.playerId}
                className={`possession-card ${p.redCard ? 'sent-off' : ''} ${p.emergencyGK ? 'has-ball' : ''}`}
                style={{ '--energy': energyPercent(p.energy) } as React.CSSProperties}
              >
                <span className="pc-pos">{positionLabel(p.position)}</span>
                <span className="pc-name">{p.name.split(' ')[0]}</span>
                <span className="pc-status">{p.goals > 0 && `⚽${p.goals}`}</span>
                <span className="pc-goals">{p.matchStats?.goals && p.matchStats.goals > 0 && `⚽${p.matchStats.goals}`}</span>
                <div className={`pc-energy-fill ${energyClass(p.energy)}`}></div>
              </div>
            ))}
          </div>
        </div>
      </div>

      <div className="main">
        <section className="card narrative">
          <div
            className={`event-hero ${userTeamIdx === 0 ? 'team-hero' : ''}`}
            id="eventHero"
            style={userTeamIdx === 1 ? { '--team-primary': awayColor, '--team-secondary': awayTeam.secondaryColor || awayColor } as React.CSSProperties : { '--team-primary': homeColor, '--team-secondary': homeTeam.secondaryColor || homeColor } as React.CSSProperties}
          >
            <div className="event-icon" id="heroIcon">⚽</div>
            <div className="event-title" id="heroTitle">
              {feed.length > 0 ? feed[feed.length - 1]?.description || 'A bola rola!' : 'A bola rola!'}
            </div>
            <div className="event-desc" id="heroDesc">
              {feed.length > 0 ? feed[feed.length - 1]?.description || 'Os times se estudam nos primeiros minutos.' : 'Os times se estudam nos primeiros minutos.'}
            </div>
          </div>

          <div id="feed" className="feed">
            <MatchFeed feed={feed} homeColor={homeColor} awayColor={awayColor} />
          </div>

          {/* The controls only exist while the match is still being played. */}
          {!state?.isFinished && (
            <>
              <MatchControls
                isPaused={isPaused}
                matchId={matchId}
                onPause={pause}
                onResume={resume}
                onSpeed={changeSpeed}
                speed={state?.speed ?? 1}
              />
              {canSubstitute && (
                <div className="controls">
                  <button
                    className="ctrl"
                    onClick={() => {
                      setSubstitutionError(null);
                      setShowSubstitutionModal(true);
                    }}
                  >
                    🔁 Substituições ({userSubstitutionsUsed}/5)
                  </button>
                  {substitutionError && (
                    <span className="competition" style={{ color: 'var(--danger)' }}>
                      {substitutionError}
                    </span>
                  )}
                </div>
              )}
            </>
          )}
        </section>

        <aside className="card side">
          <MatchdayScoreboard
            scores={matchdayScores}
            currentMatchId={matchId}
            userTeamId={userTeam?.id}
          />

          <div className="tabs">
            <div
              className={`tab ${matchScreen === 'feed' ? 'active' : ''}`}
              onClick={() => setMatchScreen('feed')}
            >Resumo</div>
            <div
              className={`tab ${matchScreen === 'stats' ? 'active' : ''}`}
              onClick={() => setMatchScreen('stats')}
            >Estatísticas do jogo</div>
            <div
              className={`tab ${matchScreen === 'playerStats' ? 'active' : ''}`}
              onClick={() => setMatchScreen('playerStats')}
            >Jogadores</div>
            <div
              className={`tab ${matchScreen === 'tactics' ? 'active' : ''}`}
              onClick={() => setMatchScreen('tactics')}
            >Tática</div>
            <div
              className={`tab ${matchScreen === 'lineup' ? 'active' : ''}`}
              onClick={() => setMatchScreen('lineup')}
            >Escalação</div>
          </div>

          <div className="tabpane active" style={{ display: matchScreen === 'feed' ? 'block' : 'none' }}>
            <MatchStats state={state} />
          </div>

          <div className="tabpane active" style={{ display: matchScreen === 'stats' ? 'block' : 'none' }}>
            <MatchStats state={state} full={true} />
          </div>

          <div className="tabpane active" style={{ display: matchScreen === 'playerStats' ? 'block' : 'none' }}>
            <PlayerStats homeLineup={lineup.homeLineup} awayLineup={lineup.awayLineup} />
          </div>

          <div className="tabpane active" style={{ display: matchScreen === 'tactics' ? 'block' : 'none' }}>
            <TacticsView homeLineup={lineup.homeLineup} awayLineup={lineup.awayLineup} />
          </div>

          <div className="tabpane active" style={{ display: matchScreen === 'lineup' ? 'block' : 'none' }}>
            <LineupView lineup={lineup} />
          </div>
        </aside>
      </div>

      <EndModal
        show={showEndModal}
        result={matchResult}
        onClose={() => setShowEndModal(false)}
        onBackToLeague={() => navigate('/league')}
      />
      <HalfTimeModal
        show={showHalfTimeModal}
        homeTeam={homeTeam}
        awayTeam={awayTeam}
        score={score}
        lineup={userLineup}
        bench={userBench}
        substitutionsUsed={userSubstitutionsUsed}
        busy={substituting}
        onSubstitute={substitute}
        onContinue={continueSecondHalf}
      />
      <SubstitutionModal
        show={showSubstitutionModal}
        team={userTeamIdx === 0 ? homeTeam : awayTeam}
        lineup={userLineup}
        bench={userBench}
        substitutionsUsed={userSubstitutionsUsed}
        busy={substituting}
        onSubstitute={substitute}
        onClose={() => {
          setShowSubstitutionModal(false);
          setSubstitutionError(null);
        }}
      />
      {/* A penalty of the manager's own club stops the clock until he names the taker, so
          the dialog follows the state the server publishes rather than a local flag. */}
      <PenaltyModal
        show={!!state?.penalty?.awaitingSelection}
        candidates={state?.penalty?.candidates ?? []}
        onSelected={async playerId => {
          if (!userTeamId) return;

          setPenaltyError(null);

          try {
            // The taker is named for the club the penalty was awarded to, which is the
            // one the manager is watching: the state that opened this dialog says which.
            const result = await MatchHubClient.selectPenaltyTaker(matchId, userTeamId, playerId);

            if (result && result.accepted === false) {
              setPenaltyError(result.errorMessage || 'O pênalti não pode ser cobrado agora.');
            }
          } catch (error) {
            console.error('Failed to select the penalty taker:', error);
            setPenaltyError('Não foi possível cobrar o pênalti.');
          }
        }}
        error={penaltyError}
        onClose={() => setPenaltyError(null)}
      />
    </div>
  );
};

export default MatchScreen;
