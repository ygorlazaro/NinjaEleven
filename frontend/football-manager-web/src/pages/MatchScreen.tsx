import React, { useEffect, useState, useCallback } from 'react';
import { useGameState } from '@/state';
import { MatchApi } from '@/api';
import MatchHubClient from '@/signalr/MatchHubClient';
import type { MatchEngineEventDto, MatchStateDto, MatchLineupDto, MatchResult } from '@/types';
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

const MatchScreen: React.FC<{ matchId?: string }> = ({ matchId: propMatchId }) => {
  const urlMatchId = propMatchId || window.location.pathname.split('/match/')[1];
  const currentMatch = useGameState((s) => s.currentMatch);
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
  const [showPenaltyModal, setShowPenaltyModal] = useState(false);
  const [penaltyCandidates, setPenaltyCandidates] = useState<any[]>([]);
  const [matchResult, setMatchResult] = useState<MatchResult | null>(null);
  const [selectedStarters, setSelectedStarters] = useState<Set<string>>(new Set());
  const [substitution, setSubstitution] = useState<{ out: string; in: string } | null>(null);

  const matchId = urlMatchId || currentMatch?.matchId || '';

  const loadLineup = useCallback(async () => {
    if (!matchId) return;
    const lineupData = await MatchApi.getLineup(matchId);
    setLineup(lineupData);
  }, [matchId]);

  const loadState = useCallback(async () => {
    if (!matchId) return;
    const stateData = await MatchApi.getState(matchId);
    setState(stateData);
    setIsPaused(stateData.isPaused);
    if (stateData.isFinished) setShowEndModal(true);
  }, [matchId]);

  useEffect(() => {
    if (!matchId) return;

    loadLineup();
    loadState();

    // The match is driven by the server: we subscribe and render whatever it pushes.
    // No client side clock and no polling loop.
    MatchHubClient.connect(matchId).catch(error => {
      console.error('Failed to connect to the match hub:', error);
    });

    MatchHubClient.onState(next => {
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

    MatchHubClient.onEvent(events => {
      const feedEvents = events.map(convertToFeedEvent);
      const currentFeed = useGameState.getState().feed;
      setFeed([...currentFeed, ...feedEvents]);
    });

    MatchHubClient.onResult(result => {
      setMatchResult(result);
      setShowEndModal(true);
    });

    return () => {
      MatchHubClient.disconnect();
    };
  }, [matchId]);

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

  if (!lineup) {
    return (
      <div className="card match-header">
        <p className="competition">Carregando partida...</p>
      </div>
    );
  }

  const homeTeam = lineup.homeTeam;
  const awayTeam = lineup.awayTeam;
  const homeColor = homeTeam.primaryColor || '#f2d34f';
  const awayColor = awayTeam.primaryColor || '#57a6ff';
  const userTeamIdx = lineup.userTeamIndex;
  const score = `${state?.homeScore ?? 0} × ${state?.awayScore ?? 0}`;
  const minute = state ? `${state.minute.toString().padStart(2, '0')}:${state.second.toString().padStart(2, '0')}` : '00:00';
  const halfLabel = state?.currentHalf === 'Second' ? ' (2º)' : '';

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

          <MatchControls
            isPaused={isPaused}
            matchId={matchId}
            onPause={pause}
            onResume={resume}
            onSpeed={changeSpeed}
            speed={state?.speed ?? 1}
          />
        </section>

        <aside className="card side">
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

      <EndModal show={showEndModal} result={matchResult} onClose={() => setShowEndModal(false)} />
      <HalfTimeModal show={showHalfTimeModal} homeTeam={homeTeam} awayTeam={awayTeam} score={score} onContinue={continueSecondHalf} />
      <PenaltyModal show={showPenaltyModal} candidates={penaltyCandidates} onSelected={(playerId) => MatchHubClient.selectPenaltyTaker(matchId, userTeamIdx === 0 ? homeTeam.id : awayTeam.id, playerId)} onClose={() => setShowPenaltyModal(false)} />
    </div>
  );
};

export default MatchScreen;
