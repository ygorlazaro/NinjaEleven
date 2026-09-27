import React from 'react';

/**
 * A club's shield, drawn as a placeholder in the club's own colours.
 *
 * The shape and the two colours are all there is: the real badge is a piece of artwork the
 * game does not have yet, and a blank space would leave the screen looking broken rather than
 * unfinished. So the shield is a shield, wearing the colours the club wears, and when the
 * artwork arrives it replaces this and nothing else on the screen moves.
 *
 * It lives in its own file because two places draw a club now — the club's page and the block
 * in the column that carries the club's name — and a shield copied into a second place is a
 * shield the two places will stop agreeing about.
 */
const ClubCrest: React.FC<{ primary: string; secondary: string; name: string }> = ({
  primary,
  secondary,
  name
}) => (
  <span
    className="club-crest"
    style={{ background: primary, borderColor: secondary, color: secondary }}
    role="img"
    aria-label={`Escudo do ${name}`}
    title="Escudo ainda não existe: um placeholder nas cores do clube"
  >
    {name
      .split(' ')
      .filter(word => word.length > 2)
      .slice(0, 2)
      .map(word => word[0])
      .join('')}
  </span>
);

export default ClubCrest;
