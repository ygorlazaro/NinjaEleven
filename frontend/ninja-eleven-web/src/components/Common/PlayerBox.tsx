import React from 'react';
import { PlayerName } from '@/components/Common/Names';
import { PlayerFace } from '@/components/Common/PlayerFace';
import PlayerStatusMarks from '@/components/Common/PlayerStatusMarks';
import ClubCrest from '@/components/Club/ClubCrest';
import EnergyBar from '@/components/Match/EnergyBar';
import {
  positionLabel,
  starsToString,
  attributeToneClass,
  energyTextClass,
  initialsOf
} from '@/services/formatters';
import { formatLimo } from '@/services/limo';

/**
 * The seven attributes, on the 1..100 scale, in the order a manager reads them.
 *
 * They are one list rather than seven props because they are one fact read seven ways, and a
 * box that took them one at a time would be a box that could be drawn with a goalkeeper's
 * reflexes missing. The order is the same on every box in the game.
 */
export interface PlayerBoxAttributes {
  speed: number;
  accuracy: number;
  dribbling: number;
  heading: number;
  strength: number;
  goalkeeperPower: number;
  reflexes: number;
}

/** The club a player is on, by the two colours and the name — a crest is a club's own. */
export interface PlayerBoxTeam {
  teamId?: string | null;
  name?: string | null;
  primaryColor?: string | null;
  secondaryColor?: string | null;
}

/**
 * The three money numbers, each one nullable on purpose.
 *
 * A free agent has a value and a wage and nobody to buy him from; a man in his last season of
 * contract is worth what he is worth and costs exactly that, and the box says so rather than
 * showing a price the market has not quoted. A null is printed as a dash, never as zero —
 * see "An Absence Is Not a Zero".
 */
export interface PlayerBoxMoney {
  value?: number | null;
  price?: number | null;
  salary?: number | null;
}

export interface PlayerBoxProps {
  playerId: string;
  name: string;
  /** The face as the raw faces.js JSON. Absent means he is faceless, which is a real state. */
  face?: string | null;
  age: number;
  /** `GK`, `DEF`, `MID` or `ATT`. The label is the world's, from `positionLabel`. */
  position: string;
  stars?: number | null;
  attributes: PlayerBoxAttributes;
  energy: number;

  /** The club he plays for, when the screen knows it. Absent leaves the line off. */
  team?: PlayerBoxTeam | null;

  /**
   * The three money numbers, when the screen has them.
   *
   * This is optional rather than three nullable props because a screen that has none of them
   * should not draw a row of dashes: the base has no contract to quote, and a box of em dashes
   * is a box saying the answer is zero.
   */
  money?: PlayerBoxMoney | null;

  /** The three marks that keep a man off the pitch, drawn beside his name. */
  injury?: string | null;
  injuryMatchesRemaining?: number;
  suspensionMatches?: number;
  retiring?: boolean;

  /**
   * Whether the keeper's two numbers are this man's to read.
   *
   * They are drawn as dashes for an outfielder rather than hidden, so twenty-three boxes hold
   * the same shape and a manager comparing two men is comparing two boxes of the same width.
   */
  keeper?: boolean;

  className?: string;

  /**
   * Said on the whole box when the pointer rests on it — the squad screen puts the words for
   * an absence there, because "injured" and "suspended" look the same dimmed and a manager
   * cannot tell which of the two he is looking at.
   */
  title?: string;

  /**
   * What the box carries that is not a player fact: the season's tallies, the contract, the
   * decisions a manager takes on this man.
   *
   * It is a slot rather than a set of props because those are the screen's own, and a box that
   * grew a prop for the squad's release and another for the market's offer would be a box
   * that grew one prop per screen.
   */
  children?: React.ReactNode;
}

/**
 * A player as a box: the face on the left, and to its right who he is, what he can do, what
 * he costs and how much football he has left.
 *
 * <para>
 * A table is the right shape for a fixed set of numbers compared down a column, and the wrong
 * shape for a man. Twenty-three men in a table of twenty columns is a spreadsheet with a
 * football on it, and the two things a manager actually reads first — who the man is and what
 * he looks like — are the two things a table is worst at, because a table has nowhere to put a
 * face and prints the name in a cell like everything else. The box puts the face back where it
 * was always meant to be, at the front of the thing that is about a person.
 * </para>
 *
 * <para>
 * It is one component and not three, because the squad, the base and the market are three
 * readings of the same man and a squad that draws him one way is a squad the market draws
 * another. The parts that differ between the three are the tallies, the contract and the
 * decisions, and those are the screen's own: they arrive through the slot at the bottom.
 * </para>
 *
 * <para>
 * The face is the same string on all three, because a face belongs to the man rather than to
 * the screen. A man with no face is a real state and draws his initials, which is what a
 * squad looked like before faces existed and is not a broken layout.
 * </para>
 */
