import type { KitDto, KitSide, TeamDto } from '@/types';

/**
 * The shirt a club is playing in, or the one it would be playing in.
 *
 * <para>
 * A club that has never been drawn a shirt still has one: its own two colours, solid. That is
 * what it wore before kits existed, and a screen that drew nothing at all for such a club would
 * be drawing an absence where there is a club.
 * </para>
 *
 * <para>
 * The two fallbacks differ, which is the whole of the fallback: a club with no shirt of its own
 * plays at home in one colour and away in the other, so the two shirts are never the same. It
 * is what the game did for every club until a manager drew one, and it is kept because a club
 * that has cleared its badge is a club in that state rather than a club with no identity.
 * </para>
 *
 * The side is asked of the fixture and not of the club: the same club plays its first shirt at
 * home and its second when the two colours on the pitch would be impossible to tell apart, and
 * a function that answered with a club's shirt without being told the side would answer half
 * the matches wrongly.
 */
export const kitOf = (team: TeamDto | null | undefined, side: KitSide = 'Home'): KitDto | null => {
  if (!team) return null;

  if (side === 'Away') {
    return (
      team.awayKit ?? {
        primaryColor: team.secondaryColor,
        secondaryColor: team.primaryColor,
        pattern: 'Solid',
        trimColor: null,
      }
    );
  }

  return (
    team.homeKit ?? {
      primaryColor: team.primaryColor,
      secondaryColor: team.secondaryColor,
      pattern: 'Solid',
      trimColor: null,
    }
  );
};

/** Which of the two shirts a team wore in a fixture, as the backend drew it. */
export const kitSideOf = (
  lineup: { homeKitSide?: KitSide; awayKitSide?: KitSide } | null | undefined,
  isHome: boolean,
): KitSide => {
  const side = isHome ? lineup?.homeKitSide : lineup?.awayKitSide;

  return side === 'Away' ? 'Away' : 'Home';
};
