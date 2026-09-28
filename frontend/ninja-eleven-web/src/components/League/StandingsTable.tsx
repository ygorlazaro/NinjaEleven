import React from 'react';
import type { StandingDto, TableZone, TeamDto } from '@/types';
import { ClubName } from '@/components/Common/Names';
import StarRating from '@/components/Common/StarRating';
import ClubCrest from '@/components/Club/ClubCrest';
import { OutcomeFormRun } from '@/components/Club/FormRun';

interface StandingsTableProps {
  standings: StandingDto[];
  userId?: string;
  teams: TeamDto[];
  /** The tier of the division being shown, which is what says which bands this table has. */
  tier?: number | null;
  /** How many divisions the pyramid has, which says whether this one has a division below it. */
  lastTier?: number | null;
}

/**
 * The legend of a table, in the bands the pyramid actually has.
 *
 * The three divisions are not the same table with a different name: the first has the title and
 * no division above it to go up to, and the last has no division below it to go down to, so the
 * band at the bottom of the last division is the cup leaving four clubs out rather than a
 * relegation into a fourth division that the country does not have. A legend that showed the
 * same four lines under all three would be promising the first division four promotions that
 * cannot happen and the last one four relegations that have nowhere to go.
 */
const LEGEND: Record<TableZone, string> = {
  None: '',
  Safe: '',
  Champion: '1º lugar — campeão',
  Promotion: '4 primeiras — zona de acesso',
  Relegation: '4 últimas — zona de descenso',
  CupExclusion: '4 últimas — desclassificado da copa na temporada seguinte'
};

const BAND_CLASS: Record<TableZone, string> = {
  None: '',
  Safe: '',
  Champion: 'champion',
  Promotion: 'promotion',
  Relegation: 'relegation',
  CupExclusion: 'cup-exclusion'
};

/**
 * The bands one division has, in the order a manager reads them down the table.
 *
 * Tier one is the title and nothing else — there is no division above it, so no club in it is
 * going up — and the last tier's bottom band is the cup leaving four clubs out, because there is
 * no division below it either. The number of tiers is passed in rather than written here: the
 * pyramid is the season's own list of divisions, and a screen with its own copy of how many
 * there are is a screen that is wrong the day a fourth one is added.
 */
const bandsOf = (tier?: number | null, lastTier?: number | null): TableZone[] => {
  if (tier == null) return [];

  const bands: TableZone[] = [tier === 1 ? 'Champion' : 'Promotion'];

  if (lastTier != null && lastTier > 0) {
    bands.push(tier < lastTier ? 'Relegation' : 'CupExclusion');
  }

  return bands;
};

/**
 * The classification table, in the order the backend put it in.
 *
 * There is no sort here. The order is the promotion rule — points, goal difference, goals
 * scored, the head-to-head, the cards, and finally the squad — and a screen that sorted the
 * lines again would be applying its own subset of it: the same clubs, the same numbers, a
 * different order, and a manager who is told his club is sixth by one and seventh by the other.
 *
 * The position is shown as it arrives rather than as the index of the row, so a table that
 * ever arrived out of order would look out of order instead of quietly looking right.
 *
 * The bands are tagged by the backend and only painted here, and the legend under the table is
 * built from the tier that table is: the first division races for the title and not for a
 * promotion, the last one has no relegation at all, and only the middle one is a race in both
 * directions. A screen that guessed the bands would be guessing a rule it has no copy of to
 * disagree with the season's end. The zone is highlighted, not recomputed.
 */
const StandingsTable: React.FC<StandingsTableProps> = ({ standings, userId, teams, tier, lastTier }) => {
  const teamMap = teams.reduce<Record<string, TeamDto>>((acc, team) => {
    acc[team.id] = team;
    return acc;
  }, {});

  return (
    <>
      <table className="standings">
        <thead>
          <tr>
            <th>#</th>
            <th>Time</th>
            <th>★</th>
            <th>P</th>
            <th>J</th>
            <th>V</th>
            <th>E</th>
            <th>D</th>
            <th>SG</th>
            <th>GP</th>
            <th>CA</th>
            <th>CV</th>
            <th>Forma</th>
          </tr>
        </thead>
        <tbody>
          {standings.map(row => {
            const team = teamMap[row.teamId] || row.team;
            if (!team) return null;

            const isUser = team.id === userId;
            const colors = team.primaryColor || '#f2d34f';
            const stars = row.stars ?? team.stars ?? 0;
            const zone: TableZone = row.zone ?? 'None';

            const rowClass = [
              isUser ? 'user-row' : '',
              zone === 'Champion' ? 'champion' : '',
              zone === 'Promotion' ? 'promotion' : '',
              zone === 'Relegation' ? 'relegation' : '',
              zone === 'CupExclusion' ? 'cup-exclusion' : '',
            ]
              .filter(Boolean)
              .join(' ');

            return (
              <tr
                key={row.teamId}
                className={rowClass || undefined}
                style={{
                  '--team-primary': colors,
                  '--team-secondary': team.secondaryColor || '#f2d34f'
                } as React.CSSProperties}
              >
                <td>{row.position}</td>
                <td>
                  <span className="standing-club">
                    <ClubCrest
                      primary={colors}
                      secondary={team.secondaryColor || '#f2d34f'}
                      name={team.name}
                      className="mini-crest"
                    />
                    <ClubName teamId={team.id}>{team.name}</ClubName>
                  </span>
                </td>
                <td>
                  <StarRating stars={stars} />
                </td>
                <td><b>{row.points}</b></td>
                <td>{row.played}</td>
                <td>{row.wins}</td>
                <td>{row.draws}</td>
                <td>{row.losses}</td>
                <td>{row.goalDifference}</td>
                <td>{row.goalsFor}</td>
                <td>{row.yellowCards}</td>
                <td>{row.redCards}</td>
                <td className="standing-form">
                  <OutcomeFormRun outcomes={row.form} />
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>

      {standings.length > 0 && bandsOf(tier, lastTier).length > 0 && (
        <div className="standings-legend">
          <strong>Legenda:</strong>
          {bandsOf(tier, lastTier).map(band => (
            <span key={band} className={`zone-${BAND_CLASS[band]}`}>{LEGEND[band]}</span>
          ))}
        </div>
      )}
    </>
  );
};

export default StandingsTable;
