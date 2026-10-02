import { RankingApi } from '@/api';
import ClubCrest from '@/components/Club/ClubCrest';
import { ClubName } from '@/components/Common/Names';
import type { ClubRankingDto } from '@/types';
import React, { useEffect, useMemo, useState } from 'react';
import { useGameState } from '@/state';

/**
 * The Ninja ranking: every club in the world, scored over three seasons of league and three of
 * cup, and the one number that is not a league number at all.
 *
 * **The manager's own club is the one row that is marked.** This list is the whole world in one
 * column, so his club is one line among sixty-three and the eye goes straight past it. It is
 * marked in the club's own colour down the edge of the row rather than by a background loud
 * enough to shout, because a row painted across a column of sixty-three stops being a highlight
 * and becomes the thing the eye goes to before the list — and the list is what the screen is for.
 *
 * **The division filter is the screen's whole subject, because a ranking is four rankings.** The
 * cup runs across the pyramid and so does this ranking, which means the top of it is four
 * divisions' best clubs competing on a score that puts a first-division champion and a
 * fourth-division one on the same ladder. A manager who wants to know where his club sits among
 * the sixteen that share his season's football narrows the list to his own division, and every
 * other division is one click away.
 */
const RankingScreen: React.FC = () => {
  const [ranking, setRanking] = useState<ClubRankingDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  // The manager's own club, read rather than asked for: the row that is marked is decided by
  // whose manager is logged in, and a screen that took the club from a parameter would mark
  // whichever one the URL happened to name.
  const selectedTeam = useGameState((s) => s.selectedTeam);
  // Which division the list is showing, or null for all of them. It is null rather than zero
  // because there is no division zero, and a filter that has to invent an "all" out of one of
  // the real values is a filter that will eventually confuse one for the other.
  const [division, setDivision] = useState<number | null>(null);

  useEffect(() => {
    const loadRanking = async () => {
      setLoading(true);
      setError(null);
      try {
        const data = await RankingApi.getRanking();
        setRanking(data);
      } catch (err: any) {
        setError(err.message || 'Não foi possível carregar o ranking.');
      } finally {
        setLoading(false);
      }
    };

    loadRanking();
  }, []);

/**
 * The divisions the list actually contains, in the pyramid's order.
 *
 * Read off the rows rather than written out as four names, because a chip for a division no
 * club is in is a control that opens onto nothing — and the names come from the backend's own
 * `currentDivisionName` rather than from a switch on the tier, so a division renamed in the
 * game is renamed here too.
 *
 * A club with no division in the season it is being ranked for is left out of this list: it is
 * in no division, and offering a chip named after the tier zero it arrived as would be offering
 * a filter for a division that does not exist. It still shows under "Todas".
 */
  const divisions = useMemo(() => {
    const seen = new Map<number, string>();

    for (const club of ranking) {
      if (club.currentDivision > 0 && club.currentDivisionName && !seen.has(club.currentDivision)) {
        seen.set(club.currentDivision, club.currentDivisionName);
      }
    }

    return [...seen.entries()]
      .sort(([tier], [other]) => tier - other)
      .map(([tier, name]) => ({ tier, name }));
  }, [ranking]);

  /**
   * The rows on screen, narrowed by the division.
   *
   * The narrowing happens here rather than at the backend because the ranking is already one
   * read of the whole world — sixty-three clubs in a single payload — and a screen that asked
   * the backend again for one division would be asking twice for an answer it is already
   * holding. It also leaves the position column alone: the number beside a club is where that
   * club stands in the whole world, and a filter that renumbered it would be printing a place
   * in a division that the ranking does not claim to be a table of.
   */
  const shown = useMemo(
    () => (division === null ? ranking : ranking.filter(club => club.currentDivision === division)),
    [ranking, division]
  );

  if (loading) {
    return (
      <div className="card start">
        <p>Carregando ranking...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="card start">
        <h1>Erro</h1>
        <p>{error}</p>
      </div>
    );
  }

  return (
    <div className="card ranking-screen">
      <h1>🏆 Ranking Ninja</h1>
      <p className="ranking-screen__hint">
        O ranking combina desempenho no campeonato (últimas 3 temporadas) e na copa (últimas 3 temporadas).
      </p>

      {/* The filter is chips rather than a dropdown, because the four divisions are the one
          choice on this screen and a chip says which of them is open without being opened. It
          carries the count of what each one would leave, so a manager who clicks "2ª Divisão"
          knows sixteen clubs are coming and not four. */}
      {divisions.length > 1 && (
        <div className="ranking-filter" role="group" aria-label="Filtrar por divisão">
          <button
            type="button"
            className={`filter-chip ${division === null ? 'active' : ''}`}
            onClick={() => setDivision(null)}
          >
            Todas <span className="ranking-filter__count">{ranking.length}</span>
          </button>
          {divisions.map(({ tier, name }) => {
            const count = ranking.filter(club => club.currentDivision === tier).length;

            return (
              <button
                key={tier}
                type="button"
                className={`filter-chip ${division === tier ? 'active' : ''}`}
                onClick={() => setDivision(tier === division ? null : tier)}
              >
                {name} <span className="ranking-filter__count">{count}</span>
              </button>
            );
          })}
        </div>
      )}

      <div className="ranking-table-wrap">
        <table className="data-table ranking-table">
          <thead>
            <tr>
              <th className="ranking-table__pos">Pos</th>
              <th>Clube</th>
              <th className="ranking-table__division">Divisão</th>
              <th className="ranking-table__num">Força</th>
              <th className="ranking-table__num">Pontos Base</th>
              <th className="ranking-table__num">Pontos Copa</th>
              <th className="ranking-table__num ranking-table__num--total">Total</th>
            </tr>
          </thead>
          <tbody>
            {shown.map(club => {
              const mine = !!selectedTeam && club.teamId === selectedTeam.id;

              return (
                <tr
                  key={club.teamId}
                  className={mine ? 'ranking-row--mine' : undefined}
                  // The mark is drawn in the club's own colour, so the row that says "this one"
                  // says it in the colours of the club rather than in a highlight nobody chose.
                  style={mine ? ({ '--mine-color': club.primaryColor } as React.CSSProperties) : undefined}
                >
                  <td className={`ranking-table__pos ${club.position <= 3 ? 'ranking-table__pos--podium' : ''}`}>
                    {club.position}º
                  </td>
                  <td>
                    <div className="ranking-row">
                      <ClubCrest
                        primary={club.primaryColor}
                        secondary={club.secondaryColor}
                        name={club.teamName}
                        className="ranking-crest"
                      />
                      <div className="ranking-row__info">
                        {/* A name is always a door, and the manager's own club is a screen like
                            any other rather than a special case that is not clickable. */}
                        <ClubName teamId={club.teamId} className="ranking-row__name">
                          {club.teamName}
                        </ClubName>
                        <span className="ranking-row__manager">{club.managerName}</span>
                      </div>
                      {mine && <span className="ranking-row__you" title="O seu clube">Você</span>}
                    </div>
                  </td>
                  {/* The division's own name, from the backend. It used to be a switch on the tier
                      written here, which is a second copy of the names the pyramid's rules
                      already carry — and a copy that goes stale the day a division is renamed.
                      A club enrolled in no division says so rather than carrying tier zero's
                      empty name, because a blank badge is a badge that says nothing. */}
                  <td className="ranking-table__division">
                    {club.currentDivisionName ? (
                      <span className={`ranking-division ranking-division--t${club.currentDivision}`}>
                        {club.currentDivisionName}
                      </span>
                    ) : (
                      <span className="ranking-division ranking-division--none">Sem divisão</span>
                    )}
                  </td>
                  <td className="ranking-table__num">{club.strength.toFixed(1)}</td>
                  <td className="ranking-table__num">{club.rankingBase}</td>
                  <td className="ranking-table__num">{club.cupScore}</td>
                  <td className="ranking-table__num ranking-table__num--total">{club.rankingPoints}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {shown.length === 0 && (
        <p className="league-empty">
          {ranking.length === 0
            ? 'Nenhum clube no ranking.'
            : 'Nenhum clube nesta divisão.'}
        </p>
      )}
    </div>
  );
};

export default RankingScreen;