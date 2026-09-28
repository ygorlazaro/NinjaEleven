import React, { useEffect, useState } from 'react';
import { FixtureApi, TeamApi } from '@/api';
import type { FixtureDto, RoundDto, TeamDto, TeamMatchRecordDto } from '@/types';
import { ClubName } from '@/components/Common/Names';

const FORM_LENGTH = 5;
const H2H_LENGTH = 5;

interface RoundContextPanelProps {
  fixture: FixtureDto;
  round: RoundDto;
  managerTeam: TeamDto;
  competitionSeasonId: string;
}

const RoundContextPanel: React.FC<RoundContextPanelProps> = ({
  fixture,
  round,
  managerTeam,
  competitionSeasonId
}) => {
  const isHome = fixture.homeTeamId === managerTeam.id;
  const opponentId = isHome ? fixture.awayTeamId : fixture.homeTeamId;

  const [opponent, setOpponent] = useState<TeamDto | null>(null);
  const [roundFixtures, setRoundFixtures] = useState<FixtureDto[]>([]);
  const [managerForm, setManagerForm] = useState<TeamMatchRecordDto[]>([]);
  const [opponentForm, setOpponentForm] = useState<TeamMatchRecordDto[]>([]);
  const [h2hMatches, setH2hMatches] = useState<TeamMatchRecordDto[]>([]);

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      try {
        const [found, fixtures, managerRun, opponentRun, h2h] = await Promise.all([
          TeamApi.get(opponentId),
          FixtureApi.listByRound(round.id),
          TeamApi.getMatches(managerTeam.id, FORM_LENGTH),
          TeamApi.getMatches(opponentId, FORM_LENGTH),
          TeamApi.getHeadToHead(managerTeam.id, opponentId, H2H_LENGTH)
        ]);

        if (cancelled) return;
        setOpponent(found);
        setRoundFixtures(fixtures || []);
        setManagerForm(managerRun);
        setOpponentForm(opponentRun);
        setH2hMatches(h2h);
      } catch (err) {
        console.error('Failed to load round context:', err);
      }
    };

    load();

    return () => {
      cancelled = true;
    };
  }, [round.id, opponentId, managerTeam.id, competitionSeasonId]);

  const getMatchStatus = (f: FixtureDto) => {
    if (f.status === 'Finished') return 'finished';
    if (f.status === 'Live') return 'live';
    return 'scheduled';
  };

  /**
   * What the fixture is doing, in words. A fixture carries no date of its own — the season is
   * counted in matchdays, and every match in a round is the same day by construction — so the
   * date this row used to print was always the same string for all of them, and was a
   * formatting of nothing. What does differ row to row is whether the match has been played.
   */
  const STATUS_WORDS: Record<string, string> = {
    finished: 'Encerrada',
    live: 'Em andamento',
    scheduled: 'Agendada'
  };

  const formatScore = (homeGoals?: number | null, awayGoals?: number | null) => {
    if (homeGoals === null || homeGoals === undefined || awayGoals === null || awayGoals === undefined) return '—';
    return `${homeGoals} x ${awayGoals}`;
  };

  return (
    <section className="round-context">
      {/* Partidas da Rodada */}
      <div className="round-context__section">
        <h3 className="round-context__title">Partidas da Rodada {round.number}</h3>
        <div className="round-context__matches">
          {roundFixtures.map((f) => {
            const isManagerMatch = f.id === fixture.id;
            const status = getMatchStatus(f);
            const homeTeam = f.homeTeam;
            const awayTeam = f.awayTeam;

            return (
              <div
                key={f.id}
                className={`round-context__match ${isManagerMatch ? 'manager-match' : ''} ${status}`}
              >
                <div className="round-context__match-info">
                  <span className="round-context__match-time">{STATUS_WORDS[status]}</span>
                  {isManagerMatch && <span className="round-context__badge">Sua partida</span>}
                </div>
                <div className="round-context__teams">
                  <span className={`round-context__team ${isHome && isManagerMatch ? 'manager-team' : ''} ${!isHome && isManagerMatch ? 'manager-team' : ''}`}>
                    <ClubName teamId={homeTeam?.id || ''}>{homeTeam?.name || '?'}</ClubName>
                  </span>
                  <span className="round-context__score">{formatScore(f.homeGoals, f.awayGoals)}</span>
                  <span className={`round-context__team ${!isHome && isManagerMatch ? 'manager-team' : ''} ${isHome && isManagerMatch ? 'manager-team' : ''}`}>
                    <ClubName teamId={awayTeam?.id || ''}>{awayTeam?.name || '?'}</ClubName>
                  </span>
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* Últimos 5 jogos de cada time */}
      <div className="round-context__section">
        <h3 className="round-context__title">Últimos {FORM_LENGTH} jogos</h3>
        <div className="round-context__forms">
          <div className="round-context__form">
            <h4 className="round-context__form-title">
              <ClubName teamId={managerTeam.id}>{managerTeam.name}</ClubName>
            </h4>
            <div className="round-context__form-list">
              {managerForm.slice(0, FORM_LENGTH).map((m, idx) => (
                <div key={`${m.matchId}-${idx}`} className="round-context__form-row">
                  <span className="round-context__form-round">Rodada {m.roundNumber}</span>
                  <span className="round-context__form-opponent">
                    {m.isHome ? '🏠' : '✈️'} <ClubName teamId={m.opponentTeamId}>{m.opponentName}</ClubName>
                  </span>
                  <span className="round-context__form-score">{m.goalsFor} x {m.goalsAgainst}</span>
                </div>
              ))}
              {managerForm.length === 0 && <span className="round-context__empty">Sem jogos recentes</span>}
            </div>
          </div>

          <div className="round-context__form">
            <h4 className="round-context__form-title">
              {opponent ? (
                <ClubName teamId={opponent.id}>{opponent.name}</ClubName>
              ) : 'Adversário'}
            </h4>
            <div className="round-context__form-list">
              {opponentForm.slice(0, FORM_LENGTH).map((m, idx) => (
                <div key={`${m.matchId}-${idx}`} className="round-context__form-row">
                  <span className="round-context__form-round">Rodada {m.roundNumber}</span>
                  <span className="round-context__form-opponent">
                    {m.isHome ? '🏠' : '✈️'} <ClubName teamId={m.opponentTeamId}>{m.opponentName}</ClubName>
                  </span>
                  <span className="round-context__form-score">{m.goalsFor} x {m.goalsAgainst}</span>
                </div>
              ))}
              {opponentForm.length === 0 && <span className="round-context__empty">Sem jogos recentes</span>}
            </div>
          </div>
        </div>
      </div>

      {/* Confrontos diretos (H2H) */}
      <div className="round-context__section">
        <h3 className="round-context__title">Últimos {H2H_LENGTH} confrontos diretos</h3>
        <div className="round-context__h2h">
          {h2hMatches.length > 0 ? (
            <table className="round-context__h2h-table">
<thead>
                  <tr>
                    <th>Temporada</th>
                    <th>Campeonato</th>
                    <th>Fase/Rodada</th>
                    <th>Local</th>
                    <th>Estádio</th>
                    <th>Placar</th>
                    <th>Público</th>
                  </tr>
                </thead>
                <tbody>
                  {h2hMatches.map((m) => (
                    <tr key={m.matchId}>
                      <td className="round-context__h2h-season">{m.seasonName || '—'}</td>
                      <td className="round-context__h2h-comp">{m.competitionName || '—'}</td>
                      <td className="round-context__h2h-phase">{m.phaseName || `Rodada ${m.roundNumber}`}</td>
                      <td className="round-context__h2h-venue">{m.isHome ? '🏠' : '✈️'}</td>
                      <td className="round-context__h2h-stadium">{m.stadiumName || '—'}</td>
                      <td className="round-context__h2h-score">
                        {m.isHome ? m.goalsFor : m.goalsAgainst} x {m.isHome ? m.goalsAgainst : m.goalsFor}
                      </td>
                      <td className="round-context__h2h-attendance">{m.attendance ? m.attendance.toLocaleString('pt-BR') : '—'}</td>
                    </tr>
                  ))}
                </tbody>
            </table>
          ) : (
            <p className="round-context__empty">Nenhum confronto anterior entre os times</p>
          )}
        </div>
      </div>
    </section>
  );
};

export default RoundContextPanel;