import React from 'react';

/**
 * Who runs this club, said in one mark.
 *
 * A manager reading a club page is asking one of three questions and the page has to answer
 * it before anything else on the screen: is this mine, is it another person's, or is it a
 * club the world runs by itself. The three are not decoration — a rename button on somebody
 * else's club and a transfer offer for a club the world runs are both mistakes this mark
 * exists to prevent, and both are invisible without it.
 *
 * <para>
 * Whether a club has a person behind it is the backend's answer, not this screen's: it reads
 * <c>controlledBy</c>, which comes from the club's own manager row. A club with no manager
 * row at all reads as NPC, which is what it is — nobody is running it.
 * </para>
 *
 * <para>
 * Whether it is <em>yours</em> is the one question the backend cannot answer, because it is
 * not about the club. It is about who is looking at it, and the account is what says so.
 * </para>
 */
export type ClubOwnership = 'mine' | 'human' | 'npc';

const MARKS: Record<ClubOwnership, { icon: string; label: string; hint: string }> = {
  mine: {
    icon: '⭐',
    label: 'Seu clube',
    hint: 'Este é o seu clube: você responde por ele.',
  },
  human: {
    icon: '🧑‍💼',
    label: 'Gerido por humano',
    hint: 'Outro gerente está no comando deste clube.',
  },
  npc: {
    icon: '🤖',
    label: 'Gerido pelo NPCs',
    hint: 'Nenhuma pessoa comanda este clube — o mundo joga por ele.',
  },
};

/**
 * Decides which of the three marks a club wears.
 *
 * The backend's answer comes first and the account's only upgrades it: a club the world says
 * has nobody behind it reads as NPC even when it is the club the browser still remembers as
 * yours, because that combination is exactly what a dismissed manager is looking at — the
 * account has not been told yet, and a mark that said "your club" would be a lie the screen
 * tells with a straight face.
 */
export function clubOwnership(
  clubId: string,
  controlledBy: boolean | null | undefined,
  yourClubId: string | null | undefined,
): ClubOwnership {
  if (controlledBy !== true) return 'npc';
  return yourClubId && clubId === yourClubId ? 'mine' : 'human';
}

const ClubOwnershipIcon: React.FC<{
  clubId: string;
  controlledBy?: boolean | null;
  yourClubId?: string | null;
}> = ({ clubId, controlledBy, yourClubId }) => {
  const ownership = clubOwnership(clubId, controlledBy, yourClubId);
  const mark = MARKS[ownership];

  return (
    <span
      className={`club-owner club-owner--${ownership}`}
      title={mark.hint}
      aria-label={mark.label}
    >
      <span className="club-owner__icon" aria-hidden="true">{mark.icon}</span>
      <span className="club-owner__label">{mark.label}</span>
    </span>
  );
};

export default ClubOwnershipIcon;