const PlayerBox: React.FC<PlayerBoxProps> = ({
  playerId,
  name,
  face,
  age,
  position,
  stars,
  attributes,
  energy,
  team,
  money,
  injury,
  injuryMatchesRemaining,
  suspensionMatches,
  retiring,
  keeper = false,
  className,
  title,
  children
}) => {
  const cells: { label: string; value: number; keeperOnly: boolean }[] = [
    { label: 'Vel', value: attributes.speed, keeperOnly: false },
    { label: 'Fin', value: attributes.accuracy, keeperOnly: false },
    { label: 'Dri', value: attributes.dribbling, keeperOnly: false },
    { label: 'Cab', value: attributes.heading, keeperOnly: false },
    { label: 'For', value: attributes.strength, keeperOnly: false },
    { label: 'Gol', value: attributes.goalkeeperPower, keeperOnly: true },
    { label: 'Ref', value: attributes.reflexes, keeperOnly: true }
  ];

  return (
    <article className={`player-box ${className ?? ''}`} title={title}>
      <div className="player-box__portrait">
        {/*
          The face, and the initials where there is none. Both occupy the same box, so a squad
          of men with faces and a squad without them are the same grid and one does not have to
          reflow when a face arrives.
        */}
        {face ? (
          <PlayerFace face={face} size={62} className="player-box__face" />
        ) : (
          <span className="player-box__initials" aria-hidden="true">
            {initialsOf(name)}
          </span>
        )}
      </div>

      <div className="player-box__body">
        {/* Who he is. The name is the door to his profile, and it is the only text on the
            box that is a door — everything else on the box is a fact to be read. */}
        <div className="player-box__name">
          <PlayerName playerId={playerId}>{name}</PlayerName>
          <PlayerStatusMarks
            retiring={retiring}
            injury={injury}
            injuryMatchesRemaining={injuryMatchesRemaining}
            suspensionMatches={suspensionMatches}
          />
        </div>

        <div className="player-box__line player-box__line--meta">
          <span className="player-box__tag">{positionLabel(position)}</span>
          <span>{age} anos</span>
          {stars != null && <span className="stars-col">{starsToString(stars)}</span>}
        </div>

        {/* What he can do, on the scale the engine decided, in the bands `attributeToneClass`
            owns. A keeper's two numbers are dashes for anybody else rather than zeros, which
            would be twenty-two men pretending to be bad at a job they do not have. */}
        <div className="player-box__line player-box__line--attributes">
          {cells.map(cell => {
            const read = !cell.keeperOnly || keeper;
            return (
              <span key={cell.label} className="player-box__attr">
                <span className="player-box__attr-label">{cell.label}</span>
                <span className={read ? attributeToneClass(cell.value) : 'attr-absent'}>
                  {read ? cell.value : '—'}
                </span>
              </span>
            );
          })}
        </div>

        {/* Who he belongs to and what he costs. The crest is beside the club's name because a
            name identifies a club to somebody reading and a shield identifies it to somebody
            glancing down a column of twenty-three boxes. */}
        {team?.name && (
          <div className="player-box__line player-box__line--club">
            <ClubCrest
              primary={team.primaryColor ?? '#1b2733'}
              secondary={team.secondaryColor ?? '#8ea3b8'}
              name={team.name}
              className="player-box__crest"
            />
            <span className="player-box__club-name">{team.name}</span>
          </div>
        )}

        {money && (
          <div className="player-box__line player-box__line--money">
            <span>
              <span className="player-box__tag">Valor</span> {money.value == null ? '—' : formatLimo(money.value)}
            </span>
            <span>
              <span className="player-box__tag">Preço</span> {money.price == null ? '—' : formatLimo(money.price)}
            </span>
            <span>
              <span className="player-box__tag">Salário</span> {money.salary == null ? '—' : formatLimo(money.salary)}
            </span>
          </div>
        )}

        {/* How much football he has left. The number is the backend's and the class is the
            one `energyTextClass` owns, so the band is decided in one place. */}
        <div className="player-box__line player-box__line--energy">
          <span className="player-box__tag">Energia</span>
          <span className={energyTextClass(energy)}>{energy}</span>
          <EnergyBar value={energy} compact />
        </div>

        {children && <div className="player-box__extra">{children}</div>}
      </div>
    </article>
  );
};

export default PlayerBox;
