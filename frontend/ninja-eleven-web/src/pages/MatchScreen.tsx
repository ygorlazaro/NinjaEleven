import React, { useEffect, useMemo, useState, useCallback, useRef } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { feedOf, useGameState, type FeedEvent } from '@/state';
import { MatchApi } from '@/api';
import MatchHubClient from '@/signalr/MatchHubClient';
import type {
  MatchEngineEventDto,
  MatchStateDto,
  MatchContextDto,
  MatchLineupDto,
  MatchPlayerDto,
  MatchResult,
} from '@/types';
import { convertToFeedEvent } from '@/services/formatters';
import MatchFeed from '@/components/Match/MatchFeed';
import { buildNameIndex } from '@/components/Match/NarrativeText';
import MatchControls from '@/components/Match/MatchControls';
import MatchStats from '@/components/Match/MatchStats';
import OnPitchList from '@/components/Match/OnPitchList';
import GoalHistory from '@/components/Match/GoalHistory';
import TeamSheet from '@/components/Match/TeamSheet';
import EndModal from '@/components/Modals/EndModal';
import HalfTimeModal from '@/components/Modals/HalfTimeModal';
import PenaltyModal from '@/components/Modals/PenaltyModal';
import SubstitutionModal from '@/components/Modals/SubstitutionModal';
import MatchdayScoreboard, { type MatchScore } from '@/components/Match/MatchdayScoreboard';
import { MatchContextBar, FirstLegStrip } from '@/components/Match/MatchContextBar';
import ShootoutPanel from '@/components/Match/ShootoutPanel';
import { FixtureApi } from '@/api';
import { useMatchAudio } from '@/hooks/useMatchAudio';
import { useCountdown } from '@/hooks/useCountdown';
import { formatLimo } from '@/services/limo';
import { ClubName } from '@/components/Common/Names';
import { SponsorMark } from '@/components/Sponsor/SponsorMark';
import ClubCrest from '@/components/Club/ClubCrest';
import { starsToString } from '@/services/formatters';

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

  const [menOnRecord, setLineup] = useState<MatchLineupDto | null>(null);
  // Where this match is: season, day, competition, phase, ground, and the other leg of
  // a cup tie. It is read once per match and never worked out here.
  const [context, setContext] = useState<MatchContextDto | null>(null);
  const [state, setState] = useState<MatchStateDto | null>(null);
  const [showEndModal, setShowEndModal] = useState(false);
  const [showHalfTimeModal, setShowHalfTimeModal] = useState(false);
  const [matchResult, setMatchResult] = useState<MatchResult | null>(null);
  const [substitution, setSubstitution] = useState<{ out: string; in: string } | null>(null);
  const [showSubstitutionModal, setShowSubstitutionModal] = useState(false);

  /**
   * Which injured man the manager has already put aside, or null when none has.
   *
   * It is the man's id and not a flag because a flag would carry over: a striker hurt in the
   * first half and the same striker hurt again in the second is two questions, and the
   * second one has to be asked.
   */
  const [dismissedInjuryFor, setDismissedInjuryFor] = useState<string | null>(null);

  /**
   * Whether the penalty dialog is on the screen.
   *
   * It follows the state the server publishes — a penalty that happened while this screen was
   * closed still has to reach the manager — and the local flag only records that he has put
   * it aside, so the question does not jump back in front of a manager who answered nothing
   * yet on purpose. The window stays open either way and the countdown keeps running.
   */
  const [showPenaltyModal, setShowPenaltyModal] = useState(false);
  /** The player the manager clicked, already picked to leave the pitch. */
  const [substitutionFor, setSubstitutionFor] = useState<string | null>(null);
  const [substituting, setSubstituting] = useState(false);
  const [substitutionError, setSubstitutionError] = useState<string | null>(null);
  const [penaltyError, setPenaltyError] = useState<string | null>(null);
  /** Why the last order of takers was refused, said where the manager is looking. */
  const [shootoutError, setShootoutError] = useState<string | null>(null);
  const [namingShootout, setNamingShootout] = useState(false);
  const [matchdayScores, setMatchdayScores] = useState<MatchScore[]>([]);
  const [roundId, setRoundId] = useState<string>('');
  const [matchdayEvents, setMatchdayEvents] = useState<Record<string, FeedEvent[]>>({});
  /** Whether the score is currently flashing after a goal. */
  const [scoringFlash, setScoringFlash] = useState(false);

  // Last sequence the feed already holds. The hub stream can leave a hole when the
  // browser tab is busy or a subscription is replaced, and the sequence is how that
  // hole is detected and filled from the persisted log.
  const lastSequence = useRef(0);

  /** The score the last goal flash was triggered by, so a goal is flashed once. */
  const lastScoreRef = useRef<string | null>(null);

  /** The matches of the round whose history has already been asked for, once each. */
  const seededMatchday = useRef<Set<string>>(new Set());

  /**
   * Whether the headless takeover has already been asked for on this screen visit. The cup
   * window starts every fixture at once, so a manager who reaches his club's match on a day
   * it has already kicked off finds a headless session: the keyboard is handed over the
   * first time that state shows up, and never asked for again while this screen is mounted.
   */
  const attachAttempted = useRef(false);
  const lastLineupMinute = useRef(-1);

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

  /**
   * Where a manager goes when the match he is watching stops existing.
   *
   * A match abandoned is one the world took back: its process is gone and the fixture has
   * been reopened, so the fixture is either owed again or already has a new match on it.
   * Staying put is the one answer that is certainly wrong — the scoreboard would be the last
   * minute anybody played, held still, with no end and no explanation. So the fixture is
   * asked where it went, and the screen follows it: to the new match when there is one, and
   * back to the club when there is not, because a manager whose fixture is owed again is a
   * manager who is going to play it.
   */
  const followTheFixtureForward = useCallback(async (abandonedMatchId: string) => {
    try {
      const abandoned = await MatchApi.get(abandonedMatchId);
      const fixture = await FixtureApi.get(abandoned.fixtureId);

      if (fixture.matchId && fixture.matchId !== abandonedMatchId) {
        navigate(`/match/${fixture.matchId}`, { replace: true });
        return;
      }

      navigate('/league', { replace: true });
    } catch {
      // A fixture the API will not answer about is a screen with nowhere to go, and a screen
      // that says so beats a screen that keeps showing a match that is over.
      navigate('/league', { replace: true });
    }
  }, [navigate]);

  /**
   * The header's own facts about the match. Fetched on its own and allowed to fail: a
   * screen that shows no ground because one call did not come back is a worse screen than
   * one that shows the score, so the rest of the header does not wait for it.
   */
  const loadContext = useCallback(async () => {
    if (!matchId) return;
    try {
      setContext(await MatchApi.getContext(matchId));
    } catch {
      setContext(null);
    }
  }, [matchId]);

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
    attachAttempted.current = false;

     loadLineup();
    loadState();
    loadContext();

    // The round of this fixture is what the matchday scoreboard follows. It comes from
    // the fixture list, because the match itself does not carry the round.
    FixtureApi.list()
      .then(fixtures => {
        const fixture = fixtures.find(item => item.matchId === matchId);
        if (fixture && !disposed) {
          setRoundId(fixture.roundId);

          if (fixture.roundId) {
            // The rest of the matchday: what the other clubs are doing this afternoon.
            FixtureApi.listByRound(fixture.roundId).then(roundFixtures => {
              if (!disposed) {
                const initialScores: MatchScore[] = roundFixtures
                  .filter(f => f.matchId !== matchId) // exclude current match
                  .map(f => ({
                    roundId: fixture.roundId,
                    matchId: f.matchId || '',
                    fixtureId: f.id,
                    homeTeamId: f.homeTeamId,
                    homeTeamName: f.homeTeam?.name || '',
                    homeShortName: f.homeTeam?.shortName || '',
                    // The shield a row of the matchday carries comes from the fixture, which
                    // already holds the whole club. The hub pushes the same two colours with
                    // every score, so a row that arrives later draws the same shield.
                    homePrimaryColor: f.homeTeam?.primaryColor || '',
                    homeSecondaryColor: f.homeTeam?.secondaryColor || '',
                    awayTeamId: f.awayTeamId,
                    awayTeamName: f.awayTeam?.name || '',
                    awayShortName: f.awayTeam?.shortName || '',
                    awayPrimaryColor: f.awayTeam?.primaryColor || '',
                    awaySecondaryColor: f.awayTeam?.secondaryColor || '',
                    homeGoals: f.homeGoals ?? 0,
                    awayGoals: f.awayGoals ?? 0,
                    minute: f.status === 'InProgress' ? 1 : 0,
                    half: 'First',
                    status: f.status,
                    isFinished: f.status === 'Finished',
                    homeOwnGoals: 0,
                    awayOwnGoals: 0,
                    homeYellowCards: 0,
                    awayYellowCards: 0,
                    homeRedCards: 0,
                    awayRedCards: 0,
                    homeInjuries: 0,
                    awayInjuries: 0,
                  }));
                setMatchdayScores(initialScores);
              }
            }).catch(() => undefined);
          }
        }
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

      // The eleven's energy is read from the lineup, and the lineup used to be read again
      // only when a substitution was announced. That froze every energy bar at its kick-off
      // value for as long as the match ran and then moved the whole eleven at once — a
      // striker who had been tiring since the first whistle appeared to lose eight points
      // the instant a manager changed a defender, which is not a thing the match did to him.
      // The server publishes a snapshot every few minutes and on anything worth seeing, so
      // the lineup follows the same beat the scoreboard does and the bars track the match.
      if (!next.isFinished && next.minute !== lastLineupMinute.current) {
        lastLineupMinute.current = next.minute;
        loadLineup();
      }

      // The interval is the backend's twenty seconds and the screen only raises the question:
      // the manager may leave the break early by pressing it, and nobody keeps it open by
      // not pressing. It is a decision about the manager's own club, so it is only raised
      // for a match he is playing: a match of two other clubs has its interval left by the
      // loop that is simulating it, and a manager watching it has no button to press about
      // a second half that is not his.
      if (next.isHalfTime && next.userTeamId) {
        setShowHalfTimeModal(true);
      }

      // And it is raised once: a state that keeps saying "half time" while the break counts
      // down would pull the dialog back in front of a manager who had already put it aside.
      if (!next.isHalfTime) {
        setShowHalfTimeModal(false);
      }

      // And the same for the penalty: the dialog follows the server's window, and a manager
      // who has put it aside is not pulled back into it by the next snapshot.
      if (next.penaltyAwaitingSelection) {
        setShowPenaltyModal(true);
      } else {
        setShowPenaltyModal(false);
      }

      // An abandoned match is finished and is not a match that was played: it is one whose
      // process is gone, and its fixture has been handed back to the world. The end modal
      // would tell the manager his team lost a game nobody finished, so the screen goes
      // wherever the fixture went instead — the new match if the world has already started
      // one, and the club's own page if it is still owed.
      if (next.status === 'Abandoned') {
        followTheFixtureForward(matchId);
        return;
      }

      if (next.isFinished) {
        setShowEndModal(true);
      }

      // A cup window kicks off every fixture of it at once — the manager's club included —
      // so a manager who reaches his own club's match while it is already being played
      // finds a headless session: the engine is running both sides, the interval passes on
      // its own and a substitution is refused because no manager is attached. The first time
      // that live state shows up, the keyboard is handed over; the server refuses anything
      // that is not the manager's own club, so another club's match is simply left running.
      if (attachAttempted.current) {
        // already claimed on this visit
      } else if (!next.isFinished && next.userTeamId === null) {
        const manager = useGameState.getState().selectedTeam;
        if (manager) {
          attachAttempted.current = true;
          MatchHubClient.attachManager(matchId, manager.id).catch(() => undefined);
        }
      }

    });

    const offEvent = MatchHubClient.onEvent(async stream => {
      // The stream says which match it is. A screen that moved from one live match to another
      // on the same connection is briefly on both, and a beat of the match left behind is not
      // a beat of this one — it would be written into this match's feed under its sequence.
      if (disposed || stream.matchId !== matchId) return;

      const events = stream.events;
      if (events.length === 0) return;

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
        // Matched on the fixture, which is known from the round's fixture list, and not on
        // the match: a fixture that has not kicked off has no match id yet, so a line keyed
        // by match id is missing for exactly the matches the manager is waiting to see start,
        // and the first score each of them sends lands as a second row for the same game.
        const index = previous.findIndex(item => item.fixtureId === score.fixtureId);

        if (index === -1) {
          return [...previous, score];
        }

        // Updated where it is, not moved to the end. A scoreboard that drops the updated row
        // and appends it reorders the whole matchday on every tick of every match, and a
        // manager cannot follow four games that will not sit still.
        const next = [...previous];
        next[index] = { ...previous[index], ...score };
        return next;
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
  }, [matchId, followTheFixtureForward]);

  /**
   * The matchday is followed on the round group, and joining it is a question about the
   * round rather than about the match being watched.
   *
   * The round is not known when this screen mounts: it comes from reading the fixture list,
   * which is another request. Asking for it inside the match's own effect therefore tests an
   * empty string, never joins, and is never retried, because the effect that would retry it
   * does not run again. The panel then shows one REST snapshot taken when the screen opened —
   * every minute hard-coded to 1 and every card to 0 — and no more, while the backend is
   * publishing every other match's score and beats to a group nobody is in. That is the whole
   * reason the other matches of a matchday used to look frozen.
   */
  useEffect(() => {
    if (!roundId) return;

    MatchHubClient.subscribeMatchday(roundId).catch(error =>
      console.error('Failed to subscribe to the round scores:', error)
    );

    return () => {
      // Nothing is awaited here: membership in a group is idempotent, so React running this
      // twice in development costs one extra join and leave rather than a screen left
      // following the matchday of a match it is no longer watching.
      MatchHubClient.leaveRound(roundId).catch(() => undefined);
    };
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

      // The change is made, so the screen that asked for it has nothing left to say. This
      // matters most when a man who cannot carry on was the reason it was open: the modal
      // is bound to the question, and the backend has answered it.
      setShowSubstitutionModal(false);
      setSubstitutionFor(null);
      setDismissedInjuryFor(null);

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

  // The eleven carries the men; the tick carries what the match is currently worth to each of
  // them. They are merged here rather than kept in one piece of state because they arrive on
  // different beats: a substitution re-reads the men and leaves the notes behind, and a tick
  // moves the notes and leaves the men alone. One stored copy would have to be patched by
  // both and would blank the numbers for a moment every time a change was made.
  //
  // The merged one is called `lineup` and the raw one `menOnRecord`, rather than the other way
  // round, so that everything below reads the notes without asking. The first version had it
  // the other way round and the eleven under the scoreboard was drawn from the raw list, which
  // is a way of saying that a number everyone had agreed to show was shown on one screen.
  const lineup = useMemo<MatchLineupDto | null>(() => {
    if (!menOnRecord) return null;

    const live = state?.liveRatings;
    if (!live || live.length === 0) return menOnRecord;

    const byPlayer = new Map(live.map(entry => [entry.playerId, entry]));
    const withNote = (player: MatchPlayerDto): MatchPlayerDto => {
      const entry = byPlayer.get(player.playerId);
      return entry
        ? { ...player, rating: entry.rating, ratingBand: entry.ratingBand, minutesPlayed: entry.minutesPlayed }
        : player;
    };

    return {
      ...menOnRecord,
      homeLineup: menOnRecord.homeLineup.map(withNote),
      awayLineup: menOnRecord.awayLineup.map(withNote),
      homeBench: menOnRecord.homeBench.map(withNote),
      awayBench: menOnRecord.awayBench.map(withNote),
    };
  }, [menOnRecord, state?.liveRatings]);

  // Everything below reads these, including the command handlers, so they are derived
  // before them. They are null only while the first request is still in flight.
  const homeTeam = lineup?.homeTeam ?? null;
  const awayTeam = lineup?.awayTeam ?? null;
  const homeColor = homeTeam?.primaryColor || '#f2d34f';
  const awayColor = awayTeam?.primaryColor || '#57a6ff';
  // Which team the manager commands comes from the server when the match is live, and
  // from the club he picked otherwise: a finished match has no manager attached to it.
  const managedTeamId = state?.userTeamId ?? userTeam?.id;
  // **Whether his club is in this match at all, asked before which side of it is his.**
  //
  // A manager follows a whole matchday: the round group carries the other clubs' scorelines
  // and their goals to the screen he is on, and a row of "Outras" is a door into a match he
  // is not playing in. There the answer to "which side is mine" is neither, and a screen
  // that answered "the away one" would put the other club's eleven under the substitution
  // button. The backend refuses a change for a club the manager does not command, so the
  // honest thing is not to offer it: this is null there and every command is closed.
  const isInThisMatch = !!managedTeamId && !!lineup
    && (lineup.homeTeam.id === managedTeamId || lineup.awayTeam.id === managedTeamId);
  const userTeamIdx = managedTeamId
    ? lineup?.homeTeam.id === managedTeamId ? 0 : 1
    : lineup?.userTeamIndex ?? 0;
  const userLineup = userTeamIdx === 0 ? lineup?.homeLineup ?? [] : lineup?.awayLineup ?? [];
  const userBench = userTeamIdx === 0 ? lineup?.homeBench ?? [] : lineup?.awayBench ?? [];
  // The club a command is sent for, and the only one it may be sent for. Null in a match of
  // two other clubs, which is what closes the substitution button over there.
  const userTeamId = isInThisMatch ? managedTeamId : null;
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
   * A goal makes the score jump, so the scoreboard is flashed the moment the home or
   * away count changes. The state arrives on every tick, but the counts move only on a
   * goal; comparing them to the last seen value is enough to know a goal just landed,
   * and the first value is seeded without flashing so a manager who joins late does not
   * see the live score strobe. The flash is cleared after a few seconds of silence.
   */
  useEffect(() => {
    if (!state) return;

    const current = `${state.homeScore ?? 0} × ${state.awayScore ?? 0}`;
    if (lastScoreRef.current === null) {
      lastScoreRef.current = current;
      return;
    }

    if (current !== lastScoreRef.current) {
      lastScoreRef.current = current;
      setScoringFlash(true);
      const timer = setTimeout(() => setScoringFlash(false), 3000);
      return () => clearTimeout(timer);
    }
  }, [state?.homeScore, state?.awayScore]);

  /**
   * The share of the ball the home side has had, as a number the engine decided — or null
   * when the engine has not said.
   *
   * It used to fall back to 50 and the bar was drawn even, which is right for a bar that only
   * has a width. The two labels beside it were the problem: they are printed in bold above
   * the pitch, so the screen was stating a possession split nobody had earned, and "50% — 50%"
   * is the shape of a real answer. Both ends now read the same value or neither of them does.
   */
  const possessionHome = state ? state.homePossessionPercent : null;
  const possessionAway = state ? state.awayPossessionPercent : null;

  /** How many other matches of the matchday are running, which is what the tab is for. */
  const otherMatches = matchdayScores.filter(score => score.matchId !== matchId).length;
  const minute = state ? `${state.minute.toString().padStart(2, '0')}:${state.second.toString().padStart(2, '0')}` : '00:00';
  const halfLabel = state?.currentHalf === 'Second' ? '2º' : '';

  /**
   * The two moments the backend is counting down on its own, drawn next to the clock.
   *
   * A penalty is the one window that stops the match, and it stops for fifteen seconds: the
   * number says what is left of that before the engine sends somebody to the spot by itself.
   * The interval is twenty seconds of break, and the number says when the second half begins
   * whether or not anybody pressed anything. Both deadlines are the server's — the screen
   * only shows the wait, and it never decides that a wait is over.
   */
  const penaltySeconds = useCountdown(state?.penaltyEndsAt);
  const halfTimeSeconds = useCountdown(state?.halfTimeEndsAt);
  const windowWaiting =
    penaltySeconds != null
      ? { label: 'Pênalti', seconds: penaltySeconds, hint: 'o melhor cobrador entra se você não escolher' }
      : halfTimeSeconds != null
        ? { label: 'Intervalo', seconds: halfTimeSeconds, hint: 'o segundo tempo começa sozinho' }
        : null;
  /**
   * A change is available only in a match the manager's club is playing in, and only while
   * it is being played. The first half of that test is the one that matters here: a manager
   * watching a match of two other clubs has no eleven to change, and a button over a
   * lineup that is not his would be a decision the backend refuses.
   */
  const canSubstitute = !!lineup && !!state && !state.isFinished && isInThisMatch;

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
        <MatchContextBar context={context} />

        <div className="competition" style={{ marginTop: 6 }}>
          <span style={{ float: 'right' }}>
            {/* The added time is drawn by the engine at the kick-off and announced by each
                half, so there is nothing to print before it arrives. It used to be a 3 in the
                client, which is a referee's decision nobody made. */}
            90 MIN +{' '}
            <span id="stoppageLabel">{state?.stoppageTimeMinutes ?? '—'}</span>
            {` • ~${estimatedMinutes} min no ritmo do servidor`}
          </span>
        </div>

        <div className="match-attendance">
          Público: <b>{state?.attendance?.toLocaleString('pt-BR') ?? '—'}</b> pagantes
          {state?.gateRevenue && (
            <span style={{ marginLeft: '16px' }}>
              Bilheteria: <b>{formatLimo(state.gateRevenue)}</b>
            </span>
          )}
        </div>

        <div className="scoreline">
          <div className={`team-name ${userTeamIdx === 0 ? 'user-team team-colored' : 'team-colored'}`}>
            {/* The shield of each club beside its name, the same one the club screen and
                the matchday carry. A scoreboard says two names; a manager recognises a club
                by its colours before he has finished reading them. */}
            <ClubCrest
              crest={homeTeam.crest}
              primary={homeTeam.primaryColor}
              secondary={homeTeam.secondaryColor}
              name={homeTeam.name}
              className="match-crest"
            />
            <ClubName teamId={homeTeam.id}>{homeTeam.name}</ClubName>
            <span className="team-stars" style={{ color: 'var(--accent)', marginLeft: '8px' }}>{starsToString(homeTeam.stars)}</span>
            {/* Who pays for the shirt, under the name it is on. Stamped at the kick-off, so a
                deal that ends at half-time does not take the company off a club's back while the
                manager is still watching it play. */}
            <SponsorMark sponsor={lineup!.homeSponsor} className="team-sponsor" />
            {/* The goals under the name they belong to. A score says how many; the manager
                watching this match reads who, and an own goal and a penalty are not the same
                goal as any other. */}
            <GoalHistory feed={feed} teamId={homeTeam.id} lineup={lineup!} team={homeTeam} side={lineup!.homeKitSide} />
          </div>
          <div>
            <div className={`score ${scoringFlash ? 'score--flash' : ''}`} id="score">{score}</div>
            <div className="clock" id="clock">
              {minute}
              {halfLabel && <span className="clock-half">{halfLabel}</span>}
            </div>
            {/* The wait the backend is counting down, under the clock and beside the score:
                a manager deciding whether to hurry sees how much is left of the decision
                being his. */}
            {windowWaiting && (
              <div className="clock-window" role="status">
                <b>{windowWaiting.label}</b>
                <span className="clock-window__count">{windowWaiting.seconds}s</span>
                <span className="clock-window__hint">{windowWaiting.hint}</span>
              </div>
            )}
          </div>
          <div className={`team-name away ${userTeamIdx === 1 ? 'user-team team-colored' : 'team-colored'}`}>
            <ClubCrest
              crest={awayTeam.crest}
              primary={awayTeam.primaryColor}
              secondary={awayTeam.secondaryColor}
              name={awayTeam.name}
              className="match-crest"
            />
            <ClubName teamId={awayTeam.id}>{awayTeam.name}</ClubName>
            <span className="team-stars" style={{ color: 'var(--accent)', marginLeft: '8px' }}>{starsToString(awayTeam.stars)}</span>
            <SponsorMark sponsor={lineup!.awaySponsor} className="team-sponsor" />
            <GoalHistory feed={feed} teamId={awayTeam.id} lineup={lineup!} team={awayTeam} side={lineup!.awayKitSide} />
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
                '--possession-home': `${possessionHome ?? 50}%`,
                '--home-team': homeTeam.primaryColor || '#2f6f4f',
                '--away-team': awayTeam.primaryColor || '#2a4a63',
              } as React.CSSProperties
            }
            title={
              possessionHome == null
                ? 'Posse de bola: o jogo ainda não começou'
                : `Posse de bola: ${homeTeam.shortName ?? homeTeam.name} ${possessionHome}% — ${
                    awayTeam.shortName ?? awayTeam.name
                  } ${possessionAway}%`
            }
          >
            <span className="possession-bar__share" aria-hidden="true" />
            <span className="possession-bar__labels">
              <span>{possessionHome == null ? '—' : `${possessionHome}%`}</span>
              <span>{possessionAway == null ? '—' : `${possessionAway}%`}</span>
            </span>
          </div>
        </div>

        {/* The leg before this one, under the score: a return leg is a different match from
            a first one, and the aggregate is the reason. */}
        <FirstLegStrip context={context} />

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
            {/* The eleven under the scoreboard is the one the manager is looking for. In a
                match of two other clubs there is no such eleven — he is watching, not
                managing — and the list shown is the home side's, named as what it is. */}
            <b>{isInThisMatch
              ? userTeamIdx === 0 ? homeTeam.name : awayTeam.name
              : homeTeam.name}</b>
            <span>
              {isInThisMatch
                ? `${ballIsOurs ? 'com a bola' : 'em campo'} • clique num jogador para substituir`
                : 'em campo • você não está neste jogo'}
            </span>
          </div>
          <OnPitchList
            players={isInThisMatch ? userLineup : lineup.homeLineup}
            team={isInThisMatch ? (userTeamIdx === 0 ? homeTeam : awayTeam) : homeTeam}
            side={
              isInThisMatch
                ? userTeamIdx === 0 ? lineup.homeKitSide : lineup.awayKitSide
                : lineup.homeKitSide
            }
            onSelect={canSubstitute ? openSubstitutionFor : undefined}
            ballCarrierId={isInThisMatch && ballIsOurs ? state?.possession?.playerId : null}
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
                {/* The engine's own words, or nothing. A sentence written here would be the
                    only narration in the game that no match ever said, above the feed that is
                    telling the story of the same evening. */}
                <div className="event-desc" id="heroDesc">
                  {latest?.description}
                </div>
              </div>
            );
          })()}

          {/* A shootout is not a tab either: the match is standing at the spot with the
              clock held, and the panel is where the kicks are read and the order is named. */}
          {state?.shootout && lineup && (
            <ShootoutPanel
              shootout={state.shootout}
              userTeamId={userTeamId ?? null}
              homeName={homeTeam.shortName || homeTeam.name}
              awayName={awayTeam.shortName || awayTeam.name}
              homePlayers={lineup.homeLineup}
              awayPlayers={lineup.awayLineup}
              busy={namingShootout}
              error={shootoutError}
              onConfirmOrder={async takerIds => {
                if (!userTeamId) return;

                setShootoutError(null);
                setNamingShootout(true);

                try {
                  // The order goes out over the hub for the same reason the taker of a
                  // penalty does: a shootout is everybody's to watch, and the order the
                  // manager named is part of what they are watching.
                  const result = await MatchHubClient.nameShootoutOrder(
                    matchId,
                    userTeamId,
                    takerIds
                  );

                  if (result && result.accepted === false) {
                    setShootoutError(result.errorMessage || 'A ordem não pode ser enviada agora.');
                  }
                } catch (error) {
                  console.error('Failed to name the order of takers:', error);
                  setShootoutError('Não foi possível enviar a ordem dos cobradores.');
                } finally {
                  setNamingShootout(false);
                }
              }}
            />
          )}

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
              lineup={lineup!}
              userTeamIndex={isInThisMatch ? userTeamIdx : null}
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
        kitSide={userTeamIdx === 0 ? lineup.homeKitSide : lineup.awayKitSide}
        busy={substituting}
        onSubstitute={substitute}
        onContinue={continueSecondHalf}
        onClose={() => setShowHalfTimeModal(false)}
        endsAt={state?.halfTimeEndsAt}
      />
      {/* A man who cannot carry on opens a window that does not stop the match: the dialog
          follows the state the server publishes rather than a local flag, because an injury
          that happened while the screen was closed still has to reach the manager — and it
          can be put aside, because the engine names somebody from the bench when nobody does
          and a question the match is not waiting on is not one that has to be answered now. */}
      <SubstitutionModal
        show={
          isInThisMatch &&
          !dismissedInjuryFor &&
          (showSubstitutionModal || !!state?.injury?.awaitingSubstitution)
        }
        team={userTeamIdx === 0 ? homeTeam : awayTeam}
        kitSide={userTeamIdx === 0 ? lineup.homeKitSide : lineup.awayKitSide}
        lineup={userLineup}
        bench={userBench}
        substitutionsUsed={userSubstitutionsUsed}
        preselectOut={state?.injury?.awaitingSubstitution ? state.injury.playerId : substitutionFor}
        forcedFor={state?.injury?.awaitingSubstitution ? state.injury.playerName : null}
        busy={substituting}
        onSubstitute={substitute}
        onClose={() => {
          // Putting the window aside remembers *which* man it was about, so a second injury
          // in the same match asks again instead of staying dismissed for ever.
          if (state?.injury?.awaitingSubstitution) {
            setDismissedInjuryFor(state.injury.playerId ?? null);
          }

          setShowSubstitutionModal(false);
          setSubstitutionFor(null);
          setSubstitutionError(null);
        }}
      />
      {/* A penalty of the manager's own club stops the clock until he names the taker, so
          the dialog follows the state the server publishes rather than a local flag. */}
      <PenaltyModal
        show={!!state?.penalty?.awaitingSelection && showPenaltyModal}
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
        endsAt={state?.penaltyEndsAt}
        onClose={() => {
          // Putting the dialog aside does not answer the question: the window stays open and
          // the countdown above it keeps going down, because the decision is still the
          // manager's until the backend takes it.
          setShowPenaltyModal(false);
          setPenaltyError(null);
        }}
      />
    </div>
  );
};

export default MatchScreen;
