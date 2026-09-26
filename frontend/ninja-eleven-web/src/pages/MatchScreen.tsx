import React, { useEffect, useMemo, useState, useCallback, useRef } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { feedOf, useGameState, type FeedEvent } from '@/state';
import { MatchApi } from '@/api';
import MatchHubClient from '@/signalr/MatchHubClient';
import type {
  MatchEngineEventDto,
  MatchStateDto,
  MatchLineupDto,
  MatchPlayerDto,
  MatchResult
} from '@/types';
import { convertToFeedEvent } from '@/services/formatters';
import MatchFeed from '@/components/Match/MatchFeed';
import { buildNameIndex } from '@/components/Match/NarrativeText';
import MatchControls from '@/components/Match/MatchControls';
import MatchStats from '@/components/Match/MatchStats';
import OnPitchList from '@/components/Match/OnPitchList';
import TeamSheet from '@/components/Match/TeamSheet';
import EndModal from '@/components/Modals/EndModal';
import HalfTimeModal from '@/components/Modals/HalfTimeModal';
import PenaltyModal from '@/components/Modals/PenaltyModal';
import SubstitutionModal from '@/components/Modals/SubstitutionModal';
import MatchdayScoreboard, { type MatchScore } from '@/components/Match/MatchdayScoreboard';
import { FixtureApi } from '@/api';
import { useMatchAudio } from '@/hooks/useMatchAudio';
import { ClubName } from '@/components/Common/Names';

/**
 * How many beats of each other match the panel keeps. A matchday is a scoreboard with a
 * line of words under it, not four more feeds: the last few are what a glance needs, and
 * anything older is the round report's business.
 */
const MATCHDAY_BEATS = 4;

