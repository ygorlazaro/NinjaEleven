import React, { useEffect, useState } from 'react';
import { LeagueApi, TeamApi } from '@/api';
import type { FixtureDto, RoundDto, StandingDto, TeamDto, TeamMatchRecordDto } from '@/types';
import { ClubName } from '@/components/Common/Names';
import FormRun from '@/components/Club/FormRun';

/** How many results of each club are shown. A run long enough to matter, short enough to read. */
const FORM_LENGTH = 5;

interface NextMatchPanelProps {
  fixture: FixtureDto;
  round: RoundDto;
  managerTeam: TeamDto;
  competitionName: string;
  /** The edition the table belongs to, which is the same one the fixture is in. */
  competitionSeasonId: string;
}

/**
 * The match this lineup is for, said before the lineup.
 *
 * A manager picking eleven without knowing who it is against is picking them blind, and a
 * manager who cannot see the table cannot answer to it. So: the opponent, the competition,
 * the round, where the club stands, and the run of both — each club's last results beside
 * its own name, because "how have we been doing" and "how have they been doing" are one
 * question with two answers, and the two are only interesting next to each other.
 *
 * Both names are doors. A club is a door everywhere in the game and this is a place in the
 * game, so it is no different here.
 */
const NextMatchPanel: React.FC<NextMatchPanelProps> = ({
  fixture,
  round,
  managerTeam,
  competitionName,
  competitionSeasonId
}) => {
  const isHome = fixture.homeTeamId === managerTeam.id;
  const opponentId = isHome ? fixture.awayTeamId : fixture.homeTeamId;

  const [opponent, setOpponent] = useState<TeamDto | null>(null);
  const [standing, setStanding] = useState<StandingDto | null>(null);
  const [form, setForm] = useState<{ manager: TeamMatchRecordDto[]; opponent: TeamMatchRecordDto[] }>({
    manager: [],
    opponent: []
  });

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      try {
        // The opponent arrives on the fixture for a manager who has the list in front of him
        // and does not for one who arrived from a bare address, so the club is asked rather
        // than hoped for.
        const [found, table, managerRun, opponentRun] = await Promise.all([
          TeamApi.get(opponentId),
          LeagueApi.getStanding(competitionSeasonId, managerTeam.id),
          TeamApi.getMatches(managerTeam.id, FORM_LENGTH),
          TeamApi.getMatches(opponentId, FORM_LENGTH)
        ]);

        if (cancelled) return;
        setOpponent(found);
        setStanding(table);
        setForm({ manager: managerRun, opponent: opponentRun });
      } catch (err) {
        console.error('Failed to load the next match panel:', err);
      }
    };

    load();

    return () => {
      cancelled = true;
    };
  }, [opponentId, competitionSeasonId, managerTeam.id]);

  return (
    <section className="next-match">
      <div className="next-match__head">
        <span className="next-match__kicker">Próxima partida</span>
        <span className="next-match__competition">{competitionName || 'Campeonato'}</span>
        <span className="next-match__round">Rodada {round.number}</span>
      </div>

      <div className="next-match__clubs">
        <div className="next-match__club">
          <span className="next-match__side">{isHome ? 'Mandante' : 'Visitante'}</span>
          <span className="next-match__name">
            <ClubName teamId={managerTeam.id}>{managerTeam.name}</ClubName>
          </span>
          <div className="form-run" title="Forma recente">
            <FormRun matches={form.manager} length={FORM_LENGTH} />
          </div>
        </div>

        <span className="next-match__versus">x</span>

        <div className="next-match__club">
          <span className="next-match__side">{isHome ? 'Visitante' : 'Mandante'}</span>
          <span className="next-match__name">
            {opponent ? (
              <ClubName teamId={opponent.id}>{opponent.name}</ClubName>
            ) : (
              '…'
            )}
          </span>
          <div className="form-run" title="Forma recente">
            {opponent ? (
              <FormRun matches={form.opponent} length={FORM_LENGTH} />
            ) : (
              <span className="form-run__empty">…</span>
            )}
          </div>
        </div>
      </div>

      {standing && (
        <p className="next-match__standing">
          {standing.position}º na tabela • {standing.points} pts em {standing.played} jogos
          {' • '}
          {standing.wins}V {standing.draws}E {standing.losses}D
        </p>
      )}
    </section>
  );
};

export default NextMatchPanel;
