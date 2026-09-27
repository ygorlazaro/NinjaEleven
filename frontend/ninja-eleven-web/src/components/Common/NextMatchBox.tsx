import React from 'react';
import { NavLink } from 'react-router-dom';
import type { TeamDto } from '@/types';
import { ClubName } from '@/components/Common/Names';
import type { NextFixture } from '@/hooks/useNextFixture';

/**
 * The match the manager is about to play, in the foot of the column.
 *
 * Four facts and no more: who it is against, where it is played, which side of the door the
 * club is on, and what the game is. That is what a manager needs before choosing eleven — and
 * the stadium is in the list because "onde" decides a lineup as much as "contra quem" does: a
 * club away at a ground with a short pitch is a different eleven from the same club at home.
 *
 * **It goes to the lineup screen**, because that is the decision the four facts are for. A
 * card about a match that is only about the match is a widget, and a widget in the foot of the
 * column is decoration a manager learns to ignore.
 *
 * Every name here is the backend's: the fixture carries both clubs and the host's ground, and
 * the round number is the round's own. Nothing about the match is worked out in the browser.
 */
const NextMatchBox: React.FC<{ team: TeamDto; next: NextFixture | null }> = ({
  team,
  next
}) => {
  if (!next) {
    return (
      <div className="next-match next-match--empty">
        <span className="next-match__label">Próxima partida</span>
        <span className="next-match__none">
          Nenhum jogo marcado no calendário desta competição.
        </span>
      </div>
    );
  }

  const { fixture, round, matchDayNumber, competition } = next;
  const isHome = fixture.homeTeamId === team.id;
  // The ground is the host's, and the host is whoever is at home — whichever club that is.
  // An away match is played in the other club's stadium, so a card that printed the manager's
  // own ground for an away game would be telling him the one thing he already knows wrong.
  const host = fixture.homeTeam;
  const opponent = isHome ? fixture.awayTeam : fixture.homeTeam;
  const stadium = host?.stadium?.name;

  return (
    <NavLink className="next-match" to={`/match/lineup/${fixture.id}`}>
      <span className="next-match__label">
        Próxima partida
        {/* The round and the day of the season, said together: "R9" on its own is a number
            out of context, and the day is the thing a manager reads to know how far away it
            is. */}
        <span className="next-match__round">
          {matchDayNumber ? `Dia ${matchDayNumber}` : `R${round.number}`}
        </span>
      </span>

      <span className="next-match__fixture">
        {/* C or F, and the words with it: a letter in a column is a label a manager has to
            know how to read, and the same letter in a sentence costs nothing. */}
        <span
          className={`next-match__side${isHome ? ' next-match__side--home' : ''}`}
          title={isHome ? 'Em casa' : 'Fora de casa'}
        >
          {isHome ? 'C' : 'F'}
        </span>
        {opponent ? (
          <span className="next-match__opponent">
            <ClubName teamId={opponent.id}>{opponent.name}</ClubName>
          </span>
        ) : (
          <span className="next-match__opponent">Adversário a definir</span>
        )}
      </span>

      <span className="next-match__where">
        <span className="next-match__stadium" title={stadium ?? 'Estádio a definir'}>
          {stadium ?? 'Estádio a definir'}
        </span>
        {/* The competition is the edition's own name, so a 1ª Divisão fixture says "1ª Divisão"
            and a cup tie says "Copa": "Campeonato Brasileiro" alone would be the same word for
            a match in the first division and a match in the third. */}
        <span className="next-match__competition">{competition?.name ?? '—'}</span>
      </span>
    </NavLink>
  );
};

export default NextMatchBox;
