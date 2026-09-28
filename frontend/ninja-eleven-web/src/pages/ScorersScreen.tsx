import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import type { ClubScorerDto, CompetitionFilter, SeasonDto } from '@/types';
import { COMPETITION_LABELS } from '@/types';
import { TeamApi, SeasonApi } from '@/api';
import { PlayerName } from '@/components/Common/Names';
import { useClubWindow } from '@/services/clubColors';
import { useGameState } from '@/state';

/**
 * A season's worth of a club's goals, one line per man who scored them.
 *
 * Three filters, and each of them answers a different question a manager actually asks. The
 * **season** is which year of the club's history is on the table. The **competition** is which
 * of the three the goals came from, which is not a detail: a cup run and a league season are
 * different football, and a striker whose goals are all from a cup tie is a different player
 * from one who scores them every week in a division. And **"ainda no clube"** is the question
 * that separates the men of today from the club's history — a page that only ever showed the
 * men under contract would quietly lose a name every time a transfer window opened, so the
 * ones who left are kept on the list and are simply not in the filter's answer.
 *
 * Every number here arrives from the backend and none of it is worked out in the browser: the
 * goals are the sum of the match lines, the order is the backend's (goals, then the fewest
 * games for them), and "still at the club" is a contract the client cannot read for itself.
 */