const MatchScreen: React.FC<{ matchId?: string }> = ({ matchId: propMatchId }) => {
  const navigate = useNavigate();
  const { matchId: routeMatchId = '' } = useParams();
  const urlMatchId = propMatchId || routeMatchId;
  const currentMatch = useGameState((s) => s.currentMatch);
  // The match this screen is about. The address says which, and the store fills in a match
  // the manager is already in — both are needed before anything else, because the feed is
  // chosen by this id and choosing it is the first thing the screen does.
  const matchId = urlMatchId || currentMatch?.matchId || '';
  const userTeam = useGameState((s) => s.selectedTeam);
  const setCurrentMatch = useGameState((s) => s.setCurrentMatch);
  const setFeed = useGameState((s) => s.setFeed);
  const setIsPaused = useGameState((s) => s.setIsPaused);
  const setMatchScreen = useGameState((s) => s.setMatchScreen);
  const matchScreen = useGameState((s) => s.matchScreen);
  const isPaused = useGameState((s) => s.isPaused);
  // Only this match's log. The store holds one list for every screen, and reading it raw
  // would show a manager the match they have just finished while they are watching the one
  // that started.
  const feed = useGameState((s) => feedOf(s, matchId));

  const [lineup, setLineup] = useState<MatchLineupDto | null>(null);
  const [state, setState] = useState<MatchStateDto | null>(null);
  const [showEndModal, setShowEndModal] = useState(false);
  const [showHalfTimeModal, setShowHalfTimeModal] = useState(false);
  const [matchResult, setMatchResult] = useState<MatchResult | null>(null);
  const [substitution, setSubstitution] = useState<{ out: string; in: string } | null>(null);
  const [showSubstitutionModal, setShowSubstitutionModal] = useState(false);
  /** The player the manager clicked, already picked to leave the pitch. */
  const [substitutionFor, setSubstitutionFor] = useState<string | null>(null);
  const [substituting, setSubstituting] = useState(false);
  const [substitutionError, setSubstitutionError] = useState<string | null>(null);
  const [penaltyError, setPenaltyError] = useState<string | null>(null);
  const [matchdayScores, setMatchdayScores] = useState<MatchScore[]>([]);
  const [roundId, setRoundId] = useState<string>('');
  /**
   * The last beats of each of the other matches of the round, by match id.
   *
   * The matchday is not a second source of truth about the game: these are the same events
   * the other matches told their own followers, kept apart by match id because a client
   * watching four matches at once receives all four in one stream.
   */
  const [matchdayEvents, setMatchdayEvents] = useState<Record<string, FeedEvent[]>>({});

  // Last sequence the feed already holds. The hub stream can leave a hole when the
  // browser tab is busy or a subscription is replaced, and the sequence is how that
  // hole is detected and filled from the persisted log.
  const lastSequence = useRef(0);

  /** The matches of the round whose history has already been asked for, once each. */
  const seededMatchday = useRef<Set<string>>(new Set());

  // The sound of the match: the crowd for as long as it is being watched, and a whistle or
  // a goal for each thing the engine reports from now on.
  const { muted, toggleMuted } = useMatchAudio({
    feed,
    live: !!matchId && !state?.isFinished,
    matchId,
  });

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
    //
    // It is merged rather than assigned *within one match*. The hub subscription below is
    // already live by the time this request comes back, so a goal scored in the meantime is
    // already in the feed — and replacing the feed with the response threw it away, along
    // with its sound. A response generated before that goal was committed does not contain
    // it either, so there was one path where a goal simply never reached the screen.
    //
    // Across matches it is replaced, not merged. The log in the store is one list shared by
    // every screen, and the previous match ends at a high sequence while this one starts at
    // 1: merged, every event of the new match looks already-known, the feed is never
    // replaced, and the narration simply stops.
    lastSequence.current = 0;
    MatchApi.getEvents(matchId)
      .then(events => {
        if (disposed) return;

        const state = useGameState.getState();
        const known = feedOf(state, matchId);
        const knownSequence = known.length > 0 ? known[known.length - 1].sequence : 0;
        const missing = events.filter(event => event.sequence > knownSequence);

        lastSequence.current =
          Math.max(knownSequence, events.length > 0 ? events[events.length - 1].sequence : 0);

        setFeed(
          knownSequence > 0
            ? [...known, ...missing.map(convertToFeedEvent)]
            : events.map(convertToFeedEvent),
          matchId
        );
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
            const currentFeed = feedOf(useGameState.getState(), matchId);
            setFeed([...currentFeed, ...fresh.map(convertToFeedEvent)], matchId);
          }
        } catch (error) {
          console.error('Failed to recover the missed match events:', error);
        }
      }

      if (disposed) return;

      const known = feedOf(useGameState.getState(), matchId);
      const knownSequence = known.length > 0 ? known[known.length - 1].sequence : 0;
      const fresh = events.filter(e => e.sequence > knownSequence);
      if (fresh.length === 0) return;

      lastSequence.current = newest;
      setFeed([...known, ...fresh.map(convertToFeedEvent)], matchId);
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

    // The beats of the other matches. They are the match's own events, so a goal in the
    // match next door reads exactly as it would in that match's own feed.
    const offMatchdayEvent = MatchHubClient.onMatchdayEvent(payload => {
      setMatchdayEvents(previous => {
        const known = previous[payload.matchId] ?? [];
        const merged = [...known, ...payload.events.map(convertToFeedEvent)]
          .sort((a, b) => a.sequence - b.sequence);

        return { ...previous, [payload.matchId]: merged.slice(-MATCHDAY_BEATS) };
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
      offMatchdayEvent();
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

  // A new round is a new set of matches, so the beats of the last one are dropped: a
  // scoreline in the panel must never be illustrated by a goal from a matchday ago.
  useEffect(() => {
    setMatchdayEvents({});
    seededMatchday.current = new Set();
  }, [roundId]);

  /**
   * The other matches are told what they have already done.
   *
   * A manager who opens the screen at the fortieth minute must not be shown a matchday
   * panel that starts counting from the moment he arrived — the goal that made it 1 x 0
   * was two minutes before he sat down. The events are persisted, so the panel is seeded
   * from them once and the live stream carries it from there.
   *
   * Each match is asked for once. The score of a matchday arrives about once a second, so
   * a filter that only looked at what had already arrived would retry a request that
   * failed on every one of them, and a panel that cannot be seeded would ask the backend
   * about it a thousand times a match.
   */
  useEffect(() => {
    const others = matchdayScores
      .map(score => score.matchId)
      .filter(id => id && id !== matchId && matchdayEvents[id] === undefined);

    if (others.length === 0) return undefined;

    const asked = others.filter(id => {
      if (seededMatchday.current.has(id)) return false;
      seededMatchday.current.add(id);
      return true;
    });

    if (asked.length === 0) return undefined;

    let cancelled = false;

    Promise.all(
      asked.map(async id => {
        const events = await MatchApi.getEvents(id);
        return { id, events: events.map(convertToFeedEvent) };
      })
    )
      .then(loaded => {
        if (cancelled) return;

        setMatchdayEvents(previous => {
          const next = { ...previous };

          for (const { id, events } of loaded) {
            // A beat that arrived over the hub while this was in flight is not thrown away.
            if (next[id] === undefined) {
              next[id] = events.slice(-MATCHDAY_BEATS);
            }
          }

          return next;
        });
      })
      .catch(error => {
        // A panel with no words is still a panel with a scoreline. The live stream keeps
        // filling it, so a failed seed is not worth interrupting the match over.
        console.error('Failed to load the matchday events:', error);
      });

    return () => {
      cancelled = true;
    };
  }, [matchdayScores, matchdayEvents, matchId]);

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

  /**
   * A player clicked under the scoreboard opens the substitution screen with him already
   * picked to come off: the manager has said who is tired, and only has to say who replaces
   * him. The same screen the interval offers.
   */
  const openSubstitutionFor = (player: MatchPlayerDto) => {
    setSubstitutionError(null);
    setSubstitutionFor(player.playerId);
    setShowSubstitutionModal(true);
  };

  const continueSecondHalf = async () => {
    if (!matchId) return;
    await MatchHubClient.continueSecondHalf(matchId);
    setShowHalfTimeModal(false);
  };

  /**
   * Puts the events a command produced into the feed straight away.
   *
   * A command the manager issues himself is a REST call, and the events it produced come
   * back in the answer. They used to be dropped on the floor: the feed was only ever fed
   * by the hub, so a manager's own substitution was the one change of the match that
   * nothing narrated. The sequence check here is the same one the hub handler uses, so if
   * the broadcast does arrive afterwards it is recognised as already known and appended
   * once.
   */
  const appendCommandEvents = (events?: MatchEngineEventDto[] | null) => {
    if (!events || events.length === 0) return;

    const known = feedOf(useGameState.getState(), matchId);
    const knownSequence = known.length > 0 ? known[known.length - 1].sequence : 0;
    const fresh = events.filter(event => event.sequence > knownSequence);

    if (fresh.length === 0) return;

    lastSequence.current = fresh[fresh.length - 1].sequence;
    setFeed([...known, ...fresh.map(convertToFeedEvent)], matchId);
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

      appendCommandEvents(result.events);
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
  // The two clubs and every man who could be named in a sentence of this match, so a name
  // in the feed is a door to a profile. Recomputed only when somebody turns up or leaves:
  // a substitution changes who the feed is allowed to talk about.
  const nameIndex = useMemo(() => {
    if (!lineup) return null;

    return buildNameIndex(
      [lineup.homeTeam, lineup.awayTeam],
      [...lineup.homeLineup, ...lineup.awayLineup, ...lineup.homeBench, ...lineup.awayBench].map(
        player => ({ id: player.playerId, name: player.name })
      )
    );
  }, [lineup]);
  const userSubstitutionsUsed = userTeamIdx === 0
    ? state?.substitutionsUsedHome ?? 0
    : state?.substitutionsUsedAway ?? 0;
  const score = `${state?.homeScore ?? 0} × ${state?.awayScore ?? 0}`;

  /**
   * The share of the ball the home side has had, as a number the engine decided. Before
   * there is a state there is no share, so the bar is left even rather than claiming a 50%
   * nobody has earned.
   */
  const possessionHome = state ? state.homePossessionPercent : 50;

  /** How many other matches of the matchday are running, which is what the tab is for. */
  const otherMatches = matchdayScores.filter(score => score.matchId !== matchId).length;
  const minute = state ? `${state.minute.toString().padStart(2, '0')}:${state.second.toString().padStart(2, '0')}` : '00:00';
  const halfLabel = state?.currentHalf === 'Second' ? '2º' : '';
  const canSubstitute = !!lineup && !!state && !state.isFinished;

  /**
   * Whether the ball is with the manager's club right now, and which of his players is
   * holding it. The engine sets it at every step of a sequence, so this is where the ball
   * is rather than which side has had more of it.
   */
  const ballIsOurs = state?.possession?.team === userTeamIdx;

  /**
   * How long the match still takes, at the pace the backend is running it. The manager
   * does not pick that pace any more, so the estimate follows the server instead of
   * assuming one.
   */
  const estimatedMinutes = Math.max(1, Math.round(3 / Math.max(1, state?.speed ?? 1)));

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
            {` • ~${estimatedMinutes} min no ritmo do servidor`}
          </span>
        </div>

        <div className="scoreline">
          <div className={`team-name ${userTeamIdx === 0 ? 'user-team team-colored' : 'team-colored'}`}>
            <ClubName teamId={homeTeam.id}>{homeTeam.name}</ClubName>
          </div>
          <div>
            <div className="score" id="score">{score}</div>
            <div className="clock" id="clock">
              {minute}
              {halfLabel && <span className="clock-half">{halfLabel}</span>}
            </div>
          </div>
          <div className={`team-name away ${userTeamIdx === 1 ? 'user-team team-colored' : 'team-colored'}`}>
            <ClubName teamId={awayTeam.id}>{awayTeam.name}</ClubName>
          </div>

          {/*
            The share of the ball, under the score and across the two names: the bar starts
            where the home name starts and ends where the away name ends, so the two halves
            of the row are the two halves of the match. The number is the engine's own —
            it counts the seconds each side actually had it — and the client adds nothing
            but the width.
          */}
          <div
            className="possession-bar"
            style={
              {
                '--possession-home': `${possessionHome}%`,
                '--home-team': homeTeam.primaryColor || '#2f6f4f',
                '--away-team': awayTeam.primaryColor || '#2a4a63',
              } as React.CSSProperties
            }
            title={`Posse de bola: ${homeTeam.shortName ?? homeTeam.name} ${possessionHome}% — ${
              awayTeam.shortName ?? awayTeam.name
            } ${100 - possessionHome}%`}
          >
            <span className="possession-bar__share" aria-hidden="true" />
            <span className="possession-bar__labels">
              <span>{possessionHome}%</span>
              <span>{100 - possessionHome}%</span>
            </span>
          </div>
        </div>

        {/*
          The shape each side is playing, read off the eleven that is on the pitch. It is
          shown rather than chosen: the engine measures the team it has been given, so a
          formation here is a description of the match, not a setting on it. It moves when a
          substitution changes the balance of a side.
        */}
        <div className="formation-strip">
          <span className="formation-strip-team">{homeTeam.shortName ?? homeTeam.name}</span>
          <b>{state?.formationHome || '—'}</b>
          <span className="formation-strip-vs">contra</span>
          <b>{state?.formationAway || '—'}</b>
          <span className="formation-strip-team away">{awayTeam.shortName ?? awayTeam.name}</span>
        </div>

        <div id="possessionPlayers" className="on-pitch-list-wrap">
          <div className="on-pitch-head">
            <b>{userTeamIdx === 0 ? homeTeam.name : awayTeam.name}</b>
            <span>
              {ballIsOurs ? 'com a bola' : 'em campo'} • clique num jogador para substituir
            </span>
          </div>
          <OnPitchList
            players={userLineup}
            onSelect={canSubstitute ? openSubstitutionFor : undefined}
            ballCarrierId={ballIsOurs ? state?.possession?.playerId : null}
          />
        </div>
      </div>

      <div className="main">
        <section className="card narrative">
          {(() => {
            // The banner is the latest thing that happened, said once. It used to say "A
            // bola rola!" whenever the last event was not a goal, which is a sentence about
            // a match that has not started being said for the whole of a match that has —
            // a banner that never changes is a banner nobody reads. So the headline is the
            // score on a goal and the minute on anything else: the two facts that are true
            // at every point of a match.
            const latest = feed.length > 0 ? feed[feed.length - 1] : undefined;
            const headline = latest?.isGoal
              ? `${latest.homeScore} × ${latest.awayScore}`
              : latest
                ? `${latest.minute}'`
                : '—';

            return (
              <div
                className={`event-hero ${userTeamIdx === 0 ? 'team-hero' : ''}`}
                id="eventHero"
                style={userTeamIdx === 1 ? { '--team-primary': awayColor, '--team-secondary': awayTeam.secondaryColor || awayColor } as React.CSSProperties : { '--team-primary': homeColor, '--team-secondary': homeTeam.secondaryColor || homeColor } as React.CSSProperties}
              >
                <div className="event-icon" id="heroIcon">{latest?.icon || '⚽'}</div>
                <div className="event-title" id="heroTitle">{headline}</div>
                <div className="event-desc" id="heroDesc">
                  {latest?.description || 'Os times se estudam nos primeiros minutos.'}
                </div>
              </div>
            );
          })()}

          {/*
            The feed is the narration of this match, and it is not a tab: it is what the
            screen is. The matchday moved next to the team sheet, which is where a manager
            looks for the state of the round rather than for the story of his own game.
          */}
          <div id="feed" className="feed">
            <MatchFeed
              feed={feed}
              homeColor={homeColor}
              awayColor={awayColor}
              managerTeamId={managedTeamId}
              homeTeamId={lineup?.homeTeam.id}
              awayTeamId={lineup?.awayTeam.id}
              nameIndex={nameIndex}
            />
          </div>

          {/* The controls only exist while the match is still being played. */}
          {!state?.isFinished && (
            <>
              <MatchControls
                isPaused={isPaused}
                onPause={pause}
                onResume={resume}
                speed={state?.speed ?? 1}
                muted={muted}
                onToggleMuted={toggleMuted}
              />
              {canSubstitute && (
                <div className="controls">
                  <button
                    className="ctrl"
                    onClick={() => {
                      setSubstitutionError(null);
                      setSubstitutionFor(null);
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
          <div className="tabs">
            <div
              className={`tab ${matchScreen === 'stats' ? 'active' : ''}`}
              onClick={() => setMatchScreen('stats')}
            >Estatísticas</div>
            <div
              className={`tab ${matchScreen === 'lineup' ? 'active' : ''}`}
              onClick={() => setMatchScreen('lineup')}
            >Escalação</div>
            <div
              className={`tab ${matchScreen === 'matchday' ? 'active' : ''}`}
              onClick={() => setMatchScreen('matchday')}
            >
              Outras
              {otherMatches > 0 && <span className="tab-badge">{otherMatches}</span>}
            </div>
          </div>

          {/* Resumo and Estatísticas do jogo were two readings of the same numbers, so there
              is one tab now and it shows every row the match has produced. */}
          <div className="tabpane active" style={{ display: matchScreen === 'stats' ? 'block' : 'none' }}>
            <MatchStats state={state} />
          </div>

          <div className="tabpane active" style={{ display: matchScreen === 'lineup' ? 'block' : 'none' }}>
            <TeamSheet
              lineup={lineup}
              userTeamIndex={userTeamIdx}
              canSubstitute={canSubstitute}
              substitutionsUsed={userSubstitutionsUsed}
              busy={substituting}
              onSubstitute={substitute}
            />
          </div>

          <div className="tabpane active" style={{ display: matchScreen === 'matchday' ? 'block' : 'none' }}>
            {otherMatches > 0 ? (
              <MatchdayScoreboard
                scores={matchdayScores}
                currentMatchId={matchId}
                userTeamId={managedTeamId ?? undefined}
                eventsByMatch={matchdayEvents}
              />
            ) : (
              <div className="league-empty">
                {matchdayScores.length === 0
                  ? 'As outras partidas da rodada ainda não começaram.'
                  : 'Esta é a única partida em andamento na rodada.'}
              </div>
            )}
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
        preselectOut={substitutionFor}
        busy={substituting}
        onSubstitute={substitute}
        onClose={() => {
          setShowSubstitutionModal(false);
          setSubstitutionFor(null);
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
