import React from 'react';
import type { MatchLineupDto, MatchPlayerDto } from '@/types';
import { positionLabel, sortByPosition, starsToString } from '@/services/formatters';
import EnergyBar from '@/components/Match/EnergyBar';
import SubstitutionPanel from '@/components/Match/SubstitutionPanel';
import { ClubName, PlayerName } from '@/components/Common/Names';
import HurtBadge from '@/components/Match/HurtBadge';

interface TeamSheetProps {
  lineup: MatchLineupDto;
  /** Which side of the team sheet belongs to the manager: 0 is home, 1 is away. */
  userTeamIndex: number;
  /** False once the match is over, so a finished match is a document and not a decision. */
  canSubstitute: boolean;
  substitutionsUsed: number;
  busy?: boolean;
  onSubstitute: (playerOutId: string, playerInId: string) => void;
}

/**
 * The Escalação tab: both team sheets, and the manager's own bench with the substitutions
 * on top of it. It is the same screen the interval offers, so a manager who learns one does
 * not have to learn two.
 */
const TeamSheet: React.FC<TeamSheetProps> = ({
  lineup,
  userTeamIndex,
  canSubstitute,
  substitutionsUsed,
  busy = false,
  onSubstitute,
}) => {
  const isUserHome = userTeamIndex === 0;

  const onPitch = isUserHome ? lineup.homeLineup : lineup.awayLineup;
  const bench = isUserHome ? lineup.homeBench : lineup.awayBench;
  const opponentOnPitch = isUserHome ? lineup.awayLineup : lineup.homeLineup;

  return (
    <div className="lineup">
      {canSubstitute && (
        <SubstitutionPanel
          lineup={onPitch}
          bench={bench}
          used={substitutionsUsed}
          busy={busy}
          onSubstitute={onSubstitute}
        />
      )}

      <div className="half-sub-area" style={{ marginTop: '14px' }}>
        <div className="half-sub-help">Em campo — {isUserHome ? (
          <>
            <ClubName teamId={lineup.homeTeam.id}>{lineup.homeTeam.name}</ClubName>
            <span className="team-stars" style={{ color: 'var(--accent)', marginLeft: '8px' }}>{starsToString(lineup.homeTeam.stars)}</span>
          </>
        ) : (
          <>
            <ClubName teamId={lineup.awayTeam.id}>{lineup.awayTeam.name}</ClubName>
            <span className="team-stars" style={{ color: 'var(--accent)', marginLeft: '8px' }}>{starsToString(lineup.awayTeam.stars)}</span>
          </>
        )}</div>
        <div className="player-grid">
          {sortByPosition(onPitch).map(p => <SheetCard key={p.playerId} player={p} />)}
        </div>
      </div>

      <div className="half-sub-area" style={{ marginTop: '14px' }}>
        <div className="half-sub-help">Banco — {isUserHome ? (
          <>
            <ClubName teamId={lineup.homeTeam.id}>{lineup.homeTeam.name}</ClubName>
            <span className="team-stars" style={{ color: 'var(--accent)', marginLeft: '8px' }}>{starsToString(lineup.homeTeam.stars)}</span>
          </>
        ) : (
          <>
            <ClubName teamId={lineup.awayTeam.id}>{lineup.awayTeam.name}</ClubName>
            <span className="team-stars" style={{ color: 'var(--accent)', marginLeft: '8px' }}>{starsToString(lineup.awayTeam.stars)}</span>
          </>
        )}</div>
        <div className="bench-grid">
          {sortByPosition(bench).map(p => <SheetCard key={p.playerId} player={p} isBench />)}
        </div>
      </div>

      <div className="half-sub-area" style={{ marginTop: '14px' }}>
        <div className="half-sub-help">Em campo — {isUserHome ? (
          <>
            <ClubName teamId={lineup.awayTeam.id}>{lineup.awayTeam.name}</ClubName>
            <span className="team-stars" style={{ color: 'var(--accent)', marginLeft: '8px' }}>{starsToString(lineup.awayTeam.stars)}</span>
          </>
        ) : (
          <>
            <ClubName teamId={lineup.homeTeam.id}>{lineup.homeTeam.name}</ClubName>
            <span className="team-stars" style={{ color: 'var(--accent)', marginLeft: '8px' }}>{starsToString(lineup.homeTeam.stars)}</span>
          </>
        )}</div>
        <div className="player-grid">
          {sortByPosition(opponentOnPitch).map(p => <SheetCard key={p.playerId} player={p} opponent />)}
        </div>
      </div>
    </div>
  );
};

interface SheetCardProps {
  player: MatchPlayerDto;
  isBench?: boolean;
  opponent?: boolean;
}

/**
 * One name on a team sheet, with the energy bar and whatever already happened to him. The
 * same information for both clubs, because a manager decides with the whole picture.
 */
const SheetCard: React.FC<SheetCardProps> = ({ player, isBench = false, opponent = false }) => (
  <div
    className={`player-card ${isBench ? 'bench-card' : ''} ${player.redCard ? 'sent-off' : ''} ${
      player.injuredOff ? 'injured' : ''
    } ${opponent ? 'opponent' : ''}`}
  >
    <div className="player-top">
      <span className="player-pos">
        {player.emergencyGK ? 'GOL*' : positionLabel(player.position)}
      </span>
      <span className="player-card__name">
        <PlayerName playerId={player.playerId}>{player.name}</PlayerName>
        <HurtBadge player={player} />
        <span className="player-stars" style={{ color: 'var(--accent)', marginLeft: '6px' }}>{starsToString(player.stars)}</span>
      </span>
      <span className="player-energy">{Math.round(player.energy)}%</span>
    </div>

    <EnergyBar value={player.energy} />

    <div className="player-stats">
      {/* This is a match screen, so these are the numbers of this match. The season's
          goals belong on a profile, not beside a man who has not scored today. */}
      {player.matchGoals > 0 && <span title={`${player.matchGoals} gol(s) na partida`}>⚽ {player.matchGoals}</span>}
      {player.matchSaves > 0 && <span title={`${player.matchSaves} defesa(s) na partida`}>🧤 {player.matchSaves}</span>}
      {player.matchOwnGoals > 0 && <span title={`${player.matchOwnGoals} gol(s) contra na partida`}>🔴 {player.matchOwnGoals}</span>}
      {player.matchYellowCards > 0 && <span title="Cartão amarelo">🟨 {player.matchYellowCards}</span>}
      {player.redCard && <span title="Expulso">🟥</span>}
      {player.injuredOff && <span title="Saiu lesionado">🚑</span>}
      {player.subbedIn && <span title="Entrou em campo">↩</span>}
      {player.subbedOff && <span title="Já saiu de campo">⇤</span>}
      {player.emergencyGK && <span title="Assumiu a meta sem goleiro">🧤</span>}
    </div>
  </div>
);

export default TeamSheet;
