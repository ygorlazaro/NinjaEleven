import React, { useEffect, useState } from 'react';
import { NavLink } from 'react-router-dom';
import type { TeamDto, TeamMatchRecordDto } from '@/types';
import { ClubName } from '@/components/Common/Names';
import ClubCrest from '@/components/Club/ClubCrest';
import FormRun, { FORM_RUN_LENGTH } from '@/components/Club/FormRun';
import { TeamApi } from '@/api';
import type { NextFixture } from '@/hooks/useNextFixture';

/**
 * The opponent's last results, asked for the club the fixture names and not for the club in
 * the column, so the run under the name is the one that belongs to it.
 *
 * The box is a card in a column and not a screen, so this is one small read rather than a
 * second one in the browser: the run is drawn off the backend's own classification of each
 * match, which is the same chain the standings and the club page are settled on.
 */
const OpponentRun: React.FC<{ opponent: TeamDto }> = ({ opponent }) => {
  const [matches, setMatches] = useState<TeamMatchRecordDto[]>([]);

  useEffect(() => {
    let cancelled = false;

    TeamApi.getMatches(opponent.id, FORM_RUN_LENGTH)
      .then(records => {
        if (!cancelled) setMatches(records);
      })
      .catch(err => {
        console.error('Failed to load the opponent\'s recent matches:', err);
        if (!cancelled) setMatches([]);
      });

    return () => {
      cancelled = true;
    };
  }, [opponent.id]);

  return (
    <span className="next-match__form" title={`Últimos ${FORM_RUN_LENGTH} jogos do ${opponent.name}`}>
      <span className="form-run">
        <FormRun matches={matches} emptyLabel="sem jogos" />
      </span>
    </span>
  );
};

/**
 * The wave a matchday is in, in the words the rest of the game uses for it. The backend
 * sends the kind of competition, and the sentence is this screen's: "depois do campeonato"
 * is what a manager reads, and a raw enum would be the one place on the card a manager has
 * to translate.
 */
const waveName = (wave: string | null): string => {
  switch (wave) {
    case 'SuperCup':
      return 'jogo da Supercopa';
    case 'League':
      return 'campeonato';
    case 'Cup':
      return 'jogo da Copa';
    default:
      return 'jogo do dia';
  }
};

/**
 * The match the manager is about to play, in the foot of the column.
 *
 * Four facts and no more: who it is against, where it is played, which side of the door the
 * club is on, and what the game is. That is what a manager needs before choosing eleven — and
 * the stadium is in the list because "onde" decide uma escalação tanto quanto "contra quem":
 * um clube fora de casa num gramado curto é um onze diferente do mesmo clube em casa.
 *
 * **The opponent arrives with a shield and a run.** The name alone is a word a manager has to
 * remember from the last one, and the run says in five letters what ten matches of news would
 * take to say: the shape of the club he is about to pick an eleven against.
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
          Nenhum jogo marcado no calendário da temporada.
        </span>
      </div>
    );
  }

  const { fixture, matchDayNumber, competitionName, playableNow, waitingFor } = next;
  const isHome = fixture.homeTeamId === team.id;
  // The ground is the host's, and the host is whoever is at home — whichever club that is.
  // An away match is played in the other club's stadium, so a card that printed the manager's
  // own ground for an away game would be telling him the one thing he already knows wrong.
  const host = fixture.homeTeam;
  const opponent = isHome ? fixture.awayTeam : fixture.homeTeam;
  const stadium = host?.stadium?.name;

  return (
    // The board, not the fixture. The order a manager leaves is his club's order and
    // applies to whichever match is next, so there is no screen per fixture to go to — a
    // door that led to one would be a door to a decision about a match that has already
    // been played.
    <NavLink className="next-match" to="/tactics">
      <span className="next-match__label">
        Próxima partida
        {/* The round and the day of the season, said together: "R9" on its own is a number
            out of context, and the day is the thing a manager reads to know how far away it
            is. */}
        <span className="next-match__round">
          {matchDayNumber ? `Dia ${matchDayNumber}` : '—'}
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
          <>
            <ClubCrest
              primary={opponent.primaryColor}
              secondary={opponent.secondaryColor}
              name={opponent.name}
              className="mini-crest"
            />
            <span className="next-match__opponent">
              <ClubName teamId={opponent.id}>{opponent.name}</ClubName>
            </span>
          </>
        ) : (
          <span className="next-match__opponent">Adversário a definir</span>
        )}
      </span>

      {opponent && <OpponentRun opponent={opponent} />}

      <span className="next-match__where">
        <span className="next-match__stadium" title={stadium ?? 'Estádio a definir'}>
          {stadium ?? 'Estádio a definir'}
        </span>
        {/* The competition is the edition's own name, so a 1ª Divisão fixture says "1ª Divisão"
            and a cup tie says "Copa": "Campeonato Brasileiro" alone would be the same word for
            a match in the first division and a match in the third. */}
        <span className="next-match__competition">{competitionName || '—'}</span>
      </span>

      {/* The match is the club's next one and is not kick-off time yet. A matchday runs one
          wave at a time — championship, then cup — so a cup tie on a day whose league games
          are still being played is the next fixture and is unplayable. Saying that here is
          the difference between a manager who knows to wait and a manager who assembles an
          eleven for a game the server will refuse. */}
      {!playableNow && (
        <span className="next-match__waiting">
          Depois do {waveName(waitingFor)} do dia {matchDayNumber}
        </span>
      )}
    </NavLink>
  );
};

export default NextMatchBox;
