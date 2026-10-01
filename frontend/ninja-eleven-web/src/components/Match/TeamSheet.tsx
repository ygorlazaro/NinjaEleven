import React, { useEffect, useState } from 'react';
import type { MatchLineupDto, MatchPlayerDto, TeamDto } from '@/types';
import { positionLabel, sortByPosition, starsToString } from '@/services/formatters';
import EnergyBar from '@/components/Match/EnergyBar';
import SubstitutionPanel from '@/components/Match/SubstitutionPanel';
import { ClubName, PlayerName } from '@/components/Common/Names';
import ClubCrest from '@/components/Club/ClubCrest';
import HurtBadge from '@/components/Match/HurtBadge';
import KitChip from '@/components/Club/KitChip';

interface TeamSheetProps {
  lineup: MatchLineupDto;
  /**
   * Which side of the team sheet belongs to the manager: 0 is home, 1 is away — and null
   * when his club is in neither, which is a match he is watching rather than playing. Both
   * sheets are then documents: neither carries a change panel, because a manager has no
   * eleven in a game of two other clubs and the backend refuses a change for either.
   */
  userTeamIndex: number | null;
  /** False once the match is over, so a finished match is a document and not a decision. */
  canSubstitute: boolean;
  substitutionsUsed: number;
  busy?: boolean;
  onSubstitute: (playerOutId: string, playerInId: string) => void;
}

/** One side of the fixture: which club it is and the eleven and bench of that club. */
type Side = {
  team: TeamDto;
  onPitch: MatchPlayerDto[];
  bench: MatchPlayerDto[];
  /** True for the club the manager commands, and the only side a change can be made on. */
  isHis: boolean;
  substitutionsUsed: number;
  /**
   * Which of this club's two shirts the match was played in. It is on the fixture and not on
   * the club, so a sheet that showed the club's first shirt beside a visitor's second would be
   * showing him the wrong men.
   */
  kitSide: 'Home' | 'Away';
};

/**
 * The Escalação tab: the two team sheets, one at a time.
 *
 * **Casa and Fora, because a manager reads a match as two clubs and not as one sheet with
 * the other club's men at the bottom of it.** The two elevens are on the pitch at the same
 * time and they are not the same list, so they get a tab each and a manager looking at the
 * man in front of his striker does not have to read past a goalkeeper he does not manage to
 * reach him. It is the same screen the interval offers, so a manager who learns one does
 * not have to learn two.
 *
 * **The tab opens on the manager's own club.** A screen that opened on the opposition would
 * be answering a question nobody asked, and the tab the manager came back to is the one he
 * is making decisions in.
 *
 * **The other club's sheet is a document, not a decision.** The eleven and the bench are
 * there to be read — the shape of the man about to come against him is a fact about the
 * match — but the change panel is only on his own side. The backend refuses a substitution
 * for a club the manager does not command, so a panel there would offer a decision the
 * server has already refused; the honest thing is not to draw one.
 */
const TeamSheet: React.FC<TeamSheetProps> = ({
  lineup,
  userTeamIndex,
  canSubstitute,
  substitutionsUsed,
  busy = false,
  onSubstitute,
}) => {
  const [side, setSide] = useState<'home' | 'away'>(
    userTeamIndex === null ? 'home' : userTeamIndex === 0 ? 'home' : 'away'
  );

  // The club he commands is the club the tab opens on. A match of two other clubs has no
  // side of his to default to, and the home side is the one a manager watching a game he is
  // not in looks at first.
  useEffect(() => {
    setSide(userTeamIndex === null ? 'home' : userTeamIndex === 0 ? 'home' : 'away');
  }, [userTeamIndex]);

  const sides: Record<'home' | 'away', Side> = {
    home: {
      team: lineup.homeTeam,
      onPitch: lineup.homeLineup,
      bench: lineup.homeBench,
      isHis: userTeamIndex === 0,
      substitutionsUsed,
      kitSide: lineup.homeKitSide,
    },
    away: {
      team: lineup.awayTeam,
      onPitch: lineup.awayLineup,
      bench: lineup.awayBench,
      isHis: userTeamIndex === 1,
      substitutionsUsed,
      kitSide: lineup.awayKitSide,
    },
  };

  const current = sides[side];

  return (
    <div className="lineup">
      <div className="side-tabs" role="tablist" aria-label="Escalação de cada time">
        {(['home', 'away'] as const).map(which => {
          const tab = sides[which];

          return (
            <button
              key={which}
              type="button"
              role="tab"
              aria-selected={side === which}
              className={`side-tab${side === which ? ' active' : ''}${tab.isHis ? ' his-team' : ''}`}
              onClick={() => setSide(which)}
              title={tab.isHis ? 'Seu time' : 'Adversário'}
            >
              <ClubCrest
                crest={tab.team.crest}
                primary={tab.team.primaryColor}
                secondary={tab.team.secondaryColor}
                name={tab.team.name}
                className="mini-crest"
              />
              {which === 'home' ? 'Casa' : 'Fora'}
            </button>
          );
        })}
      </div>

      {current.isHis && canSubstitute && (
        <SubstitutionPanel
          team={current.team}
          kitSide={current.kitSide}
          lineup={current.onPitch}
          bench={current.bench}
          used={current.substitutionsUsed}
          busy={busy}
          onSubstitute={onSubstitute}
        />
      )}

      <div className="half-sub-area" style={{ marginTop: '14px' }}>
        <div className="half-sub-help">
          Em campo —{' '}
          <ClubName teamId={current.team.id}>{current.team.name}</ClubName>
          <span className="team-stars" style={{ color: 'var(--accent)', marginLeft: '8px' }}>
            {starsToString(current.team.stars)}
          </span>
        </div>
        <div className="player-grid">
          {sortByPosition(current.onPitch).map(p => (
            <SheetCard
              key={p.playerId}
              player={p}
              team={current.team}
              kitSide={current.kitSide}
              opponent={!current.isHis}
            />
          ))}
        </div>
      </div>

      <div className="half-sub-area" style={{ marginTop: '14px' }}>
        <div className="half-sub-help">
          Banco —{' '}
          <ClubName teamId={current.team.id}>{current.team.name}</ClubName>
          <span className="team-stars" style={{ color: 'var(--accent)', marginLeft: '8px' }}>
            {starsToString(current.team.stars)}
          </span>
        </div>
        <div className="bench-grid">
          {sortByPosition(current.bench).map(p => (
            <SheetCard
              key={p.playerId}
              player={p}
              team={current.team}
              kitSide={current.kitSide}
              isBench
              opponent={!current.isHis}
            />
          ))}
        </div>
      </div>
    </div>
  );
};

interface SheetCardProps {
  player: MatchPlayerDto;
  /** The club he belongs to, so the shirt sits beside the name. */
  team?: TeamDto;
  kitSide?: 'Home' | 'Away';
  isBench?: boolean;
  opponent?: boolean;
}

/**
 * One name on a team sheet, with the energy bar and whatever already happened to him. The
 * same information for both clubs, because a manager decides with the whole picture.
 */
const SheetCard: React.FC<SheetCardProps> = ({
  player,
  team,
  kitSide,
  isBench = false,
  opponent = false,
}) => (
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
        <KitChip team={team} side={kitSide} />
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