const ScorersScreen: React.FC = () => {
  const selectedTeam = useGameState((s) => s.selectedTeam);
  const clubWindow = useClubWindow(selectedTeam);

  const [seasons, setSeasons] = useState<SeasonDto[]>([]);
  const [seasonId, setSeasonId] = useState<string>('');
  const [competition, setCompetition] = useState<CompetitionFilter | ''>('');
  const [onlyAtClub, setOnlyAtClub] = useState(false);

  const [scorers, setScorers] = useState<ClubScorerDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // The season starts on the one being played, because a manager opening the club's scorers
  // is asking about this season and can go back from there.
  useEffect(() => {
    let alive = true;

    SeasonApi.list()
      .then(list => {
        if (!alive) {
          return;
        }

        setSeasons(list);
        // The season being played is the one a manager lands on: he opens the club's
        // scorers to ask about this season, and can walk back from there.
        const current =
          list.find(season => season.status === 'InProgress') ?? list[list.length - 1];
        if (current) {
          setSeasonId(current.id);
        }
      })
      .catch(() => alive && setError('Não foi possível carregar as temporadas.'));

    return () => {
      alive = false;
    };
  }, []);

  useEffect(() => {
    if (!selectedTeam || !seasonId) {
      return;
    }

    let alive = true;
    setLoading(true);
    setError(null);

    TeamApi.getScorers(
      selectedTeam.id,
      seasonId,
      competition === '' ? undefined : competition
    )
      .then(rows => alive && setScorers(rows))
      .catch(() =>
        alive && setError('Não foi possível carregar os artilheiros. Tente de novo.')
      )
      .finally(() => alive && setLoading(false));

    return () => {
      alive = false;
    };
  }, [selectedTeam, seasonId, competition]);

  if (!selectedTeam) {
    return (
      <div className="app">
        <div className="card match-header club-modal">
          <h2 className="profile-name">Artilheiros</h2>
          <p className="competition">Escolha um clube para ver os artilheiros dele.</p>
        </div>
      </div>
    );
  }

  // The filter is a view over the answer, never a second answer: the rows arrive whole and
  // the "ainda no clube" box only says which of them are being shown.
  const shown = onlyAtClub ? scorers.filter(row => row.isStillAtClub) : scorers;
  const season = seasons.find(item => item.id === seasonId);
  const topGoals = shown.length > 0 ? shown[0].goals : 0;

  return (
    <div className="app">
      <div className="card team-view-card club-modal" style={clubWindow}>
        <header className="stadium-head">
          <div>
            <h2 className="profile-name">Artilheiros</h2>
            <p className="club-page__tag">
              {selectedTeam.name}
              {season ? ` • ${season.name}` : ''}
              {competition ? ` • ${COMPETITION_LABELS[competition]}` : ''}
            </p>
          </div>
          <Link className="ctrl" to="/club">
            Voltar ao clube
          </Link>
        </header>

        {/* Three filters and one box. The season is a dropdown because a club has several and
            a manager knows the ones he managed; the competition is a dropdown because there
            are three and each means something different; "ainda no clube" is a box because it
            is a yes or no about the men rather than a choice of one of several. */}
        <section className="scorer-filters">
          <label className="scorer-filter">
            <span className="scorer-filter__label">Temporada</span>
            <select
              className="scorer-filter__select"
              value={seasonId}
              onChange={event => setSeasonId(event.target.value)}
            >
              {seasons.map(item => (
                <option key={item.id} value={item.id}>
                  {item.name}
                </option>
              ))}
            </select>
          </label>

          <label className="scorer-filter">
            <span className="scorer-filter__label">Competição</span>
            <select
              className="scorer-filter__select"
              value={competition}
              onChange={event =>
                setCompetition(event.target.value as CompetitionFilter | '')
              }
            >
              <option value="">Todas</option>
              {(Object.keys(COMPETITION_LABELS) as CompetitionFilter[]).map(kind => (
                <option key={kind} value={kind}>
                  {COMPETITION_LABELS[kind]}
                </option>
              ))}
            </select>
          </label>

          <label className="scorer-filter scorer-filter--check">
            <input
              type="checkbox"
              checked={onlyAtClub}
              onChange={event => setOnlyAtClub(event.target.checked)}
            />
            <span>Ainda no clube</span>
          </label>
        </section>

        {error && <p className="league-empty league-empty--error">{error}</p>}

        {!error && !loading && shown.length === 0 && (
          <p className="league-empty">
            Ninguém marcou gol {onlyAtClub ? 'para quem ainda está no clube ' : ''}
            {season ? `em ${season.name}` : 'nesta temporada'}
            {competition ? ` na ${COMPETITION_LABELS[competition]}` : ''}. Um ataque que não
            marca é um ataque que se resolve no mercado, não na tabela de artilharia.
          </p>
        )}

        {shown.length > 0 && (
          <section className="scorer-figures">
            <div className="club-figure">
              <span className="club-figure__icon">⚽</span>
              <span className="club-figure__value">
                {shown.reduce((sum, row) => sum + row.goals, 0)}
              </span>
              <span className="club-figure__label">gols na tabela</span>
            </div>
            <div className="club-figure">
              <span className="club-figure__icon">👤</span>
              <span className="club-figure__value">{shown.length}</span>
              <span className="club-figure__label">jogadores na lista</span>
            </div>
            <div className="club-figure club-figure--accent">
              <span className="club-figure__icon">🥇</span>
              <span className="club-figure__value">{topGoals}</span>
              <span className="club-figure__label">gols do líder</span>
            </div>
          </section>
        )}

        {shown.length > 0 && (
          <table className="scorer-table">
            <thead>
              <tr>
                <th className="scorer-table__pos">#</th>
                <th className="scorer-table__name">Jogador</th>
                <th>Gols</th>
                <th>Jogos</th>
                <th>Cartões</th>
                <th>G/J</th>
              </tr>
            </thead>
            <tbody>
              {shown.map(row => {
                return (
                  <tr
                    key={row.playerId}
                    className={row.isStillAtClub ? '' : 'scorer-table__row--gone'}
                  >
                    {/* The position is the backend's, and it can be shared: the chain is goals,
                        then fewest games, then fewest cards, then age, and two men level on all
                        four are both in the same place rather than one above the other. */}
                    <td className="scorer-table__pos">
                      {row.position}
                      {row.tiedWith > 0 && (
                        <span className="scorers__tied" title={`Empatado com ${row.tiedWith} outro(s)`}>
                          =
                        </span>
                      )}
                    </td>
                    <td className="scorer-table__name">
                      <PlayerName playerId={row.playerId}>{row.playerName}</PlayerName>
                      <span className="scorer-table__meta">
                        {row.age} anos
                        {row.ownGoals > 0 && ` • ${row.ownGoals} contra o próprio gol`}
                        {!row.isStillAtClub && ' • saiu do clube'}
                      </span>
                    </td>
                    <td className="scorer-table__goals">{row.goals}</td>
                    {/* "14 (3)" is two numbers and not one: games started, and games entered
                        off the bench. A single appearance count throws away the only thing a
                        manager wants from it, which is whether the staff trusted him to
                        start. */}
                    <td>
                      {row.started} ({row.cameOn})
                    </td>
                    {/* The cards weighed as the order weighs them: a yellow is one and a red is
                        three. It is the third thing that decides a level pair, so it is a column
                        rather than a footnote — and the weighting is the backend's, printed here
                        as the number the order was settled on. */}
                    <td className="scorer-table__rate" title={`${row.yellowCards} amarelo(s) e ${row.redCards} vermelho(s)`}>
                      {row.cardPoints}
                    </td>
                    {/* The rate is the backend's number. A screen that divided the two
                        itself would be the second place in the game that knows what a goal a
                        game is, and the two places would answer differently. */}
                    <td className="scorer-table__rate">
                      {row.goalsPerAppearance === null
                        ? '—'
                        : row.goalsPerAppearance.toFixed(2)}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}

        {loading && <p className="league-empty">Carregando artilheiros...</p>}
      </div>
    </div>
  );
};

export default ScorersScreen;
