import type { ClubHistoryEventDto, ClubProfileDto, ClubTrophyDto, TeamDto } from '@/types';

/**
 * A club's page, drawn from a stand-in while there is no page to read.
 *
 * The screen this feeds is the club's own: its shield, its name, its manager, its bill, its
 * shelf and its history. Four of those are facts the game already keeps — the roster is
 * `GET /team/{id}/squad/{seasonId}`, the balance is the one the finance endpoint returns, the
 * shelf is `trophy_awards` and the divisions the club has been in are `competition_participants`
 * — and one is not kept at all: **a club has no manager in the world yet**, so the name on the
 * card is the only part of this page that has nowhere to come from.
 *
 * So the numbers are drawn from the real club where the real club is the source, and the
 * invented part is invented in one place and only here. When the endpoint arrives this file
 * disappears and nothing on the screen changes: the types are the ones the endpoint will
 * serve, so the swap is a matter of fetching rather than of rewriting.
 *
 * The history is a *career*, and a career is longer than a season: the club the manager has
 * today is a club that was founded, was renamed, was relegated, came back and won something.
 * A page of a club that only remembers this season is a page of a team, not of a club.
 */

const COACH_NAMES = [
  'Zé Ramalho',
  'Tavinho Camargo',
  'Nivaldo Santi',
  'Ademir Pazz',
  'Walter Boaventura',
  'Irineu Sarmento',
  'Célio Bernardes',
  'Horácio Quintela'
];

const FIRST_NAMES = [
  'Oratório',
  'Barreiro',
  'Vila Nova',
  'Ponta Seca',
  'Alto da Serra',
  'Cachoeirinha',
  'Bela Vista',
  'Rio Claro'
];

const OLD_NAMES = [
  'Esporte Clube União',
  'Grêmio Operário',
  'Associação Atlética Central',
  'Clube Atlético do Bairro',
  'Sociedade Esportiva Bandeirante'
];

/** A number a manager would believe, drawn from the club's own name so it is stable per club. */
const stableSeed = (team: TeamDto): number =>
  [...team.id].reduce((sum, character) => sum + character.charCodeAt(0), 0);

/** The pick is a function of the club, not of the call: the same club always tells the same story. */
const pick = <T,>(values: T[], seed: number, salt: number): T =>
  values[(seed + salt * 7) % values.length];

/**
 * The club's history, newest first, as a career that happened: founded, renamed, a stadium
 * built, a relegation, a promotion, a title, a top scorer. The kinds are the ones the domain
 * already has facts about — a division move is a change of `competition_participants`, a
 * title is a row of `trophy_awards`, a season's top scorer is a line of
 * `match_player_statistics` — and the sentences are the screen's, so a reworded line breaks
 * nothing.
 */
const mockHistory = (team: TeamDto, seed: number): ClubHistoryEventDto[] => {
  const events: ClubHistoryEventDto[] = [];
  const id = (kind: string) => `${team.id}-${kind}`;

  // Founded, in the first season the world drew.
  events.push({
    id: id('FirstSeason'),
    kind: 'FirstSeason',
    seasonNumber: 1,
    seasonName: 'Temporada I',
    description: `Fundado como ${pick(OLD_NAMES, seed, 1)}, ${pick(FIRST_NAMES, seed, 2)}.`
  });

  // A name and a crest, because a club that has never been renamed is a club that has just
  // been founded and this club is older than that.
  events.push({
    id: id('NameChange'),
    kind: 'NameChange',
    seasonNumber: 2,
    seasonName: 'Temporada II',
    description: `Passou a chamar-se ${team.name}.`
  });

  events.push({
    id: id('CrestChange'),
    kind: 'CrestChange',
    seasonNumber: 2,
    seasonName: 'Temporada II',
    description: 'Novo escudo, com as cores que o clube veste hoje.'
  });

  // A stadium that got better, which is the one upgrade a club's page can be sure of.
  events.push({
    id: id('StadiumUpgrade'),
    kind: 'StadiumUpgrade',
    seasonNumber: 3,
    seasonName: 'Temporada III',
    description: 'Reforma na arquibancada: mais lugares e o gramado refeito.',
    value: 1800
  });

  // A relegation and the promotion that answered it, in that order, because a club that was
  // relegated and stayed up would not have been relegated.
  events.push({
    id: id('Relegation'),
    kind: 'Relegation',
    seasonNumber: 4,
    seasonName: 'Temporada IV',
    description: 'Rebaixamento no fim da temporada.'
  });

  events.push({
    id: id('Promotion'),
    kind: 'Promotion',
    seasonNumber: 5,
    seasonName: 'Temporada V',
    description: 'Acesso conquista a divisão de cima.'
  });

  // A title, and the season's top scorer, because a page of honours without the man who made
  // them is a page of the club and not of the football.
  events.push({
    id: id('TopScorer'),
    kind: 'TopScorer',
    seasonNumber: 5,
    seasonName: 'Temporada V',
    description: `${pick(['Zé Ramalho', 'Tavinho Camargo', 'Nivaldo Santi'], seed, 3)} foi o artilheiro com 18 gols.`,
    value: 18
  });

  events.push({
    id: id('Title'),
    kind: 'Title',
    seasonNumber: 5,
    seasonName: 'Temporada V',
    description: 'Campeão da divisão: a primeira taça do clube.'
  });

  // Newest first: a club's history is read from now backwards, and a shelf of moments sorted
  // oldest-first is a shelf a manager has to read all the way down to reach this season.
  return events.reverse();
};

/** The shelf, as three competitions' worth of places won. */
const mockTrophies = (team: TeamDto, seed: number): ClubTrophyDto[] => {
  const id = (kind: string, season: number) => `${team.id}-${kind}-${season}`;
  const season = (n: number) => ({
    seasonNumber: n,
    seasonName: `Temporada ${['I', 'II', 'III', 'IV', 'V', 'VI', 'VII'][n - 1] ?? n}`
  });

  return [
    {
      id: id('Champion', 5),
      competition: 'Campeonato Brasileiro',
      kind: 'Champion',
      ...season(5),
      divisionName: '1ª Divisão'
    },
    {
      id: id('RunnerUp', 6),
      competition: 'Campeonato Brasileiro',
      kind: 'RunnerUp',
      ...season(6),
      divisionName: '1ª Divisão'
    },
    {
      id: id('Champion', 7),
      competition: 'Copa do Brasil',
      kind: 'Champion',
      ...season(7),
      divisionName: null
    }
  ];
};

/**
 * The whole page for a club, from the club itself plus a stand-in for the rest.
 *
 * The name and the colours are the real club's, so a manager who changes clubs sees that
 * club's page and not the same page twice — a mock that ignored the club would look right on
 * the first club and be obviously a mock on the second.
 */
export const mockClubProfile = (team: TeamDto): ClubProfileDto => {
  const seed = stableSeed(team);
  const winCount = (seed % 3) + 1;

  return {
    teamId: team.id,
    name: team.name,
    shortName: team.shortName,
    primaryColor: team.primaryColor || '#f2d34f',
    secondaryColor: team.secondaryColor || '#f2d34f',
    coachName: pick(COACH_NAMES, seed, 4),
    // A club keeps twenty-three men and the game is a long read: the roster is a fact, but it
    // is the one number here that comes from a screen's own count until the endpoint lands.
    squadSize: 23,
    balance: 1_000_000 - (seed % 7) * 48_000,
    promotions: winCount,
    relegations: winCount > 1 ? winCount - 1 : 0,
    trophies: mockTrophies(team, seed),
    history: mockHistory(team, seed)
  };
};
